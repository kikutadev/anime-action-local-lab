using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UALMotionMeasure
{
    private const string FbxPath = "Assets/Animations/UAL/UAL1_Standard.fbx";

    public static void Measure()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (asset == null) throw new InvalidOperationException("UAL FBX not found");

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

        GameObject instance = UnityEngine.Object.Instantiate(asset);
        instance.name = "UALMotionMeasure";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        Animator animator = instance.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            throw new InvalidOperationException("UAL source animator is not humanoid");
        }

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        foreach (string suffix in new[] { "|Walk_Loop", "|Walk_Formal_Loop", "|Jog_Fwd_Loop" })
        {
            AnimationClip clip = clips.First(c => c.name.EndsWith(suffix));
            float minLy = float.PositiveInfinity;
            float minRy = float.PositiveInfinity;
            float maxSepZ = 0f;
            float maxFootSpan = 0f;

            for (int i = 0; i < 32; i++)
            {
                float phase = i / 32f;
                clip.SampleAnimation(instance, phase * clip.length);

                Vector3 h = hips.position;
                Vector3 l = lf.position - h;
                Vector3 r = rf.position - h;

                minLy = Mathf.Min(minLy, l.y);
                minRy = Mathf.Min(minRy, r.y);
                maxSepZ = Mathf.Max(maxSepZ, Mathf.Abs(l.z - r.z));
                maxFootSpan = Mathf.Max(maxFootSpan, Vector3.Distance(l, r));

                if (i % 4 == 0)
                {
                    Debug.Log(
                        $"UAL_PHASE clip={clip.name} phase={phase:F3} " +
                        $"L=({l.x:F3},{l.y:F3},{l.z:F3}) " +
                        $"R=({r.x:F3},{r.y:F3},{r.z:F3})");
                }
            }

            float estimatedStepSpeed = (2f * maxSepZ) / clip.length;
            Debug.Log(
                $"UAL_KINEMATICS clip={clip.name} len={clip.length:F3} " +
                $"maxFootSepZ={maxSepZ:F3} maxFootSpan={maxFootSpan:F3} " +
                $"estimatedSpeed={estimatedStepSpeed:F3}");
        }

        UnityEngine.Object.DestroyImmediate(instance);
    }
}
