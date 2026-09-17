# RimWorld Upscaler

An experimental RimWorld 1.6 mod that renders the colony at a lower resolution and upscales it using **AMD FSR 1**. Menus and the normal game interface stay at display resolution.

This is a development preview, not DLSS, temporal FSR, or frame generation. It does not accelerate simulation. Upscaling has its own GPU cost; FPS gains are not guaranteed.

## Features

- Genuine AMD EASU upscaling and optional RCAS sharpening.
- Separate bilinear mode for comparison.
- Ultra Quality (77%), Quality (67%), Balanced (59%), and Performance (50%) resolution per axis.
- Immediate settings changes; Ctrl+F8 toggles native rendering.
- Optional status / FPS overlay with actual render dimensions.
- Defaults to disabled. Unsupported views and camera arrangements use native rendering.
- A safe-default reset and live renderer/status diagnostics in the mod settings.

The initial target is **RimWorld 1.6 on Windows / DirectX 11**, colony view only. Planet views, Odyssey maps with world backgrounds, external camera targets, stereo views, and partial-screen camera viewports bypass it. Compatibility with Camera+ and other graphics or camera mods still needs in-game verification.

## Use a built preview

Copy `Mod` to `RimWorld/Mods/RimWorldUpscaler`, enable **RimWorld Upscaler (Preview)** in the mod list, and restart. In **Options > Mod settings > RimWorld Upscaler**, choose a mode and quality level and enable upscaling. No Harmony dependency is needed.

Start with Quality. Zero sharpening bypasses RCAS. Ctrl+F8 switches to native for comparison. If status says the FSR bundle is missing, build the shaders; bilinear remains a separate option. Shader source alone cannot enable FSR in the game.

No data is added to saves. Disable the mod to stop its rendering changes. Keep backups for new-mod testing.

## 5120 x 1440 smoke-test settings

| Preset | Colony render size | Output / interface |
| --- | ---: | ---: |
| Ultra Quality | 3938 x 1108 | 5120 x 1440 |
| Quality | 3413 x 960 | 5120 x 1440 |
| Balanced | 3012 x 847 | 5120 x 1440 |
| Performance | 2560 x 720 | 5120 x 1440 |

Start with **FSR 1 / Quality / 50% sharpening / overlay on**. Compare it with native using Ctrl+F8 while paused on the same busy colony view. Try Balanced next if the GPU remains the limit. Ultra Quality is the image-quality comparison; Performance is the stress case for artifacts and UI separation.

For the smoke test, check mouse alignment at all four screen edges, crisp UI text, water and weather, fog, selection outlines, Camera+ zoom, planet/map transitions, and fullscreen/window changes. The overlay should report the low-resolution colony size and the 5120 x 1440 output.

## Compatibility and safety

- No Harmony dependency, gameplay definitions, simulation patches, or save data.
- Disabled by default and removable from an existing save.
- Automatically restores the camera and falls back to native rendering when a required condition changes or an exception occurs.
- Leaves the interface at display resolution and only redirects the colony camera during its render.
- Does not claim universal compatibility with camera, post-processing, screenshot, or multiplayer rendering mods. Test those combinations before a Workshop stability claim.
- Declares only RimWorld 1.6 support because that is the version it is compiled against and being tested on.

The release folder includes `About/Preview.png` at 640x360, `About/ModIcon.png` at 64x64, a unique package ID, a mod version, supported-version metadata, and third-party license notices.

## Build

Requires a .NET SDK, local RimWorld installation, and a licensed Unity **2022.3.35f1** editor. Do not distribute game or Unity assemblies.

```powershell
.\scripts\Build-Mod.ps1 -RimWorldDir 'E:\SteamLibrary\steamapps\common\RimWorld'
.\scripts\Build-Shaders.ps1 -UnityEditor 'C:\path\to\2022.3.35f1\Editor\Unity.exe'
.\scripts\Package-Mod.ps1
```

Outputs: `Mod/1.6/Assemblies/RimWorldUpscaler.dll` and `Mod/AssetBundles/rimworldupscaler_win`. The shader builder runs GPU tests and builds the Windows-player bundle. RimWorld itself then provides the authoritative player-bundle load test. Results go to `.scratch/shader-build-result.json`. Packaging creates a local ZIP; it does not publish anything.

`-SkipGpuValidation` supports machines without graphics access. Skipped tests are not passed tests. Resolve licensing failures through Unity Hub before rerunning the shader build.

## Release checks

See [VALIDATION.md](VALIDATION.md) for completed checks and [the shader contract](UnityProject/Assets/Shaders/README.md) for integration details.

Before Workshop publication, verify image orientation, mouse alignment, water, color correction, resize, scene transitions and Camera+. Compare native/FSR/bilinear at identical camera positions and speeds, repeat runs, and report CPU-limited and GPU-limited scenes separately. No improvement is assumed.

AMD's MIT license and original source hashes are in `ThirdParty/FidelityFX-FSR`. This independent mod is not an AMD or NVIDIA product.
