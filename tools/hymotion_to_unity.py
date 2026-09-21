#!/usr/bin/env python3
"""Convert HY-Motion NPZ output into a compact Unity runtime clip."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

import numpy as np


# SMPL-H indices emitted by HY-Motion -> canonical Unity lab bone names.
TRACKS = {
    "Hips": 0,
    "LeftUpperLeg": 1,
    "RightUpperLeg": 2,
    "Spine": 3,
    "LeftLowerLeg": 4,
    "RightLowerLeg": 5,
    "Chest": 6,
    "LeftFoot": 7,
    "RightFoot": 8,
    "UpperChest": 9,
    "Neck": 12,
    "Head": 15,
    "LeftUpperArm": 16,
    "RightUpperArm": 17,
    "LeftLowerArm": 18,
    "RightLowerArm": 19,
    "LeftHand": 20,
    "RightHand": 21,
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--hymotion-root", type=Path, required=True)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--fps", type=int, default=30)
    args = parser.parse_args()

    root = args.hymotion_root.resolve()
    sys.path.insert(0, str(root))

    import torch
    from hymotion.utils.geometry import matrix_to_quaternion, rot6d_to_rotation_matrix

    data = np.load(args.input)
    rot6d = torch.from_numpy(data["rot6d"][0]).float()
    matrices = rot6d_to_rotation_matrix(rot6d)
    # Normalize the global root orientation to the clip's first frame so every
    # exported clip starts in the canonical Unity facing direction.
    root0 = matrices[0, 0].clone()
    matrices[:, 0] = torch.matmul(root0.transpose(0, 1), matrices[:, 0])
    # HY-Motion quaternions are real-first (w,x,y,z).
    quaternions = matrix_to_quaternion(matrices).cpu().numpy()

    frames = int(rot6d.shape[0])
    tracks = []
    for unity_name, index in TRACKS.items():
        src = quaternions[:, index]
        flat: list[float] = []
        for w, x, y, z in src:
            # SMPL/HY-Motion is right-handed Y-up. Unity is left-handed Y-up.
            # Reflect X rather than Z so the canonical +Z forward axis is kept.
            # R_unity = S R_smpl S, S=diag(-1,1,1).
            flat.extend((float(x), float(-y), float(-z), float(w)))
        tracks.append({"name": unity_name, "rotation": flat})

    translation = data["transl"][0].astype(np.float64)
    translation -= translation[0:1]
    # Derive the initial anatomical frame from generated body keypoints rather
    # than assuming HY-Motion's global forward axis.
    keypoints0 = data["keypoints3d"][0, 0].astype(np.float64)
    right = keypoints0[2] - keypoints0[1]  # right hip - left hip
    right /= np.linalg.norm(right)
    up = keypoints0[12] - keypoints0[0]    # neck - pelvis
    up -= right * np.dot(up, right)
    up /= np.linalg.norm(up)
    forward = np.cross(right, up)
    forward /= np.linalg.norm(forward)
    body_basis = np.stack((right, up, forward), axis=1)
    translation = (body_basis.T @ translation.T).T
    root_translation: list[float] = []
    for x, y, z in translation:
        # X reflection changes handedness while preserving anatomical +Z forward.
        root_translation.extend((float(-x), float(y), float(z)))

    payload = {
        "fps": args.fps,
        "frameCount": frames,
        "duration": frames / args.fps,
        "rootTranslation": root_translation,
        "bones": tracks,
        "source": "HY-Motion-1.0-Lite",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, separators=(",", ":")), encoding="utf-8")
    print(
        json.dumps(
            {
                "output": str(args.output),
                "frames": frames,
                "duration": payload["duration"],
                "tracks": len(tracks),
                "root_end": root_translation[-3:],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
