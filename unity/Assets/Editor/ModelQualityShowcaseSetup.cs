using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ModelQualityShowcaseSetup
{
    public const string ScenePath = "Assets/Generated/ModelQualityShowcase.unity";
    private const string MaterialDir = "Assets/Generated/ModelQualityMaterials";
    private const string SwordPath = "Assets/Models/TrainingSword.fbx";

    [MenuItem("Anime Action/Generate Model Quality Showcase")]
    public static void Generate()
    {
        Directory.CreateDirectory(MaterialDir);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject stage = new("ModelQualityShowcase");
        ModelQualityShowcase controller = stage.AddComponent<ModelQualityShowcase>();
        controller.defaultIndex = 2;

        GameObject player = new("Player");
        player.transform.SetParent(stage.transform, false);
        player.transform.position = Vector3.zero;
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.30f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        VroidActionMotor motor = player.AddComponent<VroidActionMotor>();
        motor.swordUpperBodyWeight = 0.85f;
        motor.weaponIdleLocalEuler = new Vector3(0f, 0f, -90f);
        motor.weaponMoveLocalEuler = new Vector3(0f, 90f, 0f);
        controller.playerHost = player.transform;
        controller.actionMotor = motor;

        Material floorMaterial = GetOrCreateMaterial(
            $"{MaterialDir}/NeutralFloor.mat",
            new Color(0.74f, 0.77f, 0.81f),
            0f,
            0.16f);

        Material dummyMaterial = GetOrCreateMaterial(
            $"{MaterialDir}/TrainingDummy.mat",
            new Color(0.30f, 0.34f, 0.42f),
            0f,
            0.22f);

        Material bladeMaterial = GetOrCreateMaterial(
            $"{MaterialDir}/SwordBlade.mat",
            new Color(0.78f, 0.82f, 0.88f),
            0.72f,
            0.68f);

        Material guardMaterial = GetOrCreateMaterial(
            $"{MaterialDir}/SwordGuard.mat",
            new Color(0.20f, 0.23f, 0.28f),
            0.55f,
            0.48f);

        Material gripMaterial = GetOrCreateMaterial(
            $"{MaterialDir}/SwordGrip.mat",
            new Color(0.10f, 0.11f, 0.14f),
            0.05f,
            0.24f);

        GameObject swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
        if (swordAsset == null)
        {
            throw new System.InvalidOperationException($"Missing training sword asset: {SwordPath}");
        }

        GameObject sword = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset);
        sword.name = "PlayerSword";
        sword.transform.SetParent(player.transform, false);
        sword.transform.localPosition = Vector3.zero;
        sword.transform.localRotation = Quaternion.identity;
        sword.transform.localScale = Vector3.one * 0.78f;

        foreach (Renderer renderer in sword.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name.Contains("Blade") || renderer.name.Contains("Tip"))
            {
                renderer.sharedMaterial = bladeMaterial;
            }
            else if (renderer.name.Contains("Grip"))
            {
                renderer.sharedMaterial = gripMaterial;
            }
            else
            {
                renderer.sharedMaterial = guardMaterial;
            }
        }

        GameObject trailObject = new("SwordTrail");
        trailObject.transform.SetParent(sword.transform, false);
        trailObject.transform.localPosition = new Vector3(0f, 0f, 0.70f);
        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = 0.14f;
        trail.minVertexDistance = 0.025f;
        trail.startWidth = 0.048f;
        trail.endWidth = 0f;
        trail.emitting = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Shader trailShader = Shader.Find("Sprites/Default");
        if (trailShader != null)
        {
            Material trailMaterial = new(trailShader);
            trailMaterial.name = "SwordTrailRuntime";
            trailMaterial.color = new Color(0.82f, 0.90f, 1.0f, 0.68f);
            trail.material = trailMaterial;
        }

        motor.weaponVisual = sword.transform;
        motor.weaponTrail = trail;
        sword.SetActive(false);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "NeutralFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(5.5f, 1f, 5.5f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        Vector3[] dummyPositions =
        {
            new Vector3(0f, 0.75f, 3.1f),
            new Vector3(-2.4f, 0.75f, 4.2f),
            new Vector3(2.4f, 0.75f, 4.2f),
            new Vector3(0f, 0.75f, 6.2f),
        };
        for (int i = 0; i < dummyPositions.Length; i++)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = $"TrainingDummy_{i + 1}";
            dummy.transform.position = dummyPositions[i];
            dummy.transform.localScale = new Vector3(0.72f, 1.0f, 0.72f);
            dummy.GetComponent<Renderer>().sharedMaterial = dummyMaterial;
            dummy.AddComponent<TrainingDummy>();
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.70f);
        RenderSettings.fog = false;

        CreateDirectional(
            "KeyLight",
            1.05f,
            new Color(1f, 0.95f, 0.90f),
            new Vector3(38f, -28f, 0f));

        CreateDirectional(
            "FillLight",
            0.45f,
            new Color(0.74f, 0.84f, 1f),
            new Vector3(24f, 148f, 0f));

        GameObject cameraObject = new("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.88f, 0.91f, 0.95f);
        camera.fieldOfView = 48f;
        camera.nearClipPlane = 0.025f;
        camera.farClipPlane = 50f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        controller.viewCamera = camera;
        motor.viewCamera = camera;
        cameraObject.transform.position = new Vector3(0f, 1.8f, -4.2f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
        };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Generated model quality showcase: {ScenePath}");
    }

    private static void CreateDirectional(
        string name,
        float intensity,
        Color color,
        Vector3 euler)
    {
        GameObject go = new(name);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(euler);
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }
}
