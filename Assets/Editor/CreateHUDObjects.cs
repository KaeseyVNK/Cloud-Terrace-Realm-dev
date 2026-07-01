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
            HUDManager hud = Object.FindAnyObjectByType<HUDManager>();
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

        [MenuItem("Tools/Generate Action Command Panel")]
        public static void GenerateActionCommandPanel()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[CreateHUDObjects] Không tìm thấy Canvas trong scene!");
                return;
            }

            Undo.IncrementCurrentGroup();
            int groupIndex = Undo.GetCurrentGroup();

            // Tìm hoặc xóa panel cũ
            Transform oldPanel = canvas.transform.Find("ActionCommandPanel");
            if (oldPanel != null)
            {
                Undo.DestroyObjectImmediate(oldPanel.gameObject);
            }

            // Tạo Panel chính
            GameObject panelObj = new GameObject("ActionCommandPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(panelObj, "Create ActionCommandPanel");

            RectTransform panelRT = panelObj.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 0f);
            panelRT.anchorMax = new Vector2(0f, 0f);
            panelRT.pivot = new Vector2(0f, 0f);
            panelRT.anchoredPosition = new Vector2(15f, 15f);
            panelRT.sizeDelta = new Vector2(180f, 130f);

            // Nền tối
            UnityEngine.UI.Image bgImage = panelObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            bgImage.type = UnityEngine.UI.Image.Type.Sliced;
            bgImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            // Viền Outline màu vàng
            UnityEngine.UI.Outline outline = panelObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Gắn component ActionCommandPanelUI
            ActionCommandPanelUI uiComponent = panelObj.AddComponent<ActionCommandPanelUI>();

            // Tạo Grid Container con
            GameObject gridObj = new GameObject("GridContainer", typeof(RectTransform));
            gridObj.transform.SetParent(panelObj.transform, false);
            RectTransform gridRT = gridObj.GetComponent<RectTransform>();
            gridRT.anchorMin = Vector2.zero;
            gridRT.anchorMax = Vector2.one;
            gridRT.offsetMin = new Vector2(10f, 10f);
            gridRT.offsetMax = new Vector2(-10f, -10f);

            UnityEngine.UI.GridLayoutGroup gridLayout = gridObj.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(50f, 50f);
            gridLayout.spacing = new Vector2(5f, 5f);
            gridLayout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;

            // Danh sách các nút lệnh cần tạo
            string[] buttonNames = new string[] { "BuildButton", "GarrisonButton", "RepairButton", "ReturnCargoButton", "StopButton", "EmptyButton" };
            string[] buttonLabels = new string[] { "🔨 B", "🚪 G", "🔧 R", "📦 C", "🛑 S", "" };

            GameObject[] spawnedButtons = new GameObject[6];

            for (int i = 0; i < 6; i++)
            {
                GameObject btnObj = new GameObject(buttonNames[i], typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                btnObj.transform.SetParent(gridObj.transform, false);

                UnityEngine.UI.Image btnImg = btnObj.GetComponent<UnityEngine.UI.Image>();
                btnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
                btnImg.type = UnityEngine.UI.Image.Type.Sliced;
                btnImg.color = new Color(0.15f, 0.18f, 0.24f, 0.95f);

                // Add text label
                if (!string.IsNullOrEmpty(buttonLabels[i]))
                {
                    GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    txtObj.transform.SetParent(btnObj.transform, false);
                    RectTransform txtRT = txtObj.GetComponent<RectTransform>();
                    txtRT.anchorMin = Vector2.zero;
                    txtRT.anchorMax = Vector2.one;
                    txtRT.offsetMin = Vector2.zero;
                    txtRT.offsetMax = Vector2.zero;

                    TextMeshProUGUI tmpText = txtObj.GetComponent<TextMeshProUGUI>();
                    tmpText.text = buttonLabels[i];
                    tmpText.fontSize = 12f;
                    tmpText.fontStyle = FontStyles.Bold;
                    tmpText.alignment = TextAlignmentOptions.Center;
                    tmpText.color = Color.white;
                }

                spawnedButtons[i] = btnObj;
            }

            // Gán các reference vào script bằng SerializedObject
            SerializedObject so = new SerializedObject(uiComponent);
            so.FindProperty("_panelRoot").objectReferenceValue = panelObj;
            so.FindProperty("_buildButton").objectReferenceValue = spawnedButtons[0].GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("_garrisonButton").objectReferenceValue = spawnedButtons[1].GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("_repairButton").objectReferenceValue = spawnedButtons[2].GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("_returnCargoButton").objectReferenceValue = spawnedButtons[3].GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("_stopButton").objectReferenceValue = spawnedButtons[4].GetComponent<UnityEngine.UI.Button>();
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(groupIndex);

            EditorUtility.SetDirty(panelObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panelObj.scene);

            Debug.Log("[CreateHUDObjects] Đã tạo thành công ActionCommandPanel ở góc trái dưới Canvas!");
        }

        [MenuItem("Tools/Generate Pause UI Panel")]
        public static void GeneratePauseUIPanel()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[CreateHUDObjects] Không tìm thấy Canvas trong scene!");
                return;
            }

            Undo.IncrementCurrentGroup();
            int groupIndex = Undo.GetCurrentGroup();

            // Tìm hoặc xóa panel cũ
            Transform oldPanel = canvas.transform.Find("PausePanel");
            if (oldPanel != null)
            {
                Undo.DestroyObjectImmediate(oldPanel.gameObject);
            }

            // Thiết lập Default Controls Resources
            UnityEngine.UI.DefaultControls.Resources uiResources = new UnityEngine.UI.DefaultControls.Resources();
            uiResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            uiResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            uiResources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            uiResources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            uiResources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            uiResources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");

            // 1. Tạo PausePanel chính (Full Screen Background)
            GameObject pausePanelObj = new GameObject("PausePanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup), typeof(PauseUIController));
            pausePanelObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(pausePanelObj, "Create PausePanel");

            RectTransform panelRT = pausePanelObj.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = Vector2.zero; // Full stretch

            UnityEngine.UI.Image bgImg = pausePanelObj.GetComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.7f); // Nền mờ tối

            PauseUIController pauseController = pausePanelObj.GetComponent<PauseUIController>();

            // 2. Tạo CenterBox của Pause Menu
            GameObject pauseCenterBoxObj = new GameObject("CenterBox", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            pauseCenterBoxObj.transform.SetParent(pausePanelObj.transform, false);
            RectTransform pauseBoxRT = pauseCenterBoxObj.GetComponent<RectTransform>();
            pauseBoxRT.anchorMin = new Vector2(0.5f, 0.5f);
            pauseBoxRT.anchorMax = new Vector2(0.5f, 0.5f);
            pauseBoxRT.pivot = new Vector2(0.5f, 0.5f);
            pauseBoxRT.sizeDelta = new Vector2(300f, 360f); // Tăng chiều cao để chứa nút Options

            UnityEngine.UI.Image boxImg = pauseCenterBoxObj.GetComponent<UnityEngine.UI.Image>();
            boxImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            boxImg.type = UnityEngine.UI.Image.Type.Sliced;
            boxImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

            UnityEngine.UI.Outline outline = pauseCenterBoxObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Bố cục dọc cho CenterBox
            UnityEngine.UI.VerticalLayoutGroup layout = pauseCenterBoxObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 25, 25);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Tiêu đề TẠM DỪNG
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(pauseCenterBoxObj.transform, false);
            TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.text = "TẠM DỪNG";
            titleText.fontSize = 26f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.95f, 0.75f, 0.2f);
            titleText.raycastTarget = false;

            // Spacer
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(pauseCenterBoxObj.transform, false);
            spacer.GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 10f);

            // Hàm tạo Button
            System.Func<Transform, string, string, UnityEngine.UI.Button> createBtn = (parent, btnName, btnLabel) =>
            {
                GameObject btnObj = new GameObject(btnName, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                btnObj.transform.SetParent(parent, false);
                
                RectTransform btnRT = btnObj.GetComponent<RectTransform>();
                btnRT.sizeDelta = new Vector2(220f, 42f);

                UnityEngine.UI.Image btnImg = btnObj.GetComponent<UnityEngine.UI.Image>();
                btnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
                btnImg.type = UnityEngine.UI.Image.Type.Sliced;
                btnImg.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);

                GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtObj.transform.SetParent(btnObj.transform, false);
                RectTransform txtRT = txtObj.GetComponent<RectTransform>();
                txtRT.anchorMin = Vector2.zero;
                txtRT.anchorMax = Vector2.one;
                txtRT.offsetMin = Vector2.zero;
                txtRT.offsetMax = Vector2.zero;

                TextMeshProUGUI tmpText = txtObj.GetComponent<TextMeshProUGUI>();
                tmpText.text = btnLabel;
                tmpText.fontSize = 14f;
                tmpText.fontStyle = FontStyles.Bold;
                tmpText.alignment = TextAlignmentOptions.Center;
                tmpText.color = Color.white;
                tmpText.raycastTarget = false;

                return btnObj.GetComponent<UnityEngine.UI.Button>();
            };

            UnityEngine.UI.Button resumeButton = createBtn(pauseCenterBoxObj.transform, "ResumeButton", "TIẾP TỤC");
            UnityEngine.UI.Button optionsButton = createBtn(pauseCenterBoxObj.transform, "OptionsButton", "CÀI ĐẶT");
            UnityEngine.UI.Button restartButton = createBtn(pauseCenterBoxObj.transform, "RestartButton", "CHƠI LẠI");
            UnityEngine.UI.Button quitButton = createBtn(pauseCenterBoxObj.transform, "QuitButton", "THOÁT GAME");

            // 3. Tạo OptionsPanel chính
            GameObject optionsPanelObj = new GameObject("OptionsPanel", typeof(RectTransform), typeof(OptionsUIController));
            optionsPanelObj.transform.SetParent(pausePanelObj.transform, false);
            Undo.RegisterCreatedObjectUndo(optionsPanelObj, "Create OptionsPanel");

            RectTransform optionsPanelRT = optionsPanelObj.GetComponent<RectTransform>();
            optionsPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
            optionsPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
            optionsPanelRT.pivot = new Vector2(0.5f, 0.5f);
            optionsPanelRT.sizeDelta = new Vector2(420f, 480f); // Kích thước gọn gàng cho Settings

            UnityEngine.UI.Image optionsBgImg = optionsPanelObj.AddComponent<UnityEngine.UI.Image>();
            optionsBgImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            optionsBgImg.type = UnityEngine.UI.Image.Type.Sliced;
            optionsBgImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

            UnityEngine.UI.Outline optionsOutline = optionsPanelObj.AddComponent<UnityEngine.UI.Outline>();
            optionsOutline.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.75f);
            optionsOutline.effectDistance = new Vector2(1.5f, -1.5f);

            OptionsUIController optionsController = optionsPanelObj.GetComponent<OptionsUIController>();

            // Bố cục dọc cho Options
            UnityEngine.UI.VerticalLayoutGroup optionsLayout = optionsPanelObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            optionsLayout.padding = new RectOffset(25, 25, 25, 25);
            optionsLayout.spacing = 15f;
            optionsLayout.childAlignment = TextAnchor.UpperCenter;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;

            // Tiêu đề CÀI ĐẶT
            GameObject optionsTitleObj = new GameObject("OptionsTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            optionsTitleObj.transform.SetParent(optionsPanelObj.transform, false);
            TextMeshProUGUI optionsTitleText = optionsTitleObj.GetComponent<TextMeshProUGUI>();
            optionsTitleText.text = "CÀI ĐẶT";
            optionsTitleText.fontSize = 24f;
            optionsTitleText.fontStyle = FontStyles.Bold;
            optionsTitleText.alignment = TextAlignmentOptions.Center;
            optionsTitleText.color = new Color(0.95f, 0.75f, 0.2f);
            optionsTitleText.raycastTarget = false;

            // Hàm tạo Row chứa Label và Control
            System.Func<string, string, GameObject> createRow = (rowName, rowLabel) =>
            {
                GameObject rowObj = new GameObject(rowName, typeof(RectTransform));
                rowObj.transform.SetParent(optionsPanelObj.transform, false);
                RectTransform rowRT = rowObj.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(370f, 32f);

                UnityEngine.UI.HorizontalLayoutGroup rowLg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                rowLg.padding = new RectOffset(5, 5, 2, 2);
                rowLg.spacing = 15f;
                rowLg.childAlignment = TextAnchor.MiddleLeft;
                rowLg.childForceExpandWidth = false;
                rowLg.childForceExpandHeight = false;
                rowLg.childControlWidth = false;
                rowLg.childControlHeight = false;

                GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObj.transform.SetParent(rowObj.transform, false);
                RectTransform labelRT = labelObj.GetComponent<RectTransform>();
                labelRT.sizeDelta = new Vector2(140f, 25f);

                TextMeshProUGUI tmpText = labelObj.GetComponent<TextMeshProUGUI>();
                tmpText.text = rowLabel;
                tmpText.fontSize = 13f;
                tmpText.fontStyle = FontStyles.Bold;
                tmpText.alignment = TextAlignmentOptions.Left;
                tmpText.color = Color.white;
                tmpText.raycastTarget = false;

                return rowObj;
            };

            // 1. Master Volume Row
            GameObject masterVolRow = createRow("MasterVolumeRow", "Âm Lượng Tổng");
            GameObject masterSliderObj = UnityEngine.UI.DefaultControls.CreateSlider(uiResources);
            masterSliderObj.transform.SetParent(masterVolRow.transform, false);
            masterSliderObj.name = "MasterVolumeSlider";
            masterSliderObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 20f);
            UnityEngine.UI.Slider masterSlider = masterSliderObj.GetComponent<UnityEngine.UI.Slider>();

            // 2. Music Volume Row
            GameObject musicVolRow = createRow("MusicVolumeRow", "Âm Nhạc (BGM)");
            GameObject musicSliderObj = UnityEngine.UI.DefaultControls.CreateSlider(uiResources);
            musicSliderObj.transform.SetParent(musicVolRow.transform, false);
            musicSliderObj.name = "MusicVolumeSlider";
            musicSliderObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 20f);
            UnityEngine.UI.Slider musicSlider = musicSliderObj.GetComponent<UnityEngine.UI.Slider>();

            // 3. SFX Volume Row
            GameObject sfxVolRow = createRow("SFXVolumeRow", "Hiệu Ứng (SFX)");
            GameObject sfxSliderObj = UnityEngine.UI.DefaultControls.CreateSlider(uiResources);
            sfxSliderObj.transform.SetParent(sfxVolRow.transform, false);
            sfxSliderObj.name = "SFXVolumeSlider";
            sfxSliderObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 20f);
            UnityEngine.UI.Slider sfxSlider = sfxSliderObj.GetComponent<UnityEngine.UI.Slider>();

            // 4. Fullscreen Row
            GameObject fullscreenRow = createRow("FullscreenRow", "Toàn Màn Hình");
            GameObject fullscreenToggleObj = UnityEngine.UI.DefaultControls.CreateToggle(uiResources);
            fullscreenToggleObj.transform.SetParent(fullscreenRow.transform, false);
            fullscreenToggleObj.name = "FullscreenToggle";
            fullscreenToggleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 20f);
            // Hide the default Label of default Toggle
            Transform toggleLabel = fullscreenToggleObj.transform.Find("Label");
            if (toggleLabel != null) toggleLabel.gameObject.SetActive(false);
            UnityEngine.UI.Toggle fullscreenToggle = fullscreenToggleObj.GetComponent<UnityEngine.UI.Toggle>();

            // 5. Graphic Quality Row
            GameObject qualityRow = createRow("QualityRow", "Chất Lượng Đồ Họa");
            GameObject qualityDropdownObj = UnityEngine.UI.DefaultControls.CreateDropdown(uiResources);
            qualityDropdownObj.transform.SetParent(qualityRow.transform, false);
            qualityDropdownObj.name = "QualityDropdown";
            qualityDropdownObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 26f);
            UnityEngine.UI.Dropdown qualityDropdown = qualityDropdownObj.GetComponent<UnityEngine.UI.Dropdown>();

            // 6. Resolution Row
            GameObject resolutionRow = createRow("ResolutionRow", "Độ Phân Giải");
            GameObject resolutionDropdownObj = UnityEngine.UI.DefaultControls.CreateDropdown(uiResources);
            resolutionDropdownObj.transform.SetParent(resolutionRow.transform, false);
            resolutionDropdownObj.name = "ResolutionDropdown";
            resolutionDropdownObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 26f);
            UnityEngine.UI.Dropdown resolutionDropdown = resolutionDropdownObj.GetComponent<UnityEngine.UI.Dropdown>();

            // Spacer dưới
            GameObject optSpacer = new GameObject("OptSpacer", typeof(RectTransform));
            optSpacer.transform.SetParent(optionsPanelObj.transform, false);
            optSpacer.GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 5f);

            // 7. Back Button
            UnityEngine.UI.Button backButton = createBtn(optionsPanelObj.transform, "BackButton", "QUAY LẠI");
            backButton.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 38f);

            // 4. Gán references vào component PauseUIController
            SerializedObject pauseSO = new SerializedObject(pauseController);
            pauseSO.FindProperty("_panelRoot").objectReferenceValue = pausePanelObj;
            pauseSO.FindProperty("_resumeButton").objectReferenceValue = resumeButton;
            pauseSO.FindProperty("_optionsButton").objectReferenceValue = optionsButton;
            pauseSO.FindProperty("_restartButton").objectReferenceValue = restartButton;
            pauseSO.FindProperty("_quitButton").objectReferenceValue = quitButton;
            pauseSO.ApplyModifiedProperties();

            // 5. Gán references vào component OptionsUIController
            SerializedObject optSO = new SerializedObject(optionsController);
            optSO.FindProperty("_optionsRoot").objectReferenceValue = optionsPanelObj;
            optSO.FindProperty("_pauseMenuCenterBox").objectReferenceValue = pauseCenterBoxObj;
            optSO.FindProperty("_masterVolumeSlider").objectReferenceValue = masterSlider;
            optSO.FindProperty("_musicVolumeSlider").objectReferenceValue = musicSlider;
            optSO.FindProperty("_sfxVolumeSlider").objectReferenceValue = sfxSlider;
            optSO.FindProperty("_fullscreenToggle").objectReferenceValue = fullscreenToggle;
            optSO.FindProperty("_qualityDropdown").objectReferenceValue = qualityDropdown;
            optSO.FindProperty("_resolutionDropdown").objectReferenceValue = resolutionDropdown;
            optSO.FindProperty("_backButton").objectReferenceValue = backButton;
            optSO.ApplyModifiedProperties();

            // Đưa panel chính về trạng thái ẩn ban đầu
            CanvasGroup cg = pausePanelObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            pausePanelObj.transform.localScale = new Vector3(0.92f, 0.92f, 1f);

            optionsPanelObj.SetActive(false); // Ẩn options lúc đầu

            Undo.CollapseUndoOperations(groupIndex);

            EditorUtility.SetDirty(pausePanelObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pausePanelObj.scene);

            Debug.Log("[CreateHUDObjects] Đã tạo thành công PausePanel và OptionsPanel dưới Canvas!");
        }
    }
}
