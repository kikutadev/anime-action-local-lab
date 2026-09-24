#!/bin/zsh
set -euo pipefail

ROOT="${0:A:h:h}"
BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity}"
HYMOTION_ROOT="${HYMOTION_ROOT:-$ROOT/../.tmp/HY-Motion-1.0}"
CHARACTER_SPEC="${CHARACTER_SPEC:-$ROOT/blender/specs/vanguard.json}"

if [[ "$CHARACTER_SPEC" != /* ]]; then
  CHARACTER_SPEC="$ROOT/$CHARACTER_SPEC"
fi
if [[ "$HYMOTION_ROOT" != /* ]]; then
  HYMOTION_ROOT="$ROOT/$HYMOTION_ROOT"
fi

echo "[1/3] Prepare runtime assets"
if [[ "${RUN_LEGACY_CHARACTER:-0}" == "1" ]]; then
  echo "Generate legacy Blender anime fighter from $CHARACTER_SPEC"
  "$BLENDER_BIN" -b --python "$ROOT/blender/generate_anime_fighter.py" -- --spec "$CHARACTER_SPEC"
  mkdir -p "$ROOT/unity/Assets/Models"
  cp "$ROOT/generated/anime_fighter.fbx" "$ROOT/unity/Assets/Models/AnimeFighter.fbx"
else
  echo "Skip legacy Blender fighter (set RUN_LEGACY_CHARACTER=1 to regenerate)"
fi

if [[ "${RUN_HYMOTION:-0}" == "1" ]]; then
  echo "Generate HY-Motion attack and dodge"
  PY="$HYMOTION_ROOT/.venv/bin/python"
  if [[ ! -x "$PY" ]]; then
    echo "HY-Motion venv not found: $PY" >&2
    exit 2
  fi

  "$PY" "$ROOT/tools/hymotion_mps_generate.py" \
    --hymotion-root "$HYMOTION_ROOT" \
    --prompt "${MOTION_PROMPT:-A swordsman takes one step forward and performs a fast horizontal slash, then returns to a ready stance.}" \
    --duration "${MOTION_DURATION:-1.5}" \
    --seed "${MOTION_SEED:-42}" \
    --steps "${MOTION_STEPS:-12}" \
    --text-device "${TEXT_DEVICE:-cpu}" \
    --output "$ROOT/generated/hymotion/slash.npz"

  "$PY" "$ROOT/tools/hymotion_to_unity.py" \
    --hymotion-root "$HYMOTION_ROOT" \
    --input "$ROOT/generated/hymotion/slash.npz" \
    --output "$ROOT/unity/Assets/Resources/HYMotionSlash.json"

  "$PY" "$ROOT/tools/hymotion_mps_generate.py" \
    --hymotion-root "$HYMOTION_ROOT" \
    --prompt "${DODGE_PROMPT:-A swordsman makes a fast evasive backward step, lowering the torso and keeping the sword ready, then returns to a combat stance.}" \
    --duration "${DODGE_DURATION:-1.0}" \
    --seed "${DODGE_SEED:-84}" \
    --steps "${MOTION_STEPS:-12}" \
    --text-device "${TEXT_DEVICE:-cpu}" \
    --output "$ROOT/generated/hymotion/dodge.npz"

  "$PY" "$ROOT/tools/hymotion_to_unity.py" \
    --hymotion-root "$HYMOTION_ROOT" \
    --input "$ROOT/generated/hymotion/dodge.npz" \
    --output "$ROOT/unity/Assets/Resources/HYMotionDodge.json"
else
  echo "Keep checked-in HY-Motion clips (set RUN_HYMOTION=1 to regenerate)"
fi

echo "[2/3] Build verified Unity WebGL"
"$UNITY_BIN" \
  -batchmode -nographics -quit \
  -projectPath "$ROOT/unity" \
  -buildTarget WebGL \
  -executeMethod AnimeActionWebBuild.BuildWebGl \
  -logFile "$ROOT/unity-build.log"

if grep -Eq "error CS|Build failed|Aborting batchmode due to fatal error" "$ROOT/unity-build.log"; then
  echo "Unity build contains fatal errors" >&2
  grep -nE "error CS|Build failed|Aborting batchmode due to fatal error" "$ROOT/unity-build.log" >&2
  exit 3
fi

if ! grep -q "WebGL build succeeded:" "$ROOT/unity-build.log"; then
  echo "Unity exited without the WebGL success marker" >&2
  tail -120 "$ROOT/unity-build.log" >&2
  exit 4
fi

echo "[3/3] Done"
grep -E "HY-Motion asset acceptance|asset acceptance|scene acceptance|UAL_CONTROLLER|WebGL build succeeded" "$ROOT/unity-build.log" || true
