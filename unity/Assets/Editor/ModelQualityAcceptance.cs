using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ModelQualityAcceptance
{
    public static void ValidateAssets()
    {
        AnimatorController locomotion =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/Resources/VroidLocomotion.controller");
        if (locomotion == null)
        {
            throw new InvalidOperationException(
                "Missing VroidLocomotion.controller.");
        }
        ValidateLocomotionController(locomotion);

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

    private static void ValidateLocomotionController(AnimatorController controller)
    {
        if (!controller.parameters.Any(parameter => parameter.name == "MoveSpeed"))
        {
            throw new InvalidOperationException(
                "VroidLocomotion.controller is missing MoveSpeed.");
        }

        AnimatorState locomotionState = controller.layers[0].stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Locomotion");
        BlendTree tree = locomotionState?.motion as BlendTree;
        if (tree == null)
        {
            throw new InvalidOperationException(
                "VroidLocomotion.controller is missing its Locomotion BlendTree.");
        }

        ChildMotion[] children = tree.children;
        string[] expectedSuffixes =
        {
            "|Idle_Loop",
            "|Walk_Loop",
            "|Jog_Fwd_Loop",
            "|Sprint_Loop",
        };
        float[] expectedThresholds =
        {
            0f,
            VroidActionMotor.UalWalkSpeed,
            VroidActionMotor.UalJogSpeed,
            VroidActionMotor.UalRunSpeed,
        };

        if (children.Length != expectedSuffixes.Length)
        {
            throw new InvalidOperationException(
                $"Locomotion BlendTree expected 4 motions, got {children.Length}.");
        }

        for (int i = 0; i < children.Length; i++)
        {
            string motionName = children[i].motion != null
                ? children[i].motion.name
                : string.Empty;
            if (!motionName.EndsWith(expectedSuffixes[i], StringComparison.Ordinal) ||
                Mathf.Abs(children[i].threshold - expectedThresholds[i]) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Unexpected locomotion child {i}: " +
                    $"motion={motionName}, threshold={children[i].threshold:F2}.");
            }
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
        if (Mathf.Abs(motor.moveSpeed - VroidActionMotor.UalRunSpeed) > 0.01f ||
            Mathf.Abs(motor.naturalWalkSpeed - VroidActionMotor.UalWalkSpeed) > 0.01f ||
            Mathf.Abs(motor.naturalJogSpeed - VroidActionMotor.UalJogSpeed) > 0.01f ||
            Mathf.Abs(motor.naturalRunSpeed - VroidActionMotor.UalRunSpeed) > 0.01f ||
            motor.acceleration < 20f || motor.deceleration < 20f ||
            motor.turnSharpness < 16f ||
            motor.cameraMinDistance < 1.5f || motor.cameraMaxDistance < motor.cameraMinDistance ||
            motor.swordUpperBodyWeight < 0f || motor.swordUpperBodyWeight > 1f ||
            motor.attackDuration < 0.45f || motor.attackDuration > 1.10f ||
            motor.attackRootScale < 0.40f || motor.attackRootScale > 1.00f ||
            motor.attackHitPhase < 0.05f || motor.attackHitPhase > 0.95f ||
            motor.dodgeDuration < 0.45f || motor.dodgeDuration > 0.90f)
        {
            throw new InvalidOperationException(
                $"Locomotion tuning is outside sane runtime ranges: " +
                $"move={motor.moveSpeed:F2}, walk={motor.naturalWalkSpeed:F2}, " +
                $"jog={motor.naturalJogSpeed:F2}, run={motor.naturalRunSpeed:F2}, " +
                $"accel={motor.acceleration:F1}, decel={motor.deceleration:F1}, " +
                $"turn={motor.turnSharpness:F1}, upper={motor.swordUpperBodyWeight:F2}, " +
                $"attackDuration={motor.attackDuration:F2}, attackRoot={motor.attackRootScale:F2}, " +
                $"hitPhase={motor.attackHitPhase:F2}, dodgeDuration={motor.dodgeDuration:F2}.");
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
