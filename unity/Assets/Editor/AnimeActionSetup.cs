using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AnimeActionSetup
{
    private const string ScenePath = "Assets/Generated/AnimeActionDemo.unity";
    private const string MaterialDir = "Assets/Generated/Materials";

    [MenuItem("Anime Action/Generate Demo")]
    public static void GenerateDemo()
    {
        Directory.CreateDirectory(MaterialDir);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material skin = MakeMaterial("Skin", new Color(0.95f, 0.73f, 0.61f), 0f, 0.72f);
        Material hair = MakeMaterial("Hair", new Color(0.045f, 0.065f, 0.11f), 0f, 0.42f);
        Material coat = MakeMaterial("Coat", new Color(0.08f, 0.17f, 0.38f), 0f, 0.58f);
        Material shirt = MakeMaterial("Shirt", new Color(0.77f, 0.84f, 0.92f), 0f, 0.72f);
        Material pants = MakeMaterial("Pants", new Color(0.06f, 0.08f, 0.13f), 0f, 0.74f);
        Material boots = MakeMaterial("Boots", new Color(0.035f, 0.03f, 0.04f), 0f, 0.62f);
        Material accent = MakeMaterial("Accent", new Color(0.72f, 0.05f, 0.09f), 0f, 0.5f);
        Material white = MakeMaterial("EyeWhite", new Color(0.96f, 0.98f, 1f), 0f, 0.55f);
        Material iris = MakeMaterial("Iris", new Color(0.05f, 0.48f, 0.72f), 0.05f, 0.3f);
        Material metal = MakeMaterial("Metal", new Color(0.55f, 0.64f, 0.72f), 0.85f, 0.24f);
        Material floorMat = MakeMaterial("Floor", new Color(0.075f, 0.09f, 0.12f), 0f, 0.86f);
        Material enemyMat = MakeMaterial("Enemy", new Color(0.42f, 0.11f, 0.15f), 0f, 0.6f);

        GameObject fighter = new("AnimeFighter");
        fighter.transform.position = Vector3.zero;
        CharacterController cc = fighter.AddComponent<CharacterController>();
        cc.height = 1.9f;
        cc.radius = 0.34f;
        cc.center = new Vector3(0f, 0.95f, 0f);
        fighter.AddComponent<AnimeFighterMotor>();
        fighter.AddComponent<DemoAutoPlay>();

        GameObject rigRoot = new("GeneratedHumanoid");
        rigRoot.transform.SetParent(fighter.transform, false);
        ProceduralAnimeRig rig = rigRoot.AddComponent<ProceduralAnimeRig>();
        rigRoot.AddComponent<GeneratedMotionClipPlayer>();

        Transform root = Bone("Root", rigRoot.transform, new Vector3(0f, 0f, 0f), rigRoot.transform);
        Transform hips = Bone("Hips", root, new Vector3(0f, 0.91f, 0f), rigRoot.transform);
        Transform spine = Bone("Spine", hips, new Vector3(0f, 1.06f, 0f), rigRoot.transform);
        Transform chest = Bone("Chest", spine, new Vector3(0f, 1.22f, 0f), rigRoot.transform);
        Transform upperChest = Bone("UpperChest", chest, new Vector3(0f, 1.36f, 0f), rigRoot.transform);
        Transform neck = Bone("Neck", upperChest, new Vector3(0f, 1.47f, 0f), rigRoot.transform);
        Transform head = Bone("Head", neck, new Vector3(0f, 1.58f, 0f), rigRoot.transform);

        Transform lua = Bone("LeftUpperArm", upperChest, new Vector3(-0.22f, 1.39f, 0f), rigRoot.transform);
        Transform lla = Bone("LeftLowerArm", lua, new Vector3(-0.43f, 1.20f, 0f), rigRoot.transform);
        Transform lh = Bone("LeftHand", lla, new Vector3(-0.58f, 1.02f, -0.01f), rigRoot.transform);
        Transform rua = Bone("RightUpperArm", upperChest, new Vector3(0.22f, 1.39f, 0f), rigRoot.transform);
        Transform rla = Bone("RightLowerArm", rua, new Vector3(0.43f, 1.20f, 0f), rigRoot.transform);
        Transform rh = Bone("RightHand", rla, new Vector3(0.58f, 1.02f, -0.01f), rigRoot.transform);

        Transform lul = Bone("LeftUpperLeg", hips, new Vector3(-0.105f, 0.91f, 0f), rigRoot.transform);
        Transform lll = Bone("LeftLowerLeg", lul, new Vector3(-0.11f, 0.55f, 0f), rigRoot.transform);
        Transform lf = Bone("LeftFoot", lll, new Vector3(-0.11f, 0.18f, -0.02f), rigRoot.transform);
        Transform rul = Bone("RightUpperLeg", hips, new Vector3(0.105f, 0.91f, 0f), rigRoot.transform);
        Transform rll = Bone("RightLowerLeg", rul, new Vector3(0.11f, 0.55f, 0f), rigRoot.transform);
        Transform rf = Bone("RightFoot", rll, new Vector3(0.11f, 0.18f, -0.02f), rigRoot.transform);

        // Torso and anime proportions.
        VisualSphere("HeadMesh", new Vector3(0f, 1.76f, 0f), new Vector3(0.42f, 0.37f, 0.48f), skin, head, rigRoot.transform);
        VisualCapsule("NeckMesh", new Vector3(0f, 1.50f, 0f), new Vector3(0.12f, 0.12f, 0.18f), skin, neck, rigRoot.transform);
        VisualSphere("ChestMesh", new Vector3(0f, 1.31f, 0f), new Vector3(0.48f, 0.27f, 0.43f), coat, chest, rigRoot.transform);
        VisualSphere("WaistMesh", new Vector3(0f, 1.08f, 0f), new Vector3(0.34f, 0.23f, 0.34f), shirt, spine, rigRoot.transform);
        VisualSphere("HipMesh", new Vector3(0f, 0.91f, 0f), new Vector3(0.39f, 0.26f, 0.29f), pants, hips, rigRoot.transform);

        AddLimb("LeftUpperArmMesh", lua, new Vector3(-0.325f, 1.295f, 0f), new Vector3(0.13f, 0.27f, 0.13f), coat, 46f, rigRoot.transform);
        AddLimb("LeftLowerArmMesh", lla, new Vector3(-0.505f, 1.11f, 0f), new Vector3(0.105f, 0.245f, 0.105f), skin, 40f, rigRoot.transform);
        AddLimb("RightUpperArmMesh", rua, new Vector3(0.325f, 1.295f, 0f), new Vector3(0.13f, 0.27f, 0.13f), coat, -46f, rigRoot.transform);
        AddLimb("RightLowerArmMesh", rla, new Vector3(0.505f, 1.11f, 0f), new Vector3(0.105f, 0.245f, 0.105f), skin, -40f, rigRoot.transform);
        VisualSphere("LeftHandMesh", new Vector3(-0.60f, 0.99f, -0.01f), new Vector3(0.10f, 0.09f, 0.15f), skin, lh, rigRoot.transform);
        VisualSphere("RightHandMesh", new Vector3(0.60f, 0.99f, -0.01f), new Vector3(0.10f, 0.09f, 0.15f), skin, rh, rigRoot.transform);

        VisualCapsule("LeftThigh", new Vector3(-0.105f, 0.72f, 0f), new Vector3(0.19f, 0.40f, 0.19f), pants, lul, rigRoot.transform);
        VisualCapsule("RightThigh", new Vector3(0.105f, 0.72f, 0f), new Vector3(0.19f, 0.40f, 0.19f), pants, rul, rigRoot.transform);
        VisualCapsule("LeftShin", new Vector3(-0.11f, 0.365f, 0f), new Vector3(0.15f, 0.39f, 0.15f), pants, lll, rigRoot.transform);
        VisualCapsule("RightShin", new Vector3(0.11f, 0.365f, 0f), new Vector3(0.15f, 0.39f, 0.15f), pants, rll, rigRoot.transform);
        VisualCube("LeftBoot", new Vector3(-0.11f, 0.10f, -0.10f), new Vector3(0.17f, 0.18f, 0.32f), boots, lf, rigRoot.transform);
        VisualCube("RightBoot", new Vector3(0.11f, 0.10f, -0.10f), new Vector3(0.17f, 0.18f, 0.32f), boots, rf, rigRoot.transform);

        // Anime face. Forward is +Z in Unity.
        VisualSphere("LeftEye", new Vector3(-0.075f, 1.79f, 0.178f), new Vector3(0.105f, 0.13f, 0.038f), white, head, rigRoot.transform);
        VisualSphere("RightEye", new Vector3(0.075f, 1.79f, 0.178f), new Vector3(0.105f, 0.13f, 0.038f), white, head, rigRoot.transform);
        VisualSphere("LeftIris", new Vector3(-0.075f, 1.79f, 0.207f), new Vector3(0.055f, 0.082f, 0.018f), iris, head, rigRoot.transform);
        VisualSphere("RightIris", new Vector3(0.075f, 1.79f, 0.207f), new Vector3(0.055f, 0.082f, 0.018f), iris, head, rigRoot.transform);

        // Hair cap and readable layered spikes.
        VisualSphere("HairCap", new Vector3(0f, 1.865f, -0.015f), new Vector3(0.45f, 0.35f, 0.43f), hair, head, rigRoot.transform);
        AddHairSpike("HairFront", new Vector3(0f, 1.94f, 0.16f), new Vector3(0.14f, 0.32f, 0.11f), new Vector3(62f,0f,0f), hair, head, rigRoot.transform);
        AddHairSpike("HairLeft", new Vector3(-0.13f, 1.92f, 0.11f), new Vector3(0.13f, 0.28f, 0.10f), new Vector3(58f,0f,24f), hair, head, rigRoot.transform);
        AddHairSpike("HairRight", new Vector3(0.13f, 1.92f, 0.11f), new Vector3(0.13f, 0.28f, 0.10f), new Vector3(58f,0f,-24f), hair, head, rigRoot.transform);

        VisualCube("Scarf", new Vector3(0f, 1.43f, 0.11f), new Vector3(0.38f, 0.10f, 0.10f), accent, chest, rigRoot.transform);
        VisualCube("CoatTailL", new Vector3(-0.09f, 0.91f, -0.07f), new Vector3(0.14f, 0.42f, 0.10f), coat, hips, rigRoot.transform);
        VisualCube("CoatTailR", new Vector3(0.09f, 0.91f, -0.07f), new Vector3(0.14f, 0.42f, 0.10f), coat, hips, rigRoot.transform);

        Transform swordRoot = Bone("SwordRoot", rh, new Vector3(0.60f, 0.99f, 0f), rigRoot.transform);
        VisualCylinder("SwordGrip", new Vector3(0.60f, 0.90f, 0f), new Vector3(0.05f, 0.20f, 0.05f), boots, swordRoot, rigRoot.transform);
        VisualCube("SwordGuard", new Vector3(0.60f, 0.81f, 0f), new Vector3(0.30f, 0.045f, 0.07f), metal, swordRoot, rigRoot.transform);
        VisualCube("SwordBlade", new Vector3(0.60f, 0.49f, 0f), new Vector3(0.11f, 0.64f, 0.045f), metal, swordRoot, rigRoot.transform);

        rig.hips = hips;
        rig.spine = spine;
        rig.chest = chest;
        rig.upperChest = upperChest;
        rig.neck = neck;
        rig.head = head;
        rig.leftUpperArm = lua;
        rig.rightUpperArm = rua;
        rig.leftLowerArm = lla;
        rig.rightLowerArm = rla;
        rig.leftUpperLeg = lul;
        rig.rightUpperLeg = rul;
        rig.leftLowerLeg = lll;
        rig.rightLowerLeg = rll;
        rig.swordRoot = swordRoot;

        // Prefer the Blender-generated model when present. The procedural rig above
        // remains an always-buildable fallback for fresh clones or generator failures.
        GameObject generatedModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/AnimeFighter.fbx");
        if (generatedModelAsset != null)
        {
            GameObject generatedModel = (GameObject)PrefabUtility.InstantiatePrefab(generatedModelAsset);
            generatedModel.name = "BlenderGeneratedHumanoid";
            generatedModel.transform.SetParent(fighter.transform, false);
            generatedModel.transform.localPosition = Vector3.zero;
            generatedModel.transform.localRotation = Quaternion.identity;
            generatedModel.transform.localScale = Vector3.one;

            Animator importedAnimator = generatedModel.GetComponentInChildren<Animator>();
            if (importedAnimator != null)
            {
                importedAnimator.enabled = false;
            }

            ProceduralAnimeRig generatedRig = generatedModel.AddComponent<ProceduralAnimeRig>();
            generatedModel.AddComponent<GeneratedMotionClipPlayer>();
            generatedRig.hips = FindNamedTransform(generatedModel.transform, "Hips");
            generatedRig.spine = FindNamedTransform(generatedModel.transform, "Spine");
            generatedRig.chest = FindNamedTransform(generatedModel.transform, "Chest");
            generatedRig.upperChest = FindNamedTransform(generatedModel.transform, "UpperChest");
            generatedRig.neck = FindNamedTransform(generatedModel.transform, "Neck");
            generatedRig.head = FindNamedTransform(generatedModel.transform, "Head");
            generatedRig.leftUpperArm = FindNamedTransform(generatedModel.transform, "LeftUpperArm");
            generatedRig.rightUpperArm = FindNamedTransform(generatedModel.transform, "RightUpperArm");
            generatedRig.leftLowerArm = FindNamedTransform(generatedModel.transform, "LeftLowerArm");
            generatedRig.rightLowerArm = FindNamedTransform(generatedModel.transform, "RightLowerArm");
            generatedRig.leftUpperLeg = FindNamedTransform(generatedModel.transform, "LeftUpperLeg");
            generatedRig.rightUpperLeg = FindNamedTransform(generatedModel.transform, "RightUpperLeg");
            generatedRig.leftLowerLeg = FindNamedTransform(generatedModel.transform, "LeftLowerLeg");
            generatedRig.rightLowerLeg = FindNamedTransform(generatedModel.transform, "RightLowerLeg");
            generatedRig.swordRoot = FindNamedTransform(generatedModel.transform, "RightHand");

            if (generatedRig.hips != null && generatedRig.head != null && generatedRig.rightUpperArm != null)
            {
                rigRoot.SetActive(false);
                Debug.Log("Using Blender-generated anime fighter model.");
            }
            else
            {
                Object.DestroyImmediate(generatedModel);
                Debug.LogWarning("Generated FBX is missing required bones; using procedural fallback.");
            }
        }

        // Stage.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "ArenaFloor";
        floor.transform.localScale = new Vector3(3.5f, 1f, 3.5f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2f / 5f;
            Vector3 p = new(Mathf.Sin(angle) * 3.2f, 0.65f, Mathf.Cos(angle) * 3.2f + 1.4f);
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = $"TrainingEnemy_{i+1}";
            enemy.transform.position = p;
            enemy.transform.localScale = new Vector3(0.8f, 1.25f, 0.8f);
            enemy.GetComponent<Renderer>().sharedMaterial = enemyMat;
            enemy.AddComponent<EnemyTarget>();
        }

        // Decorative arena blocks, free of external assets.
        for (int i = 0; i < 10; i++)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "ArenaBlock";
            float a = i * Mathf.PI * 2f / 10f;
            pillar.transform.position = new Vector3(Mathf.Sin(a) * 7f, 0.55f, Mathf.Cos(a) * 7f);
            pillar.transform.localScale = new Vector3(0.65f, 1.1f + (i % 3) * 0.35f, 0.65f);
            pillar.GetComponent<Renderer>().sharedMaterial = floorMat;
        }

        // Lighting.
        RenderSettings.ambientLight = new Color(0.22f, 0.27f, 0.38f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.06f, 0.075f, 0.10f);
        RenderSettings.fogDensity = 0.015f;
        GameObject sun = new("KeyLight");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.45f;
        light.color = new Color(0.92f, 0.95f, 1f);
        sun.transform.rotation = Quaternion.Euler(47f, -32f, 0f);

        GameObject rimObj = new("RimLight");
        Light rim = rimObj.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.intensity = 0.75f;
        rim.color = new Color(0.35f, 0.48f, 1f);
        rimObj.transform.rotation = Quaternion.Euler(28f, 150f, 0f);

        GameObject cameraObj = new("Main Camera");
        Camera camera = cameraObj.AddComponent<Camera>();
        cameraObj.tag = "MainCamera";
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 100f;
        camera.fieldOfView = 50f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
        ThirdPersonCamera follow = cameraObj.AddComponent<ThirdPersonCamera>();
        follow.target = fighter.transform;
        cameraObj.transform.position = new Vector3(2.8f, 2.4f, -4.6f);

        GameObject hud = new("HUD");
        hud.AddComponent<ActionHud>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated anime action demo: {ScenePath}");
    }

    private static Transform Bone(string name, Transform parent, Vector3 worldLocalPosition, Transform rigRoot)
    {
        GameObject go = new(name);
        go.transform.position = rigRoot.TransformPoint(worldLocalPosition);
        go.transform.rotation = rigRoot.rotation;
        go.transform.SetParent(parent, true);
        return go.transform;
    }

    private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialDir}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            existing.SetFloat("_Metallic", metallic);
            existing.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Standard");
        Material material = new(shader) { name = name, color = color };
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject VisualSphere(string name, Vector3 position, Vector3 scale, Material mat, Transform parent, Transform root)
        => VisualPrimitive(PrimitiveType.Sphere, name, position, scale, Vector3.zero, mat, parent, root);

    private static GameObject VisualCapsule(string name, Vector3 position, Vector3 scale, Material mat, Transform parent, Transform root)
        => VisualPrimitive(PrimitiveType.Capsule, name, position, scale, Vector3.zero, mat, parent, root);

    private static GameObject VisualCube(string name, Vector3 position, Vector3 scale, Material mat, Transform parent, Transform root)
        => VisualPrimitive(PrimitiveType.Cube, name, position, scale, Vector3.zero, mat, parent, root);

    private static GameObject VisualCylinder(string name, Vector3 position, Vector3 scale, Material mat, Transform parent, Transform root)
        => VisualPrimitive(PrimitiveType.Cylinder, name, position, scale, Vector3.zero, mat, parent, root);

    private static GameObject VisualPrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Vector3 euler, Material mat, Transform parent, Transform root)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = root.TransformPoint(position);
        go.transform.rotation = root.rotation * Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        go.transform.SetParent(parent, true);
        return go;
    }

    private static void AddLimb(string name, Transform bone, Vector3 position, Vector3 scale, Material mat, float zAngle, Transform root)
        => VisualPrimitive(PrimitiveType.Capsule, name, position, scale, new Vector3(0f,0f,zAngle), mat, bone, root);

    private static void AddHairSpike(string name, Vector3 position, Vector3 scale, Vector3 euler, Material mat, Transform head, Transform root)
        => VisualPrimitive(PrimitiveType.Capsule, name, position, scale, euler, mat, head, root);

    private static Transform FindNamedTransform(Transform root, string name)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == name)
            {
                return transform;
            }
        }

        return null;
    }

}
