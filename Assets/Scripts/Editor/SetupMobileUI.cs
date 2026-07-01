#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.Events;
using TMPro;
using CloudTerraceRealm.UI;

namespace CloudTerraceRealm.Editor
{
    /// <summary>
    /// Script Editor tiện ích để tự động tạo cấu trúc GameObject UGUI cho Mobile
    /// và tự động gán các reference/sự kiện Click của các nút bấm.
    /// </summary>
    public static class SetupMobileUI
    {
        [MenuItem("Tools/Setup Mobile UI")]
        public static void Setup()
        {
            // 1. Tìm Canvas chính và HUD
            var canvasGo = GameObject.Find("/Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[SetupMobileUI] Không tìm thấy GameObject '/Canvas' trong scene!");
                return;
            }

            var hudGo = GameObject.Find("/Canvas/HUD");
            if (hudGo == null)
            {
                Debug.LogError("[SetupMobileUI] Không tìm thấy GameObject '/Canvas/HUD' trong scene!");
                return;
            }

            // 2. Kích hoạt lại GameObject UITest (chứa Selection Controllers quan trọng)
            var uiTestGo = GameObject.Find("/UITest");
            if (uiTestGo == null)
            {
                // Tìm kiếm cả gameobject inactive
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    if (go.name == "UITest")
                    {
                        uiTestGo = go;
                        break;
                    }
                }
            }

            if (uiTestGo != null)
            {
                uiTestGo.SetActive(true);
                Debug.Log("[SetupMobileUI] Đã đảm bảo kích hoạt GameObject /UITest chứa Selection logic.");
            }

