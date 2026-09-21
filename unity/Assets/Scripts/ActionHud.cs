using UnityEngine;

public sealed class ActionHud : MonoBehaviour
{
    private GUIStyle title;
    private GUIStyle body;

    private void Awake()
    {
        title = new GUIStyle { fontSize = 25, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        body = new GUIStyle { fontSize = 15 };
        body.normal.textColor = new Color(0.85f, 0.91f, 1f);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(22, 18, 500, 40), "LOCAL ANIME ACTION LAB", title);
        GUI.Label(new Rect(24, 55, 650, 70),
            "WASD / 矢印: 移動   J / 左クリック: 斬撃   Space / K: 回避\n右ドラッグ: カメラ   ※ キャラ・骨格・基準モーションはローカル生成", body);
    }
}
