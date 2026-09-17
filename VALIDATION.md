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

## Pending

- First colony-frame presentation and in-game FSR/bilinear/native visual comparison, including mouse alignment.
- Water, color correction, resize, scene transitions and Camera+ interaction.
- Performance measurements. No FPS/TPS improvement has been established.

For a broader isolated test, the active mod list and a copy of the latest save were loaded through a temporary smoke-test helper. Loading reached HugsLib's `OnMapLoaded`, but the hidden automated launch then encountered GUI initialization errors in RimHUD/HugsLib/Verse.Text before a stable rendered frame. No upscaler code appeared in those exception stacks. The helper was removed, the original save was never changed, and no visual pass is claimed from that run. The normal mod list now contains the upscaler by user request, while its rendering setting still defaults to disabled.

The shader build and GPU validation establish that the shaders compile and render test images, and the isolated RimWorld launch establishes real player-bundle loading. Interactive RimWorld testing remains authoritative for camera integration and image quality. The shader builder records its result and bundle hash in `.scratch/shader-build-result.json`.
