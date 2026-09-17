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

RimWorld loads files in AssetBundles itself. The runtime reuses its loaded bundle, with the _win suffix restricting game auto-loading to Windows. It does not duplicate-load or unload a bundle owned by RimWorld. The Unity project must retain the built-in AssetBundle module; without it Unity can emit a shader data file that looks like UnityFS but lacks the AssetBundle index required by the player.

FSR is implemented with fragment shaders around AMD's original MIT headers. The user's existing -disable-compute-shaders launch option is not changed.

## Build and validation status

The C# runtime compiles cleanly, and both actual FSR fragment programs compile as Shader Model 5.0 without warnings. The GPU suite also exercises both passes, including odd sizes and a 32:9 ultrawide case. These checks do not establish in-game performance improvement.

Unity 2022.3.35f1 is installed at .tools/UnityEditor-2022.3.35f1. With normal desktop access, the active Personal license was detected. Both shader passes completed the D3D11 GPU validation suite on the RTX 2070 SUPER. The build then reopened the Windows-player bundle, verified the shader and both passes, and recorded its SHA-256 hash for the package gate.

A separate test profile is at .scratch/TestProfile. RimWorld 1.6.4871 started there at 5120x1440 on DirectX 11 and successfully loaded the bundle, reporting EASU and RCAS ready. A copied save with the user's 67-mod list reached `OnMapLoaded`, but hidden automation hit existing RimHUD/HugsLib/Verse.Text GUI initialization errors before a stable colony frame. No upscaler code appeared in those stacks, and no gameplay visual pass is recorded from that run.

The final local test copy is installed at E:/SteamLibrary/steamapps/common/RimWorld/Mods/RimWorldUpscaler and is now active in the normal mod list by user request. Its rendering setting still defaults to disabled. The temporary test helper was removed, the prior ModsConfig.xml was backed up, and no original saves were edited.

## Camera+ Mods-page crash

The user's complete mod stack reproduced a native crash when the Mods page opened, including when RimWorld Upscaler was merely visible and disabled. The native stack was `Brrainz.CrossPromotion -> Verse.ModMetaData.GetWorkshopItemHook -> Verse.Steam.WorkshopItemHook`; no upscaler assembly or rendering method appeared. Camera+ 3.4.7 bundles CrossPromotion 1.1.2, which adds the promotional panel that makes this call.

The local test environment backs up the original Camera+ CrossPromotion DLL and uses a same-identity no-op shim. This leaves Camera+'s camera features active and disables only its promotional panel. With the shim, the exact full stack opened the Mods page without a native crash; the upscaler-active 5120x1440 variant remained stable for 15 seconds. This workaround is local and is not distributed in the upscaler package. A Steam update to Camera+ may replace it.

## Next verification

Enable the final local copy and test native/FSR/bilinear, selection alignment, Camera+ zoom, water, color correction, resizing, map transitions, and enable/disable behavior. RimWorld remains the authoritative camera-integration and visual-quality test.

Compare identical paused views first. Measure frame times at fixed game speed/camera/resolution, include upscaling overhead, repeat runs and report variability. Fewer shaded pixels do not guarantee more FPS. Workshop publication remains pending testing.

## Sources

- https://gpuopen.com/fidelityfx-superresolution/
- https://github.com/GPUOpen-Effects/FidelityFX-FSR
- https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS.md
- https://unity.com/releases/editor/whats-new/2022.3.35f1
