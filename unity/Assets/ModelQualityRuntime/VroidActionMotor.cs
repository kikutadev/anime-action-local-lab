using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class VroidActionMotor : MonoBehaviour
{
    public Camera viewCamera;
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
        bones.Clear();
        baseRelativeRotations.Clear();

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
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
        };

        foreach (HumanBodyBones bone in wanted)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null) continue;
            bones[bone] = t;
            baseRelativeRotations[t] = Quaternion.Inverse(transform.rotation) * t.rotation;
        }

        Debug.Log($"VRoid action motor bound humanoid: {bones.Count} tracked bones.");
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
        attackTime = 0.52f;
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
        dodgeTime = 0.34f;
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
        foreach ((Transform t, Quaternion relative) in baseRelativeRotations)
        {
            if (t != null) t.rotation = transform.rotation * relative;
        }

        if (bones.TryGetValue(HumanBodyBones.Hips, out Transform hips))
        {
            float bob = moveAmount > 0.05f ? Mathf.Abs(Mathf.Sin(walkClock)) * 0.018f : 0f;
            hips.position += Vector3.up * bob;
        }

        if (IsDodging)
        {
            float u = 1f - dodgeTime / 0.34f;
            float crouch = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
            Rotate(HumanBodyBones.Spine, Vector3.right, 22f * crouch);
            Rotate(HumanBodyBones.Chest, Vector3.right, 15f * crouch);
            Rotate(HumanBodyBones.LeftUpperArm, Vector3.right, -28f * crouch);
            Rotate(HumanBodyBones.RightUpperArm, Vector3.right, -28f * crouch);
            return;
        }

        if (IsAttacking)
        {
            float u = Mathf.Clamp01(1f - attackTime / 0.52f);
            float slash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.18f) / 0.55f));
            float recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.75f) / 0.25f));
            float amount = Mathf.Lerp(slash, 0f, recover);

            Rotate(HumanBodyBones.Chest, Vector3.up, Mathf.Lerp(-18f, 32f, amount));
            Rotate(HumanBodyBones.Spine, Vector3.up, Mathf.Lerp(-8f, 14f, amount));
            Rotate(HumanBodyBones.RightUpperArm, Vector3.up, Mathf.Lerp(-65f, 55f, amount));
            Rotate(HumanBodyBones.RightUpperArm, Vector3.right, -52f * Mathf.Sin(u * Mathf.PI));
            Rotate(HumanBodyBones.RightLowerArm, Vector3.right, -42f * Mathf.Sin(u * Mathf.PI));
            Rotate(HumanBodyBones.LeftUpperArm, Vector3.right, 18f * Mathf.Sin(u * Mathf.PI));
            return;
        }

        if (moveAmount > 0.05f)
        {
            float swing = Mathf.Sin(walkClock) * 25f * moveAmount;
            Rotate(HumanBodyBones.LeftUpperLeg, Vector3.right, swing);
            Rotate(HumanBodyBones.RightUpperLeg, Vector3.right, -swing);
            Rotate(HumanBodyBones.LeftUpperArm, Vector3.right, -swing * 0.72f);
            Rotate(HumanBodyBones.RightUpperArm, Vector3.right, swing * 0.72f);
            Rotate(HumanBodyBones.LeftLowerLeg, Vector3.right, Mathf.Max(0f, -swing) * 0.62f);
            Rotate(HumanBodyBones.RightLowerLeg, Vector3.right, Mathf.Max(0f, swing) * 0.62f);
        }
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
