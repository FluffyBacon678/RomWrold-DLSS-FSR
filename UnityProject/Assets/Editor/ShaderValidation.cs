using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace RimWorldUpscaler.EditorTools
{
    // Exercises the actual GPU programs, including odd sizes and render-texture
    // input, so shader compilation alone cannot masquerade as a working upscaler.
    internal static class ShaderValidation
    {
        internal static string Run(Shader shader, string outputDirectory)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new InvalidOperationException("GPU validation requires Direct3D 11 (do not use -nographics).");
            if (!shader.isSupported)
                throw new InvalidOperationException("The FSR shader is unsupported on this GPU.");
            Material material = new Material(shader);
            try
            {
                if (material.passCount != 2)
                    throw new InvalidOperationException("Expected the EASU and RCAS shader passes.");
                material.SetFloat("_FsrLinearColorSpace", 0f);
                material.SetFloat("_FsrSharpness", 0.5f);
                material.SetFloat("_FsrFlipY", 0f);

                Color[] solids = { Color.black, new Color(0.23f, 0.51f, 0.77f, 1f), Color.white };
                foreach (Color solid in solids)
                    Execute(material, 31, 19, 62, 38, (x, y) => solid,
                        pixels => CheckSolid(pixels, solid), null);

                // Distinct corners detect upside-down copies or mismatched pixel
                // origins. Use asymmetric and odd dimensions to catch rounding.
                Execute(material, 31, 19, 47, 29,
                    (x, y) => Corner(x >= 15, y >= 9),
                    pixels => CheckCorners(pixels, 47, 29), null);

                Execute(material, 63, 47, 96, 72,
                    (x, y) => new Color(x / 62f, y / 46f,
                        ((x / 5 + y / 7) % 2 == 0) ? 0.25f : 0.7f, 1f),
                    pixels => { }, Path.Combine(outputDirectory, "fsr-shader-validation"));
                BuildShaders.ThrowIfShaderErrors(shader);
                return "Passed: both passes, black/gray/white preservation, finite colors, all edges, orientation, odd dimensions and gradient/checker pattern (D3D11)";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void Execute(Material material, int inputWidth, int inputHeight, int outputWidth,
            int outputHeight, Func<int, int, Color> pattern, Action<Color[]> check, string previewPrefix)
        {
            Texture2D input = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBAFloat, false, true);
            input.filterMode = FilterMode.Bilinear;
            input.wrapMode = TextureWrapMode.Clamp;
            RenderTexture source = CreateTarget(inputWidth, inputHeight);
            RenderTexture easu = CreateTarget(outputWidth, outputHeight);
            RenderTexture rcas = CreateTarget(outputWidth, outputHeight);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Color[] colors = new Color[inputWidth * inputHeight];
                for (int y = 0; y < inputHeight; y++)
                    for (int x = 0; x < inputWidth; x++)
                        colors[y * inputWidth + x] = pattern(x, y);
                input.SetPixels(colors);
                input.Apply(false, false);
                Graphics.Blit(input, source);
                material.SetVector("_FsrInputSize", new Vector4(inputWidth, inputHeight, 1f / inputWidth, 1f / inputHeight));
                material.SetVector("_FsrOutputSize", new Vector4(outputWidth, outputHeight, 1f / outputWidth, 1f / outputHeight));
                Graphics.Blit(source, easu, material, 0);
                Graphics.Blit(easu, rcas, material, 1);
                ValidateOutput(easu, check, previewPrefix == null ? null : previewPrefix + "-easu.png");
                ValidateOutput(rcas, check, previewPrefix == null ? null : previewPrefix + "-rcas.png");
                if (previewPrefix != null)
                    ValidateOutput(source, pixels => { }, previewPrefix + "-input.png");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(input);
                DestroyTarget(source);
                DestroyTarget(easu);
                DestroyTarget(rcas);
            }
        }

        private static RenderTexture CreateTarget(int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.filterMode = FilterMode.Bilinear;
            target.wrapMode = TextureWrapMode.Clamp;
            if (!target.Create())
                throw new InvalidOperationException("Could not create the shader validation render target.");
            return target;
        }

        private static void DestroyTarget(RenderTexture target)
        {
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void ValidateOutput(RenderTexture target, Action<Color[]> check, string previewPath)
        {
            Texture2D readback = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            Texture2D preview = null;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false, false);
                Color[] pixels = readback.GetPixels();
                foreach (Color pixel in pixels)
                {
                    for (int component = 0; component < 4; component++)
                    {
                        float value = pixel[component];
                        if (float.IsNaN(value) || float.IsInfinity(value) || value < -0.002f || value > 1.002f)
                            throw new InvalidOperationException("FSR produced a non-finite or out-of-range pixel: " + pixel);
                    }
                    if (Mathf.Abs(pixel.a - 1f) > 0.002f)
                        throw new InvalidOperationException("FSR changed opaque source alpha.");
                }
                check(pixels);
                if (previewPath != null)
                {
                    preview = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                    preview.SetPixels(pixels);
                    preview.Apply(false, false);
                    File.WriteAllBytes(previewPath, preview.EncodeToPNG());
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readback);
                if (preview != null)
                    UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        private static void CheckSolid(Color[] pixels, Color expected)
        {
            foreach (Color pixel in pixels)
                CheckColor(pixel, expected);
        }

        private static void CheckCorners(Color[] pixels, int width, int height)
        {
            CheckColor(pixels[2 * width + 2], Corner(false, false));
            CheckColor(pixels[2 * width + width - 3], Corner(true, false));
            CheckColor(pixels[(height - 3) * width + 2], Corner(false, true));
            CheckColor(pixels[(height - 3) * width + width - 3], Corner(true, true));
        }

        private static Color Corner(bool right, bool upper)
        {
            if (upper)
                return right ? new Color(0.7f, 0.7f, 0.15f, 1f) : new Color(0.15f, 0.25f, 0.8f, 1f);
            return right ? new Color(0.2f, 0.8f, 0.3f, 1f) : new Color(0.8f, 0.2f, 0.3f, 1f);
        }

        private static void CheckColor(Color actual, Color expected)
        {
            if (Mathf.Abs(actual.r - expected.r) > 0.025f || Mathf.Abs(actual.g - expected.g) > 0.025f ||
                Mathf.Abs(actual.b - expected.b) > 0.025f)
                throw new InvalidOperationException("FSR color/orientation check failed: " + actual + "; expected " + expected);
        }
    }
}
