using TheRacer;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameBootstrap))]
public sealed class GameBootstrapEditor : Editor
{
    private SerializedProperty _badgeSize;
    private SerializedProperty _horizontalOffset;
    private SerializedProperty _verticalOffset;

    private void OnEnable()
    {
        _badgeSize = serializedObject.FindProperty("hudCornerBadgeSize");
        _horizontalOffset = serializedObject.FindProperty("hudCornerHorizontalOffset");
        _verticalOffset = serializedObject.FindProperty("hudCornerVerticalOffset");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Live HUD Layout Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Adjust HUD Corner Badge Size and the corner offsets above. The preview updates immediately; these same values are used in Play mode.", MessageType.Info);
        DrawHudPreview();
    }

    private void DrawHudPreview()
    {
        float width = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 40f);
        float height = width * 9f / 16f;
        Rect preview = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(preview, new Color(0.035f, 0.018f, 0.075f));

        Handles.BeginGUI();
        Color oldColor = Handles.color;
        DrawRoadGrid(preview);

        float unit = preview.width / 1920f;
        float radius = Mathf.Clamp(_badgeSize.floatValue * unit * 0.5f, 12f, preview.height * 0.45f);
        float xOffset = _horizontalOffset.floatValue * unit;
        float yOffset = _verticalOffset.floatValue * unit;
        Vector2 speedCenter = new Vector2(preview.xMin + xOffset, preview.yMax - yOffset);
        Vector2 scoreCenter = new Vector2(preview.xMax - xOffset, preview.yMax - yOffset);

        DrawBadge(speedCenter, radius, "000", "KM/H", false);
        DrawBadge(scoreCenter, radius, "000000", "SCORE", true);

        Handles.color = oldColor;
        Handles.EndGUI();
    }

    private static void DrawRoadGrid(Rect preview)
    {
        Handles.color = new Color(0.04f, 0.2f, 0.35f, 0.8f);
        for (int i = 1; i <= 8; i++)
        {
            float t = i / 9f;
            float y = Mathf.Lerp(preview.yMin, preview.yMax, t);
            Handles.DrawLine(new Vector2(preview.xMin, y), new Vector2(preview.xMax, y));
        }
        Handles.color = new Color(1f, 0.08f, 0.2f, 0.75f);
        Vector2 horizon = new Vector2(preview.center.x, preview.yMin + preview.height * 0.42f);
        for (int i = -4; i <= 4; i++)
        {
            Vector2 edge = new Vector2(preview.center.x + i * preview.width * 0.22f, preview.yMax);
            Handles.DrawLine(horizon, edge);
        }
    }

    private static void DrawBadge(Vector2 center, float radius, string value, string label, bool score)
    {
        Color rim = score ? new Color(1f, 0.08f, 0.75f) : new Color(1f, 0.08f, 0.75f);
        Handles.color = new Color(0.05f, 0.8f, 1f, 0.2f);
        Handles.DrawSolidDisc(center, Vector3.forward, radius * 1.16f);
        Handles.color = rim;
        Handles.DrawSolidDisc(center, Vector3.forward, radius * 1.04f);
        Handles.color = new Color(0.02f, 0.76f, 0.96f);
        Handles.DrawSolidDisc(center, Vector3.forward, radius * 0.96f);
        Handles.color = new Color(0.015f, 0.035f, 0.10f);
        Handles.DrawSolidDisc(center, Vector3.forward, radius * 0.79f);

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(radius * (score ? 0.32f : 0.38f)), 8, 20),
            normal = { textColor = new Color(0.48f, 0.94f, 1f) }
        };
        Rect valueRect = new Rect(center.x - radius * 0.78f, center.y - radius * 0.18f,
            radius * 1.56f, radius * 0.42f);
        GUI.Label(valueRect, value, style);
        style.fontSize = Mathf.Clamp(Mathf.RoundToInt(radius * 0.14f), 6, 11);
        style.normal.textColor = score ? new Color(1f, 0.2f, 0.8f) : new Color(0.15f, 0.9f, 1f);
        Rect labelRect = new Rect(center.x - radius * 0.72f, center.y - radius * 0.55f,
            radius * 1.44f, radius * 0.24f);
        GUI.Label(labelRect, label, style);
    }
}
