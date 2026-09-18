# Validation status

## Completed

- Runtime compiled against local RimWorld 1.6.4871 / Unity 2022.3.35f1 assemblies, targeting .NET Framework 4.7.2: zero warnings, zero errors.
- Inspected game camera, UI coordinate conversion, water globals, vanilla color correction and Camera+ camera/UI patches.
- Vendored genuine AMD FSR 1 headers, license and pinned source hashes.
- Compiled both actual fragment programs as Shader Model 5.0 with Windows' D3D compiler. This checks HLSL compilation independently of Unity; it is not a GPU image test.
- Parsed build/package scripts and mod XML; verified the packaging script refuses to create an FSR release when the shader bundle is missing.
- Built the Windows player AssetBundle with licensed Unity 2022.3.35f1.
- Passed GPU shader validation on an RTX 2070 SUPER / DirectX 11 for both shader passes, solid colors, finite output, edges, orientation, odd dimensions, a 32:9 ultrawide case, gradients and checker patterns.
- Reloaded the completed Windows-player bundle in Unity, verified its AssetBundle index, shader asset and two passes, and recorded its SHA-256 hash. Packaging refuses a bundle that does not match that verified build.
- Launched RimWorld 1.6.4871 at 5120x1440 on DirectX 11 with an isolated profile. RimWorld loaded the shipped bundle and reported that EASU and RCAS were ready.
- Exercised the normal native-rendering fallback with an intentionally invalid earlier bundle; RimWorld continued without a crash and logged the reason.
- Added the required About metadata plus a 640x360 preview and 64x64 mod icon; the preview is below Steam's 1 MB limit.
- Reproduced the user's native Mods-page crash in an isolated copy of the complete mod list. The stack ends in Camera+ 3.4.7's bundled CrossPromotion 1.1.2 calling `WorkshopItemHook` before the disabled upscaler loads. A local same-identity shim that disables only Camera+'s promotional panel removed the crash.
- Repeated the full-stack test at 5120x1440 with the upscaler active: runtime 0.1.2 initialized with rendering disabled, the Mods page opened, remained stable for 15 seconds, and logged no upscaler errors.
- Built runtime 0.1.3 with 50-77% performance presets, Native 100%, 125% / 150% supersampling, clearer ultrawide guidance, and a reversible FPS limiter; compilation completed with zero warnings and zero errors.
- Ran a Core-only RimWorld 1.6.4871 test at 5120x1440: the settings panel rendered, the FSR bundle loaded, a 30 FPS cap set Unity to 30 with VSync disabled, and the first colony frame presented at 6400x1800 -> 5120x1440.
- Disabled the FPS cap during that run and verified restoration to the pre-existing 120 FPS target and VSync-on state. The temporary probe exited and was removed from the game installation.
- After the user encountered a blank/stuck options panel with a saved 60 FPS cap, version 0.1.4 removed the limiter and nested scrolling UI. A targeted RimWorld test loaded the legacy cap field, rendered the compact panel successfully, ignored the obsolete cap, and retained the game's 120 FPS/VSync-on state.
- A normal user-session log confirmed that 0.1.4 loaded EASU/RCAS and presented a 3198x1676 colony frame into a 2558x1341 window at the selected 125% setting.
- A strict 0.1.5 audit removed the global Ctrl+F8 input hook and its legacy-input assembly reference. A Core-only RimWorld 1.6.4871 probe verified that FSR Native/0%, Bilinear Native, and disabled mode owned no camera, allocated no render targets, and left the presenter disabled.
- Corrected the settings and documentation to describe supersampling as an experimental filtered resolve. No unverified clarity benefit is claimed.

## Pending

- Interactive FSR/bilinear/native/supersampling visual comparison, including mouse alignment and blur assessment.
- Water, color correction, resize, scene transitions and Camera+ interaction.
- Performance measurements. No FPS/TPS improvement has been established.

For a broader isolated test, the active mod list and a copy of the latest save were loaded through a temporary smoke-test helper. Loading reached HugsLib's `OnMapLoaded`, but the hidden automated launch then encountered GUI initialization errors in RimHUD/HugsLib/Verse.Text before a stable rendered frame. No upscaler code appeared in those exception stacks. The helper was removed, the original save was never changed, and no visual pass is claimed from that run. The normal mod list now contains the upscaler by user request, while its rendering setting still defaults to disabled.

An automated hidden-window image-quality attempt produced black screenshots and did not exercise the present path, so it was rejected rather than counted as evidence. The shader build and GPU validation establish that the shaders compile and render test images, and normal RimWorld logs establish real player-bundle loading and presentation. Interactive RimWorld testing remains authoritative for camera integration and image quality. The shader builder records its result and bundle hash in `.scratch/shader-build-result.json`.
