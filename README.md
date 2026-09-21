# Anime Action Local Lab

M1 Max / 32GB 上で、既製キャラクターを主成果物にせず、ローカル生成したアニメ調人型キャラクターとモーションを Unity WebGL まで通す検証プロジェクト。

## Current state

- Blender/Python でアニメ調人型・骨格・剣を生成
- キャラ外観を JSON spec で差し替え、共通骨格のまま別キャラを再生成
- Blender 生成 FBX を Unity の操作キャラとして使用
- HY-Motion 1.0 Lite を Apple Silicon / MPS で実行
- Qwen3-8B + CLIP と motion DiT を段階ロードして 32 GB 内に収める
- HY-Motion の SMPL-H 主要18トラックを Unity 骨格へリターゲット
- 斬撃 45f / 回避 30f をローカル生成し、実ゲーム操作へ接続
- 移動・カメラ・回避・敵への斬撃判定を実装
- Unity 6.3 WebGL を GitHub Pages へデプロイ
- ビルド前にモデル骨格・モーションクリップを acceptance validation

Preview:

https://kikutadev.github.io/anime-action-local-lab/

## Local rebuild

通常の再ビルドは次で、Blender モデル生成 → Unity Assets 反映 → acceptance → WebGL build まで実行する。

```bash
./tools/rebuild_local.sh
```

別キャラ仕様で再生成する場合:

```bash
CHARACTER_SPEC=blender/specs/rose_duelist.json ./tools/rebuild_local.sh
```

HY-Motion の斬撃・回避まで再生成する場合:

```bash
RUN_HYMOTION=1 ./tools/rebuild_local.sh
```

主要環境変数:

- `BLENDER_BIN`: Blender executable
- `UNITY_BIN`: Unity executable
- `HYMOTION_ROOT`: external HY-Motion checkout
- `CHARACTER_SPEC`: Blender character spec JSON; 既定は `blender/specs/vanguard.json`
- `MOTION_PROMPT`: Text-to-Motion prompt
- `MOTION_DURATION`: motion duration seconds
- `MOTION_STEPS`: validation/inference steps
- `MOTION_SEED`: attack seed
- `DODGE_PROMPT`, `DODGE_DURATION`, `DODGE_SEED`: dodge generation
- `TEXT_DEVICE`: `cpu` or `mps`; 32 GB Mac では staged loading のため `cpu` を既定

## Structure

- `blender/generate_anime_fighter.py`: 自前アニメ調キャラクター生成
- `blender/specs/`: 色・目・髪・肩・コート丈などのキャラ仕様
- `tools/hymotion_mps_generate.py`: Apple Silicon 向け staged Text-to-Motion
- `tools/hymotion_to_unity.py`: HY-Motion 出力 → Unity runtime clip
- `tools/patches/hymotion-mps.patch`: upstream HY-Motion の MPS 対応差分
- `unity/`: Unity runtime/editor
- `publish/`: ローカルで検証済みの GitHub Pages 配信物
- `docs/HY_MOTION_MPS.md`: M1 Max 32 GB 実測

## Design rule

第三者の完成済みキャラクターを主成果物にはしない。フリーアセットは環境・VFX・補助用途には利用可能だが、キャラクター生成・骨格・モーション生成の技術検証がこの repo の中心。
