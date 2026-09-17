# Validation status

## Completed

- Runtime compiled against local RimWorld 1.6.4871 / Unity 2022.3.35f1 assemblies, targeting .NET Framework 4.7.2: zero warnings, zero errors.
- Inspected game camera, UI coordinate conversion, water globals, vanilla color correction and Camera+ camera/UI patches.
- Vendored genuine AMD FSR 1 headers, license and pinned source hashes.
- Compiled both actual fragment programs as Shader Model 5.0 with Windows' D3D compiler. This checks HLSL compilation independently of Unity; it is not a GPU image test.
- Parsed build/package scripts and mod XML; verified the packaging script refuses to create an FSR release when the shader bundle is missing.
- Built the Windows player AssetBundle with licensed Unity 2022.3.35f1.
- Passed GPU shader validation on an RTX 2070 SUPER / DirectX 11 for both shader passes, solid colors, finite output, edges, orientation, odd dimensions, gradients and checker patterns.
- Added the required About metadata plus a 640x360 preview and 64x64 mod icon; the preview is below Steam's 1 MB limit.

## Pending

- RimWorld player-bundle loading and in-game rendering remain pending. A normal user game was already running during the final check, so it was deliberately left untouched rather than launching a competing test instance.
- In-game FSR/bilinear/native visuals and mouse alignment.
- Water, color correction, resize, scene transitions and Camera+ interaction.
- Performance measurements. No FPS/TPS improvement has been established.

An isolated game profile was previously launched without changing the normal mod list or saves. Window inspection timed out waiting for computer-use app approval; no in-game visual check is marked as passed.

The shader build and GPU validation establish that the shaders compile and render test images. RimWorld remains the authoritative test of player-bundle loading and integration with the game camera. The shader builder records its result in `.scratch/shader-build-result.json`.
