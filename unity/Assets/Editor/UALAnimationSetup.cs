using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class UALAnimationSetup
{
    private const string FbxPath = "Assets/Animations/UAL/UAL1_Standard.fbx";
    private const string ControllerPath = "Assets/Resources/VroidLocomotion.controller";
    private const string UpperBodyMaskPath = "Assets/Resources/VroidUpperBody.mask";

    public static void ConfigureAndReport()
    {
        ConfigureImporter();
        CreateController(ControllerPath, "Walk_Loop", "VroidLocomotionBlend");
        Report();
    }

    private static void ConfigureImporter()
    {
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new System.InvalidOperationException("UAL model importer not found.");

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            bool loop = clip.name.Contains("_Loop") || clip.name.EndsWith("Sword_Idle");
            clip.loopTime = loop;
            clip.loopPose = loop;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static void CreateController(string path, string walkSuffix, string treeName)
    {
        Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.DeleteAsset(path);

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

        AnimationClip idle = clips.First(c => c.name.EndsWith("|Idle_Loop"));
        AnimationClip swordIdle = clips.First(c => c.name.EndsWith("|Sword_Idle"));
        AnimationClip walk = clips.First(c => c.name.EndsWith(walkSuffix));
        AnimationClip walkFormal = clips.First(c => c.name.EndsWith("|Walk_Formal_Loop"));
        AnimationClip jog = clips.First(c => c.name.EndsWith("|Jog_Fwd_Loop"));
        AnimationClip sprint = clips.First(c => c.name.EndsWith("|Sprint_Loop"));

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);

        AnimatorState state = controller.layers[0].stateMachine.AddState("Locomotion");
        controller.layers[0].stateMachine.defaultState = state;

        BlendTree tree = new()
        {
            name = treeName,
            blendType = BlendTreeType.Simple1D,
            blendParameter = "MoveSpeed",
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        // MoveSpeed is expressed in metres per second so the gameplay motor,
        // BlendTree thresholds, and clip playback all share one physical unit.
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, VroidActionMotor.UalWalkSpeed);
        tree.AddChild(jog, VroidActionMotor.UalJogSpeed);
        tree.AddChild(sprint, VroidActionMotor.UalRunSpeed);
        state.motion = tree;

        AnimatorState qaIdle = controller.layers[0].stateMachine.AddState("QA_Idle");
        qaIdle.motion = idle;
        AnimatorState qaSwordIdle = controller.layers[0].stateMachine.AddState("QA_SwordIdle");
        qaSwordIdle.motion = swordIdle;
        AnimatorState qaWalk = controller.layers[0].stateMachine.AddState("QA_Walk");
        qaWalk.motion = walk;
        AnimatorState qaWalkFormal = controller.layers[0].stateMachine.AddState("QA_WalkFormal");
        qaWalkFormal.motion = walkFormal;
        AnimatorState qaJog = controller.layers[0].stateMachine.AddState("QA_Jog");
        qaJog.motion = jog;
        AnimatorState qaSprint = controller.layers[0].stateMachine.AddState("QA_Sprint");
        qaSprint.motion = sprint;

        AvatarMask upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (upperMask == null)
        {
            upperMask = new AvatarMask();
            AssetDatabase.CreateAsset(upperMask, UpperBodyMaskPath);
        }

        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
        upperMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        EditorUtility.SetDirty(upperMask);

        AnimatorControllerLayer upperLayer = new()
        {
            name = "SwordUpperBody",
            defaultWeight = 1f,
            avatarMask = upperMask,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = new AnimatorStateMachine { name = "SwordUpperBody" },
        };
        AssetDatabase.AddObjectToAsset(upperLayer.stateMachine, controller);
        AnimatorState upperState = upperLayer.stateMachine.AddState("SwordUpperBody");
        upperState.motion = swordIdle;
        upperState.speed = 0f;
        upperState.cycleOffset = 0f;
        controller.AddLayer(upperLayer);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"UAL_CONTROLLER idle={idle.name}({idle.length:F3}s) " +
            $"walk={walk.name}({walk.length:F3}s) jog={jog.name}({jog.length:F3}s) " +
            $"sprint={sprint.name}({sprint.length:F3}s) path={path}");
    }

    private static void Report()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .OrderBy(c => c.name)
            .ToArray();

        Debug.Log($"UAL_CLIPS count={clips.Length}");
        foreach (AnimationClip clip in clips)
        {
            if (clip.name.Contains("Idle") || clip.name.Contains("Walk") ||
                clip.name.Contains("Jog") || clip.name.Contains("Sprint"))
            {
                Debug.Log(
                    $"UAL_LOCOMOTION name={clip.name} length={clip.length:F3} " +
                    $"fps={clip.frameRate:F1} loop={clip.isLooping} " +
                    $"avgSpeed={clip.averageSpeed.magnitude:F3} " +
                    $"avgAngular={clip.averageAngularSpeed:F3}");
            }
        }
    }
}
