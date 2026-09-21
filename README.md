# Anime Action Local Lab

M1 Max / 32GB 上で、既製キャラクターを主成果物にせず、ローカル生成したアニメ調人型キャラクターとモーションを Unity WebGL まで通す検証プロジェクト。

## Goals

- Blender/Python で新規アニメ調人型を生成
- Humanoid互換を意識した共通骨格
- ローカル Text-to-Motion (HY-Motion 1.0 Lite) の実測・取り込み
- Video-to-Motion (GVHMR) の代替経路
- Unity 6.3 で三人称アクションとして操作
- WebGL build を GitHub Pages でプレビュー
- source/private と build/public を分離

## Baseline

blender/generate_anime_fighter.py は第三者キャラクター/モーションを含まず、新規キャラクター・骨格・基準モーションをコードから生成する。HY-Motion 統合前でも Unity 側の受け口とWebGL品質を検証できるようにする。
