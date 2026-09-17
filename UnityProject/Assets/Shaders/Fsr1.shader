Shader "Hidden/RimWorldUpscaler/FSR1"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _FsrInputSize ("Input width, height, inverse width, inverse height", Vector) = (1, 1, 1, 1)
        _FsrOutputSize ("Output width, height, inverse width, inverse height", Vector) = (1, 1, 1, 1)
        _FsrSharpness ("RCAS sharpness attenuation in stops", Range(0, 2)) = 0.5
        _FsrLinearColorSpace ("Source sampling and output are linear", Float) = 0
        _FsrFlipY ("Flip source vertically for this pass", Float) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        HLSLINCLUDE
        #include "UnityCG.cginc"

        // These files are the unmodified AMD FSR 1 reference implementation.
        // The use of 32-bit fragment shaders does not require compute shaders.
        #define A_GPU 1
        #define A_HLSL 1
        #include "ThirdParty/FidelityFX/ffx_a.h"
        #define FSR_EASU_F 1
        #define FSR_RCAS_F 1
        #define FSR_RCAS_PASSTHROUGH_ALPHA 1
        #include "ThirdParty/FidelityFX/ffx_fsr1.h"

        Texture2D<float4> _MainTex;
        // Unity recognizes the inline sampler name and creates linear/clamp.
        SamplerState fsr_linear_clamp_sampler;
        float4 _FsrInputSize;
        float4 _FsrOutputSize;
        float _FsrSharpness;
        float _FsrLinearColorSpace;
        float _FsrFlipY;

        float FsrToGamma(float value)
        {
            value = saturate(value);
            return lerp(value * 12.92,
                        1.055 * pow(max(value, 0.0031308), 1.0 / 2.4) - 0.055,
                        step(0.0031308, value));
        }

        float FsrToLinear(float value)
        {
            value = saturate(value);
            return lerp(value / 12.92,
                        pow((value + 0.055) / 1.055, 2.4),
                        step(0.04045, value));
        }

        float4 FsrPrepareGather(float4 value)
        {
            value = saturate(value);
            if (_FsrLinearColorSpace > 0.5)
                return lerp(value * 12.92,
                            1.055 * pow(max(value, 0.0031308), 1.0 / 2.4) - 0.055,
                            step(0.0031308, value));
            return value;
        }

        float3 FsrFinishColor(float3 value)
        {
            if (_FsrLinearColorSpace > 0.5)
                return float3(FsrToLinear(value.r), FsrToLinear(value.g), FsrToLinear(value.b));
            return saturate(value);
        }

        AF4 FsrEasuRF(AF2 p) { return FsrPrepareGather(_MainTex.GatherRed(fsr_linear_clamp_sampler, p)); }
        AF4 FsrEasuGF(AF2 p) { return FsrPrepareGather(_MainTex.GatherGreen(fsr_linear_clamp_sampler, p)); }
        AF4 FsrEasuBF(AF2 p) { return FsrPrepareGather(_MainTex.GatherBlue(fsr_linear_clamp_sampler, p)); }

        AF4 FsrRcasLoadF(ASU2 p)
        {
            // Texture.Load does not obey sampler wrap mode. Clamp every tap,
            // including the four neighbors along the edge of the output.
            p = clamp(p, int2(0, 0), int2(_FsrOutputSize.xy) - 1);
            return _MainTex.Load(int3(p, 0));
        }

        void FsrRcasInputF(inout AF1 r, inout AF1 g, inout AF1 b)
        {
            if (_FsrLinearColorSpace > 0.5)
            {
                r = FsrToGamma(r);
                g = FsrToGamma(g);
                b = FsrToGamma(b);
            }
        }

        uint2 FsrOutputPixel(float2 uv)
        {
            // Use Graphics.Blit UVs instead of SV_Position so that both passes
            // follow Unity's render-texture convention. Flip the output index,
            // not individual gather coordinates (which would reorder taps).
            if (_FsrFlipY > 0.5)
                uv.y = 1.0 - uv.y;
            return uint2(clamp(floor(uv * _FsrOutputSize.xy),
                               float2(0, 0), _FsrOutputSize.xy - 1.0));
        }

        float4 FragEasu(v2f_img input) : SV_Target
        {
            uint4 con0, con1, con2, con3;
            FsrEasuCon(con0, con1, con2, con3,
                       _FsrInputSize.x, _FsrInputSize.y,
                       _FsrInputSize.x, _FsrInputSize.y,
                       _FsrOutputSize.x, _FsrOutputSize.y);
            uint2 pixel = FsrOutputPixel(input.uv);
            float3 color;
            FsrEasuF(color, pixel, con0, con1, con2, con3);

            // EASU reconstructs RGB. Keep alpha from the corresponding source
            // location; the world camera should normally produce opaque alpha.
            float2 sourceUv = (float2(pixel) + 0.5) * _FsrOutputSize.zw;
            float alpha = _MainTex.SampleLevel(fsr_linear_clamp_sampler, sourceUv, 0).a;
            return float4(FsrFinishColor(color), alpha);
        }

        float4 FragRcas(v2f_img input) : SV_Target
        {
            uint4 con;
            FsrRcasCon(con, clamp(_FsrSharpness, 0.0, 2.0));
            float3 color;
            float alpha;
            FsrRcasF(color.r, color.g, color.b, alpha, FsrOutputPixel(input.uv), con);
            return float4(FsrFinishColor(color), alpha);
        }
        ENDHLSL

        Pass
        {
            Name "EASU"
            HLSLPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vert_img
            #pragma fragment FragEasu
            ENDHLSL
        }

        Pass
        {
            Name "RCAS"
            HLSLPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vert_img
            #pragma fragment FragRcas
            ENDHLSL
        }
    }
    Fallback Off
}
