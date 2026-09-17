# RimWorld Upscaler project decisions

Updated 2026-09-17.

The chosen direction is graphics upscaling. The user explicitly prefers lower-resolution world rendering and FSR functionality even when CPU work limits the resulting FPS. The earlier CPU-optimization proposal is superseded. Combat Extended's compatibility message is outside scope.

## Implemented preview scope

- RimWorld 1.6, Windows, DirectX 11; colony-map camera only.
- Genuine AMD FSR 1 EASU spatial upscaling and optional RCAS sharpening.
- Separate bilinear baseline; four quality presets; native-resolution UI.
- Immediate settings, FPS/status overlay and Ctrl+F8 native-rendering toggle.
- No simulation patches or save-game components.
- Planet views, orbital backgrounds, stereo/partial viewports and external targets bypass upscaling.
- DLSS and temporal FSR are future research, not implemented features.

## Integration findings

The local game is RimWorld 1.6.4871 on Unity 2022.3.35f1 and DirectX 11. Camera+ uses the main camera for screen/world conversion and caches results during Update. Camera target changes must therefore be restricted to the rendering portion of the frame.

Vanilla ColorCorrectionCurves uses OnRenderImage after the map camera's OnPostRender. A later presentation camera lets that effect finish, restores the main camera, and composites the low-resolution image before UI rendering. Camera aspect/HDR and water globals are restored. The water-depth subcamera stays native for this preview; water orientation still needs a visual test.

RimWorld loads files in AssetBundles itself. The runtime reuses its loaded bundle, with the _win suffix restricting game auto-loading to Windows. It does not duplicate-load or unload a bundle owned by RimWorld.

FSR is implemented with fragment shaders around AMD's original MIT headers. The user's existing -disable-compute-shaders launch option is not changed.

## Build status and blocker

The C# runtime compiles cleanly, and both actual FSR fragment programs compile as Shader Model 5.0 without warnings. These are compilation checks, not evidence of a rendered image or performance improvement.

Unity 2022.3.35f1 is installed at .tools/UnityEditor-2022.3.35f1. With normal desktop access, the active Personal license was detected. Both shader passes completed the D3D11 GPU validation suite on the RTX 2070 SUPER, and the Windows-player bundle was created. The editor does not reopen that player-targeted bundle; RimWorld remains the authoritative bundle load test.

A separate test profile is at .scratch/TestProfile and a development mod copy is at E:/SteamLibrary/steamapps/common/RimWorld/Mods/RimWorldUpscalerDev. The normal mod list and saves were not edited. Computer-use inspection of the test game timed out awaiting app permission; no gameplay visual pass is recorded.

## Next verification

Run scripts/Build-Shaders.ps1 after license activation. It imports the shader, executes EASU/RCAS GPU tests, builds the bundle, reloads it, and validates the bundled shader. Then test native/FSR/bilinear, selection alignment, Camera+ zoom, water, color correction, resizing, map transitions, and enable/disable in the isolated profile.

Compare identical paused views first. Measure frame times at fixed game speed/camera/resolution, include upscaling overhead, repeat runs and report variability. Fewer shaded pixels do not guarantee more FPS. Workshop publication remains pending testing.

## Sources

- https://gpuopen.com/fidelityfx-superresolution/
- https://github.com/GPUOpen-Effects/FidelityFX-FSR
- https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS.md
- https://unity.com/releases/editor/whats-new/2022.3.35f1
