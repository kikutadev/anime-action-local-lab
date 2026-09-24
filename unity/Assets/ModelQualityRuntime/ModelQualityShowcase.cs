using System;
using System.Collections;
using System.IO;
using UniGLTF;
using UnityEngine;
using UnityEngine.Networking;
using VRM;

public sealed class ModelQualityShowcase : MonoBehaviour
{
    public Camera viewCamera;
    public Transform playerHost;
    public VroidActionMotor actionMotor;
    public int defaultIndex = 2;
    public string[] modelFiles =
    {
        "AvatarSample_A.vrm",
        "AvatarSample_B.vrm",
        "AvatarSample_C.vrm",
    };

    public float orbitSensitivity = 0.22f;
    public float zoomSensitivity = 0.16f;

    private RuntimeGltfInstance currentInstance;
    private GameObject currentRoot;
    private int currentIndex;
    private int loadSerial;
    private bool loading;
    private string errorMessage;

    private float yaw;
    private float pitch = 3f;
    private float distance = 3f;
    private float fitDistance = 3f;
    private Vector3 focus = Vector3.up;
    private Vector2 lastPointer;
    private bool dragging;
    private float lastPinchDistance;
    private bool qaActive;
    private string pendingQaSpec;

    private void Start()
    {
        pendingQaSpec = ReadQaSpecFromUrl();
        Select(Mathf.Clamp(defaultIndex, 0, modelFiles.Length - 1));
    }

    private void OnDestroy()
    {
        actionMotor?.BindAvatar(null);
        if (currentInstance != null)
        {
            currentInstance.Dispose();
            currentInstance = null;
        }
    }

    private static string ReadQaSpecFromUrl()
    {
        string url = Application.absoluteURL;
        if (string.IsNullOrEmpty(url)) return null;

        int marker = url.IndexOf("?qa=", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            marker = url.IndexOf("&qa=", StringComparison.OrdinalIgnoreCase);
        }
        if (marker < 0) return null;

        int start = marker + 4;
        int end = url.IndexOf('&', start);
        string encoded = end >= 0 ? url.Substring(start, end - start) : url.Substring(start);
        return UnityWebRequest.UnEscapeURL(encoded);
    }

