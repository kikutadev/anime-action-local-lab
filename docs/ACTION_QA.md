# Action QA

This project treats action quality as two separate QA problems:

1. **Static multi-angle QA** — inspect silhouette, grounding, weight transfer, weapon readability, and joint deformation at authored key phases.
2. **Runtime continuity QA** — inspect the actual playable timing and cadence rather than trusting frozen poses.

## Canonical views

Every core action is captured from the same eight horizontal views:

- front
- front_threequarter_l
- side_l
- back_threequarter_l
- back
- back_threequarter_r
- side_r
- front_threequarter_r

The action's key phase is also captured from top_slight and low_slight.

The view list lives only in tools/qa_pose_matrix.mjs. Capture code and contact-sheet generation must consume that definition rather than duplicating filenames or angles.

## Canonical phases

### Slash

- anticipation — 0.120
- early_swing — 0.280
- contact — 0.420
- follow_through — 0.680
- recovery — 0.900

The contact phase intentionally matches VroidActionMotor.attackHitPhase.

### Dodge

- ready — 0.050
- push_off — 0.220
- travel — 0.500
- settle — 0.860

### Run

- contact_a — 0.000
- down — 0.125
- passing — 0.250
- up — 0.375
- contact_b — 0.500

## Running QA

Fast capture:

    tools/run_pose_qa.sh

Full lifecycle validation plus capture:

    QA_VALIDATE_RUNTIME=1 tools/run_pose_qa.sh

The QA runtime owns its HTTP server, CDP port, Chrome profile, Chrome process group, and cleanup guardian. Capture tools must not reconnect to a fixed external CDP endpoint.

### Targeted iteration

For a single action, use the smaller static matrix instead of waiting for the full suite:

    QA_ACTION=slash node tools/capture_action_matrix.mjs
    QA_CAPTURE_ROOT=qa-captures/action-slash python3 tools/make_pose_contact.py

Replace slash with dodge or run as needed. This path uses the same canonical phase/view definitions as the full suite.

For slash weapon-axis investigation, tools/capture_slash_weapon_candidates.mjs accepts QA_PHASE, for example:

    QA_PHASE=0.12 node tools/capture_slash_weapon_candidates.mjs

## Outputs

The latest successful run is promoted atomically to:

    qa-captures/current/

A failed capture must not delete or partially replace the last successful current result.

Important artifacts:

- matrix_slash.jpg
- matrix_dodge.jpg
- matrix_run.jpg
- supplementary_slash.jpg
- supplementary_dodge.jpg
- supplementary_run.jpg
- runtime_slash_contact.jpg / runtime_slash.gif
- runtime_dodge_contact.jpg / runtime_dodge.gif
- runtime_run_contact.jpg / runtime_run.gif
- manifest.json

manifest.json is the output contract. Contact sheets are generated from it; they must not maintain a second filename list.

## Review criteria

For every angle and key phase, review:

- **Silhouette** — weapon arc remains readable; arms do not disappear into the torso; leg separation is clear.
- **Weight transfer** — pelvis and chest participate; push-off and settle phases have a believable direction of force.
- **Grounding** — planted feet do not slide; toe-off and landing heights are plausible.
- **Upper-body chain** — chest, shoulder, elbow, wrist, and weapon move as a connected chain.
- **Action timing** — anticipation, contact/travel, follow-through, and recovery are visibly distinct.
- **Runtime continuity** — playable motion has useful intermediate frames and does not collapse into a pose snap.

## Baseline findings — 2026-09-24

The first eight-direction run proves the QA matrix is exposing problems that front / three-quarter-only review hid:

- **Slash:** contact readability fails from multiple angles. In the supplementary top/low contact views, the weapon does not form a readable attack silhouette. Follow-through also collapses toward a neutral-looking pose too quickly.
- **Dodge:** push-off shows excessive lateral torso folding, and rear/side views make the weak weight transfer obvious. The travel and settle phases read more like pose displacement than a grounded evasive action.
- **Run:** gait phases remain identifiable across all eight views, so the matrix is usable for locomotion polish. Rear and side views should be the primary grounding checks.
- **Runtime capture:** the original serial-screenshot capture missed most intermediate action frames. This was a QA limitation as well as an animation problem.

These are baseline observations, not acceptance.

## Tuned state — 2026-09-24

The first action-polish pass has now been rerun through the same matrix and through live CDP screencast capture:

- **Slash:** gameplay duration is 0.78s instead of consuming the source clip's full 1.5s. Source motion is retimed so anticipation, contact, follow-through, and recovery remain distinct at gameplay speed. Root displacement is scaled to 0.72 of the generated source.
- **Slash weapon arc:** the weapon has separate windup/contact/follow local poses. Candidate sweeps were captured at anticipation/contact/follow phases before choosing the runtime curve. Contact now keeps the blade readable in the top/low supplementary views instead of pointing it into the camera.
- **Slash trail:** trail emission is limited to the swing window rather than emitting through the entire attack.
- **Dodge:** gameplay duration is 0.62s. Lower-body generated motion is preserved, while excessive chest/upper-chest retargeting is reduced. A forward crouch and restrained roll counter are layered over the source so push-off and low travel read more like an evasive action than a lateral collapse.
- **Run:** the authored sprint remains the source of truth. A small high-speed accent reinforces reciprocal pelvis/chest rotation and opposite arm swing; it fades below the run range so walk/jog are not distorted. The same accent is applied to frozen run QA phases, keeping static and runtime review consistent.
- **Runtime continuity:** run/slash/dodge are captured with Page.startScreencast while the actual gameplay action is executing. This avoids the old problem where synchronous screenshots consumed most of a 0.6–0.8s action between frames.
- **Lifecycle:** the CDP event subscription path is covered by qa_runtime.test.mjs; the complete lifecycle suite remains 12/12 passing and leaves no QA Chrome/process/profile residue.

The current slash, dodge, and run matrices are the comparison baseline for subsequent action work. Dodge still carries some lateral roll from the generated source at push-off; replacing or re-authoring that source motion is preferable to stacking much stronger corrective rotations.
