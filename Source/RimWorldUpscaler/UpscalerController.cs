using System;
using System.IO;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace RimWorldUpscaler
{
    // The map target exists only during rendering. In particular, it must never
    // affect RimWorld / Camera+ picking and world-to-screen conversions in UI.
    public sealed class UpscalerController : MonoBehaviour
    {
        private static UpscalerController instance;
        public static string Status { get; private set; } = "Starting";
        private UpscalerMod mod;
        private Camera mapCamera;
        private Camera presenter;
        private RenderTexture lowTarget;
        private RenderTexture fullTarget;
        private AssetBundle bundle;
        private Material material;
        private bool shaderAttempted;
        private bool shaderReadyLogged;
        private bool firstFrameLogged;
        private string shaderError;
        private string fault;
        private bool lastEnabled;
        private bool ownsCamera;
        private RenderTexture savedTarget;
        private float savedAspect;
        private bool restoreAutomaticAspect;
        private bool savedHdr;
        private Vector4 savedScreenParams;
        private Matrix4x4 savedVp;
        private int preparedFrame = -1;
        private int presentedFrame = -1;
        private int outputWidth;
        private int outputHeight;
        private UpscaleFilter frameFilter;
        private float frameSharpness;
        private float frameSeconds;
        private GUIStyle overlayStyle;
        private string overlayText = "";
        private float nextOverlayUpdate;

        internal static void Initialize(UpscalerMod owner)
        {
            if (instance != null) return;
            var host = new GameObject("RimWorld Upscaler") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            instance = host.AddComponent<UpscalerController>();
            instance.mod = owner;
            Status = "Native rendering (disabled)";
            Log.Message($"[RimWorld Upscaler] Runtime {typeof(UpscalerMod).Assembly.GetName().Version} initialized; " +
                $"renderer={SystemInfo.graphicsDeviceType}, display={Screen.width}x{Screen.height}, enabled={owner.Settings.Enabled}.");

            // Preflight the exact Windows-player bundle after RimWorld finishes
            // loading mod content. A failed preflight remains retryable on the
            // colony map in case another loader was still finishing.
            if (owner.Settings.Enabled && owner.Settings.Filter == UpscaleFilter.Fsr1 &&
                Application.platform == RuntimePlatform.WindowsPlayer &&
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 &&
                GraphicsSettings.currentRenderPipeline == null && !instance.LoadShader())
            {
                Log.Warning("[RimWorld Upscaler] Shader preflight deferred: " + instance.shaderError);
                instance.shaderAttempted = false;
            }
        }

        private void OnEnable()
        {
            Camera.onPreCull += BeforeCamera;
            Camera.onPostRender += AfterCamera;
        }

        private void Update()
        {
            if (mod == null) return;
            try
            {
                if (ownsCamera)
                {
                    RestoreCamera();
                    Fail("Presentation camera did not finish the previous frame.");
                }
                if (Input.GetKeyDown(KeyCode.F8) &&
                    (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    mod.Settings.Enabled = !mod.Settings.Enabled;
                    mod.WriteSettings();
                }
                if (mod.Settings.Enabled && !lastEnabled)
                {
                    fault = null;
                    if (material == null) shaderAttempted = false;
                }
                lastEnabled = mod.Settings.Enabled;
                frameSeconds = frameSeconds <= 0f ? Time.unscaledDeltaTime :
                    Mathf.Lerp(frameSeconds, Time.unscaledDeltaTime, 0.05f);

                string reason;
                if (!CanRender(out reason))
                {
                    Suspend(reason);
                    return;
                }
                Camera current = Find.Camera;
                if (mapCamera != current)
                {
                    RestoreCamera();
                    mapCamera = current;
                    ReleaseTargets();
                }
                if (mod.Settings.Filter == UpscaleFilter.Fsr1 && !LoadShader())
                {
                    Suspend(shaderError);
                    return;
                }
                EnsureTargets();
                EnsurePresenter();
                frameFilter = mod.Settings.Filter;
                frameSharpness = mod.Settings.Sharpness;
                presenter.depth = mapCamera.depth + 0.01f;
                presenter.enabled = true;
                if (presentedFrame < Time.frameCount - 2) Status = "Waiting for colony camera";
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
        }

        private bool CanRender(out string reason)
        {
            reason = null;
            if (!mod.Settings.Enabled) reason = "Native rendering (disabled)";
            else if (fault != null) reason = "Native fallback: " + fault + " Toggle off/on to retry.";
            else if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11 ||
                Application.platform != RuntimePlatform.WindowsPlayer)
                reason = "Native rendering: this preview requires Windows / DirectX 11";
            else if (GraphicsSettings.currentRenderPipeline != null)
                reason = "Native rendering: unsupported render pipeline";
            else if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                reason = "Native rendering: required render textures are unsupported";
            else if (SystemInfo.maxTextureSize > 0 &&
                (Screen.width > SystemInfo.maxTextureSize || Screen.height > SystemInfo.maxTextureSize))
                reason = $"Native rendering: {Screen.width} x {Screen.height} exceeds the GPU texture limit";
            else if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null ||
                LongEventHandler.ShouldWaitForEvent)
                reason = "Native rendering: open a colony map";
            else if (WorldRendererUtility.WorldRendered)
                reason = "Native rendering: planet / orbital background view";
            else if (Find.Camera == null || !Find.Camera.isActiveAndEnabled)
                reason = "Native rendering: colony camera is inactive";
            else if (Find.Camera.targetTexture != null || Find.Camera.targetDisplay != 0 ||
                Find.Camera.rect != new Rect(0f, 0f, 1f, 1f) || Find.Camera.stereoEnabled)
                reason = "Native rendering: another mod owns the camera output";
            else if (Screen.width < 16 || Screen.height < 16)
                reason = "Native rendering: window minimized";
            return reason == null;
        }

        private void EnsurePresenter()
        {
            if (presenter != null) return;
            var host = new GameObject("RimWorld Upscaler Presentation") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            presenter = host.AddComponent<Camera>();
            presenter.enabled = false;
            presenter.cullingMask = 0;
            presenter.clearFlags = CameraClearFlags.Nothing;
            presenter.useOcclusionCulling = false;
            presenter.allowHDR = false;
            presenter.allowMSAA = false;
            presenter.orthographic = true;
            presenter.targetDisplay = 0;
        }

        private void EnsureTargets()
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * mod.Settings.RenderScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * mod.Settings.RenderScale));
            if (lowTarget != null && lowTarget.width == width && lowTarget.height == height &&
                outputWidth == Screen.width && outputHeight == Screen.height && lowTarget.IsCreated() &&
                (mod.Settings.Filter != UpscaleFilter.Fsr1 || fullTarget != null && fullTarget.IsCreated())) return;
            ReleaseTargets();
            outputWidth = Screen.width;
            outputHeight = Screen.height;
            lowTarget = CreateTarget(width, height, 24, "Upscaler world");
            if (mod.Settings.Filter == UpscaleFilter.Fsr1)
                fullTarget = CreateTarget(outputWidth, outputHeight, 0, "Upscaler EASU");
        }

        private static RenderTexture CreateTarget(int width, int height, int depth, string name)
        {
            var target = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1, useMipMap = false, autoGenerateMips = false
            };
            if (!target.Create())
            {
                Object.Destroy(target);
                throw new InvalidOperationException("Unable to allocate rendering texture.");
            }
            return target;
        }

        private bool LoadShader()
        {
            if (material != null) return true;
            if (shaderAttempted) return false;
            shaderAttempted = true;
            string path = Path.Combine(mod.Content.RootDir, "AssetBundles", "rimworldupscaler_win");
            if (!File.Exists(path))
            {
                shaderError = "Native rendering: FSR shader bundle missing. Build shaders or choose Bilinear.";
                return false;
            }
            // RimWorld owns bundles in AssetBundles; loading the same file twice
            // fails in Unity and unloading it here would invalidate game assets.
            if (bundle == null && mod.Content.assetBundles != null)
                foreach (AssetBundle loaded in mod.Content.assetBundles.loadedAssetBundles)
                    if (loaded != null && loaded.name == "rimworldupscaler_win") { bundle = loaded; break; }
            if (bundle != null)
            {
                foreach (Shader shader in bundle.LoadAllAssets<Shader>())
                {
                    if (shader.name != "Hidden/RimWorldUpscaler/FSR1" || !shader.isSupported) continue;
                    material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    if (material.passCount == 2)
                    {
                        if (!shaderReadyLogged)
                        {
                            Log.Message("[RimWorld Upscaler] FSR 1 player shader bundle loaded; EASU and RCAS are ready.");
                            shaderReadyLogged = true;
                        }
                        return true;
                    }
                    Object.Destroy(material);
                    material = null;
                }
            }
            shaderError = "Native rendering: FSR shader could not load or is unsupported on this GPU";
            return false;
        }

        private void BeforeCamera(Camera camera)
        {
            if (mod == null) return;
            try
            {
                if (camera == presenter)
                {
                    // Main-camera OnRenderImage (including vanilla color
                    // correction) has now finished writing to lowTarget.
                    RestoreCamera();
                    return;
                }
                if (camera != mapCamera || presenter == null || !presenter.enabled || ownsCamera) return;
                string reason;
                if (!CanRender(out reason)) { Suspend(reason); return; }
                if (lowTarget == null || !lowTarget.IsCreated() || Screen.width != outputWidth || Screen.height != outputHeight)
                    return;
                savedTarget = camera.targetTexture;
                savedAspect = camera.aspect;
                restoreAutomaticAspect = Mathf.Abs(savedAspect - camera.pixelRect.width / camera.pixelRect.height) < 0.0001f;
                savedHdr = camera.allowHDR;
                savedScreenParams = Shader.GetGlobalVector(ShaderPropertyIDs.MainCameraScreenParams);
                savedVp = Shader.GetGlobalMatrix(ShaderPropertyIDs.MainCameraVP);
                ownsCamera = true;
                camera.targetTexture = lowTarget;
                camera.aspect = savedAspect;
                camera.allowHDR = false;
                Shader.SetGlobalVector(ShaderPropertyIDs.MainCameraScreenParams,
                    new Vector4(lowTarget.width, lowTarget.height, 1f / lowTarget.width, 1f / lowTarget.height));
                Shader.SetGlobalMatrix(ShaderPropertyIDs.MainCameraVP,
                    GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
                preparedFrame = Time.frameCount;
            }
            catch (Exception exception) { Fail(exception.Message); }
        }

        private void AfterCamera(Camera camera)
        {
            if (camera != presenter || preparedFrame != Time.frameCount || lowTarget == null) return;
            RenderTexture active = RenderTexture.active;
            bool srgb = GL.sRGBWrite;
            try
            {
                RestoreCamera();
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                GL.sRGBWrite = linear;
                if (frameFilter == UpscaleFilter.Fsr1)
                {
                    if (material == null || fullTarget == null) throw new InvalidOperationException("FSR resources were lost.");
                    material.SetVector("_FsrInputSize", new Vector4(lowTarget.width, lowTarget.height, 1f / lowTarget.width, 1f / lowTarget.height));
                    material.SetVector("_FsrOutputSize", new Vector4(outputWidth, outputHeight, 1f / outputWidth, 1f / outputHeight));
                    material.SetFloat("_FsrSharpness", 2f * (1f - frameSharpness));
                    material.SetFloat("_FsrLinearColorSpace", linear ? 1f : 0f);
                    material.SetFloat("_FsrFlipY", 0f);
                    Graphics.Blit(lowTarget, fullTarget, material, 0);
                    if (frameSharpness > 0f) Graphics.Blit(fullTarget, (RenderTexture)null, material, 1);
                    else Graphics.Blit(fullTarget, (RenderTexture)null);
                }
                else Graphics.Blit(lowTarget, (RenderTexture)null);
                presentedFrame = Time.frameCount;
                Status = $"{UpscalerMod.FilterLabel(frameFilter)}: {lowTarget.width} x {lowTarget.height} -> {outputWidth} x {outputHeight}";
                if (!firstFrameLogged)
                {
                    Log.Message("[RimWorld Upscaler] First upscaled colony frame presented: " + Status);
                    firstFrameLogged = true;
                }
            }
            catch (Exception exception) { Fail(exception.Message); }
            finally
            {
                GL.sRGBWrite = srgb;
                RenderTexture.active = active;
                preparedFrame = -1;
            }
        }

        private void RestoreCamera()
        {
            if (!ownsCamera) return;
            ownsCamera = false;
            if (mapCamera != null)
            {
                mapCamera.targetTexture = savedTarget;
                if (restoreAutomaticAspect) mapCamera.ResetAspect();
                else mapCamera.aspect = savedAspect;
                mapCamera.allowHDR = savedHdr;
            }
            Shader.SetGlobalVector(ShaderPropertyIDs.MainCameraScreenParams, savedScreenParams);
            Shader.SetGlobalMatrix(ShaderPropertyIDs.MainCameraVP, savedVp);
            savedTarget = null;
        }

        private void Suspend(string reason)
        {
            RestoreCamera();
            if (presenter != null) presenter.enabled = false;
            ReleaseTargets();
            Status = reason;
        }

        private void Fail(string reason)
        {
            RestoreCamera();
            fault = reason;
            if (presenter != null) presenter.enabled = false;
            preparedFrame = -1;
            Status = "Native fallback: " + reason;
            Log.Warning("[RimWorld Upscaler] " + Status);
        }

        private void ReleaseTargets()
        {
            preparedFrame = -1;
            Release(ref lowTarget);
            Release(ref fullTarget);
        }

        private static void Release(ref RenderTexture target)
        {
            if (target == null) return;
            if (RenderTexture.active == target) RenderTexture.active = null;
            target.Release();
            Object.Destroy(target);
            target = null;
        }

        private void OnGUI()
        {
            if (mod == null || !mod.Settings.ShowOverlay || Current.ProgramState != ProgramState.Playing) return;
            if (Event.current.type != EventType.Repaint) return;
            if (Time.unscaledTime >= nextOverlayUpdate)
            {
                nextOverlayUpdate = Time.unscaledTime + 0.25f;
                overlayText = "Upscaler | " + Status + (frameSeconds > 0f ? $" | {1f / frameSeconds:F0} FPS" : "");
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.depth = -100;
                GUI.color = Color.white;
                if (overlayStyle == null) overlayStyle = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
                GUI.Box(new Rect(12, 12, Mathf.Min(Screen.width - 24, 800), 25), overlayText, overlayStyle);
            }
            finally { GUI.matrix = previousMatrix; GUI.depth = previousDepth; GUI.color = previousColor; }
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            Camera.onPostRender -= AfterCamera;
            RestoreCamera();
            if (presenter != null) presenter.enabled = false;
            ReleaseTargets();
        }

        private void OnDestroy()
        {
            if (presenter != null) Object.Destroy(presenter.gameObject);
            if (material != null) Object.Destroy(material);
            if (instance == this) instance = null;
        }
    }
}