    public void SetQaState(string spec)
    {
        if (actionMotor == null || !actionMotor.Ready) return;

        string[] parts = (spec ?? string.Empty).Split('|');
        string motion = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])
            ? parts[0]
            : "idle";
        float phase = 0f;
        if (parts.Length > 1)
        {
            float.TryParse(
                parts[1],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out phase);
        }
        string view = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2])
            ? parts[2]
            : "threequarter";
        float upperBodyWeight = actionMotor.swordUpperBodyWeight;
        if (parts.Length > 3)
        {
            float.TryParse(
                parts[3],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out upperBodyWeight);
        }

        Vector3 weaponEuler = actionMotor.weaponHandLocalEuler;
        if (parts.Length > 6)
        {
            float.TryParse(
                parts[4],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out weaponEuler.x);
            float.TryParse(
                parts[5],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out weaponEuler.y);
            float.TryParse(
                parts[6],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out weaponEuler.z);
        }

        bool useRuntimeWeaponPose =
            parts.Length > 7 &&
            string.Equals(
                parts[7],
                "auto",
                StringComparison.OrdinalIgnoreCase);

        qaActive = true;
        foreach (TrainingDummy dummy in FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
        {
            dummy.gameObject.SetActive(false);
        }
        actionMotor.SetQaPose(
            motion,
            phase,
            view,
            upperBodyWeight,
            weaponEuler,
            useRuntimeWeaponPose);
    }

    public void ExitQaState()
    {
        qaActive = false;
        actionMotor?.ExitQaPose();
        foreach (TrainingDummy dummy in FindObjectsByType<TrainingDummy>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            dummy.gameObject.SetActive(true);
        }
    }

    public void TriggerQaRuntimeAttack()
    {
        if (qaActive)
        {
            ExitQaState();
        }
        actionMotor?.TriggerAttack();
    }

    public void TriggerQaRuntimeDodge()
    {
        if (qaActive)
        {
            ExitQaState();
        }
        actionMotor?.TriggerDodge();
    }

    public void Select(int index)
    {
        if (modelFiles == null || modelFiles.Length == 0) return;
        currentIndex = (index + modelFiles.Length) % modelFiles.Length;
        int serial = ++loadSerial;
        StopAllCoroutines();
        StartCoroutine(LoadModelCoroutine(currentIndex, serial));
    }

    private IEnumerator LoadModelCoroutine(int index, int serial)
    {
        loading = true;
        errorMessage = null;

        if (currentInstance != null)
        {
            actionMotor?.BindAvatar(null);
            currentInstance.Dispose();
            currentInstance = null;
            currentRoot = null;
            yield return Resources.UnloadUnusedAssets();
        }

        string fileName = modelFiles[index];
        string url = Application.streamingAssetsPath.TrimEnd('/') + "/Models/" + fileName;
        if (!url.Contains("://"))
        {
            url = "file://" + url;
        }

        using UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (serial != loadSerial) yield break;

        if (request.result != UnityWebRequest.Result.Success)
        {
            loading = false;
            errorMessage = $"読み込み失敗: {request.error}";
            Debug.LogError($"Failed to load {url}: {request.error}");
            yield break;
        }

        byte[] bytes = request.downloadHandler.data;
        ImportBytes(fileName, bytes, serial);
    }

    private async void ImportBytes(string fileName, byte[] bytes, int serial)
    {
        try
        {
            IAwaitCaller awaitCaller;
#if UNITY_WEBGL && !UNITY_EDITOR
            awaitCaller = new RuntimeOnlyNoThreadAwaitCaller();
#else
            awaitCaller = new RuntimeOnlyAwaitCaller();
#endif
            RuntimeGltfInstance loaded = await VrmUtility.LoadBytesAsync(
                fileName,
                bytes,
                awaitCaller);

            if (serial != loadSerial)
            {
                loaded.Dispose();
                return;
            }

            currentInstance = loaded;
            currentRoot = loaded.gameObject;
            currentRoot.name = Path.GetFileNameWithoutExtension(fileName);

            Transform host = playerHost != null ? playerHost : transform;
            Transform visualPivot = host.Find("AvatarVisualPivot");
            if (visualPivot == null)
            {
                GameObject pivotObject = new("AvatarVisualPivot");
                visualPivot = pivotObject.transform;
                visualPivot.SetParent(host, false);
                visualPivot.localPosition = Vector3.zero;
                visualPivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                visualPivot.localScale = Vector3.one;
            }

            currentRoot.transform.SetParent(visualPivot, false);
            currentRoot.transform.localPosition = Vector3.zero;
            currentRoot.transform.localRotation = Quaternion.identity;
            currentRoot.transform.localScale = Vector3.one;

            loaded.ShowMeshes();
            loaded.EnableUpdateWhenOffscreen();

            NormalizeForAction();
            actionMotor?.BindAvatar(currentRoot);
            loading = false;

            if (!string.IsNullOrEmpty(pendingQaSpec))
            {
                SetQaState(pendingQaSpec);
            }

            int vertices = 0;
            int triangles = 0;
            foreach (Mesh mesh in loaded.Meshes)
            {
                if (mesh == null) continue;
                vertices += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    triangles += (int)(mesh.GetIndexCount(i) / 3);
                }
            }
            Debug.Log($"Loaded {fileName}: meshes={loaded.Meshes.Count}, materials={loaded.Materials.Count}, textures={loaded.Textures.Count}, vertices={vertices}, triangles={triangles}");
        }
        catch (Exception ex)
        {
            loading = false;
            errorMessage = "モデルの展開に失敗しました";
            Debug.LogException(ex);
        }
    }

    private void NormalizeForAction()
    {
        if (currentRoot == null) return;

        Bounds bounds = CalculateBounds(currentRoot);
        Vector3 anchor = playerHost != null ? playerHost.position : transform.position;
        currentRoot.transform.position += new Vector3(
            anchor.x - bounds.center.x,
            anchor.y - bounds.min.y,
            anchor.z - bounds.center.z);

        bounds = CalculateBounds(currentRoot);
        focus = bounds.center;
        fitDistance = CalculateFitDistance(bounds);
        distance = fitDistance;
        yaw = 0f;
        pitch = 3f;
    }

    private Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds
            ? bounds
            : new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);
    }

    private float CalculateFitDistance(Bounds bounds)
    {
        if (viewCamera == null) return 3f;

        float verticalHalfFov = Mathf.Deg2Rad * viewCamera.fieldOfView * 0.5f;
        float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * Mathf.Max(0.25f, viewCamera.aspect));
        float byHeight = bounds.extents.y / Mathf.Max(0.05f, Mathf.Tan(verticalHalfFov));
        float byWidth = bounds.extents.x / Mathf.Max(0.05f, Mathf.Tan(horizontalHalfFov));
        return Mathf.Max(byHeight, byWidth) * 1.14f;
    }

    private void Update()
    {
        HandleKeyboard();

        // Keep the original orbit viewer available when no gameplay motor is bound.
        if (actionMotor == null)
        {
            HandleMouse();
            HandleTouch();
            UpdateCamera();
        }
    }

    private void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
    }

    private void HandleMouse()
    {
        if (Input.touchCount > 0) return;

        if (Input.GetMouseButtonDown(0))
        {
            dragging = true;
            lastPointer = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0)) dragging = false;

        if (dragging)
        {
            Vector2 now = Input.mousePosition;
            Vector2 delta = now - lastPointer;
            yaw += delta.x * orbitSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity * 0.55f, -18f, 25f);
            lastPointer = now;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(
                distance * (1f - scroll * zoomSensitivity),
                fitDistance * 0.46f,
                fitDistance * 1.8f);
        }
    }

    private void HandleTouch()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                yaw += touch.deltaPosition.x * orbitSensitivity * 0.7f;
                pitch = Mathf.Clamp(
                    pitch - touch.deltaPosition.y * orbitSensitivity * 0.35f,
                    -18f,
                    25f);
            }
            lastPinchDistance = 0f;
        }
        else if (Input.touchCount >= 2)
        {
            Vector2 a = Input.GetTouch(0).position;
            Vector2 b = Input.GetTouch(1).position;
            float pinch = Vector2.Distance(a, b);
            if (lastPinchDistance > 0f)
            {
                float delta = pinch - lastPinchDistance;
                distance = Mathf.Clamp(
                    distance * (1f - delta * 0.0025f),
                    fitDistance * 0.46f,
                    fitDistance * 1.8f);
            }
            lastPinchDistance = pinch;
        }
        else
        {
            lastPinchDistance = 0f;
        }
    }

    private void UpdateCamera()
    {
        if (viewCamera == null) return;

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = orbit * new Vector3(0f, 0f, distance);
        viewCamera.transform.position = focus + offset;
        viewCamera.transform.rotation = Quaternion.LookRotation(
            focus - viewCamera.transform.position,
            Vector3.up);
    }

    private void OnGUI()
    {
        if (qaActive) return;

        float scale = Mathf.Clamp(Screen.width / 430f, 0.82f, 1.35f);
        float margin = 14f * scale;
        Rect safe = Screen.safeArea;

        GUIStyle title = new(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(18f * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        GUIStyle hint = new(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(11f * scale),
            alignment = TextAnchor.MiddleLeft,
        };
        GUIStyle button = new(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(15f * scale),
            fontStyle = FontStyle.Bold,
        };

        string modelName = $"AvatarSample_{(char)('A' + currentIndex)}";
        GUI.Label(
            new Rect(safe.x + margin, safe.y + margin, safe.width - margin * 2f, 30f * scale),
            modelName,
            title);

        string sub = loading
            ? "読み込み中…"
            : errorMessage ?? "移動: WASD / 左スティック　視点: 画面ドラッグ　攻撃: J　回避: Space";
        GUI.Label(
            new Rect(safe.x + margin, safe.y + 39f * scale, safe.width - margin * 2f, 24f * scale),
            sub,
            hint);

        float gap = 6f * scale;
        float w = 42f * scale;
        float y = safe.y + 12f * scale;
        float x0 = safe.xMax - margin - (w * 3f + gap * 2f);

        for (int i = 0; i < 3; i++)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = i == currentIndex
                ? new Color(0.72f, 0.82f, 1f)
                : Color.white;

            if (GUI.Button(
                new Rect(x0 + i * (w + gap), y, w, 34f * scale),
                ((char)('A' + i)).ToString(),
                button))
            {
                Select(i);
            }

            GUI.backgroundColor = previous;
        }
    }
}
