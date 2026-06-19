using UnityEditor;
using UnityEngine;
using TMPro;

namespace CloudTerraceRealm.Editor
{
    /// <summary>
    /// Editor script to generate HUD text GameObjects in the active scene
    /// so that the designer can easily see and customize them during edit mode.
    /// </summary>
    public static class CreateHUDObjects
    {
        [MenuItem("Tools/Generate HUD Text Objects")]
        public static void GenerateHUDObjects()
        {
            HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
            if (hud == null)
            {
                Debug.LogError("[CreateHUDObjects] Không tìm thấy HUDManager trong scene hiện tại!");
                EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy HUDManager trong scene hiện tại!", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int groupIndex = Undo.GetCurrentGroup();

            // 1. Create PopulationText
            CreateTextObject(hud.transform, "PopulationText", new Vector2(300f, 472f), new Vector2(260f, 50f), 32f, "Dan: 0/0");

            // 2. Create RelicText
            CreateTextObject(hud.transform, "RelicText", new Vector2(300f, 442f), new Vector2(260f, 50f), 24f, "0");

            // 3. Create CapacityText
            CreateTextObject(hud.transform, "CapacityText", new Vector2(150f, 472f), new Vector2(200f, 50f), 24f, "Kho: 0/0");

            // 4. Create FoodWarningText
            CreateTextObject(hud.transform, "FoodWarningText", new Vector2(300f, 412f), new Vector2(500f, 50f), 24f, "⚠️ THIẾU LƯƠNG THỰC! CƯ DÂN BỊ ĐÓI!", Color.red);

            // 5. Create BloodMoonBannerText
            CreateTextObject(hud.transform, "BloodMoonBannerText", new Vector2(0f, 120f), new Vector2(800f, 150f), 24f, "", Color.white);

            Undo.CollapseUndoOperations(groupIndex);
            
            // Mark scene as dirty so it prompts save
            EditorUtility.SetDirty(hud.gameObject);
            if (hud.gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            }

            Debug.Log("[CreateHUDObjects] Đã tạo/cập nhật thành công các đối tượng văn bản HUD!");
            EditorUtility.DisplayDialog("Thành công", "Đã tạo/cập nhật thành công các đối tượng văn bản HUD dưới HUDManager trong Hierarchy!", "OK");
        }

        private static void CreateTextObject(Transform parent, string name, Vector2 anchoredPos, Vector2 size, float fontSize, string defaultText, Color? textColor = null)
        {
            Transform existing = parent.Find(name);
            GameObject obj;
            if (existing != null)
            {
                obj = existing.gameObject;
            }
            else
            {
                obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                obj.transform.SetParent(parent, false);
                obj.layer = parent.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            }

            RectTransform rect = obj.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Configure RectTransform " + name);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            Undo.RecordObject(text, "Configure TextMeshProUGUI " + name);
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = textColor ?? Color.white;
            text.text = defaultText;
            text.raycastTarget = false;
        }
    }
}
