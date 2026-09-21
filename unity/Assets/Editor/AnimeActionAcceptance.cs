using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AnimeActionAcceptance
{
    private sealed class ClipHeader
    {
        public int fps;
        public int frameCount;
        public float duration;
        public string source;
    }

    public static void Validate()
    {
        ValidateGeneratedModel();
        ValidateGeneratedMotion("Assets/Resources/HYMotionSlash.json", 30);
        ValidateGeneratedMotion("Assets/Resources/HYMotionDodge.json", 30);
        Debug.Log("Anime action asset acceptance passed.");
    }

    public static void ValidateGeneratedScene()
    {
        GameObject generatedModel = GameObject.Find("BlenderGeneratedHumanoid");
        if (generatedModel == null)
        {
            throw new InvalidOperationException("Generated scene is not using the Blender fighter model.");
        }

        Renderer[] renderers = generatedModel.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length < 10)
        {
            throw new InvalidOperationException($"Generated scene has too few Blender renderers: {renderers.Length}");
        }

        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || material.shader.name != "AnimeAction/Toon")
                {
                    string materialName = material != null ? material.name : "<null>";
                    string shaderName = material?.shader != null ? material.shader.name : "<null>";
                    throw new InvalidOperationException(
                        $"Renderer {renderer.name} is not toon shaded: material={materialName}, shader={shaderName}");
                }
            }
        }

        Debug.Log($"Anime action scene acceptance passed: {renderers.Length} toon-shaded renderers.");
    }

    private static void ValidateGeneratedModel()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/AnimeFighter.fbx");
        if (model == null)
        {
            throw new InvalidOperationException("Generated Blender model is missing: Assets/Models/AnimeFighter.fbx");
        }

        string[] requiredBones =
        {
            "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm",
            "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg",
            "LeftFoot", "RightFoot", "LeftHand", "RightHand",
        };

        var names = model.GetComponentsInChildren<Transform>(true)
            .Select(transform => transform.name)
            .ToHashSet(StringComparer.Ordinal);

        string[] missing = requiredBones.Where(name => !names.Contains(name)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Generated Blender model is missing bones: {string.Join(", ", missing)}");
        }

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length < 10)
        {
            throw new InvalidOperationException($"Generated Blender model has too few renderer parts: {renderers.Length}");
        }
    }

    private static void ValidateGeneratedMotion(string assetPath, int minimumFrames)
    {
        TextAsset motion = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (motion == null)
        {
            throw new InvalidOperationException($"Generated HY-Motion clip is missing: {assetPath}");
        }

        ClipHeader header = JsonUtility.FromJson<ClipHeader>(motion.text);
        if (header == null || header.frameCount < minimumFrames || header.fps != 30)
        {
            throw new InvalidOperationException($"Generated HY-Motion clip header is invalid: {assetPath}");
        }

        if (!string.Equals(header.source, "HY-Motion-1.0-Lite", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected motion source in {assetPath}: {header.source}");
        }

        if (header.duration < 0.9f)
        {
            throw new InvalidOperationException($"Generated motion is unexpectedly short: {assetPath}, {header.duration:F2}s");
        }
    }
}
