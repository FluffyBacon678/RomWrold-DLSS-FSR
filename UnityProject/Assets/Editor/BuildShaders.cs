using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            public string bundleSha256;
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
                PlayerSettings.stripEngineCode = false;
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
                ValidatePlayerBundle(builtPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(builtPath, destination, true);
                result.bundleSha256 = Sha256(destination);
                result.gpuValidation += "; Windows player bundle reload passed";
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

        private static void ValidatePlayerBundle(string path)
        {
            AssetBundle playerBundle = null;
            Material playerMaterial = null;
            try
            {
                playerBundle = AssetBundle.LoadFromFile(path);
                if (playerBundle == null)
                    throw new InvalidOperationException("Unity could not reload the Windows player AssetBundle.");
                Shader playerShader = playerBundle.LoadAllAssets<Shader>()
                    .FirstOrDefault(candidate => candidate.name == ShaderName);
                if (playerShader == null || !playerShader.isSupported)
                    throw new InvalidOperationException("The Windows player AssetBundle does not contain a supported FSR shader.");
                playerMaterial = new Material(playerShader);
                if (playerMaterial.passCount != 2)
                    throw new InvalidOperationException("The bundled FSR shader does not contain EASU and RCAS.");
            }
            finally
            {
                if (playerMaterial != null) UnityEngine.Object.DestroyImmediate(playerMaterial);
                if (playerBundle != null) playerBundle.Unload(true);
            }
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
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
