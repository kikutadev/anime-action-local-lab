# Local anime humanoid pipeline

## Production path

1. Character
   - VRoid official sample avatars are the production visual baseline.
   - Local full-character generation remains research-only because current quality/cost is not competitive.
2. Skeleton / retarget
   - Runtime avatars must expose a valid Unity Humanoid avatar.
   - Quaternius Universal Animation Library (UAL) is imported as Humanoid and retargeted by Unity to VRoid.
3. Locomotion
   - Idle: UAL `Idle_Loop`
   - Walk: UAL `Walk_Loop`
   - Jog: UAL `Jog_Fwd_Loop`
   - Full run: UAL `Sprint_Loop`
   - `MoveSpeed` is expressed in metres per second across gameplay and the BlendTree.
   - Runtime reference speeds are 1.55 m/s walk, 3.10 m/s jog, and 5.20 m/s sprint.
   - High-speed run polish is additive only: a small pelvis/chest counter-rotation and restrained arm accent are layered in `LateUpdate`; the authored sprint remains the base motion.
4. Generated actions
   - HY-Motion 1.0 Lite is the primary source for attack and dodge motions.
   - Generated joint rotations are converted to the runtime Humanoid track format.
   - Generated root translation supplies the source action displacement curve.
   - Gameplay chooses the action direction: attack follows character facing, dodge follows requested dodge direction.
   - Source clip timing is validated, then gameplay retimes it explicitly: slash currently plays in 0.78s with source-time remapping and scaled root displacement; dodge plays in 0.62s.
   - Hit timing is expressed as a normalized phase (`attackHitPhase`), so gameplay duration can change without hard-coding hit seconds.
5. Runtime
   - `VroidActionMotor` owns CharacterController movement and coordinates locomotion/action playback.
   - Unity Humanoid remains the production retarget path for authored UAL locomotion.
   - HY-Motion runtime delta retarget is retained for generated actions; a canonical-Humanoid bake is the next fallback if shoulder/wrist/pelvis basis errors remain visible.
6. Delivery
   - Unity 6.3 WebGL -> GitHub Pages.

## QA gate

A motion change is not complete because assets compile.

The production gate requires:

- eight-direction static phase matrices for slash / dodge / run, plus top/low supplementary views
- actual runtime run / slash / dodge sequences captured while gameplay logic is executing
- static and runtime QA must share the same action tuning and weapon-pose rules
- no Unity runtime exceptions
- no leaked QA browser/server process
- visual inspection of generated captures

The WebGL build regenerates and validates the UAL AnimatorController and validates the HY-Motion timing/root/bone-track contract before building. `docs/ACTION_QA.md` defines the canonical views, key phases, review criteria, and lifecycle rules.
