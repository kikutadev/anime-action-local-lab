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
        public List<MotionTrack> tracks;
    }

    private const float AttackDuration = 0.68f;
    private const float DodgeDuration = 0.46f;
    public Camera viewCamera;
    public Transform weaponVisual;
    public TrailRenderer weaponTrail;
    public float moveSpeed = 4.6f;
    public float turnSharpness = 14f;
    public float cameraDistance = 4.2f;
    public float cameraHeight = 1.8f;
    public float cameraLookHeight = 1.05f;

    private CharacterController controller;
    private Animator animator;
    private Transform avatarRoot;
    private readonly Dictionary<HumanBodyBones, Transform> bones = new();
    private readonly Dictionary<Transform, Quaternion> baseRelativeRotations = new();
    private readonly Dictionary<Transform, Quaternion> baseLocalRotations = new();
    private readonly Dictionary<Transform, Vector3> baseLocalPositions = new();

    private RuntimeMotion slashMotion;
    private RuntimeMotion dodgeMotion;

    private Vector3 avatarBaseLocalPosition;
    private float referenceFootHeight;
    private float groundError;
    private float lastGroundY;
    private bool hasFootReference;

    private float verticalVelocity;
    private float attackTime;
    private float dodgeTime;
    private Vector3 dodgeDirection;
    private float walkClock;

    private int joystickFinger = -1;
    private Vector2 joystickOrigin;
    private Vector2 joystickCurrent;
    private Vector2 touchMove;

    public bool Ready => animator != null;
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
        baseRelativeRotations.Clear();
        baseLocalRotations.Clear();
        baseLocalPositions.Clear();
        slashMotion = null;
        dodgeMotion = null;
        hasFootReference = false;
        groundError = 0f;

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
            baseRelativeRotations[t] = Quaternion.Inverse(transform.rotation) * t.rotation;
            baseLocalRotations[t] = t.localRotation;
            baseLocalPositions[t] = t.localPosition;
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
        slashMotion = LoadMotion("HYMotionSlash");
        dodgeMotion = LoadMotion("HYMotionDodge");

        Debug.Log(
            $"VRoid action motor bound humanoid: {bones.Count} tracked bones, " +
            $"footReference={referenceFootHeight:F4}, controllerHeight={controller.height:F3}.");
    }

    private void Update()
    {
        attackTime = Mathf.Max(0f, attackTime - Time.deltaTime);
        dodgeTime = Mathf.Max(0f, dodgeTime - Time.deltaTime);
        HandleTouchJoystick();

        Vector2 keyboard = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 input = keyboard.sqrMagnitude > 0.01f ? keyboard : touchMove;
        input = Vector2.ClampMagnitude(input, 1f);

        if (Input.GetKeyDown(KeyCode.J) ||
            (Input.touchCount == 0 && Input.GetMouseButtonDown(0)))
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

        Vector3 desired = CameraRelative(input);
        float moveAmount = Mathf.Clamp01(desired.magnitude);

        if (IsDodging)
        {
            desired = dodgeDirection * 9.5f;
        }
        else
        {
            desired *= moveSpeed;
            if (!IsAttacking && moveAmount > 0.05f)
            {
                Quaternion target = Quaternion.LookRotation(desired.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target,
                    1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            }
        }

        if (controller.isGrounded) verticalVelocity = -1f;
        else verticalVelocity -= 20f * Time.deltaTime;

        desired.y = verticalVelocity;
        controller.Move(desired * Time.deltaTime);

        if (moveAmount > 0.05f && !IsDodging) walkClock += Time.deltaTime * (7.5f + moveAmount * 2f);

        if (Ready)
        {
            ApplyProceduralPose(moveAmount);
        }
    }

    private void LateUpdate()
    {
        CorrectVisualGrounding();
        UpdateWeaponPose();
        if (weaponTrail != null)
        {
            weaponTrail.emitting = Ready && IsAttacking;
        }

        if (viewCamera == null) return;

        Vector3 desired = transform.position
            - transform.forward * cameraDistance
            + Vector3.up * cameraHeight;
        viewCamera.transform.position = Vector3.Lerp(
            viewCamera.transform.position,
            desired,
            1f - Mathf.Exp(-10f * Time.deltaTime));

        Vector3 look = transform.position + Vector3.up * cameraLookHeight;
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
        attackTime = AttackDuration;
        Invoke(nameof(ResolveAttack), 0.27f);
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
        dodgeTime = DodgeDuration;
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

    private void ApplyProceduralPose(float moveAmount)
    {
        foreach ((Transform t, Quaternion localRotation) in baseLocalRotations)
        {
            if (t == null) continue;
            t.localRotation = localRotation;
            if (baseLocalPositions.TryGetValue(t, out Vector3 localPosition))
            {
                t.localPosition = localPosition;
            }
        }

        if (IsDodging)
        {
            float u = Mathf.Clamp01(1f - dodgeTime / DodgeDuration);
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
            float u = Mathf.Clamp01(1f - attackTime / AttackDuration);
            if (slashMotion != null)
            {
                ApplyGeneratedMotion(slashMotion, u, true);
            }
            else
            {
                float slash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.18f) / 0.55f));
                float recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.75f) / 0.25f));
                float amount = Mathf.Lerp(slash, 0f, recover);
                Rotate(HumanBodyBones.Chest, Vector3.up, Mathf.Lerp(-18f, 32f, amount));
                Rotate(HumanBodyBones.RightUpperArm, Vector3.right, -52f * Mathf.Sin(u * Mathf.PI));
            }
            return;
        }

        if (moveAmount > 0.05f)
        {
            float swing = Mathf.Sin(walkClock) * 25f * moveAmount;
            Rotate(HumanBodyBones.LeftUpperLeg, Vector3.right, swing);
            Rotate(HumanBodyBones.RightUpperLeg, Vector3.right, -swing);
            Rotate(HumanBodyBones.LeftUpperArm, Vector3.right, -swing * 0.72f);
            Rotate(HumanBodyBones.RightUpperArm, Vector3.right, swing * 0.72f);
            Rotate(HumanBodyBones.LeftLowerLeg, Vector3.right, Mathf.Max(0f, -swing) * 0.72f);
            Rotate(HumanBodyBones.RightLowerLeg, Vector3.right, Mathf.Max(0f, swing) * 0.72f);

            float bodyCounter = Mathf.Sin(walkClock) * 3.2f * moveAmount;
            Rotate(HumanBodyBones.Spine, Vector3.up, -bodyCounter);
            Rotate(HumanBodyBones.Chest, Vector3.up, bodyCounter * 0.65f);
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

        Debug.Log(
            $"Bound generated humanoid motion {resourceName}: " +
            $"{data.frameCount}f @ {data.fps}fps, {tracks.Count} tracks.");

        return new RuntimeMotion
        {
            fps = data.fps,
            frameCount = data.frameCount,
            tracks = tracks,
        };
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
                target == null ||
                !baseLocalRotations.TryGetValue(target, out Quaternion rest))
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
            target.localRotation = rest * delta;
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
            !bones.TryGetValue(HumanBodyBones.RightLowerArm, out Transform lowerArm) ||
            hand == null ||
            lowerArm == null)
        {
            weaponVisual.gameObject.SetActive(false);
            return;
        }

        weaponVisual.gameObject.SetActive(true);

        Vector3 forward = hand.position - lowerArm.position;
        if (forward.sqrMagnitude < 1e-6f)
        {
            forward = transform.forward;
        }
        forward.Normalize();

        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (up.sqrMagnitude < 1e-5f)
        {
            up = Vector3.ProjectOnPlane(transform.right, forward);
        }
        up.Normalize();

        weaponVisual.SetPositionAndRotation(
            hand.position + forward * 0.055f,
            Quaternion.LookRotation(forward, up));
    }

    private void CorrectVisualGrounding()
    {
        if (!Ready || avatarRoot == null || !hasFootReference) return;
        if (!TryGetLowestFootWorldY(out float footY)) return;

        lastGroundY = ResolveGroundY();
        float targetFootY = lastGroundY + referenceFootHeight;
        groundError = targetFootY - footY;

        Vector3 local = avatarRoot.localPosition;
        float desiredLocalY = Mathf.Clamp(
            local.y + groundError,
            avatarBaseLocalPosition.y - 0.16f,
            avatarBaseLocalPosition.y + 0.16f);

        float snap = 1f - Mathf.Exp(-28f * Time.deltaTime);
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

    private void HandleTouchJoystick()
    {
        touchMove = Vector2.zero;

        foreach (Touch touch in Input.touches)
        {
            bool leftZone = touch.position.x < Screen.width * 0.52f
                && touch.position.y < Screen.height * 0.48f;

            if (touch.phase == TouchPhase.Began && leftZone && joystickFinger < 0)
            {
                joystickFinger = touch.fingerId;
                joystickOrigin = touch.position;
                joystickCurrent = touch.position;
            }

            if (touch.fingerId != joystickFinger) continue;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                joystickFinger = -1;
                joystickCurrent = joystickOrigin;
                continue;
            }

            joystickCurrent = touch.position;
            Vector2 delta = joystickCurrent - joystickOrigin;
            touchMove = Vector2.ClampMagnitude(delta / Mathf.Max(72f, Screen.width * 0.18f), 1f);
        }
    }

    private void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.width / 430f, 0.78f, 1.25f);
        Rect safe = Screen.safeArea;
        float size = 72f * scale;
        float margin = 18f * scale;

        GUIStyle action = new(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(14f * scale),
            fontStyle = FontStyle.Bold,
        };

        Rect attack = new(safe.xMax - margin - size, safe.yMax - margin - size, size, size);
        Rect dodge = new(safe.xMax - margin * 1.25f - size * 2f, safe.yMax - margin - size * 0.78f, size * 0.78f, size * 0.78f);

        if (GUI.Button(attack, "ATTACK", action)) TriggerAttack();
        if (GUI.Button(dodge, "DODGE", action)) TriggerDodge();

        Vector2 center = joystickFinger >= 0
            ? joystickOrigin
            : new Vector2(safe.x + margin + size * 0.55f, safe.yMax - margin - size * 0.55f);
        Vector2 knob = joystickFinger >= 0
            ? center + Vector2.ClampMagnitude(joystickCurrent - center, size * 0.42f)
            : center;

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.24f);
        GUI.Box(new Rect(center.x - size * 0.5f, Screen.height - center.y - size * 0.5f, size, size), "");
        GUI.color = new Color(1f, 1f, 1f, 0.48f);
        GUI.Box(new Rect(knob.x - size * 0.18f, Screen.height - knob.y - size * 0.18f, size * 0.36f, size * 0.36f), "");
        GUI.color = prev;
    }
}
