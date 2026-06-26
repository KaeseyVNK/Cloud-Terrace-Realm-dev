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

            // 6. Create ActiveCardsPanel
            CreateActiveCardSlotPrefab();
            CreateActiveCardsPanelObject(hud.transform);

            Undo.CollapseUndoOperations(groupIndex);
            
            // Mark scene as dirty so it prompts save
            EditorUtility.SetDirty(hud.gameObject);
            if (hud.gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            }

            Debug.Log("[CreateHUDObjects] Đã tạo/cập nhật thành công các đối tượng văn bản HUD và Panel Buff dưới HUDManager trong Hierarchy!");
        }

        private static void CreateActiveCardSlotPrefab()
        {
            // 1. Tạo GameObject tạm thời trong scene để xây dựng cấu trúc prefab
            GameObject root = new GameObject("ActiveCardSlot", typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(50f, 50f);

            // Gắn component ActiveCardSlotUI
            ActiveCardSlotUI slotUI = root.AddComponent<ActiveCardSlotUI>();

            // 2. Tạo BorderImage (viền hiển thị độ hiếm)
            GameObject borderObj = new GameObject("BorderImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            borderObj.transform.SetParent(root.transform, false);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero; // Stretch
            UnityEngine.UI.Image borderImg = borderObj.GetComponent<UnityEngine.UI.Image>();
            // Gán sprite mặc định của Unity UI (Sliced)
            borderImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            borderImg.type = UnityEngine.UI.Image.Type.Sliced;

            // 3. Tạo IconImage (ảnh của thẻ nâng cấp)
            GameObject iconObj = new GameObject("IconImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconObj.transform.SetParent(root.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.15f, 0.15f);
            iconRect.anchorMax = new Vector2(0.85f, 0.85f);
            iconRect.sizeDelta = Vector2.zero; // Stretch chừa biên lề
            UnityEngine.UI.Image iconImg = iconObj.GetComponent<UnityEngine.UI.Image>();
            iconImg.raycastTarget = false;

            // 4. Tạo TooltipRoot (bảng mô tả khi hover)
            GameObject tooltipRootObj = new GameObject("TooltipRoot", typeof(RectTransform));
            tooltipRootObj.transform.SetParent(root.transform, false);
            RectTransform tooltipRect = tooltipRootObj.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.5f, 1f); // Đặt phía trên Icon
            tooltipRect.anchorMax = new Vector2(0.5f, 1f);
            tooltipRect.pivot = new Vector2(0.5f, 0f);
            tooltipRect.anchoredPosition = new Vector2(0f, 15f);
            tooltipRect.sizeDelta = new Vector2(250f, 120f);

            // Ảnh nền đen mờ cho Tooltip
            UnityEngine.UI.Image tooltipBg = tooltipRootObj.AddComponent<UnityEngine.UI.Image>();
            tooltipBg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            tooltipBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            tooltipBg.type = UnityEngine.UI.Image.Type.Sliced;

            // Viền vàng kim mỏng bằng Outline
            UnityEngine.UI.Outline outline = tooltipRootObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Thêm Canvas component để đảm bảo đè lên các icon sibling khác
            Canvas tooltipCanvas = tooltipRootObj.AddComponent<Canvas>();
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = 100;

            // Bố cục chiều dọc cho các nội dung văn bản bên trong Tooltip
            UnityEngine.UI.VerticalLayoutGroup tooltipLayout = tooltipRootObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            tooltipLayout.padding = new UnityEngine.RectOffset(10, 10, 10, 10);
            tooltipLayout.spacing = 5f;
            tooltipLayout.childForceExpandWidth = true;
            tooltipLayout.childForceExpandHeight = false;
            tooltipLayout.childControlWidth = true;
            tooltipLayout.childControlHeight = true;

            // TitleText (Tên thẻ)
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(tooltipRootObj.transform, false);
            TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 13f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.TopLeft;

            // RarityText (Độ hiếm)
            GameObject rarityObj = new GameObject("RarityText", typeof(RectTransform), typeof(TextMeshProUGUI));
            rarityObj.transform.SetParent(tooltipRootObj.transform, false);
            TextMeshProUGUI rarityText = rarityObj.GetComponent<TextMeshProUGUI>();
            rarityText.fontSize = 10f;
            rarityText.fontStyle = FontStyles.Bold;
            rarityText.alignment = TextAlignmentOptions.TopLeft;

            // DescriptionText (Mô tả)
            GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(tooltipRootObj.transform, false);
            TextMeshProUGUI descText = descObj.GetComponent<TextMeshProUGUI>();
            descText.fontSize = 10f;
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.color = new Color(0.9f, 0.9f, 0.9f);

            // Gán các liên kết reference vào component ActiveCardSlotUI thông qua SerializedObject
            SerializedObject so = new SerializedObject(slotUI);
            so.FindProperty("_iconImage").objectReferenceValue = iconImg;
            so.FindProperty("_rarityBorderImage").objectReferenceValue = borderImg;
            so.FindProperty("_tooltipRoot").objectReferenceValue = tooltipRootObj;
            so.FindProperty("_tooltipTitleText").objectReferenceValue = titleText;
            so.FindProperty("_tooltipRarityText").objectReferenceValue = rarityText;
            so.FindProperty("_tooltipDescriptionText").objectReferenceValue = descText;
            so.ApplyModifiedProperties();

            // 5. Lưu thành Prefab tại Assets/Resources/UI/ActiveCardSlot.prefab
            string folderPath = "Assets/Resources/UI";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
            }

            string prefabPath = folderPath + "/ActiveCardSlot.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log("[CreateHUDObjects] Đã tạo/cập nhật thành công prefab ActiveCardSlot tại: " + prefabPath);
        }

        private static void CreateActiveCardsPanelObject(Transform parent)
        {
            string name = "ActiveCardsPanel";
            Transform existing = parent.Find(name);
            GameObject obj;
            if (existing != null)
            {
                obj = existing.gameObject;
            }
            else
            {
                obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(parent, false);
                obj.layer = parent.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            }

            RectTransform rect = obj.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Configure RectTransform " + name);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-450f, 380f); // Dưới thanh tài nguyên bên trái
            rect.sizeDelta = new Vector2(400f, 60f);

            // Gắn controller
            ActiveCardHUDController controller = obj.GetComponent<ActiveCardHUDController>();
            if (controller == null)
            {
                controller = obj.AddComponent<ActiveCardHUDController>();
                Undo.RegisterCreatedObjectUndo(controller, "Add ActiveCardHUDController");
            }

            // Tự động load và gán prefab ActiveCardSlot vào controller
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/ActiveCardSlot.prefab");
            if (slotPrefab != null)
            {
                SerializedObject controllerSO = new SerializedObject(controller);
                controllerSO.FindProperty("_iconSlotPrefab").objectReferenceValue = slotPrefab;
                controllerSO.ApplyModifiedProperties();
            }

            // Gắn HorizontalLayoutGroup
            UnityEngine.UI.HorizontalLayoutGroup layout = obj.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                Undo.RegisterCreatedObjectUndo(layout, "Add HorizontalLayoutGroup");
            }
            Undo.RecordObject(layout, "Configure HorizontalLayoutGroup");
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Gắn ContentSizeFitter
            UnityEngine.UI.ContentSizeFitter fitter = obj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = obj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                Undo.RegisterCreatedObjectUndo(fitter, "Add ContentSizeFitter");
            }
            Undo.RecordObject(fitter, "Configure ContentSizeFitter");
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
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

        [MenuItem("Tools/Setup Sliding Panel on Selected UI")]
        public static void SetupSlidingPanelOnSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("[CreateHUDObjects] Vui lòng chọn GameObject panel (ví dụ: SeenActiveCardPanel) trong Hierarchy trước!");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn GameObject panel (ví dụ: SeenActiveCardPanel) trong Hierarchy trước khi chạy setup!", "OK");
                }
                return;
            }

            Undo.IncrementCurrentGroup();
            int groupIndex = Undo.GetCurrentGroup();

            RectTransform rect = selected.GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogError("[CreateHUDObjects] GameObject được chọn phải có RectTransform (UI Element)!");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Lỗi", "GameObject được chọn phải là đối tượng UI (có RectTransform)!", "OK");
                }
                return;
            }

            // 1. Thêm ActiveCardHUDController nếu chưa có
            ActiveCardHUDController hudController = selected.GetComponent<ActiveCardHUDController>();
            if (hudController == null)
            {
                hudController = selected.AddComponent<ActiveCardHUDController>();
                Undo.RegisterCreatedObjectUndo(hudController, "Add ActiveCardHUDController");
            }

            // Thiết lập ScrollRect và RectMask2D trên Panel cha để hỗ trợ cuộn dọc khi tràn chiều cao
            UnityEngine.UI.ScrollRect panelScroll = selected.GetComponent<UnityEngine.UI.ScrollRect>();
            if (panelScroll == null)
            {
                panelScroll = selected.AddComponent<UnityEngine.UI.ScrollRect>();
                Undo.RegisterCreatedObjectUndo(panelScroll, "Add ScrollRect to Panel");
            }
            Undo.RecordObject(panelScroll, "Configure ScrollRect");
            panelScroll.horizontal = false; // Chỉ cuộn dọc
            panelScroll.vertical = true;
            panelScroll.scrollSensitivity = 25f; // Độ nhạy cuộn
            panelScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

            UnityEngine.UI.RectMask2D panelMask = selected.GetComponent<UnityEngine.UI.RectMask2D>();
            if (panelMask == null)
            {
                panelMask = selected.AddComponent<UnityEngine.UI.RectMask2D>();
                Undo.RegisterCreatedObjectUndo(panelMask, "Add RectMask2D to Panel");
            }

            // 2. Tìm Container con
            Transform container = selected.transform.Find("Container");
            if (container == null)
            {
                // Thử tìm bất cứ con nào chứa chữ "Container" hoặc "container"
                foreach (Transform child in selected.transform)
                {
                    if (child.name.ToLower().Contains("container"))
                    {
                        container = child;
                        break;
                    }
                }
            }

            // Nếu tìm thấy container con, gán vào hudController và cấu hình layout
            if (container != null)
            {
                SerializedObject controllerSO = new SerializedObject(hudController);
                controllerSO.FindProperty("_container").objectReferenceValue = container;
                controllerSO.ApplyModifiedProperties();

                // Gán Container làm nội dung cuộn của ScrollRect
                Undo.RecordObject(panelScroll, "Assign ScrollRect Content");
                panelScroll.content = container as RectTransform;

                // Xóa HorizontalLayoutGroup nếu có để tránh xung đột
                UnityEngine.UI.HorizontalLayoutGroup oldHorizontal = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                if (oldHorizontal != null)
                {
                    Undo.DestroyObjectImmediate(oldHorizontal);
                }

                // Cấu hình GridLayoutGroup cho Container con
                UnityEngine.UI.GridLayoutGroup containerGrid = container.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                if (containerGrid == null)
                {
                    containerGrid = container.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                    Undo.RegisterCreatedObjectUndo(containerGrid, "Add GridLayoutGroup to Container");
                }
                Undo.RecordObject(containerGrid, "Configure GridLayoutGroup");
                containerGrid.cellSize = new Vector2(50f, 50f);
                containerGrid.spacing = new Vector2(8f, 8f);
                containerGrid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
                containerGrid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
                containerGrid.childAlignment = TextAnchor.UpperLeft;
                containerGrid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.Flexible;

                // Cấu hình ContentSizeFitter cho Container con để tự phình chiều cao theo lưới
                UnityEngine.UI.ContentSizeFitter containerFitter = container.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                if (containerFitter == null)
                {
                    containerFitter = container.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                    Undo.RegisterCreatedObjectUndo(containerFitter, "Add ContentSizeFitter to Container");
                }
                Undo.RecordObject(containerFitter, "Configure ContentSizeFitter");
                containerFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
                containerFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

                // Cấu hình RectTransform cho Container con để tự co giãn theo ngang và neo ở Top
                RectTransform containerRT = container as RectTransform;
                if (containerRT != null)
                {
                    Undo.RecordObject(containerRT, "Configure Container RectTransform");
                    containerRT.anchorMin = new Vector2(0f, 1f); // Stretch ngang, neo Top dọc
                    containerRT.anchorMax = new Vector2(1f, 1f);
                    containerRT.pivot = new Vector2(0.5f, 1f);   // Pivot ở đỉnh trên
                    // Margins: Trái = 20px, Phải = 40px (tránh đè nút mũi tên trượt), Trên = 20px
                    containerRT.offsetMin = new Vector2(20f, containerRT.offsetMin.y);
                    containerRT.offsetMax = new Vector2(-40f, -20f);
                }

                Debug.Log("[CreateHUDObjects] Đã tự động phát hiện, gán và cấu hình Layout cho Container con: " + container.name);
            }
            else
            {
                Debug.LogWarning("[CreateHUDObjects] Không tìm thấy đối tượng con nào tên là 'Container'. Vui lòng gán thủ công trường Container trên script ActiveCardHUDController.");
            }

            // 3. Gán prefab ActiveCardSlot
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/ActiveCardSlot.prefab");
            if (slotPrefab == null)
            {
                CreateActiveCardSlotPrefab();
                slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/ActiveCardSlot.prefab");
            }
            if (slotPrefab != null)
            {
                SerializedObject controllerSO = new SerializedObject(hudController);
                controllerSO.FindProperty("_iconSlotPrefab").objectReferenceValue = slotPrefab;
                controllerSO.ApplyModifiedProperties();
            }

            // 4. Thêm SlidingPanelUI nếu chưa có
            SlidingPanelUI slidingUI = selected.GetComponent<SlidingPanelUI>();
            if (slidingUI == null)
            {
                slidingUI = selected.AddComponent<SlidingPanelUI>();
                Undo.RegisterCreatedObjectUndo(slidingUI, "Add SlidingPanelUI");
            }

            // 5. Cấu hình vị trí cho SlidingPanelUI
            SerializedObject slidingSO = new SerializedObject(slidingUI);
            slidingSO.FindProperty("_panelRect").objectReferenceValue = rect;
            
            // Lấy vị trí hiện tại làm vị trí Expanded (mở ra)
            Vector2 currentPos = rect.anchoredPosition;
            slidingSO.FindProperty("_expandedPosition").vector2Value = currentPos;

            // Vị trí Collapsed (thu lại): Trượt sang trái để ẩn, ví dụ trừ đi chiều rộng của panel
            // Thường panel RectTransform width là rect.rect.width. Ta trượt sang trái bằng cách trừ đi width + 10px margin
            float width = rect.rect.width;
            Vector2 collapsedPos = new Vector2(currentPos.x - width, currentPos.y);
            slidingSO.FindProperty("_collapsedPosition").vector2Value = collapsedPos;
            slidingSO.ApplyModifiedProperties();

            // 6. Tìm nút toggle và mũi tên tự động
            UnityEngine.UI.Button toggleBtn = selected.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (toggleBtn != null)
            {
                SerializedObject slidingSO2 = new SerializedObject(slidingUI);
                slidingSO2.FindProperty("_toggleButton").objectReferenceValue = toggleBtn;
                
                // Tìm Image/mũi tên bên trong nút
                Transform arrowTransform = toggleBtn.transform.Find("Arrow") ?? toggleBtn.transform.Find("ArrowIcon") ?? toggleBtn.transform.Find("Image");
                if (arrowTransform != null)
                {
                    slidingSO2.FindProperty("_arrowIconRect").objectReferenceValue = arrowTransform.GetComponent<RectTransform>();
                }
                slidingSO2.ApplyModifiedProperties();
                Debug.Log("[CreateHUDObjects] Đã tự động phát hiện và gán nút kích hoạt (Toggle Button): " + toggleBtn.name);
            }
            else
            {
                Debug.LogWarning("[CreateHUDObjects] Không tìm thấy nút Button con nào. Vui lòng gán thủ công trường Toggle Button trên script SlidingPanelUI.");
            }

            Undo.CollapseUndoOperations(groupIndex);

            EditorUtility.SetDirty(selected);
            if (selected.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(selected.scene);
            }

            Debug.Log("[CreateHUDObjects] Đã thiết lập xong Sliding Panel và ActiveCardHUDController trên " + selected.name);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Thành công", "Đã thiết lập tự động xong panel trượt cho " + selected.name + "!\nHãy kiểm tra lại các thông số Collapsed/Expanded Position trong Inspector của script SlidingPanelUI.", "OK");
            }
        }
    }
}
