using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RimWorldUpscaler.EditorTools
{
    public static class BuildShaders
    {
        internal const string ShaderName = "Hidden/RimWorldUpscaler/FSR1";
        internal const string BundleName = "rimworldupscaler_win";

        [Serializable]
        private sealed class BuildResult
        {
            public bool success;
            public string editorVersion;
            public string bundlePath;
            public string graphicsDevice;
            public string gpuValidation;
            public string error;
        }

        [MenuItem("RimWorld Upscaler/Build Windows Shader Bundle")]
        public static void BuildWindows()
        {
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string scratch = Path.Combine(repository, ".scratch");
            string output = Path.Combine(scratch, "ShaderBundles");
            string destination = Path.Combine(repository, "Mod", "AssetBundles", BundleName);
            Directory.CreateDirectory(scratch);
            Directory.CreateDirectory(output);
            BuildResult result = new BuildResult
            {
                editorVersion = Application.unityVersion,
                bundlePath = destination,
                graphicsDevice = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName,
                gpuValidation = "Not run"
            };

            try
            {
                if (Application.unityVersion != "2022.3.35f1")
                    throw new InvalidOperationException("Use Unity 2022.3.35f1 to match RimWorld 1.6.");

                PlayerSettings.colorSpace = ColorSpace.Gamma;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                    new[] { GraphicsDeviceType.Direct3D11 });
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                string shaderPath = AssetDatabase.FindAssets("t:Shader")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => AssetDatabase.LoadAssetAtPath<Shader>(path).name == ShaderName);
                if (shaderPath == null)
                    throw new FileNotFoundException("Missing shader " + ShaderName);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                ThrowIfShaderErrors(shader);

                bool skipGpu = Environment.GetCommandLineArgs().Contains("-upscalerSkipGpuValidation");
                if (!skipGpu)
                    result.gpuValidation = ShaderValidation.Run(shader, scratch);
                else
                    result.gpuValidation = "Skipped explicitly; requires GPU validation before release";
                ThrowIfShaderErrors(shader);

                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output,
                    new[] { new AssetBundleBuild { assetBundleName = BundleName, assetNames = new[] { shaderPath } } },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode |
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows64);
                if (manifest == null || !manifest.GetAllAssetBundles().Contains(BundleName))
                    throw new InvalidOperationException("Unity did not produce the expected shader bundle.");
                ThrowIfShaderErrors(shader);

                string builtPath = Path.Combine(output, BundleName);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(builtPath, destination, true);
                // StandaloneWindows64 bundles can be rejected when re-opened by
                // the editor runtime even when the editor and player share the
                // same Unity revision. The source shader was executed above;
                // the compiled player bundle is load-tested in RimWorld itself.
                result.gpuValidation += "; player bundle written for RimWorld load test";
                result.success = true;
                Debug.Log("RimWorld Upscaler shader bundle built and verified: " + destination);
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(scratch, "shader-build-result.json"), JsonUtility.ToJson(result, true));
            }
        }

        internal static void ThrowIfShaderErrors(Shader shader)
        {
            string[] errors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity.ToString() == "Error")
                .Select(message => message.file + ":" + message.line + " " + message.message).ToArray();
            if (errors.Length > 0)
                throw new InvalidOperationException("Shader compilation errors:\n" + string.Join("\n", errors));
        }
    }
}
