using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
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

    void Start()
    {
        EnsurePopulationText();
        EnsureFoodWarningText();

        // Đăng ký lắng nghe sự kiện tài nguyên thay đổi từ ResourceManager
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            
            // Cập nhật UI lần đầu tiên
            UpdateResourceUI(ResourceType.Wood, ResourceManager.Instance.GetResourceAmount(ResourceType.Wood));
            UpdateResourceUI(ResourceType.Stone, ResourceManager.Instance.GetResourceAmount(ResourceType.Stone));
            UpdateResourceUI(ResourceType.Food, ResourceManager.Instance.GetResourceAmount(ResourceType.Food));
            UpdateResourceUI(ResourceType.Gold, ResourceManager.Instance.GetResourceAmount(ResourceType.Gold));
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
        switch (type)
        {
            case ResourceType.Wood:
                if (_woodText != null) _woodText.text = "Go: " + newAmount;
                break;
            case ResourceType.Stone:
                if (_stoneText != null) _stoneText.text = "Da: " + newAmount;
                break;
            case ResourceType.Food:
                if (_foodText != null) _foodText.text = "Luong: " + newAmount;
                break;
            case ResourceType.Gold:
                if (_goldText != null) _goldText.text = "Vang: " + newAmount;
                break;
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
}
