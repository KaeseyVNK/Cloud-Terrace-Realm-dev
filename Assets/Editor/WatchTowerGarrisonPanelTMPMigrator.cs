using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WatchTowerGarrisonPanelTMPMigrator
{
    private const string PanelPath = "Canvas/WatchTowerGarrisonPanel";

    [MenuItem("Tools/Cloud Terrace/UI/Convert Watch Tower Panel To TMP")]
    public static void Convert()
    {
        GameObject panel = GameObject.Find(PanelPath);
        if (panel == null)
        {
            Debug.LogWarning($"[WatchTowerGarrisonPanelTMPMigrator] Panel not found: {PanelPath}");
            return;
        }

        ConvertDirectText(panel.transform, "WatchTowerTitleText", 24f);
        ConvertDirectText(panel.transform, "WatchTowerOccupancyText", 18f);
        ConvertButtonLabel(panel.transform, "WatchTowerEjectAllButton", "EJECT ALL", 18f);
        ConvertButtonLabel(panel.transform, "WatchTowerCloseButton", "X", 20f);

        EditorUtility.SetDirty(panel);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[WatchTowerGarrisonPanelTMPMigrator] Watch tower panel text converted to TextMeshProUGUI.");
    }

    private static void ConvertDirectText(Transform panel, string childName, float fontSize)
    {
        Transform target = panel.Find(childName);
        if (target == null)
        {
            Debug.LogWarning($"[WatchTowerGarrisonPanelTMPMigrator] Missing text object: {childName}");
            return;
        }

        ConvertTextObject(target.gameObject, null, fontSize);
    }

    private static void ConvertButtonLabel(Transform panel, string buttonName, string fallbackText, float fontSize)
    {
        Transform button = panel.Find(buttonName);
        if (button == null)
        {
            Debug.LogWarning($"[WatchTowerGarrisonPanelTMPMigrator] Missing button object: {buttonName}");
            return;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy == null)
        {
            Debug.LogWarning($"[WatchTowerGarrisonPanelTMPMigrator] Missing legacy label under button: {buttonName}");
            return;
        }

        ConvertTextObject(legacy.gameObject, fallbackText, fontSize);
    }

    private static void ConvertTextObject(GameObject target, string fallbackText, float fontSize)
    {
        Text legacy = target.GetComponent<Text>();
        string text = fallbackText;
        Color color = Color.white;
        TextAnchor alignment = TextAnchor.MiddleCenter;

        if (legacy != null)
        {
            if (string.IsNullOrEmpty(text))
            {
                text = legacy.text;
            }

            color = legacy.color;
            alignment = legacy.alignment;
            Undo.DestroyObjectImmediate(legacy);
        }

        TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = Undo.AddComponent<TextMeshProUGUI>(target);
        }

        tmp.text = text ?? string.Empty;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = ToTMPAlignment(alignment);
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;

        EditorUtility.SetDirty(tmp);
        EditorUtility.SetDirty(target);
    }

    private static TextAlignmentOptions ToTMPAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };
    }
}
