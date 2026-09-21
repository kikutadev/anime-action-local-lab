using System.Collections.Generic;
using UnityEngine;

public sealed class ProceduralAnimeRig : MonoBehaviour
{
    [Header("Bones")]
    public Transform hips;
    public Transform spine;
    public Transform chest;
    public Transform upperChest;
    public Transform neck;
    public Transform head;
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftLowerArm;
    public Transform rightLowerArm;
    public Transform leftUpperLeg;
    public Transform rightUpperLeg;
    public Transform leftLowerLeg;
    public Transform rightLowerLeg;
    public Transform swordRoot;

    private readonly Dictionary<Transform, Quaternion> baseRotations = new();
    private Vector3 hipsBasePosition;
    private float moveAmount;
    private bool attacking;
    private bool dodging;
    private float attackPhase;
    private float dodgePhase;
    private GeneratedMotionClipPlayer generatedMotion;

    public void SetState(float move, bool attack, bool dodge)
    {
        moveAmount = move;

        if (attack && !attacking)
        {
            attackPhase = 0f;
        }

        if (dodge && !dodging)
        {
            dodgePhase = 0f;
        }

        attacking = attack;
        dodging = dodge;
    }

    private void Awake()
    {
        Cache(hips, spine, chest, upperChest, neck, head, leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm,
            leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg, swordRoot);
        hipsBasePosition = hips != null ? hips.localPosition : Vector3.zero;
        generatedMotion = GetComponent<GeneratedMotionClipPlayer>();
    }

    private void Cache(params Transform[] bones)
    {
        foreach (Transform bone in bones)
        {
            if (bone != null)
            {
                baseRotations[bone] = bone.localRotation;
            }
        }
    }

    public float PlayAttack()
    {
        if (generatedMotion != null && generatedMotion.HasClip("HYMotionSlash"))
        {
            return generatedMotion.Play("HYMotionSlash");
        }

        return 0.52f;
    }

    public float PlayDodge()
    {
        if (generatedMotion != null && generatedMotion.HasClip("HYMotionDodge"))
        {
            return generatedMotion.Play("HYMotionDodge");
        }

        return 0.42f;
    }

    private void LateUpdate()
    {
        if (generatedMotion != null && generatedMotion.IsPlaying)
        {
            return;
        }

        foreach ((Transform bone, Quaternion rotation) in baseRotations)
        {
            bone.localRotation = rotation;
        }

        if (hips == null)
        {
            return;
        }

        float t = Time.time;
        float stride = Mathf.Sin(t * 11f) * moveAmount;
        float bounce = Mathf.Abs(Mathf.Sin(t * 11f)) * 0.025f * moveAmount;
        hips.localPosition = hipsBasePosition + Vector3.up * bounce;

        AddRot(leftUpperLeg, stride * 34f, 0f, 0f);
        AddRot(rightUpperLeg, -stride * 34f, 0f, 0f);
        AddRot(leftLowerLeg, Mathf.Max(0f, -stride) * 42f, 0f, 0f);
        AddRot(rightLowerLeg, Mathf.Max(0f, stride) * 42f, 0f, 0f);
        AddRot(leftUpperArm, -stride * 30f, 0f, -7f);
        AddRot(rightUpperArm, stride * 30f, 0f, 7f);
        AddRot(chest, 5f * moveAmount, 0f, -stride * 4f);
        AddRot(head, -2f * moveAmount, 0f, stride * 2f);

        if (attacking && (generatedMotion == null || !generatedMotion.IsReady))
        {
            attackPhase = Mathf.Min(1f, attackPhase + Time.deltaTime / 0.52f);
            ApplyAttack(attackPhase);
        }

        if (dodging)
        {
            dodgePhase = Mathf.Min(1f, dodgePhase + Time.deltaTime / 0.42f);
            ApplyDodge(dodgePhase);
        }
    }

    private void ApplyAttack(float phase)
    {
        float windup = Mathf.Clamp01(phase / 0.32f);
        float strike = Mathf.Clamp01((phase - 0.32f) / 0.22f);
        float recover = Mathf.Clamp01((phase - 0.54f) / 0.46f);

        if (phase < 0.32f)
        {
            float e = Smooth(windup);
            AddRot(chest, -5f, -18f * e, -25f * e);
            AddRot(rightUpperArm, -25f * e, -35f * e, -85f * e);
            AddRot(rightLowerArm, -55f * e, 0f, -25f * e);
        }
        else if (phase < 0.54f)
        {
            float e = Smooth(strike);
            AddRot(chest, 8f, Mathf.Lerp(-18f, 28f, e), Mathf.Lerp(-25f, 34f, e));
            AddRot(rightUpperArm, Mathf.Lerp(-25f, 20f, e), Mathf.Lerp(-35f, 20f, e), Mathf.Lerp(-85f, 92f, e));
            AddRot(rightLowerArm, Mathf.Lerp(-55f, -12f, e), 0f, Mathf.Lerp(-25f, 35f, e));
        }
        else
        {
            float e = 1f - Smooth(recover);
            AddRot(chest, 8f * e, 28f * e, 34f * e);
            AddRot(rightUpperArm, 20f * e, 20f * e, 92f * e);
            AddRot(rightLowerArm, -12f * e, 0f, 35f * e);
        }
    }

    private void ApplyDodge(float phase)
    {
        float arc = Mathf.Sin(phase * Mathf.PI);
        AddRot(chest, 58f * arc, 0f, 12f * arc);
        AddRot(head, -28f * arc, 0f, 0f);
        AddRot(leftUpperArm, 28f * arc, 0f, 28f * arc);
        AddRot(rightUpperArm, 28f * arc, 0f, -28f * arc);
        hips.localPosition += Vector3.down * 0.14f * arc;
    }

    private void AddRot(Transform bone, float x, float y, float z)
    {
        if (bone != null)
        {
            bone.localRotation *= Quaternion.Euler(x, y, z);
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
