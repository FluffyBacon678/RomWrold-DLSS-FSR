# Compile the actual fragment code with Windows' D3D compiler. This is a
# syntax/type check, not a substitute for Unity import or GPU rendering tests.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$shaderPath = Join-Path $repoRoot 'UnityProject\Assets\Shaders\Fsr1.shader'
$shaderText = Get-Content -LiteralPath $shaderPath -Raw
$match = [regex]::Match($shaderText, '(?s)HLSLINCLUDE(.*?)ENDHLSL')
if (-not $match.Success) { throw 'Shader HLSLINCLUDE block missing.' }
$hlsl = $match.Groups[1].Value.Replace('#include "UnityCG.cginc"', 'struct v2f_img { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };')
$includePath = (Join-Path $repoRoot 'UnityProject\Assets\Shaders\ThirdParty\FidelityFX').Replace('\', '/')
$hlsl = $hlsl.Replace('"ThirdParty/FidelityFX/', ('"' + $includePath + '/'))
$scratchPath = Join-Path $repoRoot '.scratch\HlslValidation'
New-Item -ItemType Directory -Path $scratchPath -Force | Out-Null
$hlslPath = Join-Path $scratchPath 'Fsr1.hlsl'
[IO.File]::WriteAllText($hlslPath, $hlsl)
if (-not ('FsrHlslCompiler' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
[ComImport, Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFsrShaderBlob {
    [PreserveSig] IntPtr GetBufferPointer();
    [PreserveSig] UIntPtr GetBufferSize();
}
public static class FsrHlslCompiler {
    [DllImport("d3dcompiler_47.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.StdCall)]
    static extern int D3DCompileFromFile(string path, IntPtr defines, IntPtr include,
        [MarshalAs(UnmanagedType.LPStr)] string entry, [MarshalAs(UnmanagedType.LPStr)] string target,
        uint flags1, uint flags2, out IFsrShaderBlob code, out IFsrShaderBlob errors);
    public static string Compile(string path, string entry, string destination) {
        IFsrShaderBlob code = null, errors = null;
        try {
            int result = D3DCompileFromFile(path, IntPtr.Zero, new IntPtr(1), entry, "ps_5_0", 2048, 0, out code, out errors);
            string message = errors == null ? "" : Marshal.PtrToStringAnsi(errors.GetBufferPointer());
            if (result < 0) throw new InvalidOperationException(entry + ": " + message);
            byte[] bytes = new byte[checked((int)code.GetBufferSize().ToUInt64())];
            Marshal.Copy(code.GetBufferPointer(), bytes, 0, bytes.Length);
            File.WriteAllBytes(destination, bytes);
            return entry + ": compiled " + bytes.Length + " bytes. " + message;
        } finally {
            if (code != null) Marshal.ReleaseComObject(code);
            if (errors != null) Marshal.ReleaseComObject(errors);
        }
    }
}
'@
}
foreach ($entry in @('FragEasu', 'FragRcas')) {
    [FsrHlslCompiler]::Compile($hlslPath, $entry, (Join-Path $scratchPath ($entry + '.dxbc')))
}
Write-Output 'Both actual FSR fragment programs compiled as Shader Model 5.0. Unity import and GPU validation are separate checks.'
