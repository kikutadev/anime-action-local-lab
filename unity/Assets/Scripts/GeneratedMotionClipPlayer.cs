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
        public Quaternion[] rotations;
    }

    private sealed class RuntimeClip
    {
        public MotionClipData data;
        public List<RuntimeTrack> tracks;
        public Vector3[] rootTranslations;
    }

    [SerializeField] private string defaultResourceName = "HYMotionSlash";
    [SerializeField] private bool applyRootMotion;
    [SerializeField, Range(0f, 1.5f)] private float rootMotionScale = 1f;

    private readonly Dictionary<string, RuntimeClip> clips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Transform> boneMap = new(StringComparer.Ordinal);
    private readonly Dictionary<Transform, Quaternion> baseRotations = new();

    private RuntimeClip activeClip;
    private Vector3 basePosition;
    private float playhead;

    public bool IsReady => GetOrLoad(defaultResourceName) != null;
    public bool IsPlaying { get; private set; }
    public float Duration => activeClip?.data.duration ?? GetOrLoad(defaultResourceName)?.data.duration ?? 0f;
    public string Source => activeClip?.data.source ?? GetOrLoad(defaultResourceName)?.data.source ?? string.Empty;
    public int FrameCount => activeClip?.data.frameCount ?? GetOrLoad(defaultResourceName)?.data.frameCount ?? 0;

    private void Awake()
    {
        basePosition = transform.localPosition;

        foreach (Transform node in GetComponentsInChildren<Transform>(true))
        {
            boneMap.TryAdd(node.name, node);
            baseRotations.TryAdd(node, node.localRotation);
        }

        GetOrLoad(defaultResourceName);
        GetOrLoad("HYMotionDodge", warnIfMissing: false);
    }

    public bool HasClip(string resourceName) => GetOrLoad(resourceName, warnIfMissing: false) != null;

    public float Play() => Play(defaultResourceName);

    public float Play(string resourceName)
    {
        RuntimeClip next = GetOrLoad(resourceName);
        if (next == null)
        {
            return 0f;
        }

        RestoreBasePose();
        activeClip = next;
        playhead = 0f;
        IsPlaying = true;
        return next.data.duration;
    }

    public void Stop()
    {
        IsPlaying = false;
        activeClip = null;
        RestoreBasePose();
    }

    private RuntimeClip GetOrLoad(string resourceName, bool warnIfMissing = true)
    {
        if (clips.TryGetValue(resourceName, out RuntimeClip existing))
        {
            return existing;
        }

        TextAsset asset = Resources.Load<TextAsset>(resourceName);
        if (asset == null)
        {
            if (warnIfMissing)
            {
                Debug.LogWarning($"Generated motion resource not found: {resourceName}");
            }

            return null;
        }

        MotionClipData data = JsonUtility.FromJson<MotionClipData>(asset.text);
        if (data == null || data.frameCount < 2 || data.fps <= 0 || data.bones == null)
        {
            Debug.LogError($"Invalid generated motion resource: {resourceName}");
            return null;
        }

        List<RuntimeTrack> tracks = new();
        foreach (MotionBoneData bone in data.bones)
        {
            if (!boneMap.TryGetValue(bone.name, out Transform target))
            {
                Debug.LogWarning($"Generated motion bone not found in runtime rig: {bone.name}");
                continue;
            }

            int frameCount = bone.rotation?.Length / 4 ?? 0;
            if (frameCount == 0)
            {
                continue;
            }

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
                rotations = rotations,
            });
        }

        int rootFrames = data.rootTranslation?.Length / 3 ?? 0;
        Vector3[] rootTranslations = new Vector3[rootFrames];
        for (int frame = 0; frame < rootFrames; frame++)
        {
            int i = frame * 3;
            rootTranslations[frame] = new Vector3(
                data.rootTranslation[i],
                data.rootTranslation[i + 1],
                data.rootTranslation[i + 2]);
        }

        if (tracks.Count == 0)
        {
            Debug.LogError($"Generated motion has no bindable tracks: {resourceName}");
            return null;
        }

        RuntimeClip loaded = new()
        {
            data = data,
            tracks = tracks,
            rootTranslations = rootTranslations,
        };
        clips[resourceName] = loaded;

        Debug.Log(
            $"Loaded generated motion: {resourceName}, {data.frameCount}f @ {data.fps}fps, " +
            $"{tracks.Count} tracks, source={data.source}");
        return loaded;
    }

    private void LateUpdate()
    {
        if (!IsPlaying || activeClip == null)
        {
            return;
        }

        MotionClipData data = activeClip.data;
        playhead += Time.deltaTime;
        float frame = Mathf.Clamp(playhead * data.fps, 0f, data.frameCount - 1f);
        int a = Mathf.FloorToInt(frame);
        int b = Mathf.Min(a + 1, data.frameCount - 1);
        float t = frame - a;

        foreach (RuntimeTrack track in activeClip.tracks)
        {
            int ta = Mathf.Min(a, track.rotations.Length - 1);
            int tb = Mathf.Min(b, track.rotations.Length - 1);
            Quaternion generated = Quaternion.Slerp(track.rotations[ta], track.rotations[tb], t);
            track.transform.localRotation = baseRotations[track.transform] * generated;
        }

        if (applyRootMotion && activeClip.rootTranslations.Length > 0)
        {
            int ra = Mathf.Min(a, activeClip.rootTranslations.Length - 1);
            int rb = Mathf.Min(b, activeClip.rootTranslations.Length - 1);
            transform.localPosition =
                basePosition +
                Vector3.Lerp(activeClip.rootTranslations[ra], activeClip.rootTranslations[rb], t) * rootMotionScale;
        }

        if (playhead >= data.duration)
        {
            Stop();
        }
    }

    private void RestoreBasePose()
    {
        foreach ((Transform target, Quaternion rotation) in baseRotations)
        {
            if (target != null)
            {
                target.localRotation = rotation;
            }
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
