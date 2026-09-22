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

    [MenuItem("Anime Action/Generate Model Quality Showcase")]
    public static void Generate()
    {
        Directory.CreateDirectory(MaterialDir);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject stage = new("ModelQualityShowcase");
        ModelQualityShowcase controller = stage.AddComponent<ModelQualityShowcase>();
        controller.defaultIndex = 2;

        Material floorMaterial = GetOrCreateMaterial(
            $"{MaterialDir}/NeutralFloor.mat",
            new Color(0.74f, 0.77f, 0.81f),
            0f,
            0.16f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "NeutralFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(3.5f, 1f, 3.5f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

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
        camera.fieldOfView = 32f;
        camera.nearClipPlane = 0.025f;
        camera.farClipPlane = 50f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        controller.viewCamera = camera;

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
