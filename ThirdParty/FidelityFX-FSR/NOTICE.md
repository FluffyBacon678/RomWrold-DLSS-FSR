# AMD FidelityFX Super Resolution 1

This project includes the original `ffx_a.h` and `ffx_fsr1.h` from:

* Repository: https://github.com/GPUOpen-Effects/FidelityFX-FSR
* Revision: `a21ffb8f6c13233ba336352bdff293894c706575`
* Upstream directory: `ffx-fsr/`
* Upstream header version: `v1.20210629`
* Retrieved: 2026-09-17
* Copyright: (c) 2021 Advanced Micro Devices, Inc.
* License: MIT; the original text is included in `LICENSE.txt` and in each header.

The vendored headers are unchanged. The surrounding Unity ShaderLab material,
texture callbacks, pixel-coordinate conversion, and color-space handling are
project integration code and are not part of AMD's upstream implementation.

SHA-256 hashes of the original downloaded files:

| File | SHA-256 |
| --- | --- |
| `ffx_a.h` | `f60e2722fcd13989523b9164d776ab382b3692791767f3bf8bb19967f763f3fb` |
| `ffx_fsr1.h` | `93c3922362ea7fc99cbcc698ca30c98de4f8c246d1fbb0b09e015ddef38ce3a5` |
| `LICENSE.txt` | `db089274ce766da70f5b7d791029c3486f9f9e27c8c79c652689603d3192e802` |

Redistribute `LICENSE.txt` with mod releases containing these algorithms,
including when the shader is delivered compiled inside an AssetBundle.
FidelityFX and FSR are AMD names; this independent mod is not endorsed by AMD.
