using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ModelQualityAcceptance
{
    public static void ValidateAssets()
    {
        string root = Path.Combine(Application.dataPath, "StreamingAssets", "Models");
        for (int i = 0; i < 3; i++)
        {
            char suffix = (char)('A' + i);
            string path = Path.Combine(root, $"AvatarSample_{suffix}.vrm");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Missing VRoid sample: {path}");
            }

            long bytes = new FileInfo(path).Length;
            if (bytes < 1_000_000)
            {
                throw new InvalidOperationException($"VRoid sample is unexpectedly small: {path} ({bytes} bytes)");
            }

            Debug.Log($"Model quality asset {suffix} passed: {bytes} bytes");
        }
    }

    public static void ValidateScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ModelQualityShowcaseSetup.ScenePath)
        {
            throw new InvalidOperationException($"Unexpected showcase scene: {scene.path}");
        }

        ModelQualityShowcase controller =
            UnityEngine.Object.FindFirstObjectByType<ModelQualityShowcase>();

        if (controller == null || controller.viewCamera == null)
        {
            throw new InvalidOperationException("Model quality showcase is missing its controller or camera.");
        }

        if (GameObject.Find("BlenderGeneratedHumanoid") != null ||
            GameObject.Find("GeneratedHumanoid") != null)
        {
            throw new InvalidOperationException("Legacy generated dummy leaked into the model-quality scene.");
        }

        Debug.Log("Model quality showcase scene acceptance passed.");
    }
}
