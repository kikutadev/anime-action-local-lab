using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class VroidActionMotor : MonoBehaviour
{
    [Serializable]
    private sealed class MotionClipData
    {
        public int fps;
        public int frameCount;
        public float duration;
        public float[] rootTranslation;
        public MotionBoneData[] bones;
    }

    [Serializable]
    private sealed class MotionBoneData
    {
        public string name;
        public float[] rotation;
    }

    private sealed class MotionTrack
    {
        public HumanBodyBones bone;
        public Quaternion[] deltaRotations;
    }

    private sealed class RuntimeMotion
    {
        public int fps;
        public int frameCount;
        public float duration;
        public float[] rootProgress;
        public List<MotionTrack> tracks;
    }

    public const float UalWalkSpeed = 1.55f;
    public const float UalJogSpeed = 3.10f;
    public const float UalRunSpeed = 5.20f;
    private const float FallbackAttackDuration = 0.68f;
    private const float FallbackDodgeDuration = 0.46f;
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    public Camera viewCamera;
    public Transform weaponVisual;
    public TrailRenderer weaponTrail;
    public float moveSpeed = UalRunSpeed;
    public float acceleration = 30f;
    public float deceleration = 38f;
    public float turnSharpness = 20f;
    public float naturalWalkSpeed = UalWalkSpeed;
    public float naturalJogSpeed = UalJogSpeed;
    public float naturalRunSpeed = UalRunSpeed;
    public float cameraDistance = 4.2f;
    public float cameraMinDistance = 2.2f;
    public float cameraMaxDistance = 6.2f;
    public float cameraLookHeight = 1.05f;
    public float cameraPitch = 13f;
    public float mouseCameraSensitivity = 0.18f;
    public float touchCameraSensitivity = 0.13f;
    public float swordUpperBodyWeight = 0.85f;
    [Range(0.05f, 0.95f)]
    public float attackHitPhase = 0.42f;
    public Vector3 weaponHandLocalPosition = Vector3.zero;
    public Vector3 weaponHandLocalEuler = Vector3.zero;
    public Vector3 weaponIdleLocalEuler = new(0f, 0f, -90f);
    public Vector3 weaponMoveLocalEuler = new(0f, 90f, 0f);

    private CharacterController controller;
    private Animator animator;
    private Transform avatarRoot;
    private readonly Dictionary<HumanBodyBones, Transform> bones = new();

    private RuntimeMotion slashMotion;
    private RuntimeMotion dodgeMotion;

    private Vector3 avatarBaseLocalPosition;
    private float referenceFootHeight;
    private float groundError;
    private float lastGroundY;
    private bool hasFootReference;

    private float verticalVelocity;
    private float locomotionVisualAmount;
    private float currentPlanarSpeed;
    private Vector3 planarVelocity;
    private float attackTime;
    private float dodgeTime;
    private float actionRootDistance;
    private bool attackHitResolved;
    private Vector3 dodgeDirection;

    private int joystickFinger = -1;
    private Vector2 joystickCurrent;
    private Vector2 touchMove;
    private int cameraFinger = -1;
    private Vector2 cameraLastTouch;
    private bool mouseCameraDragging;
    private Vector2 mouseCameraLast;
    private float cameraYaw;
    private bool cameraInitialized;

    private bool qaMode;
    private string qaMotion;
    private float qaPhase;
    private float qaCameraYaw;

    public bool Ready => animator != null;
    public bool QaMode => qaMode;
    public bool IsAttacking => attackTime > 0f;
    public bool IsDodging => dodgeTime > 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void BindAvatar(GameObject root)
    {
        avatarRoot = root != null ? root.transform : null;
        animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        if (weaponVisual != null)
        {
            weaponVisual.gameObject.SetActive(root != null);
        }
        bones.Clear();
        slashMotion = null;
        dodgeMotion = null;
        hasFootReference = false;
        groundError = 0f;
        planarVelocity = Vector3.zero;
        currentPlanarSpeed = 0f;
        if (!cameraInitialized)
        {
            cameraYaw = transform.eulerAngles.y;
            cameraInitialized = true;
        }

        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning("VRoid action motor: humanoid Animator is unavailable.");
            return;
        }

        HumanBodyBones[] wanted =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
        };

        foreach (HumanBodyBones bone in wanted)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null) continue;
            bones[bone] = t;
        }

        avatarBaseLocalPosition = avatarRoot.localPosition;
        if (TryGetLowestFootWorldY(out float footY))
        {
            Bounds visibleBounds = CalculateVisibleBounds(root);
            referenceFootHeight = footY - visibleBounds.min.y;
            lastGroundY = visibleBounds.min.y;
            hasFootReference = true;
        }

        FitCharacterControllerToAvatar(root);

        RuntimeAnimatorController locomotion =
            Resources.Load<RuntimeAnimatorController>("VroidLocomotion");
        if (locomotion != null)
        {
            animator.runtimeAnimatorController = locomotion;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.stabilizeFeet = true;
            animator.SetFloat(MoveSpeedHash, 0f);
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, swordUpperBodyWeight);
            }
            animator.Play("Locomotion", 0, 0f);
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, swordUpperBodyWeight);
                animator.Play("SwordUpperBody", 1, 0f);
            }
            animator.Update(0f);
        }
        else
        {
            Debug.LogWarning("VroidLocomotion controller was not found in Resources.");
        }

        slashMotion = LoadMotion("HYMotionSlash");
        dodgeMotion = LoadMotion("HYMotionDodge");

        Debug.Log(
            $"VRoid action motor bound humanoid: {bones.Count} tracked bones, " +
            $"footReference={referenceFootHeight:F4}, controllerHeight={controller.height:F3}.");
    }

    private void Update()
    {
        if (qaMode)
        {
            return;
        }

        HandlePointerControls();

        Vector2 keyboard = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 input = keyboard.sqrMagnitude > 0.01f ? keyboard : touchMove;
        input = ApplyStickDeadZone(Vector2.ClampMagnitude(input, 1f), 0.10f);

        if (Input.GetKeyDown(KeyCode.J))
        {
            TryAttack();
        }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.K))
        {
            TryDodge(input);
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            LogGrounding();
        }

        Vector3 inputDirection = CameraRelative(input);
        float moveAmount = Mathf.Clamp01(inputDirection.magnitude);
        bool wantsMove = moveAmount > 0.01f;
        bool actionWasActive = IsAttacking || IsDodging;

        Vector3 horizontalDisplacement = Vector3.zero;
        if (actionWasActive)
        {
            planarVelocity = Vector3.zero;
            horizontalDisplacement = AdvanceActionMotion(Time.deltaTime);
            currentPlanarSpeed = Time.deltaTime > 1e-5f
                ? horizontalDisplacement.magnitude / Time.deltaTime
                : 0f;
        }
        else
        {
            Vector3 desiredVelocity = wantsMove
                ? inputDirection.normalized * (moveAmount * moveSpeed)
                : Vector3.zero;
            float rate = wantsMove ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(
                planarVelocity,
                desiredVelocity,
                Mathf.Max(1f, rate) * Time.deltaTime);
            currentPlanarSpeed = planarVelocity.magnitude;
            horizontalDisplacement = planarVelocity * Time.deltaTime;

            if (currentPlanarSpeed > 0.08f)
            {
                Quaternion target = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target,
                    1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            }
        }

        bool locomotionActive = !actionWasActive && wantsMove;
        float targetVisualMove = locomotionActive
            ? Mathf.Clamp01(currentPlanarSpeed / Mathf.Max(0.1f, naturalJogSpeed))
            : 0f;
        locomotionVisualAmount = Mathf.MoveTowards(
            locomotionVisualAmount,
            targetVisualMove,
            Time.deltaTime * 8f);

        if (controller.isGrounded) verticalVelocity = -1f;
        else verticalVelocity -= 20f * Time.deltaTime;

        Vector3 displacement = horizontalDisplacement;
        displacement.y = verticalVelocity * Time.deltaTime;
        controller.Move(displacement);

        if (Ready && animator.runtimeAnimatorController != null)
        {
            float animatorMoveSpeed = locomotionActive ? currentPlanarSpeed : 0f;
            animator.SetFloat(MoveSpeedHash, animatorMoveSpeed, 0.10f, Time.deltaTime);

            float targetPlayback = CalculateLocomotionPlaybackSpeed(animatorMoveSpeed);
            animator.speed = Mathf.MoveTowards(
                animator.speed,
                targetPlayback,
                Time.deltaTime * 5f);

            if (animator.layerCount > 1)
            {
                float targetUpperWeight = Mathf.Lerp(
                    swordUpperBodyWeight,
                    0.12f,
                    locomotionVisualAmount);
                animator.SetLayerWeight(1, targetUpperWeight);
            }
        }
    }

    private float CalculateLocomotionPlaybackSpeed(float planarSpeed)
    {
        if (planarSpeed <= 0.05f) return 1f;

        if (planarSpeed <= naturalWalkSpeed)
        {
            // Keep the walk readable at low analogue input without making feet
            // cycle implausibly slowly.
            return Mathf.Clamp(
                planarSpeed / Mathf.Max(0.1f, naturalWalkSpeed),
                0.72f,
                1.08f);
        }

        if (planarSpeed >= naturalRunSpeed)
        {
            return Mathf.Clamp(
                planarSpeed / Mathf.Max(0.1f, naturalRunSpeed),
                0.96f,
                1.12f);
        }

        // The BlendTree contains authored walk, jog, and sprint cycles at
        // physical speed thresholds, so each gait plays near its natural cadence.
        return 1f;
    }

    private Vector3 AdvanceActionMotion(float deltaTime)
    {
        if (IsAttacking)
        {
            float duration = GetAttackDuration();
            attackTime = Mathf.Max(0f, attackTime - deltaTime);
            float phase = Mathf.Clamp01(1f - attackTime / duration);

            float root = SampleRootDistance(slashMotion, phase);
            float delta = root - actionRootDistance;
            actionRootDistance = root;

            if (!attackHitResolved && phase >= attackHitPhase)
            {
                attackHitResolved = true;
                ResolveAttack();
            }

            return transform.forward * delta;
        }

        if (IsDodging)
        {
            float duration = GetDodgeDuration();
            dodgeTime = Mathf.Max(0f, dodgeTime - deltaTime);
            float phase = Mathf.Clamp01(1f - dodgeTime / duration);

            float root = SampleRootDistance(dodgeMotion, phase);
            float delta = root - actionRootDistance;
            actionRootDistance = root;
            return dodgeDirection * delta;
        }

        return Vector3.zero;
    }

    private float GetAttackDuration()
    {
        return slashMotion != null && slashMotion.duration > 0.05f
            ? slashMotion.duration
            : FallbackAttackDuration;
    }

    private float GetDodgeDuration()
    {
        return dodgeMotion != null && dodgeMotion.duration > 0.05f
            ? dodgeMotion.duration
            : FallbackDodgeDuration;
    }

    private static float SampleRootDistance(RuntimeMotion motion, float normalizedTime)
    {
        if (motion?.rootProgress == null ||
            motion.rootProgress.Length < 2)
        {
            return 0f;
        }

        float frame = Mathf.Clamp01(normalizedTime) *
            (motion.rootProgress.Length - 1);
        int a = Mathf.FloorToInt(frame);
        int b = Mathf.Min(a + 1, motion.rootProgress.Length - 1);
        return Mathf.Lerp(
            motion.rootProgress[a],
            motion.rootProgress[b],
            frame - a);
    }

    private void LateUpdate()
    {
        if (Ready && (IsAttacking || IsDodging))
        {
            ApplyActionPose();
        }
        else if (Ready && qaMode)
        {
            ApplyQaActionPose();
        }

        CorrectVisualGrounding();
        if (qaMode)
        {
            UpdateWeaponPose();
            if (weaponTrail != null) weaponTrail.emitting = false;
        }
        else
        {
            UpdateWeaponPose();
            if (weaponTrail != null)
            {
                weaponTrail.emitting = Ready && IsAttacking;
            }
        }

        if (viewCamera == null) return;

        if (qaMode)
        {
            UpdateQaCamera();
            return;
        }

        Vector3 look = transform.position + Vector3.up * cameraLookHeight;
        Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        Vector3 cameraDirection = orbit * Vector3.back;
        float resolvedDistance = ResolveCameraDistance(look, cameraDirection);
        Vector3 desired = look + cameraDirection * resolvedDistance;

        viewCamera.transform.position = Vector3.Lerp(
            viewCamera.transform.position,
            desired,
            1f - Mathf.Exp(-14f * Time.deltaTime));
        viewCamera.transform.rotation = Quaternion.LookRotation(
            look - viewCamera.transform.position,
            Vector3.up);
    }

    public void SetQaPose(
        string motion,
        float phase,
        string view,
        float upperBodyWeight,
        Vector3 weaponEuler)
    {
        if (!Ready || animator.runtimeAnimatorController == null) return;

        qaMode = true;
        qaMotion = motion;
        qaPhase = Mathf.Clamp01(phase);
        weaponHandLocalEuler = weaponEuler;
        attackTime = 0f;
        dodgeTime = 0f;
        controller.enabled = false;

        float speed = motion switch
        {
            "walk" or "walkformal" => 0.35f,
            "jog" => UalJogSpeed,
            "run" or "sprint" => UalRunSpeed,
            _ => 0f,
        };

        string stateName = motion switch
        {
            "swordidle" => "QA_SwordIdle",
            "walk" => "QA_Walk",
            "walkformal" => "QA_WalkFormal",
            "jog" => "QA_Jog",
            "run" or "sprint" => "QA_Sprint",
            "slash" or "dodge" => "QA_Idle",
            _ => "QA_Idle",
        };

        qaCameraYaw = view switch
        {
            "front" => 180f,
            "side" => 90f,
            "back" => 0f,
            _ => 215f,
        };

        animator.speed = 0f;
        animator.SetFloat(MoveSpeedHash, speed);
        if (animator.layerCount > 1)
        {
            animator.SetLayerWeight(1, Mathf.Clamp01(upperBodyWeight));
        }
        animator.Play(stateName, 0, Mathf.Repeat(phase, 1f));
        if (animator.layerCount > 1)
        {
            animator.Play("SwordUpperBody", 1, 0f);
        }
        animator.Update(0f);

        CorrectVisualGrounding();
        UpdateWeaponPose();
        if (weaponTrail != null) weaponTrail.emitting = false;
        UpdateQaCamera();

        Debug.Log(
            $"QA_POSE motion={motion} phase={phase:F3} view={view} " +
            $"speed={speed:F2} upper={upperBodyWeight:F2} " +
            $"weaponEuler={weaponEuler.x:F0},{weaponEuler.y:F0},{weaponEuler.z:F0}");
    }

    public void ExitQaPose()
    {
        if (!qaMode) return;

        qaMode = false;
        qaMotion = null;
        qaPhase = 0f;
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetFloat(MoveSpeedHash, 0f);
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, swordUpperBodyWeight);
            }
            animator.Play("Locomotion", 0, 0f);
        }
        controller.enabled = true;
        if (weaponVisual != null) weaponVisual.gameObject.SetActive(Ready);
    }

    private void UpdateQaCamera()
    {
        if (viewCamera == null) return;

        Quaternion yaw = Quaternion.Euler(0f, qaCameraYaw, 0f);
        Vector3 offset = yaw * (Vector3.back * 3.25f);
        Vector3 look = transform.position + Vector3.up * 0.92f;
        viewCamera.transform.position = look + offset + Vector3.up * 0.10f;
        viewCamera.transform.rotation = Quaternion.LookRotation(
            look - viewCamera.transform.position,
            Vector3.up);
    }

    public void TriggerAttack() => TryAttack();

    public void TriggerDodge()
    {
        Vector2 input = touchMove.sqrMagnitude > 0.01f ? touchMove : Vector2.up;
        TryDodge(input);
    }

    private void TryAttack()
    {
        if (!Ready || IsAttacking || IsDodging) return;
        planarVelocity = Vector3.zero;
        attackTime = GetAttackDuration();
        actionRootDistance = 0f;
        attackHitResolved = false;
    }

    private void ResolveAttack()
    {
        Vector3 center = transform.position + transform.forward * 1.15f + Vector3.up * 0.9f;
        Collider[] hits = Physics.OverlapSphere(center, 0.9f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.transform.root == transform.root) continue;
            TrainingDummy dummy = hit.GetComponentInParent<TrainingDummy>();
            if (dummy != null) dummy.Hit(transform.forward);
        }
    }

    private void TryDodge(Vector2 input)
    {
        if (!Ready || IsAttacking || IsDodging) return;
        Vector3 move = CameraRelative(input);
        dodgeDirection = move.sqrMagnitude > 0.05f ? move.normalized : transform.forward;
        planarVelocity = Vector3.zero;
        dodgeTime = GetDodgeDuration();
        actionRootDistance = 0f;
    }

    private Vector3 CameraRelative(Vector2 input)
    {
        if (viewCamera == null) return new Vector3(input.x, 0f, input.y);

        Vector3 forward = viewCamera.transform.forward;
        Vector3 right = viewCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        return forward * input.y + right * input.x;
    }

    private void ApplyQaActionPose()
    {
        if (animator == null || (qaMotion != "slash" && qaMotion != "dodge"))
        {
            return;
        }

        // QA freezes the Animator. Re-evaluate the same authored idle pose on
        // every frame before applying the generated delta so the delta is never
        // accumulated by repeated LateUpdate calls.
        animator.Play("QA_Idle", 0, 0f);
        if (animator.layerCount > 1)
        {
            animator.SetLayerWeight(1, 0f);
        }
        animator.Update(0f);

        if (qaMotion == "slash" && slashMotion != null)
        {
            ApplyGeneratedMotion(slashMotion, qaPhase, true);
        }
        else if (qaMotion == "dodge" && dodgeMotion != null)
        {
            ApplyGeneratedMotion(dodgeMotion, qaPhase, false);
        }
    }

    private void ApplyActionPose()
    {
        if (IsDodging)
        {
            float u = Mathf.Clamp01(1f - dodgeTime / GetDodgeDuration());
            if (dodgeMotion != null)
            {
                ApplyGeneratedMotion(dodgeMotion, u, false);
            }
            else
            {
                float crouch = Mathf.Sin(u * Mathf.PI);
                Rotate(HumanBodyBones.Spine, Vector3.right, 22f * crouch);
                Rotate(HumanBodyBones.Chest, Vector3.right, 15f * crouch);
            }
            return;
        }

        if (IsAttacking)
        {
            float u = Mathf.Clamp01(1f - attackTime / GetAttackDuration());
            if (slashMotion != null)
            {
                ApplyGeneratedMotion(slashMotion, u, true);
            }
            else
            {
                float slash = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((u - 0.18f) / 0.55f));
                float recover = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((u - 0.75f) / 0.25f));
                float amount = Mathf.Lerp(slash, 0f, recover);
                Rotate(
                    HumanBodyBones.Chest,
                    Vector3.up,
                    Mathf.Lerp(-18f, 32f, amount));
                Rotate(
                    HumanBodyBones.RightUpperArm,
                    Vector3.right,
                    -52f * Mathf.Sin(u * Mathf.PI));
            }
        }
    }

    private RuntimeMotion LoadMotion(string resourceName)
    {
        TextAsset asset = Resources.Load<TextAsset>(resourceName);
        if (asset == null) return null;

        MotionClipData data = JsonUtility.FromJson<MotionClipData>(asset.text);
        if (data == null || data.frameCount < 2 || data.fps <= 0 || data.bones == null)
        {
            Debug.LogWarning($"Invalid humanoid motion resource: {resourceName}");
            return null;
        }

        List<MotionTrack> tracks = new();
        foreach (MotionBoneData source in data.bones)
        {
            if (!Enum.TryParse(source.name, out HumanBodyBones bone) ||
                !bones.ContainsKey(bone) ||
                source.rotation == null ||
                source.rotation.Length < 8)
            {
                continue;
            }

            int frames = source.rotation.Length / 4;
            Quaternion[] raw = new Quaternion[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                int i = frame * 4;
                raw[frame] = NormalizeQuaternion(new Quaternion(
                    source.rotation[i],
                    source.rotation[i + 1],
                    source.rotation[i + 2],
                    source.rotation[i + 3]));
            }

            Quaternion inverseStart = Quaternion.Inverse(raw[0]);
            Quaternion[] deltas = new Quaternion[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                deltas[frame] = NormalizeQuaternion(inverseStart * raw[frame]);
            }

            tracks.Add(new MotionTrack
            {
                bone = bone,
                deltaRotations = deltas,
            });
        }

        if (tracks.Count == 0) return null;

        float[] rootProgress = BuildRootProgress(
            data.rootTranslation,
            data.frameCount);

        Debug.Log(
            $"Bound generated humanoid motion {resourceName}: " +
            $"{data.frameCount}f @ {data.fps}fps, {tracks.Count} tracks, " +
            $"duration={data.duration:F3}s, rootDisplacement=" +
            $"{rootProgress[rootProgress.Length - 1]:F3}m.");

        return new RuntimeMotion
        {
            fps = data.fps,
            frameCount = data.frameCount,
            duration = data.duration > 0f
                ? data.duration
                : data.frameCount / (float)data.fps,
            rootProgress = rootProgress,
            tracks = tracks,
        };
    }

    private static float[] BuildRootProgress(
        float[] rootTranslation,
        int frameCount)
    {
        int frames = Mathf.Max(2, frameCount);
        float[] progress = new float[frames];

        if (rootTranslation == null || rootTranslation.Length < frames * 3)
        {
            return progress;
        }

        int end = (frames - 1) * 3;
        Vector2 final = new(rootTranslation[end], rootTranslation[end + 2]);
        if (final.sqrMagnitude < 1e-6f)
        {
            return progress;
        }

        Vector2 axis = final.normalized;
        for (int frame = 1; frame < frames; frame++)
        {
            int i = frame * 3;
            Vector2 position = new(rootTranslation[i], rootTranslation[i + 2]);
            progress[frame] = Vector2.Dot(position, axis);
        }

        return progress;
    }

    private void ApplyGeneratedMotion(RuntimeMotion motion, float normalizedTime, bool attack)
    {
        if (motion == null || motion.frameCount < 2) return;

        float frame = Mathf.Clamp01(normalizedTime) * (motion.frameCount - 1);
        int a = Mathf.FloorToInt(frame);
        int b = Mathf.Min(a + 1, motion.frameCount - 1);
        float t = frame - a;

        float envelope = normalizedTime < 0.86f
            ? 1f
            : Mathf.Clamp01((1f - normalizedTime) / 0.14f);

        foreach (MotionTrack track in motion.tracks)
        {
            if (!bones.TryGetValue(track.bone, out Transform target) ||
                target == null)
            {
                continue;
            }

            int ta = Mathf.Min(a, track.deltaRotations.Length - 1);
            int tb = Mathf.Min(b, track.deltaRotations.Length - 1);
            Quaternion delta = Quaternion.Slerp(
                track.deltaRotations[ta],
                track.deltaRotations[tb],
                t);

            float anatomicalWeight = attack
                ? GetAttackRetargetWeight(track.bone)
                : 1f;
            delta = Quaternion.Slerp(
                Quaternion.identity,
                delta,
                envelope * anatomicalWeight);
            target.localRotation = target.localRotation * delta;
        }
    }

    private static float GetAttackRetargetWeight(HumanBodyBones bone)
    {
        return bone switch
        {
            // HY-Motion slash contains an almost 180-degree pelvis spin.
            // Keep gameplay facing controlled by the CharacterController and
            // retain only a small amount of pelvic torque.
            HumanBodyBones.Hips => 0.14f,

            HumanBodyBones.LeftUpperLeg or
            HumanBodyBones.RightUpperLeg => 0.58f,

            HumanBodyBones.LeftLowerLeg or
            HumanBodyBones.RightLowerLeg or
            HumanBodyBones.LeftFoot or
            HumanBodyBones.RightFoot => 0.52f,

            HumanBodyBones.Neck or
            HumanBodyBones.Head => 0.70f,

            _ => 1f,
        };
    }

    private static Quaternion NormalizeQuaternion(Quaternion q)
    {
        float magnitude = Mathf.Sqrt(
            q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        return magnitude > 1e-6f
            ? new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude)
            : Quaternion.identity;
    }

    private void UpdateWeaponPose()
    {
        if (weaponVisual == null) return;

        if (!Ready ||
            !bones.TryGetValue(HumanBodyBones.RightHand, out Transform hand) ||
            hand == null)
        {
            weaponVisual.gameObject.SetActive(false);
            return;
        }

        weaponVisual.gameObject.SetActive(true);

        Vector3 weaponEuler;
        if (qaMode)
        {
            weaponEuler = weaponHandLocalEuler;
        }
        else if (IsAttacking || IsDodging)
        {
            weaponEuler = weaponMoveLocalEuler;
        }
        else
        {
            weaponEuler = Vector3.Lerp(
                weaponIdleLocalEuler,
                weaponMoveLocalEuler,
                locomotionVisualAmount);
        }

        Quaternion rotation = hand.rotation * Quaternion.Euler(weaponEuler);
        Vector3 position = hand.TransformPoint(weaponHandLocalPosition);
        weaponVisual.SetPositionAndRotation(position, rotation);
    }

    private void CorrectVisualGrounding()
    {
        if (!Ready || avatarRoot == null || !hasFootReference) return;
        if (!TryGetLowestFootWorldY(out float footY)) return;

        lastGroundY = ResolveGroundY();
        float targetFootY = lastGroundY + referenceFootHeight;
        groundError = targetFootY - footY;

        Vector3 local = avatarRoot.localPosition;
        bool mayBeAirborne = !qaMode &&
            (currentPlanarSpeed > naturalWalkSpeed * 1.05f || IsAttacking || IsDodging);

        float desiredLocalY;
        float correctionSharpness;
        if (mayBeAirborne && groundError < -0.045f)
        {
            // Jog/action clips can have a legitimate flight phase. Do not pull
            // the whole avatar down just to keep one foot touching the floor.
            desiredLocalY = avatarBaseLocalPosition.y;
            correctionSharpness = 7f;
        }
        else
        {
            desiredLocalY = Mathf.Clamp(
                local.y + groundError,
                avatarBaseLocalPosition.y - 0.16f,
                avatarBaseLocalPosition.y + 0.16f);
            correctionSharpness = 28f;
        }

        float snap = 1f - Mathf.Exp(-correctionSharpness * Time.deltaTime);
        local.y = Mathf.Lerp(local.y, desiredLocalY, snap);
        avatarRoot.localPosition = local;
    }

    private float ResolveGroundY()
    {
        Vector3 origin = transform.position + Vector3.up * 0.65f;
        if (Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            2.0f,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return transform.position.y - controller.skinWidth;
    }

    private Bounds CalculateVisibleBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);
        }

        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds
            ? bounds
            : new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);
    }

    private bool TryGetLowestFootWorldY(out float y)
    {
        y = float.PositiveInfinity;
        bool found = false;
        HumanBodyBones[] contacts =
        {
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
        };

        foreach (HumanBodyBones bone in contacts)
        {
            if (!bones.TryGetValue(bone, out Transform t) || t == null) continue;
            y = Mathf.Min(y, t.position.y);
            found = true;
        }
        return found;
    }

    private void FitCharacterControllerToAvatar(GameObject root)
    {
        Bounds bounds = CalculateVisibleBounds(root);
        float height = Mathf.Clamp(bounds.size.y * 0.96f, 1.20f, 2.20f);
        controller.height = height;
        controller.center = new Vector3(0f, height * 0.5f, 0f);
        controller.radius = Mathf.Clamp(height * 0.17f, 0.24f, 0.36f);
        controller.skinWidth = 0.04f;
        controller.stepOffset = Mathf.Min(0.28f, height * 0.16f);
    }

    public void LogGrounding()
    {
        float left = GetFootHeight(HumanBodyBones.LeftFoot);
        float right = GetFootHeight(HumanBodyBones.RightFoot);
        float leftToe = GetFootHeight(HumanBodyBones.LeftToes);
        float rightToe = GetFootHeight(HumanBodyBones.RightToes);
        TryGetLowestFootWorldY(out float minFootWorldY);
        float estimatedSoleY = minFootWorldY - referenceFootHeight;
        float soleGap = estimatedSoleY - lastGroundY;
        Debug.Log(
            $"GROUNDING groundY={lastGroundY:F4} soleGap={soleGap:F4} ref={referenceFootHeight:F4} " +
            $"err={groundError:F4} LF={left:F4} RF={right:F4} LT={leftToe:F4} RT={rightToe:F4} " +
            $"hostY={transform.position.y:F4} rootLocalY={(avatarRoot != null ? avatarRoot.localPosition.y : 0f):F4}");
    }

    private float GetFootHeight(HumanBodyBones bone)
    {
        if (!bones.TryGetValue(bone, out Transform t) || t == null) return float.NaN;
        return t.position.y - transform.position.y;
    }

    private void Rotate(HumanBodyBones bone, Vector3 localAxis, float degrees)
    {
        if (!bones.TryGetValue(bone, out Transform t) || t == null) return;
        Vector3 worldAxis = transform.TransformDirection(localAxis);
        t.rotation = Quaternion.AngleAxis(degrees, worldAxis) * t.rotation;
    }

    private static Vector2 ApplyStickDeadZone(Vector2 value, float deadZone)
    {
        float magnitude = value.magnitude;
        if (magnitude <= deadZone) return Vector2.zero;

        float scaled = Mathf.InverseLerp(deadZone, 1f, Mathf.Clamp01(magnitude));
        return value.normalized * scaled;
    }

    private void HandlePointerControls()
    {
        Vector2 previousTouchMove = touchMove;
        touchMove = Vector2.zero;
        HandleMouseCamera();

        Rect attackRect = GetAttackTouchRect();
        Rect dodgeRect = GetDodgeTouchRect();
        Vector2 joystickCenter = GetJoystickCenter();
        float joystickRadius = GetJoystickRadius();

        foreach (Touch touch in Input.touches)
        {
            if (touch.phase == TouchPhase.Began)
            {
                if (attackRect.Contains(touch.position))
                {
                    TryAttack();
                    continue;
                }

                if (dodgeRect.Contains(touch.position))
                {
                    Vector2 dodgeInput = touchMove.sqrMagnitude > 0.01f
                        ? touchMove
                        : previousTouchMove;
                    TryDodge(dodgeInput);
                    continue;
                }

                if (joystickFinger < 0 &&
                    touch.position.x < Screen.width * 0.48f &&
                    touch.position.y < Screen.height * 0.48f &&
                    Vector2.Distance(touch.position, joystickCenter) <= joystickRadius * 1.45f)
                {
                    joystickFinger = touch.fingerId;
                    joystickCurrent = touch.position;
                    continue;
                }

                if (cameraFinger < 0 &&
                    touch.position.x > Screen.width * 0.42f &&
                    !attackRect.Contains(touch.position) &&
                    !dodgeRect.Contains(touch.position))
                {
                    cameraFinger = touch.fingerId;
                    cameraLastTouch = touch.position;
                    continue;
                }
            }

            if (touch.fingerId == joystickFinger)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    joystickFinger = -1;
                    joystickCurrent = joystickCenter;
                    continue;
                }

                joystickCurrent = touch.position;
                Vector2 delta = joystickCurrent - joystickCenter;
                touchMove = ApplyStickDeadZone(
                    Vector2.ClampMagnitude(delta / joystickRadius, 1f),
                    0.10f);
                continue;
            }

            if (touch.fingerId == cameraFinger)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    cameraFinger = -1;
                    continue;
                }

                Vector2 delta = touch.position - cameraLastTouch;
                cameraYaw += delta.x * touchCameraSensitivity;
                cameraPitch = Mathf.Clamp(
                    cameraPitch - delta.y * touchCameraSensitivity * 0.72f,
                    -8f,
                    42f);
                cameraLastTouch = touch.position;
            }
        }

        if (joystickFinger < 0)
        {
            joystickCurrent = joystickCenter;
        }

        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
        {
            Vector2 mouse = Input.mousePosition;
            if (attackRect.Contains(mouse))
            {
                TryAttack();
            }
            else if (dodgeRect.Contains(mouse))
            {
                TryDodge(Vector2.up);
            }
        }
    }

    private void HandleMouseCamera()
    {
        if (Input.touchCount > 0) return;

        Vector2 mouse = Input.mousePosition;
        bool overActionButton =
            GetAttackTouchRect().Contains(mouse) ||
            GetDodgeTouchRect().Contains(mouse);

        if (Input.GetMouseButtonDown(1) ||
            (Input.GetMouseButtonDown(0) && !overActionButton))
        {
            mouseCameraDragging = true;
            mouseCameraLast = mouse;
        }

        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
        {
            mouseCameraDragging = false;
        }

        if (mouseCameraDragging)
        {
            Vector2 now = Input.mousePosition;
            Vector2 delta = now - mouseCameraLast;
            cameraYaw += delta.x * mouseCameraSensitivity;
            cameraPitch = Mathf.Clamp(
                cameraPitch - delta.y * mouseCameraSensitivity * 0.72f,
                -8f,
                42f);
            mouseCameraLast = now;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cameraDistance = Mathf.Clamp(
                cameraDistance - scroll * 0.35f,
                cameraMinDistance,
                cameraMaxDistance);
        }
    }

    private float ResolveCameraDistance(Vector3 target, Vector3 direction)
    {
        float desired = Mathf.Clamp(cameraDistance, cameraMinDistance, cameraMaxDistance);
        RaycastHit[] hits = Physics.SphereCastAll(
            target,
            0.16f,
            direction,
            desired,
            ~0,
            QueryTriggerInteraction.Ignore);

        float resolved = desired;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            resolved = Mathf.Min(resolved, Mathf.Max(cameraMinDistance * 0.55f, hit.distance - 0.12f));
        }
        return resolved;
    }

    private Vector2 GetJoystickCenter()
    {
        float scale = Mathf.Clamp(Screen.width / 430f, 0.78f, 1.25f);
        Rect safe = Screen.safeArea;
        float baseSize = 92f * scale;
        float margin = 18f * scale;
        return new Vector2(
            safe.x + margin + baseSize * 0.58f,
            safe.y + margin + baseSize * 0.58f);
    }

    private float GetJoystickRadius()
    {
        float scale = Mathf.Clamp(Screen.width / 430f, 0.78f, 1.25f);
        return 52f * scale;
    }

    private Rect GetAttackTouchRect()
    {
        float scale = Mathf.Clamp(Screen.width / 430f, 0.78f, 1.25f);
        Rect safe = Screen.safeArea;
        float size = 82f * scale;
        float margin = 18f * scale;
        return new Rect(
            safe.xMax - margin - size,
            safe.y + margin,
            size,
            size);
    }

    private Rect GetDodgeTouchRect()
    {
        float scale = Mathf.Clamp(Screen.width / 430f, 0.78f, 1.25f);
        Rect safe = Screen.safeArea;
        float size = 82f * scale;
        float margin = 18f * scale;
        float dodgeSize = size * 0.78f;
        return new Rect(
            safe.xMax - margin * 1.25f - size * 2f,
            safe.y + margin + size * 0.04f,
            dodgeSize,
            dodgeSize);
    }

    private static Rect TouchRectToGuiRect(Rect touchRect)
    {
        return new Rect(
            touchRect.x,
            Screen.height - touchRect.yMax,
            touchRect.width,
            touchRect.height);
    }

    private void OnGUI()
    {
        if (qaMode) return;

        float scale = Mathf.Clamp(Screen.width / 430f, 0.78f, 1.25f);
        Rect safe = Screen.safeArea;
        float size = 82f * scale;

        GUIStyle action = new(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(14f * scale),
            fontStyle = FontStyle.Bold,
        };

        Rect attack = TouchRectToGuiRect(GetAttackTouchRect());
        Rect dodge = TouchRectToGuiRect(GetDodgeTouchRect());

        GUI.Box(attack, "ATTACK", action);
        GUI.Box(dodge, "DODGE", action);

        Vector2 center = GetJoystickCenter();
        float radius = GetJoystickRadius();
        Vector2 knob = joystickFinger >= 0
            ? center + Vector2.ClampMagnitude(joystickCurrent - center, radius)
            : center;

        Color prev = GUI.color;
        GUI.color = new Color(0.08f, 0.12f, 0.18f, 0.24f);
        GUI.Box(
            new Rect(center.x - radius, Screen.height - center.y - radius, radius * 2f, radius * 2f),
            "");
        GUI.color = new Color(0.90f, 0.94f, 1f, 0.58f);
        float knobRadius = radius * 0.34f;
        GUI.Box(
            new Rect(knob.x - knobRadius, Screen.height - knob.y - knobRadius, knobRadius * 2f, knobRadius * 2f),
            "");
        GUI.color = prev;
    }
}
