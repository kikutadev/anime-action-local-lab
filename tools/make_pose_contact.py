from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageFont

DEFAULT_ROOT = Path(__file__).resolve().parents[1] / "qa-captures" / "current"
ROOT = Path(os.environ.get("QA_CAPTURE_ROOT", str(DEFAULT_ROOT))).resolve()
MANIFEST = ROOT / "manifest.json"

BACKGROUND = (24, 26, 31)
TEXT = (245, 245, 245)
MUTED = (175, 180, 190)
BORDER = (72, 76, 86)


def load_manifest() -> dict:
    if not MANIFEST.exists():
        raise FileNotFoundError(f"QA manifest not found: {MANIFEST}")
    data = json.loads(MANIFEST.read_text())
    if data.get("schemaVersion") != 2:
        raise ValueError(
            f"Unsupported QA manifest schema: {data.get('schemaVersion')!r}"
        )
    return data


def load_image(filename: str) -> Image.Image:
    path = ROOT / filename
    if not path.exists():
        raise FileNotFoundError(f"QA capture referenced by manifest is missing: {path}")
    return Image.open(path).convert("RGB")


def fit_image(image: Image.Image, width: int, height: int) -> Image.Image:
    fitted = image.copy()
    fitted.thumbnail((width, height), Image.Resampling.LANCZOS)
    return fitted


def draw_centered(
    canvas: Image.Image,
    image: Image.Image,
    x: int,
    y: int,
    width: int,
    height: int,
) -> None:
    fitted = fit_image(image, width, height)
    canvas.paste(
        fitted,
        (
            x + (width - fitted.width) // 2,
            y + (height - fitted.height) // 2,
        ),
    )


def make_matrix(manifest: dict, action_id: str) -> Path:
    views = manifest["views"]
    phases = manifest["actions"][action_id]["phases"]
    captures = {
        (item["phaseId"], item["view"]): item["file"]
        for item in manifest["staticCaptures"]
        if item["kind"] == "matrix" and item["action"] == action_id
    }

    cell_w = 280
    cell_h = 360
    header_w = 155
    header_h = 42
    width = header_w + cell_w * len(views)
    height = header_h + cell_h * len(phases)
    out = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()

    draw.text((12, 14), f"{action_id.upper()} / phase x view", fill=TEXT, font=font)

    for col, view in enumerate(views):
        x0 = header_w + col * cell_w
        draw.text((x0 + 8, 14), view["label"], fill=TEXT, font=font)
        draw.line((x0, 0, x0, height), fill=BORDER)

    for row, phase in enumerate(phases):
        y0 = header_h + row * cell_h
        draw.text((10, y0 + 10), phase["id"], fill=TEXT, font=font)
        draw.text(
            (10, y0 + 28),
            f"t={phase['phase']:.3f}",
            fill=MUTED,
            font=font,
        )
        draw.line((0, y0, width, y0), fill=BORDER)

        for col, view in enumerate(views):
            filename = captures.get((phase["id"], view["id"]))
            if filename is None:
                raise KeyError(
                    f"Manifest is missing {action_id}/{phase['id']}/{view['id']}"
                )
            image = load_image(filename)
            x = header_w + col * cell_w + 6
            y = y0 + 6
            draw_centered(out, image, x, y, cell_w - 12, cell_h - 12)

    output = ROOT / f"matrix_{action_id}.jpg"
    out.save(output, quality=90, optimize=True)
    return output


def make_reference_strip(manifest: dict) -> Path:
    views = manifest["views"]
    captures = {
        item["view"]: item["file"]
        for item in manifest["staticCaptures"]
        if item["kind"] == "reference" and item["action"] == "idle"
    }
    cell_w = 260
    cell_h = 350
    header_h = 40
    width = cell_w * len(views)
    height = header_h + cell_h
    out = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()

    for col, view in enumerate(views):
        x0 = col * cell_w
        draw.text((x0 + 8, 14), view["label"], fill=TEXT, font=font)
        image = load_image(captures[view["id"]])
        draw_centered(out, image, x0 + 6, header_h + 6, cell_w - 12, cell_h - 12)

    output = ROOT / "reference_idle_views.jpg"
    out.save(output, quality=90, optimize=True)
    return output


