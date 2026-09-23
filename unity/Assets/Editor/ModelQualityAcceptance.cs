using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ModelQualityAcceptance
{
    public static void ValidateAssets()
    {
        RuntimeAnimatorController locomotion =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Resources/VroidLocomotion.controller");
        if (locomotion == null)
        {
            throw new InvalidOperationException(
                "Missing VroidLocomotion.controller.");
        }

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

        if (controller == null || controller.viewCamera == null ||
            controller.playerHost == null || controller.actionMotor == null ||
            controller.actionMotor.weaponVisual == null ||
            controller.actionMotor.weaponTrail == null)
        {
            throw new InvalidOperationException(
                "Action showcase is missing controller, camera, player host, action motor, sword, or trail.");
        }

        VroidActionMotor motor = controller.actionMotor;
        if (motor.moveSpeed < 1.2f || motor.moveSpeed > 5.0f ||
            motor.naturalWalkSpeed < 0.5f || motor.naturalWalkSpeed > 3.0f ||
            motor.naturalJogSpeed < 1.0f || motor.naturalJogSpeed > 5.0f ||
            motor.swordUpperBodyWeight < 0f || motor.swordUpperBodyWeight > 1f)
        {
            throw new InvalidOperationException(
                $"Locomotion tuning is outside sane runtime ranges: " +
                $"move={motor.moveSpeed:F2}, walk={motor.naturalWalkSpeed:F2}, " +
                $"jog={motor.naturalJogSpeed:F2}, upper={motor.swordUpperBodyWeight:F2}.");
        }

        if (Vector3.Distance(motor.weaponVisual.localScale, Vector3.one * 0.78f) > 0.01f)
        {
            throw new InvalidOperationException(
                $"Unexpected sword scale: {motor.weaponVisual.localScale}");
        }

        TrainingDummy[] dummies = UnityEngine.Object.FindObjectsByType<TrainingDummy>(
            FindObjectsSortMode.None);
        if (dummies.Length < 4)
        {
            throw new InvalidOperationException(
                $"Action showcase expected at least 4 training dummies, got {dummies.Length}.");
        }

        if (GameObject.Find("BlenderGeneratedHumanoid") != null ||
            GameObject.Find("GeneratedHumanoid") != null)
        {
            throw new InvalidOperationException("Legacy generated dummy leaked into the model-quality scene.");
        }

        Debug.Log("Model quality showcase scene acceptance passed.");
    }
}
