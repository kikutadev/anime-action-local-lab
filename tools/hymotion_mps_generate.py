#!/usr/bin/env python3
"""Generate a HY-Motion clip on Apple Silicon with staged model loading.

The upstream demo keeps Qwen3-8B, CLIP and the motion DiT resident together.
On a 32 GB unified-memory Mac this script instead:
1. encodes the prompt,
2. moves the prompt features to CPU,
3. releases Qwen/CLIP,
4. loads HY-Motion Lite on MPS,
5. generates the motion and writes a compact NPZ/JSON artifact.

Requires the MPS patch in tools/patches/hymotion-mps.patch to be applied to the
external HY-Motion checkout.
"""

from __future__ import annotations

import argparse
import gc
import json
import os
from pathlib import Path
import sys
import time

import numpy as np
import psutil


def memory_snapshot(torch, label: str) -> dict:
    rss = psutil.Process().memory_info().rss
    snapshot = {"label": label, "rss_gib": round(rss / 2**30, 3)}
    if torch.backends.mps.is_available():
        snapshot["mps_allocated_gib"] = round(torch.mps.current_allocated_memory() / 2**30, 3)
        try:
            snapshot["mps_driver_gib"] = round(torch.mps.driver_allocated_memory() / 2**30, 3)
        except AttributeError:
            pass
    print("[memory]", snapshot, flush=True)
    return snapshot


def release_mps(torch) -> None:
    gc.collect()
    if torch.backends.mps.is_available():
        torch.mps.empty_cache()
    gc.collect()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--hymotion-root", type=Path, required=True)
    parser.add_argument("--prompt", default="A swordsman takes one step forward and performs a fast horizontal slash.")
    parser.add_argument("--duration", type=float, default=1.5)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--steps", type=int, default=8)
    parser.add_argument("--cfg-scale", type=float, default=5.0)
    parser.add_argument("--text-device", choices=("cpu", "mps"), default="cpu")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    root = args.hymotion_root.resolve()
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")
    os.environ["USE_HF_MODELS"] = "0"
    sys.path.insert(0, str(root))
    os.chdir(root)

    import torch

    if not (torch.backends.mps.is_built() and torch.backends.mps.is_available()):
        raise RuntimeError("Apple Metal/MPS is not available.")

    from hymotion.network.text_encoders.text_encoder import HYTextModel
    from hymotion.utils.t2m_runtime import T2MRuntime

    timings: dict[str, float] = {}
    memories: list[dict] = [memory_snapshot(torch, "start")]

    # Stage 1: only prompt encoders are resident.
    started = time.perf_counter()
    encoder = HYTextModel(llm_type="qwen3", max_length_llm=128)
    encoder.eval().to(torch.device(args.text_device))
    memories.append(memory_snapshot(torch, "text_encoder_loaded"))

    with torch.inference_mode():
        vtxt, ctxt, ctxt_length = encoder.encode([args.prompt])
        hidden_cpu = {
            "text_vec_raw": vtxt.detach().to("cpu"),
            "text_ctxt_raw": ctxt.detach().to("cpu"),
            "text_ctxt_raw_length": ctxt_length.detach().to("cpu"),
        }
    timings["encode_prompt_seconds"] = round(time.perf_counter() - started, 3)
    memories.append(memory_snapshot(torch, "prompt_encoded"))

    del vtxt, ctxt, ctxt_length, encoder
    release_mps(torch)
    memories.append(memory_snapshot(torch, "text_encoder_released"))

    # Stage 2: only the motion generator is resident.
    config = root / "ckpts/tencent/HY-Motion-1.0-Lite/config.yml"
    checkpoint = root / "ckpts/tencent/HY-Motion-1.0-Lite/latest.ckpt"
    started = time.perf_counter()
    runtime = T2MRuntime(
        config_path=str(config),
        ckpt_name=str(checkpoint),
        skip_text=True,
        disable_prompt_engineering=True,
    )
    pipeline = runtime.pipelines[0]
    pipeline.validation_steps = args.steps
    device = next(pipeline.parameters()).device
    timings["load_motion_model_seconds"] = round(time.perf_counter() - started, 3)
    memories.append(memory_snapshot(torch, "motion_model_loaded"))

    hidden = {
        key: value.to(device) if hasattr(value, "to") else value
        for key, value in hidden_cpu.items()
    }
    del hidden_cpu
    release_mps(torch)

    started = time.perf_counter()
    with torch.inference_mode():
        result = pipeline.generate(
            args.prompt,
            [args.seed],
            args.duration,
            cfg_scale=args.cfg_scale,
            hidden_state_dict=hidden,
        )
    timings["generate_motion_seconds"] = round(time.perf_counter() - started, 3)
    memories.append(memory_snapshot(torch, "motion_generated"))

    # Keep the artifact engine-agnostic. Unity conversion is handled separately.
    arrays: dict[str, np.ndarray] = {}
    for name in ("transl", "rot6d", "keypoints3d", "root_rotations_mat", "latent_denorm"):
        value = result.get(name)
        if value is not None:
            arrays[name] = value.detach().cpu().numpy()

    np.savez_compressed(output, **arrays)
    metadata = {
        "prompt": args.prompt,
        "duration_seconds": args.duration,
        "seed": args.seed,
        "steps": args.steps,
        "cfg_scale": args.cfg_scale,
        "device": str(device),
        "frames": int(arrays["rot6d"].shape[1]) if "rot6d" in arrays else None,
        "joints": int(arrays["rot6d"].shape[2]) if "rot6d" in arrays else None,
        "timings": timings,
        "memory": memories,
        "array_shapes": {key: list(value.shape) for key, value in arrays.items()},
    }
    output.with_suffix(".json").write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(json.dumps(metadata, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
