#!/bin/zsh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

START=$SECONDS
NODE_BIN="${QA_NODE:-$(command -v node)}"

if [[ -z "$NODE_BIN" ]]; then
  echo "Node.js was not found" >&2
  exit 1
fi

"$NODE_BIN" tools/qa_pose_matrix.test.mjs

if [[ "${QA_VALIDATE_RUNTIME:-0}" == "1" ]]; then
  "$NODE_BIN" tools/qa_runtime.test.mjs
fi

"$NODE_BIN" tools/capture_pose_qa.mjs

QA_PYTHON="${QA_PYTHON:-}"
if [[ -z "$QA_PYTHON" ]]; then
  for candidate in \
    /usr/local/bin/python3.12 \
    /Users/kiku28/.local/bin/python3.12 \
    /Library/Frameworks/Python.framework/Versions/3.12/bin/python3 \
    python3.12 \
    python3
  do
    if command -v "$candidate" >/dev/null 2>&1 && \
       "$candidate" -c 'import PIL' >/dev/null 2>&1
    then
      QA_PYTHON="$candidate"
      break
    fi
  done
fi

if [[ -z "$QA_PYTHON" ]]; then
  echo "Pillow-enabled Python was not found" >&2
  exit 1
fi

"$QA_PYTHON" tools/make_pose_contact.py

echo "pose QA completed in $((SECONDS - START))s"
