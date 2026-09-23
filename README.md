# Anime Action Local Lab

高品質なアニメ調3Dキャラクターを Unity WebGL 上で実際に操作し、アクションゲームとして見た時のモデル・モーション・カメラ・操作感を短いサイクルで検証するプロジェクト。

## Current direction

ローカルAIによる人間モデル全生成は、TRELLIS.2 / AniGen-mac まで実機検証した結果、制作コストと出力品質が釣り合わないため本線から外した。

現在は、見た目が既に成立している無料3Dキャラクターをベースにして、ゲーム側の品質を先に詰める。

- VRoid公式 AvatarSample A / B / C を UniVRM でruntimeロード
- Cを既定プレイヤーとして使用
- A / B / C は実行中に切替可能
- Quaternius Universal Animation Library (CC0) をUnity HumanoidでVRoidへretarget
- 立ち姿はIdle_Loop、移動はJog_Fwd_Loopを使用。QAで不自然だったWalk系はproduction locomotionから除外
- HY-Motion 1.0 Liteの既存slash / dodgeを18 Humanoid trackへ差分リターゲット
- Foot / Toes基準＋床Raycastで見た目の足裏接地を補正
- CharacterControllerによる移動・旋回
- 三人称追従カメラ
- 練習用ダミーと近接攻撃判定
- PC: WASD / J / Space
- Mobile: 左スティック / ATTACK / DODGE
- Unity 6.3 WebGL
- GitHub Pagesへデプロイ

Preview:

https://kikutadev.github.io/anime-action-local-lab/

## Visual QA gate

立ち姿・移動姿勢の変更は、固定ポーズと実runtime連番をheadless Chromeで撮影し、contact sheetを目視確認してから公開する。ビルド成功や接地数値だけでは合格扱いにしない。

## Fast iteration rule

速度を優先し、通常は次の順番で検証する。

1. Unity batchmodeでscene生成 + C# compileのみ
2. エラーがなければWebGLを1回だけbuild
3. headless Chrome / CDPで390x844を実操作
4. 問題なければcommit / push / Pages確認

重いAI生成やフルbuildを修正ごとに繰り返さない。

## Assets

VRoid sample avatarsの出典・利用条件は `THIRD_PARTY_ASSETS.md` に記録している。

## Research retained

以下は本線ではないが、ローカル3D生成・モーション生成の検証結果として残す。

- TRELLIS.2 Apple Silicon移植検証
- AniGen-mac: mesh + skeleton + skin weights生成
- HY-Motion 1.0 Lite MPS検証
- Blender/Python自前人型生成

## Local QA process lifecycle

ローカルQAは `tools/qa_runtime.mjs` を唯一のbrowser/server lifecycle ownerとして使う。

- 1回のQA実行ごとに専用HTTP server・専用Chrome profile・動的CDP portを生成する
- 固定のHTTP/CDP portへ接続しない
- 事前起動したChromeへ接続しない
- 旧来の共有QA profileを使わない
- 正常終了・例外・timeout・SIGINT・SIGTERMではrunner自身がcleanupする
- runnerがSIGKILL等で異常終了した場合はcleanup guardianがChrome/serverを限定回収する

capture/debug scriptからChromeやHTTP serverを直接spawnしない。新しいQA scriptは `withQaRuntime(...)` の中でCDP操作だけを行う。
