using UnityEngine;

public sealed class ActionHud : MonoBehaviour
{
    private GUIStyle title;
    private GUIStyle body;
    private GeneratedMotionClipPlayer generatedMotion;

    private void Awake()
    {
        title = new GUIStyle { fontSize = 25, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        body = new GUIStyle { fontSize = 15 };
        body.normal.textColor = new Color(0.85f, 0.91f, 1f);
        generatedMotion = FindFirstObjectByType<GeneratedMotionClipPlayer>();
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(22, 18, 500, 40), "LOCAL ANIME ACTION LAB", title);
        string motion = generatedMotion != null && generatedMotion.IsReady
            ? $"攻撃: {generatedMotion.Source} / {generatedMotion.FrameCount} frames"
            : "攻撃: procedural fallback";
        GUI.Label(new Rect(24, 55, 760, 92),
            $"WASD / 矢印: 移動   J / 左クリック: 斬撃   Space / K: 回避\n右ドラッグ: カメラ   {motion}", body);
    }
}
