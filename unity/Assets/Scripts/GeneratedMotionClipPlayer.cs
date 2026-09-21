using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
public sealed class GeneratedMotionClipPlayer : MonoBehaviour
{
    [Serializable]
    private sealed class MotionClipData
    {
        public int fps;
        public int frameCount;
        public float duration;
        public float[] rootTranslation;
        public MotionBoneData[] bones;
        public string source;
    }

    [Serializable]
    private sealed class MotionBoneData
    {
        public string name;
        public float[] rotation;
    }

    private sealed class RuntimeTrack
    {
        public Transform transform;
        public Quaternion baseRotation;
        public Quaternion[] rotations;
    }

    [SerializeField] private string resourceName = "HYMotionSlash";
    [SerializeField] private bool applyRootMotion;
    [SerializeField, Range(0f, 1.5f)] private float rootMotionScale = 1f;

    private readonly List<RuntimeTrack> tracks = new();
    private MotionClipData clip;
    private Vector3[] rootTranslations = Array.Empty<Vector3>();
    private Vector3 basePosition;
    private float playhead;

    public bool IsReady => clip != null && tracks.Count > 0;
    public bool IsPlaying { get; private set; }
    public float Duration => clip?.duration ?? 0f;
    public string Source => clip?.source ?? string.Empty;
    public int FrameCount => clip?.frameCount ?? 0;

    private void Awake()
    {
        basePosition = transform.localPosition;
        Load();
    }

    private void Load()
    {
        TextAsset asset = Resources.Load<TextAsset>(resourceName);
        if (asset == null)
        {
            Debug.LogWarning($"Generated motion resource not found: {resourceName}");
            return;
        }

        clip = JsonUtility.FromJson<MotionClipData>(asset.text);
        if (clip == null || clip.frameCount < 2 || clip.fps <= 0)
        {
            Debug.LogError($"Invalid generated motion resource: {resourceName}");
            clip = null;
            return;
        }

        Dictionary<string, Transform> boneMap = new(StringComparer.Ordinal);
        foreach (Transform node in GetComponentsInChildren<Transform>(true))
        {
            boneMap.TryAdd(node.name, node);
        }

        foreach (MotionBoneData bone in clip.bones)
        {
            if (!boneMap.TryGetValue(bone.name, out Transform target))
            {
                Debug.LogWarning($"Generated motion bone not found in runtime rig: {bone.name}");
                continue;
            }

            int frameCount = bone.rotation.Length / 4;
            Quaternion[] rotations = new Quaternion[frameCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                int i = frame * 4;
                rotations[frame] = Normalize(new Quaternion(
                    bone.rotation[i],
                    bone.rotation[i + 1],
                    bone.rotation[i + 2],
                    bone.rotation[i + 3]));
            }

            tracks.Add(new RuntimeTrack
            {
                transform = target,
                baseRotation = target.localRotation,
                rotations = rotations,
            });
        }

        int rootFrames = clip.rootTranslation?.Length / 3 ?? 0;
        rootTranslations = new Vector3[rootFrames];
        for (int frame = 0; frame < rootFrames; frame++)
        {
            int i = frame * 3;
            rootTranslations[frame] = new Vector3(
                clip.rootTranslation[i],
                clip.rootTranslation[i + 1],
                clip.rootTranslation[i + 2]);
        }

        Debug.Log($"Loaded generated motion: {resourceName}, {clip.frameCount}f @ {clip.fps}fps, {tracks.Count} tracks, source={clip.source}");
    }

    public float Play()
    {
        if (!IsReady)
        {
            return 0f;
        }

        playhead = 0f;
        IsPlaying = true;
        return Duration;
    }

    public void Stop()
    {
        IsPlaying = false;
        RestoreBasePose();
    }

    private void LateUpdate()
    {
        if (!IsPlaying || clip == null)
        {
            return;
        }

        playhead += Time.deltaTime;
        float frame = Mathf.Clamp(playhead * clip.fps, 0f, clip.frameCount - 1f);
        int a = Mathf.FloorToInt(frame);
        int b = Mathf.Min(a + 1, clip.frameCount - 1);
        float t = frame - a;

        foreach (RuntimeTrack track in tracks)
        {
            int ta = Mathf.Min(a, track.rotations.Length - 1);
            int tb = Mathf.Min(b, track.rotations.Length - 1);
            Quaternion generated = Quaternion.Slerp(track.rotations[ta], track.rotations[tb], t);
            track.transform.localRotation = track.baseRotation * generated;
        }

        if (applyRootMotion && rootTranslations.Length > 0)
        {
            int ra = Mathf.Min(a, rootTranslations.Length - 1);
            int rb = Mathf.Min(b, rootTranslations.Length - 1);
            transform.localPosition = basePosition + Vector3.Lerp(rootTranslations[ra], rootTranslations[rb], t) * rootMotionScale;
        }

        if (playhead >= Duration)
        {
            Stop();
        }
    }

    private void RestoreBasePose()
    {
        foreach (RuntimeTrack track in tracks)
        {
            track.transform.localRotation = track.baseRotation;
        }

        transform.localPosition = basePosition;
    }

    private static Quaternion Normalize(Quaternion value)
    {
        float magnitude = Mathf.Sqrt(
            value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
        return magnitude > 1e-6f
            ? new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude)
            : Quaternion.identity;
    }
}
