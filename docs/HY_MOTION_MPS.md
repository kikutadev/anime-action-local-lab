# HY-Motion 1.0 Lite — M1 Max 32 GB benchmark

Date: 2026-09-21  
Machine: Apple M1 Max / 32 GB unified memory  
Runtime: PyTorch 2.5.1, MPS, macOS 27.0  
Model: HY-Motion-1.0-Lite

## Result

The upstream runtime fell back to CPU on non-CUDA systems even though the model code itself can execute on MPS. The patch at `tools/patches/hymotion-mps.patch` adds an explicit MPS device path and fixes generic device detection.

A DiT-only smoke test succeeded on `mps:0`.

### Full text-to-motion, staged loading

Prompt:

> A swordsman takes one step forward and performs a fast horizontal slash, then returns to a ready stance.

Settings:

- duration: 1.5 s
- output: 45 frames / 22 body rotations
- seed: 42
- validation steps: 12
- CFG: 5.0
- text encoder: CPU/unified-memory stage
- motion DiT: MPS stage

Measured:

| Stage | Time / memory |
| --- | ---: |
| Prompt encode | 19.276 s |
| Text-encoder RSS after encode | 15.240 GiB |
| RSS after releasing text encoder | 0.814 GiB |
| Motion model load | 4.376 s |
| Motion model MPS allocated | 1.718 GiB |
| Motion generation | 3.506 s |
| MPS driver memory after generation | 3.109 GiB |

The stages intentionally do not coexist. This is the important memory-saving change for a 32 GB Mac: Qwen3-8B + CLIP are released before HY-Motion Lite is loaded on MPS.

The output was converted to the Unity runtime clip at `unity/Assets/Resources/HYMotionSlash.json` and is used for the attack animation in the WebGL demo.

## Notes

The 3.506 s generation time was measured after an earlier MPS smoke/generation run on the same machine, so it should not be interpreted as a cold-start benchmark. The stage-level memory figures are more important for the 32 GB feasibility result.
