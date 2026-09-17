# FSR 1 shader integration

`Fsr1.shader` provides `Hidden/RimWorldUpscaler/FSR1` with two passes:

| Pass | Input | Destination | Operation |
| --- | --- | --- | --- |
| 0 / `EASU` | Reduced-resolution world camera image | Full-resolution temporary render texture | AMD Edge Adaptive Spatial Upsampling |
| 1 / `RCAS` | EASU temporary render texture | Full-resolution presentation target | AMD Robust Contrast Adaptive Sharpening |

Both passes call the genuine 32-bit reference algorithms from the unchanged AMD
headers in `ThirdParty/FidelityFX`. These are ordinary D3D11 fragment shaders
(shader model 5.0), not compute shaders. They require neither motion vectors nor
an RTX GPU. This is FSR **1**, not FSR 2/3 or DLSS, and does not generate frames.

## Material contract

`Graphics.Blit(source, destination, material, passIndex)` supplies `_MainTex`.
Set these before drawing:

* `_FsrInputSize`: `(lowWidth, lowHeight, 1/lowWidth, 1/lowHeight)`; the entire input
  texture is the active viewport. EASU uses this value; RCAS does not.
* `_FsrOutputSize`: `(fullWidth, fullHeight, 1/fullWidth, 1/fullHeight)`; this must
  equal the EASU destination, RCAS source, and RCAS destination dimensions.
* `_FsrSharpness`: AMD sharpness attenuation in stops, clamped to `0..2`.
  `0` is strongest, `1` halves the sharpening coefficient, and `2` quarters it.
  `0.5` is the default. Disabling sharpening means skipping RCAS, not setting
  sharpness to zero. A UI strength slider may map `stops = 2 * (1 - strength)`.
* `_FsrLinearColorSpace`: `0` for a Gamma Unity project; `1` when source samples
  and fragment output are linear. For ordinary Unity color render textures,
  derive it from `QualitySettings.activeColorSpace == ColorSpace.Linear`.
* `_FsrFlipY`: default `0`; `1` explicitly reverses the source for that pass.
  If a source needs correction, set this only on EASU, then restore `0` on RCAS.

Do not blit a texture into itself. Use separate input, intermediate, and output
textures. All dimensions must be positive integers. Use an ARGB32 SDR camera
target and an ARGB32 full-size intermediate, with single-sample MSAA. Textures
must contain tone-mapped display color; HDR values are not supported.

## Alignment and orientation

The input callbacks use real HLSL `GatherRed/Green/Blue` operations with a
linear/clamp sampler. The native HLSL gather component ordering is the ordering
expected by AMD's implementation. Each fragment derives its integer output
pixel from Unity's `Graphics.Blit` UV, with a floor and edge clamp. AMD's
`FsrEasuCon` supplies the half-texel mapping; do not add another half-texel offset.
RCAS clamps each integer load, including negative neighbor coordinates, because
`Texture2D.Load` ignores sampler addressing modes.

Both passes keep the ordinary Blit UV convention. They do not separately flip
the texture based on graphics API or on `_MainTex_TexelSize`. A camera image with
a different orientation can be corrected with the explicit `_FsrFlipY` flag.
Verify orientation and corner alignment in Unity and in game, including odd
resolutions and a colored/asymmetric test pattern. Never flip each EASU gather
coordinate without also reordering gather components.

## Color space

AMD requires display-referred, nonlinear RGB in `0..1`. Gamma projects pass
their SDR color through directly. In a Linear project the shader applies the
standard sRGB transfer curve to sampled values before EASU/RCAS, and reverses it
before writing the result. Configure source/destination render texture sRGB
flags and `GL.sRGBWrite` consistently with normal Unity color blits; do not
combine `_FsrLinearColorSpace=1` with a manually gamma-encoded linear target.
The temporary render texture must preserve the same color-space convention as
the camera source. Alpha is bilinear through EASU and passed through by RCAS;
this shader is intended for an opaque world camera, not translucent UI layers.

Render the UI after this chain at native resolution. A lower game resolution
would also reduce UI resolution and is a different feature.

## Validation status

The shader source must be imported and packed into an AssetBundle by Unity
2022.3 for the runtime mod. Source files alone are not runtime-loadable shaders.
Full rendering, color-space, camera orientation, and mod compatibility checks
require a Unity editor build and an actual game run. See the project release
checklist before claiming FSR support in a published binary.
