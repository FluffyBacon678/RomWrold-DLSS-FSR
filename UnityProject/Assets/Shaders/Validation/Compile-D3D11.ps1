param([switch]$WarningsAsErrors)

$ErrorActionPreference = 'Stop'

# Checks the actual AMD HLSL and integration callbacks using Windows' native
# shader compiler. A minimal vertex stub stands in for UnityCG; this does not
# replace importing the ShaderLab asset and building it in the Unity editor.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class FsrNativeCompiler {
    [DllImport("d3dcompiler_47.dll", CallingConvention=CallingConvention.StdCall)]
    static extern int D3DCompile(byte[] source, UIntPtr length, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr defines, IntPtr include, [MarshalAs(UnmanagedType.LPStr)] string entry, [MarshalAs(UnmanagedType.LPStr)] string target, uint flags1, uint flags2, out IntPtr code, out IntPtr errors);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr GetBufferPointer(IntPtr blob);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate UIntPtr GetBufferSize(IntPtr blob);
    static IntPtr VTable(IntPtr blob, int slot) { return Marshal.ReadIntPtr(Marshal.ReadIntPtr(blob), slot * IntPtr.Size); }
    static ulong Size(IntPtr blob) { return Marshal.GetDelegateForFunctionPointer<GetBufferSize>(VTable(blob, 4))(blob).ToUInt64(); }
    static string Contents(IntPtr blob) {
        if (blob == IntPtr.Zero) return "";
        return Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<GetBufferPointer>(VTable(blob, 3))(blob), (int)Size(blob));
    }
    public static string Compile(string source, string entry, string target, bool warningsAsErrors) {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        IntPtr code, errors;
        uint flags = (1u << 11) | (1u << 15); // Strict syntax, optimization level 3.
        if (warningsAsErrors) flags |= 1u << 18;
        int status = D3DCompile(bytes, (UIntPtr)bytes.Length, "Fsr1.shader", IntPtr.Zero, IntPtr.Zero, entry, target, flags, 0, out code, out errors);
        string result = entry + " " + target + " HRESULT=0x" + status.ToString("X8") + " bytes=" + (code == IntPtr.Zero ? 0 : Size(code)) + Environment.NewLine + Contents(errors);
        if (code != IntPtr.Zero) Marshal.Release(code);
        if (errors != IntPtr.Zero) Marshal.Release(errors);
        if (status < 0) throw new Exception(result);
        return result;
    }
}
'@

$shaderDirectory = Split-Path $PSScriptRoot -Parent
$shader = Get-Content -Raw (Join-Path $shaderDirectory 'Fsr1.shader')
$program = [regex]::Match($shader, '(?s)HLSLINCLUDE(.*?)ENDHLSL').Groups[1].Value
if (-not $program) { throw 'Shader HLSLINCLUDE block not found.' }
$stub = @'
struct v2f_img { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
struct appdata_img { float4 vertex : POSITION; float2 texcoord : TEXCOORD0; };
v2f_img vert_img(appdata_img input) { v2f_img output; output.pos=input.vertex; output.uv=input.texcoord; return output; }
'@
$program = $program.Replace('#include "UnityCG.cginc"', $stub)
foreach ($header in @('ffx_a.h', 'ffx_fsr1.h')) {
    $program = $program.Replace(('#include "ThirdParty/FidelityFX/' + $header + '"'),
        (Get-Content -Raw (Join-Path $shaderDirectory ('ThirdParty/FidelityFX/' + $header))))
}
[FsrNativeCompiler]::Compile($program, 'FragEasu', 'ps_5_0', $WarningsAsErrors.IsPresent)
[FsrNativeCompiler]::Compile($program, 'FragRcas', 'ps_5_0', $WarningsAsErrors.IsPresent)
[FsrNativeCompiler]::Compile($program, 'vert_img', 'vs_5_0', $WarningsAsErrors.IsPresent)
