# Third-party assets

## VRoid sample avatars

The model-quality showcase uses the following VRoid sample models:

- AvatarSample_A.vrm
- AvatarSample_B.vrm
- AvatarSample_C.vrm

Source used for this repository:
https://github.com/madjin/vrm-samples
commit: e16eb187100149a315ad92c3c9968f1d5baa6c7d

Official VRoid usage conditions:
https://vroid.pixiv.help/hc/ja/articles/4402394424089-AvatarSample-A-Z

These samples are not CC0. According to the official conditions checked on
2026-09-22, they may be used free of charge for commercial and non-commercial
purposes and credit is not required. Copyright is not waived. The official
conditions remain authoritative.

## UniVRM

Runtime VRM loading uses UniVRM v0.131.2:
https://github.com/vrm-c/UniVRM

See the upstream repository/packages for their license notices.


## Quaternius Universal Animation Library

Locomotion uses the free Standard edition of Universal Animation Library by Quaternius.

- Source: https://quaternius.itch.io/universal-animation-library
- Imported file: `Assets/Animations/UAL/UAL1_Standard.fbx`
- Standard pack upload used: 2026-06-16 build, downloaded 2026-09-23
- License: CC0 1.0 Universal / Public Domain Dedication
- Local license copy: `Assets/Animations/UAL/LICENSE.txt`

The production locomotion controller currently uses `Idle_Loop` for standing and
`Jog_Fwd_Loop` for movement through Unity Humanoid retargeting. `Walk_Loop` and
`Walk_Formal_Loop` remain QA comparison clips but are not used for production movement.
