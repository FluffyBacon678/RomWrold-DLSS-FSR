# RimWorld Upscaler

An experimental RimWorld 1.6 mod that renders the colony at a configurable resolution and scales it to the display using **AMD FSR 1** or filtered downsampling. Menus and the normal game interface stay at display resolution.

This is a development preview, not DLSS, temporal FSR, or frame generation. It does not accelerate simulation. Scaling has its own GPU cost; FPS gains are not guaranteed.

## Features

- Genuine AMD EASU upscaling below native resolution and optional RCAS sharpening.
- Separate bilinear mode for comparison.
- Performance presets from 50% to 77%, Native 100%, and 125% / 150% supersampling.
- A reversible 30-240 FPS limiter with an explicit VSync override.
- Immediate settings changes; Ctrl+F8 toggles native rendering.
- Optional status / FPS overlay with actual render dimensions.
- Defaults to disabled. Unsupported views and camera arrangements use native rendering.
- A safe-default reset and live renderer/status diagnostics in the mod settings.

The initial target is **RimWorld 1.6 on Windows / DirectX 11**, colony view only. Planet views, Odyssey maps with world backgrounds, external camera targets, stereo views, and partial-screen camera viewports bypass it. Compatibility with Camera+ and other graphics or camera mods still needs in-game verification.

## Use a built preview

Copy `Mod` to `RimWorld/Mods/RimWorldUpscaler`, enable **RimWorld Upscaler (Preview)** in the mod list, and restart. In **Options > Mod options > RimWorld Upscaler**, choose a scaling method and render resolution, then enable render scaling. No Harmony dependency is needed.

For a sharp image, start with the **Sharper ultrawide preset** or select Ultra Quality with 70% sharpening. Native and supersampling settings trade GPU performance for clarity. Zero sharpening bypasses RCAS. Ctrl+F8 switches to normal rendering for comparison. If status says the FSR bundle is missing, build the shaders; bilinear remains a separate option.

No data is added to saves. Disable the mod to stop its rendering changes. Keep backups for new-mod testing.

## 5120 x 1440 smoke-test settings

| Preset | Colony render size | Output / interface |
| --- | ---: | ---: |
| Performance | 2560 x 720 | 5120 x 1440 |
| Balanced | 3012 x 847 | 5120 x 1440 |
| Quality | 3413 x 960 | 5120 x 1440 |
| Ultra Quality | 3938 x 1108 | 5120 x 1440 |
| Native | 5120 x 1440 | 5120 x 1440 |
| Supersampling | 6400 x 1800 | 5120 x 1440 |
| High supersampling | 7680 x 2160 | 5120 x 1440 |

Start with **FSR 1 / Ultra Quality / 70% sharpening / overlay on**. Compare it with normal rendering using Ctrl+F8 while paused on the same busy colony view. If it is still too soft, try Native at about 35% sharpening. Use 125% supersampling with light sharpening only when clarity matters more than GPU performance. The 150% mode is a high-cost comparison setting.

The FPS cap defaults to off. Select a cap under the mod settings. If VSync is active, it takes priority unless **Override VSync to enforce this cap** is enabled. Turning the cap off restores the target frame rate and VSync setting that were active before the limiter took control.

For the smoke test, check mouse alignment at all four screen edges, crisp UI text, water and weather, fog, selection outlines, Camera+ zoom, planet/map transitions, and fullscreen/window changes. The overlay should report the selected colony render size and the 5120 x 1440 output.

## Why DLSS is not included

True DLSS Super Resolution is temporal: it needs reliable motion vectors, depth, jitter, exposure, camera-reset handling, native SDK binaries, and renderer integration. RimWorld uses Unity's built-in render pipeline; Unity 2022.3 lists DLSS support for HDRP rather than the built-in pipeline. Injecting NVIDIA Streamline into a third-party Unity executable would also require low-level graphics-device and swap-chain ownership that is inappropriate for a safe Workshop mod. See the [Unity render-pipeline comparison](https://docs.unity3d.com/2022.3/Documentation/Manual/render-pipelines-feature-comparison.html) and [NVIDIA Streamline integration guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuide.md).

FSR 1 remains the cross-vendor performance option. Native resolution with RCAS and the new supersampling modes provide sharper alternatives without pretending to be DLSS.

## Compatibility and safety

- No Harmony dependency, gameplay definitions, simulation patches, or save data.
- Disabled by default and removable from an existing save.
- Preflights the shipped FSR player shader after mod loading, then automatically restores the camera and falls back to native rendering when a required condition changes or an exception occurs.
- Leaves the interface at display resolution and only redirects the colony camera during its render.
- Does not claim universal compatibility with camera, post-processing, screenshot, or multiplayer rendering mods. Test those combinations before a Workshop stability claim.
- Declares only RimWorld 1.6 support because that is the version it is compiled against and being tested on.

A local 66-mod Prepatcher test exposed a separate Camera+ 3.4.7 issue: its bundled CrossPromotion 1.1.2 could native-crash the Mods page in `WorkshopItemHook` even while this upscaler was disabled and had not loaded. That stack is a Camera+ promotional-panel failure, not an upscaler rendering failure. Update or repair Camera+ if the crash stack names `Brrainz.CrossPromotion`.

The release folder includes `About/Preview.png` at 640x360, `About/ModIcon.png` at 64x64, a unique package ID, a mod version, supported-version metadata, and third-party license notices.

## Build

Requires a .NET SDK, local RimWorld installation, and a licensed Unity **2022.3.35f1** editor. Do not distribute game or Unity assemblies.

```powershell
.\scripts\Build-Mod.ps1 -RimWorldDir 'E:\SteamLibrary\steamapps\common\RimWorld'
.\scripts\Build-Shaders.ps1 -UnityEditor 'C:\path\to\2022.3.35f1\Editor\Unity.exe'
.\scripts\Package-Mod.ps1
```

Outputs: `Mod/1.6/Assemblies/RimWorldUpscaler.dll` and `Mod/AssetBundles/rimworldupscaler_win`. The shader builder runs GPU tests, builds the Windows-player bundle, and reloads it to verify the shader and both passes. Results and the verified bundle hash go to `.scratch/shader-build-result.json`; packaging rejects a stale or different bundle. Packaging creates a local ZIP and does not publish anything.

`-SkipGpuValidation` supports machines without graphics access. Skipped tests are not passed tests. Resolve licensing failures through Unity Hub before rerunning the shader build.

## Release checks

See [VALIDATION.md](VALIDATION.md) for completed checks and [the shader contract](UnityProject/Assets/Shaders/README.md) for integration details.

Before Workshop publication, verify image orientation, mouse alignment, water, color correction, resize, scene transitions and Camera+. Compare native/FSR/bilinear at identical camera positions and speeds, repeat runs, and report CPU-limited and GPU-limited scenes separately. No improvement is assumed.

AMD's MIT license and original source hashes are in `ThirdParty/FidelityFX-FSR`. This independent mod is not an AMD or NVIDIA product.