            // --- A. TẠO IDLE VILLAGER HUD BUTTON ---
            var idleVillagerPath = "/Canvas/HUD/IdleVillagerButton";
            var idleVillagerGo = GameObject.Find(idleVillagerPath);
            if (idleVillagerGo == null)
            {
                idleVillagerGo = new GameObject("IdleVillagerButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(IdleVillagerUIController));
                idleVillagerGo.transform.SetParent(hudGo.transform, false);
                Debug.Log("[SetupMobileUI] Đã tạo mới IdleVillagerButton.");
            }

            var ivRect = idleVillagerGo.GetComponent<RectTransform>();
            ivRect.anchorMin = new Vector2(0, 1); // Top-Left
            ivRect.anchorMax = new Vector2(0, 1);
            ivRect.pivot = new Vector2(0.5f, 0.5f);
            ivRect.anchoredPosition = new Vector2(60, -210); // Đặt ở góc trái bên dưới HUD tài nguyên
            ivRect.sizeDelta = new Vector2(80, 80);

            // Tạo CountText con
            var countTextGo = GameObject.Find(idleVillagerPath + "/CountText");
            if (countTextGo == null)
            {
                countTextGo = new GameObject("CountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                countTextGo.transform.SetParent(idleVillagerGo.transform, false);
            }
            var countRect = countTextGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1, 0); // Bottom-Right
            countRect.anchorMax = new Vector2(1, 0);
            countRect.pivot = new Vector2(0.5f, 0.5f);
            countRect.anchoredPosition = new Vector2(0, 0);
            countRect.sizeDelta = new Vector2(30, 30);

            var countText = countTextGo.GetComponent<TextMeshProUGUI>();
            countText.text = "0";
            countText.fontSize = 18;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.Center;
            countText.color = Color.yellow;

            // Thiết lập Controller
            var ivController = idleVillagerGo.GetComponent<IdleVillagerUIController>();
            var ivButton = idleVillagerGo.GetComponent<Button>();
            
            // Dùng reflection gán field private
            var countField = typeof(IdleVillagerUIController).GetField("_countText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (countField != null)
            {
                countField.SetValue(ivController, countText);
            }

            // Gán Event Click
            while (ivButton.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(ivButton.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(ivButton.onClick, ivController.FocusNextIdleVillager);


            // --- B. TẠO BOX SELECT TOGGLE BUTTON ---
            var boxSelectPath = "/Canvas/HUD/BoxSelectToggleButton";
            var boxSelectGo = GameObject.Find(boxSelectPath);
            if (boxSelectGo == null)
            {
                boxSelectGo = new GameObject("BoxSelectToggleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(BoxSelectToggleUI));
                boxSelectGo.transform.SetParent(hudGo.transform, false);
                Debug.Log("[SetupMobileUI] Đã tạo mới BoxSelectToggleButton.");
            }

            var bsRect = boxSelectGo.GetComponent<RectTransform>();
            bsRect.anchorMin = new Vector2(1, 1); // Top-Right
            bsRect.anchorMax = new Vector2(1, 1);
            bsRect.pivot = new Vector2(0.5f, 0.5f);
            bsRect.anchoredPosition = new Vector2(-220, -60); // Gần bảng đếm thời gian
            bsRect.sizeDelta = new Vector2(140, 50);

            // Tạo StatusText con
            var statusTextGo = GameObject.Find(boxSelectPath + "/StatusText");
            if (statusTextGo == null)
            {
                statusTextGo = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                statusTextGo.transform.SetParent(boxSelectGo.transform, false);
            }
            var statusRect = statusTextGo.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero; // Stretch
            statusRect.anchorMax = Vector2.one;
            statusRect.sizeDelta = Vector2.zero;

            var statusText = statusTextGo.GetComponent<TextMeshProUGUI>();
            statusText.text = "Quét Chọn: TẮT";
            statusText.fontSize = 15;
            statusText.fontStyle = FontStyles.Bold;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;

            // Thiết lập Controller
            var bsController = boxSelectGo.GetComponent<BoxSelectToggleUI>();
            var bsButton = boxSelectGo.GetComponent<Button>();
            var bsImage = boxSelectGo.GetComponent<Image>();

            var textChangeField = typeof(BoxSelectToggleUI).GetField("_statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (textChangeField != null) textChangeField.SetValue(bsController, statusText);

            var imgChangeField = typeof(BoxSelectToggleUI).GetField("_buttonImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (imgChangeField != null) imgChangeField.SetValue(bsController, bsImage);

            // Gán Event Click
            while (bsButton.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(bsButton.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(bsButton.onClick, bsController.ToggleBoxSelectMode);


            // --- C. TẠO HOME SHELTER UI PANEL ---
            var shelterPath = "/Canvas/HomeShelterUIPanel";
            var shelterGo = GameObject.Find(shelterPath);
            if (shelterGo == null)
            {
                shelterGo = new GameObject("HomeShelterUIPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HomeShelterUIController));
                shelterGo.transform.SetParent(canvasGo.transform, false);
                Debug.Log("[SetupMobileUI] Đã tạo mới HomeShelterUIPanel.");
            }

            var shRect = shelterGo.GetComponent<RectTransform>();
            shRect.anchorMin = new Vector2(0f, 0f); // Bottom-Left
            shRect.anchorMax = new Vector2(0f, 0f);
            shRect.pivot = new Vector2(0f, 0f);
            shRect.anchoredPosition = new Vector2(200, 180);
            shRect.sizeDelta = new Vector2(350, 200);

            // Tạo TitleText con
            var shTitleGo = GameObject.Find(shelterPath + "/TitleText");
            if (shTitleGo == null)
            {
                shTitleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                shTitleGo.transform.SetParent(shelterGo.transform, false);
            }
            var shTitleRect = shTitleGo.GetComponent<RectTransform>();
            shTitleRect.anchorMin = new Vector2(0.5f, 1f); // Top-Center
            shTitleRect.anchorMax = new Vector2(0.5f, 1f);
            shTitleRect.anchoredPosition = new Vector2(0, -25);
            shTitleRect.sizeDelta = new Vector2(330, 40);
            var shTitle = shTitleGo.GetComponent<TextMeshProUGUI>();
            shTitle.text = "Nhà Cư Dân";
            shTitle.fontSize = 20;
            shTitle.fontStyle = FontStyles.Bold;
            shTitle.alignment = TextAlignmentOptions.Center;

            // Tạo InfoText con
            var shInfoGo = GameObject.Find(shelterPath + "/InfoText");
            if (shInfoGo == null)
            {
                shInfoGo = new GameObject("InfoText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                shInfoGo.transform.SetParent(shelterGo.transform, false);
            }
            var shInfoRect = shInfoGo.GetComponent<RectTransform>();
            shInfoRect.anchorMin = new Vector2(0.5f, 0.5f); // Center
            shInfoRect.anchorMax = new Vector2(0.5f, 0.5f);
            shInfoRect.anchoredPosition = new Vector2(0, -10);
            shInfoRect.sizeDelta = new Vector2(330, 100);
            var shInfo = shInfoGo.GetComponent<TextMeshProUGUI>();
            shInfo.text = "Cư Dân Trú Ngụ: 0/5";
            shInfo.fontSize = 16;
            shInfo.alignment = TextAlignmentOptions.Center;

            // Tạo CloseButton con
            var shCloseGo = GameObject.Find(shelterPath + "/CloseButton");
            if (shCloseGo == null)
            {
                shCloseGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                shCloseGo.transform.SetParent(shelterGo.transform, false);
            }
            var shCloseRect = shCloseGo.GetComponent<RectTransform>();
            shCloseRect.anchorMin = new Vector2(1, 1); // Top-Right
            shCloseRect.anchorMax = new Vector2(1, 1);
            shCloseRect.anchoredPosition = new Vector2(-25, -25);
            shCloseRect.sizeDelta = new Vector2(30, 30);

            // Thiết lập Controller
            var shController = shelterGo.GetComponent<HomeShelterUIController>();
            var shCloseBtn = shCloseGo.GetComponent<Button>();

            var shParentField = typeof(HomeShelterUIController).GetField("_panelParent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shParentField != null) shParentField.SetValue(shController, shelterGo);

            var shTitleField = typeof(HomeShelterUIController).GetField("_titleText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shTitleField != null) shTitleField.SetValue(shController, shTitle);

            var shInfoField = typeof(HomeShelterUIController).GetField("_infoText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shInfoField != null) shInfoField.SetValue(shController, shInfo);

            // Gán Event Click Close
            while (shCloseBtn.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(shCloseBtn.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(shCloseBtn.onClick, shController.Deselect);


            // --- D. TẠO STORAGE UI PANEL ---
            var storagePath = "/Canvas/StorageUIPanel";
            var storageGo = GameObject.Find(storagePath);
            if (storageGo == null)
            {
                storageGo = new GameObject("StorageUIPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(StorageUIController));
                storageGo.transform.SetParent(canvasGo.transform, false);
                Debug.Log("[SetupMobileUI] Đã tạo mới StorageUIPanel.");
            }

            var stRect = storageGo.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 0f); // Bottom-Left
            stRect.anchorMax = new Vector2(0f, 0f);
            stRect.pivot = new Vector2(0f, 0f);
            stRect.anchoredPosition = new Vector2(200, 180); // Đè cùng vị trí
            stRect.sizeDelta = new Vector2(350, 240);

            // Tạo TitleText con
            var stTitleGo = GameObject.Find(storagePath + "/TitleText");
            if (stTitleGo == null)
            {
                stTitleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                stTitleGo.transform.SetParent(storageGo.transform, false);
            }
            var stTitleRect = stTitleGo.GetComponent<RectTransform>();
            stTitleRect.anchorMin = new Vector2(0.5f, 1f); // Top-Center
            stTitleRect.anchorMax = new Vector2(0.5f, 1f);
            stTitleRect.anchoredPosition = new Vector2(0, -25);
            stTitleRect.sizeDelta = new Vector2(330, 40);
            var stTitle = stTitleGo.GetComponent<TextMeshProUGUI>();
            stTitle.text = "Kho Chứa Tài Nguyên";
            stTitle.fontSize = 20;
            stTitle.fontStyle = FontStyles.Bold;
            stTitle.alignment = TextAlignmentOptions.Center;

            // Tạo InfoText con
            var stInfoGo = GameObject.Find(storagePath + "/InfoText");
            if (stInfoGo == null)
            {
                stInfoGo = new GameObject("InfoText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                stInfoGo.transform.SetParent(storageGo.transform, false);
            }
            var stInfoRect = stInfoGo.GetComponent<RectTransform>();
            stInfoRect.anchorMin = new Vector2(0.5f, 0.5f); // Center
            stInfoRect.anchorMax = new Vector2(0.5f, 0.5f);
            stInfoRect.anchoredPosition = new Vector2(0, -10);
            stInfoRect.sizeDelta = new Vector2(330, 140);
            var stInfo = stInfoGo.GetComponent<TextMeshProUGUI>();
            stInfo.text = "Gỗ: 0\nLương thực: 0\nĐá: 0\nVàng: 0";
            stInfo.fontSize = 16;
            stInfo.alignment = TextAlignmentOptions.Center;

            // Tạo CloseButton con
            var stCloseGo = GameObject.Find(storagePath + "/CloseButton");
            if (stCloseGo == null)
            {
                stCloseGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                stCloseGo.transform.SetParent(storageGo.transform, false);
            }
            var stCloseRect = stCloseGo.GetComponent<RectTransform>();
            stCloseRect.anchorMin = new Vector2(1, 1); // Top-Right
            stCloseRect.anchorMax = new Vector2(1, 1);
            stCloseRect.anchoredPosition = new Vector2(-25, -25);
            stCloseRect.sizeDelta = new Vector2(30, 30);

            // Thiết lập Controller
            var stController = storageGo.GetComponent<StorageUIController>();
            var stCloseBtn = stCloseGo.GetComponent<Button>();

            var stParentField = typeof(StorageUIController).GetField("_panelParent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (stParentField != null) stParentField.SetValue(stController, storageGo);

            var stTitleField = typeof(StorageUIController).GetField("_titleText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (stTitleField != null) stTitleField.SetValue(stController, stTitle);

            var stInfoField = typeof(StorageUIController).GetField("_infoText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (stInfoField != null) stInfoField.SetValue(stController, stInfo);

            // Gán Event Click Close
            while (stCloseBtn.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(stCloseBtn.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(stCloseBtn.onClick, stController.Deselect);

            // Lưu scene
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SetupMobileUI] Tạo và cấu hình các UI Mobile thành công và đã lưu Scene.");
        }
    }
}
#endif