def make_supplementary_strip(manifest: dict, action_id: str) -> Path:
    supplementary = manifest["supplementaryViews"]
    key_phase = manifest["actions"][action_id]["keyPhase"]
    captures = {
        item["view"]: item["file"]
        for item in manifest["staticCaptures"]
        if item["kind"] == "supplementary"
        and item["action"] == action_id
        and item["phaseId"] == key_phase
    }

    cell_w = 360
    cell_h = 460
    header_h = 42
    width = cell_w * len(supplementary)
    height = header_h + cell_h
    out = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()

    for col, view in enumerate(supplementary):
        x0 = col * cell_w
        draw.text(
            (x0 + 10, 14),
            f"{action_id} / {key_phase} / {view['label']}",
            fill=TEXT,
            font=font,
        )
        image = load_image(captures[view["id"]])
        draw_centered(out, image, x0 + 6, header_h + 6, cell_w - 12, cell_h - 12)

    output = ROOT / f"supplementary_{action_id}.jpg"
    out.save(output, quality=92, optimize=True)
    return output


def make_runtime_contact(
    sequence_name: str,
    frame_names: Iterable[str],
) -> Path:
    frame_names = list(frame_names)
    cols = min(5, max(1, len(frame_names)))
    rows = (len(frame_names) + cols - 1) // cols
    cell_w = 260
    cell_h = 340
    label_h = 28
    out = Image.new(
        "RGB",
        (cell_w * cols, (cell_h + label_h) * rows),
        BACKGROUND,
    )
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()

    for index, frame_name in enumerate(frame_names):
        col = index % cols
        row = index // cols
        x0 = col * cell_w
        y0 = row * (cell_h + label_h)
        draw.text(
            (x0 + 8, y0 + 8),
            f"{sequence_name} {index:02d}",
            fill=TEXT,
            font=font,
        )
        image = load_image(frame_name + ".png")
        draw_centered(
            out,
            image,
            x0 + 5,
            y0 + label_h,
            cell_w - 10,
            cell_h - 5,
        )

    output = ROOT / f"runtime_{sequence_name}_contact.jpg"
    out.save(output, quality=88, optimize=True)
    return output


def make_runtime_gif(
    sequence_name: str,
    frame_names: Iterable[str],
    interval_ms: int,
) -> Path:
    frames = []
    for frame_name in frame_names:
        image = fit_image(load_image(frame_name + ".png"), 420, 540)
        canvas = Image.new("RGB", (420, 540), BACKGROUND)
        canvas.paste(
            image,
            (
                (420 - image.width) // 2,
                (540 - image.height) // 2,
            ),
        )
        frames.append(canvas)

    if not frames:
        raise ValueError(f"Runtime sequence has no frames: {sequence_name}")

    output = ROOT / f"runtime_{sequence_name}.gif"
    frames[0].save(
        output,
        save_all=True,
        append_images=frames[1:],
        duration=max(20, int(interval_ms)),
        loop=0,
        optimize=False,
        disposal=2,
    )
    return output


def write_review_index(outputs: list[Path]) -> Path:
    lines = [
        "# Action QA artifacts",
        "",
        "Generated from `manifest.json`. Review static matrices first, then runtime GIFs.",
        "",
    ]
    lines.extend(f"- `{path.name}`" for path in outputs)
    lines.extend(
        [
            "",
            "## Review criteria",
            "",
            "- silhouette: weapon arc, arm/body overlap, leg separation",
            "- weight: pelvis/chest transfer, push-off, settle",
            "- grounding: planted foot slip, toe-off, landing height",
            "- upper body: chest/shoulder/elbow/wrist chain",
            "- action timing: anticipation, contact/travel, follow-through/recovery",
        ]
    )
    output = ROOT / "README.md"
    output.write_text("\\n".join(lines) + "\\n")
    return output


def main() -> None:
    manifest = load_manifest()
    has_reference = any(
        item.get("kind") == "reference" and item.get("action") == "idle"
        for item in manifest.get("staticCaptures", [])
    )
    outputs: list[Path] = []
    if has_reference:
        outputs.append(make_reference_strip(manifest))

    for action_id in manifest["actions"]:
        outputs.append(make_matrix(manifest, action_id))
        outputs.append(make_supplementary_strip(manifest, action_id))

    for sequence_name, sequence in manifest["runtimeSequences"].items():
        frames = sequence["frames"]
        outputs.append(make_runtime_contact(sequence_name, frames))
        outputs.append(
            make_runtime_gif(
                sequence_name,
                frames,
                int(sequence["intervalMs"]),
            )
        )

    outputs.append(write_review_index(outputs))
    for output in outputs:
        print(output)


if __name__ == "__main__":
    main()
