using UnityEngine;
using TMPro;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [Header("Tài nguyên")]
    [UnityEngine.Serialization.FormerlySerializedAs("woodText")]
    [SerializeField] private TextMeshProUGUI _woodText;
    
    [UnityEngine.Serialization.FormerlySerializedAs("stoneText")]
    [SerializeField] private TextMeshProUGUI _stoneText;
    
    [UnityEngine.Serialization.FormerlySerializedAs("foodText")]
    [SerializeField] private TextMeshProUGUI _foodText;
    
    [UnityEngine.Serialization.FormerlySerializedAs("goldText")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Thời gian")]
    [UnityEngine.Serialization.FormerlySerializedAs("timeText")]
    [SerializeField] private TextMeshProUGUI _timeText;

    [Header("Population")]
    [SerializeField] private TextMeshProUGUI _populationText;
    [SerializeField] private float _populationRefreshInterval = 0.25f;

    private float _nextPopulationRefreshTime;

    [Header("Đói Lương Thực Warning")]
    [SerializeField] private TextMeshProUGUI _foodWarningText;

    [Header("Ancient Relics")]
    [SerializeField] private TextMeshProUGUI _relicText;

    [Header("Blood Moon UI")]
    [SerializeField] private TextMeshProUGUI _bloodMoonBannerText;

    [Header("Instructions UI")]
    [SerializeField] private GameObject _instructionsPanel;

    void Start()
    {
        EnsurePopulationText();
        EnsureFoodWarningText();
        EnsureRelicText();
        EnsureBloodMoonBannerText();
        EnsureInstructionsUI();

        // Đăng ký lắng nghe sự kiện tài nguyên thay đổi từ ResourceManager
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            
            // Cập nhật UI lần đầu tiên
            UpdateResourceUI(ResourceType.Wood, ResourceManager.Instance.GetResourceAmount(ResourceType.Wood));
            UpdateResourceUI(ResourceType.Stone, ResourceManager.Instance.GetResourceAmount(ResourceType.Stone));
            UpdateResourceUI(ResourceType.Food, ResourceManager.Instance.GetResourceAmount(ResourceType.Food));
            UpdateResourceUI(ResourceType.Gold, ResourceManager.Instance.GetResourceAmount(ResourceType.Gold));
            UpdateResourceUI(ResourceType.AncientRelic, ResourceManager.Instance.GetResourceAmount(ResourceType.AncientRelic));
        }

        UpdatePopulationUI();
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
        }
    }

    private void UpdateResourceUI(ResourceType type, int newAmount)
    {
        int gatherers = VillagerController.GetGathererCount(type);

        switch (type)
        {
            case ResourceType.Wood:
                if (_woodText != null) _woodText.text = $"Go: {newAmount}/{gatherers}";
                break;
            case ResourceType.Stone:
                if (_stoneText != null) _stoneText.text = $"Da: {newAmount}/{gatherers}";
                break;
            case ResourceType.Food:
                if (_foodText != null) _foodText.text = $"Luong: {newAmount}/{gatherers}";
                break;
            case ResourceType.Gold:
                if (_goldText != null) _goldText.text = $"Vang: {newAmount}/{gatherers}";
                break;
            case ResourceType.AncientRelic:
                if (_relicText != null) _relicText.text = "Co vat: " + newAmount;
                break;
        }

        if (ResourceManager.Instance != null)
        {
            UpdateCapacityUI(ResourceManager.Instance.GetTotalPrimaryResources(), ResourceManager.Instance.GetMaxResourceCapacity());
        }
    }

    void Update()
    {
        if (TimeManager.Instance != null && _timeText != null)
        {
            float ratio = TimeManager.Instance.GetTimeRatio();
            float clockHoursRaw = (ratio * 24f + 6f) % 24f; // Bắt đầu chu kỳ ngày ở 6:00 AM
            int hours = Mathf.FloorToInt(clockHoursRaw);
            int minutes = Mathf.FloorToInt((clockHoursRaw - hours) * 60f);
            
            string stateText = TimeManager.Instance.IsNight ? "Ban Dem" : "Ban Ngay";
            _timeText.text = $"Ngay {TimeManager.Instance.dayCount} | {hours:00}:{minutes:00} ({stateText})";
        }

        if (Time.unscaledTime >= _nextPopulationRefreshTime)
        {
            _nextPopulationRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _populationRefreshInterval);
            UpdatePopulationUI();

            if (ResourceManager.Instance != null)
            {
                UpdateResourceUI(ResourceType.Wood, ResourceManager.Instance.GetResourceAmount(ResourceType.Wood));
                UpdateResourceUI(ResourceType.Stone, ResourceManager.Instance.GetResourceAmount(ResourceType.Stone));
                UpdateResourceUI(ResourceType.Food, ResourceManager.Instance.GetResourceAmount(ResourceType.Food));
                UpdateResourceUI(ResourceType.Gold, ResourceManager.Instance.GetResourceAmount(ResourceType.Gold));
            }
        }

        // Cập nhật trạng thái thiếu lương thực
        if (HungerSystem.Instance != null)
        {
            bool isShortage = HungerSystem.Instance.IsFoodShortage;
            if (_foodWarningText != null)
            {
                _foodWarningText.gameObject.SetActive(isShortage);
            }
            if (_foodText != null)
            {
                _foodText.color = isShortage ? Color.red : Color.white;
            }
        }
    }

    private void EnsurePopulationText()
    {
        if (_populationText != null)
        {
            return;
        }

        Transform existing = transform.Find("PopulationText");
        if (existing != null)
        {
            _populationText = existing.GetComponent<TextMeshProUGUI>();
            if (_populationText != null)
            {
                return;
            }
        }

        GameObject populationObject = new GameObject("PopulationText", typeof(RectTransform), typeof(TextMeshProUGUI));
        populationObject.transform.SetParent(transform, false);
        populationObject.layer = gameObject.layer;

        RectTransform rectTransform = populationObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(300f, 472f);
        rectTransform.sizeDelta = new Vector2(260f, 50f);

        _populationText = populationObject.GetComponent<TextMeshProUGUI>();
        _populationText.fontSize = 32f;
        _populationText.alignment = TextAlignmentOptions.Center;
        _populationText.color = Color.white;
        _populationText.raycastTarget = false;
    }

    private void UpdatePopulationUI()
    {
        if (_populationText == null)
        {
            return;
        }

        int current = PopulationManager.CurrentVillagers;
        int reserved = PopulationManager.ReservedVillagers;
        int max = PopulationManager.MaxVillagers;
        _populationText.text = reserved > 0
            ? $"Dan: {current}+{reserved}/{max}"
            : $"Dan: {current}/{max}";
    }

    private void EnsureFoodWarningText()
    {
        if (_foodWarningText != null)
        {
            return;
        }

        Transform existing = transform.Find("FoodWarningText");
        if (existing != null)
        {
            _foodWarningText = existing.GetComponent<TextMeshProUGUI>();
            if (_foodWarningText != null)
            {
                return;
            }
        }

        GameObject warningObject = new GameObject("FoodWarningText", typeof(RectTransform), typeof(TextMeshProUGUI));
        warningObject.transform.SetParent(transform, false);
        warningObject.layer = gameObject.layer;

        RectTransform rectTransform = warningObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(300f, 412f); // Dưới PopulationText
        rectTransform.sizeDelta = new Vector2(500f, 50f);

        _foodWarningText = warningObject.GetComponent<TextMeshProUGUI>();
        _foodWarningText.fontSize = 24f;
        _foodWarningText.alignment = TextAlignmentOptions.Center;
        _foodWarningText.color = Color.red;
        _foodWarningText.text = "⚠️ THIẾU LƯƠNG THỰC! CƯ DÂN BỊ ĐÓI!";
        _foodWarningText.raycastTarget = false;
        _foodWarningText.gameObject.SetActive(false); // Ẩn mặc định
    }

    private void EnsureRelicText()
    {
        if (_relicText != null)
        {
            return;
        }

        Transform existing = transform.Find("RelicText");
        if (existing != null)
        {
            _relicText = existing.GetComponent<TextMeshProUGUI>();
            if (_relicText != null)
            {
                return;
            }
        }

        GameObject relicObject = new GameObject("RelicText", typeof(RectTransform), typeof(TextMeshProUGUI));
        relicObject.transform.SetParent(transform, false);
        relicObject.layer = gameObject.layer;

        RectTransform rectTransform = relicObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(300f, 442f); // Ở giữa PopulationText và FoodWarningText
        rectTransform.sizeDelta = new Vector2(260f, 50f);

        _relicText = relicObject.GetComponent<TextMeshProUGUI>();
        _relicText.fontSize = 24f;
        _relicText.alignment = TextAlignmentOptions.Center;
        _relicText.color = new Color(1f, 0.84f, 0f); // Màu vàng Gold nổi bật
        _relicText.raycastTarget = false;
        _relicText.text = "Co vat: 0";
    }

    private void EnsureBloodMoonBannerText()
    {
        if (_bloodMoonBannerText != null)
        {
            return;
        }

        Transform existing = transform.Find("BloodMoonBannerText");
        if (existing != null)
        {
            _bloodMoonBannerText = existing.GetComponent<TextMeshProUGUI>();
            if (_bloodMoonBannerText != null)
            {
                return;
            }
        }

        GameObject bannerObj = new GameObject("BloodMoonBannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bannerObj.transform.SetParent(transform, false);
        bannerObj.layer = gameObject.layer;

        RectTransform rectTransform = bannerObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, 120f); // Giữa trên màn hình
        rectTransform.sizeDelta = new Vector2(800f, 150f);

        _bloodMoonBannerText = bannerObj.GetComponent<TextMeshProUGUI>();
        _bloodMoonBannerText.fontSize = 24f;
        _bloodMoonBannerText.alignment = TextAlignmentOptions.Center;
        _bloodMoonBannerText.color = Color.white;
        _bloodMoonBannerText.raycastTarget = false;
        _bloodMoonBannerText.gameObject.SetActive(false);
    }

    public void ShowBloodMoonAlert(string title, string subtitle, float duration)
    {
        StartCoroutine(BloodMoonAlertRoutine(title, subtitle, duration));
    }

    private IEnumerator BloodMoonAlertRoutine(string title, string subtitle, float duration)
    {
        EnsureBloodMoonBannerText();
        if (_bloodMoonBannerText != null)
        {
            _bloodMoonBannerText.text = $"<color=red><size=42><b>{title}</b></size></color>\n<size=22>{subtitle}</size>";
            _bloodMoonBannerText.alpha = 1f;
            _bloodMoonBannerText.gameObject.SetActive(true);
            
            // Hiệu ứng phóng to nhẹ (Punch scale)
            RectTransform rect = _bloodMoonBannerText.GetComponent<RectTransform>();
            rect.localScale = Vector3.one * 0.8f;
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                rect.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, elapsed / 0.2f);
                yield return null;
            }
            rect.localScale = Vector3.one;

            yield return new WaitForSeconds(duration);

            // Hiệu ứng mờ dần (Fade out)
            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                _bloodMoonBannerText.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
                yield return null;
            }
            _bloodMoonBannerText.gameObject.SetActive(false);
            _bloodMoonBannerText.alpha = 1f;
        }
    }

    private void EnsureInstructionsUI()
    {
        if (_instructionsPanel != null)
        {
            return;
        }

        Transform existing = transform.Find("InstructionsPanel");
        if (existing != null)
        {
            _instructionsPanel = existing.gameObject;
            return;
        }

        // Tạo InstructionsPanel
        GameObject panelObj = new GameObject("InstructionsPanel", typeof(RectTransform));
        panelObj.transform.SetParent(transform, false);
        panelObj.layer = gameObject.layer;

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f); // Góc dưới bên phải
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);     // Điểm neo dưới phải
        panelRect.anchoredPosition = new Vector2(-20f, 20f); // Lùi vào 20 pixel
        panelRect.sizeDelta = new Vector2(320f, 210f);       // Kích thước bảng hướng dẫn

        // Thêm hình nền Panel bán trong suốt
        UnityEngine.UI.Image bgImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.75f); // Màu đen mờ sang trọng

        // Thêm viền mỏng
        UnityEngine.UI.Outline outline = panelObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Tạo Text bên trong Panel
        GameObject textObj = new GameObject("InstructionsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(panelObj.transform, false);
        textObj.layer = gameObject.layer;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero; // Stretch
        textRect.anchoredPosition = Vector2.zero;

        // Thêm padding cho text
        textRect.offsetMin = new Vector2(12f, 12f);
        textRect.offsetMax = new Vector2(-12f, -12f);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.fontSize = 13f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.lineSpacing = 6f; // Thu nhỏ khoảng cách dòng một chút để vừa vặn
        text.raycastTarget = false;

        text.text = "<b>HƯỚNG DẪN ĐIỀU KHIỂN</b>\n" +
                    "<color=#c8c8c8>" +
                    "• <b>W/S/A/D / Mũi tên:</b> Di chuyển camera\n" +
                    "• <b>Chuột Trái:</b> Chọn đơn vị / Hộp chọn\n" +
                    "• <b>Chuột Phải:</b> Di chuyển / Chỉ định nhanh\n" +
                    "• <b>Phím T + L-Click:</b> Di chuyển Tấn công\n" +
                    "• <b>Phím G + L-Click:</b> Chỉ định Khai thác\n" +
                    "• <b>Phím B + L-Click:</b> Chỉ định Xây/Sửa\n" +
                    "• <b>Phím Tab:</b> Chọn nhanh dân rảnh rỗi\n" +
                    "• <b>Phím Esc:</b> Hủy lệnh / Bỏ chọn" +
                    "</color>";

        _instructionsPanel = panelObj;
    }

    [Header("Capacity UI")]
    [SerializeField] private TextMeshProUGUI _capacityText;

    private void EnsureCapacityText()
    {
        if (_capacityText != null) return;

        Transform existing = transform.Find("CapacityText");
        if (existing != null)
        {
            _capacityText = existing.GetComponent<TextMeshProUGUI>();
            if (_capacityText != null) return;
        }

        GameObject capObject = new GameObject("CapacityText", typeof(RectTransform), typeof(TextMeshProUGUI));
        capObject.transform.SetParent(transform, false);
        capObject.layer = gameObject.layer;

        RectTransform rectTransform = capObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(150f, 472f); // Ở giữa Food (-6) và Population (300)
        rectTransform.sizeDelta = new Vector2(200f, 50f);

        _capacityText = capObject.GetComponent<TextMeshProUGUI>();
        _capacityText.fontSize = 24f;
        _capacityText.alignment = TextAlignmentOptions.Center;
        _capacityText.color = Color.white;
        _capacityText.raycastTarget = false;
        _capacityText.text = "Kho: 0/1000";
    }

    public void UpdateCapacityUI(int current, int max)
    {
        EnsureCapacityText();
        if (_capacityText != null)
        {
            _capacityText.text = $"Kho: {current}/{max}";
            _capacityText.color = current >= max ? Color.red : Color.white;
        }
    }

    [Header("Kho đầy Warning")]
    [SerializeField] private TextMeshProUGUI _storageFullWarningText;
    private Coroutine _warningFadeCoroutine;

    private void EnsureStorageFullWarningText()
    {
        if (_storageFullWarningText != null) return;

        Transform existing = transform.Find("StorageFullWarningText");
        if (existing != null)
        {
            _storageFullWarningText = existing.GetComponent<TextMeshProUGUI>();
            if (_storageFullWarningText != null) return;
        }

        GameObject warningObject = new GameObject("StorageFullWarningText", typeof(RectTransform), typeof(TextMeshProUGUI));
        warningObject.transform.SetParent(transform, false);
        warningObject.layer = gameObject.layer;

        RectTransform rectTransform = warningObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, 250f); // Ở giữa màn hình phía trên
        rectTransform.sizeDelta = new Vector2(700f, 50f);

        _storageFullWarningText = warningObject.GetComponent<TextMeshProUGUI>();
        _storageFullWarningText.fontSize = 24f;
        _storageFullWarningText.alignment = TextAlignmentOptions.Center;
        _storageFullWarningText.color = Color.red;
        _storageFullWarningText.text = "⚠️ KHO ĐẦY! CẦN XÂY DỰNG STORAGE MỚI ĐỂ TIẾP TỤC NỘP!";
        _storageFullWarningText.raycastTarget = false;
        _storageFullWarningText.gameObject.SetActive(false);
    }

    public void TriggerStorageFullWarning()
    {
        EnsureStorageFullWarningText();
        if (_storageFullWarningText == null) return;

        if (_warningFadeCoroutine != null)
        {
            StopCoroutine(_warningFadeCoroutine);
        }
        _warningFadeCoroutine = StartCoroutine(ShowWarningRoutine());
    }

    private IEnumerator ShowWarningRoutine()
    {
        _storageFullWarningText.gameObject.SetActive(true);
        _storageFullWarningText.alpha = 1f;

        // Nhấp nháy màu đỏ/vàng
        for (int i = 0; i < 3; i++)
        {
            _storageFullWarningText.color = Color.yellow;
            yield return new WaitForSeconds(0.2f);
            _storageFullWarningText.color = Color.red;
            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(2.0f);

        // Mờ dần
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            _storageFullWarningText.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
            yield return null;
        }

        _storageFullWarningText.gameObject.SetActive(false);
        _warningFadeCoroutine = null;
    }
}
