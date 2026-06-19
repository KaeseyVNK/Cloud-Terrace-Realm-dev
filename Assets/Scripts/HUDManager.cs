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

    // Cache variables for resource and UI animations
    private int _prevWood = -1;
    private int _prevStone = -1;
    private int _prevFood = -1;
    private int _prevGold = -1;
    private int _prevRelic = -1;
    private int _prevPopulation = -1;
    private int _prevCapacity = -1;
    private bool _wasFoodShortage = false;
    private Coroutine _foodWarningCoroutine;
    private Vector3 _originalWarningPos;
    private bool _hasStoredWarningPos = false;
    private readonly System.Collections.Generic.Dictionary<TextMeshProUGUI, Coroutine> _activePunchCoroutines = new System.Collections.Generic.Dictionary<TextMeshProUGUI, Coroutine>();

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
        
        // Deactivate instructions panel as requested
        if (_instructionsPanel != null)
        {
            _instructionsPanel.SetActive(false);
        }
        else
        {
            GameObject existing = FindGameObjectInChildren("InstructionsPanel");
            if (existing != null)
            {
                existing.SetActive(false);
            }
        }

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
        bool hasChanged = false;

        switch (type)
        {
            case ResourceType.Wood:
                if (_woodText != null)
                {
                    _woodText.text = $"{newAmount}/{gatherers}";
                    if (_prevWood != -1 && _prevWood != newAmount) hasChanged = true;
                    _prevWood = newAmount;
                    if (hasChanged) TriggerPunchScale(_woodText);
                }
                break;
            case ResourceType.Stone:
                if (_stoneText != null)
                {
                    _stoneText.text = $"{newAmount}/{gatherers}";
                    if (_prevStone != -1 && _prevStone != newAmount) hasChanged = true;
                    _prevStone = newAmount;
                    if (hasChanged) TriggerPunchScale(_stoneText);
                }
                break;
            case ResourceType.Food:
                if (_foodText != null)
                {
                    _foodText.text = $"{newAmount}/{gatherers}";
                    if (_prevFood != -1 && _prevFood != newAmount) hasChanged = true;
                    _prevFood = newAmount;
                    if (hasChanged) TriggerPunchScale(_foodText);
                }
                break;
            case ResourceType.Gold:
                if (_goldText != null)
                {
                    _goldText.text = $"{newAmount}/{gatherers}";
                    if (_prevGold != -1 && _prevGold != newAmount) hasChanged = true;
                    _prevGold = newAmount;
                    if (hasChanged) TriggerPunchScale(_goldText);
                }
                break;
            case ResourceType.AncientRelic:
                if (_relicText != null)
                {
                    _relicText.text = newAmount.ToString();
                    if (_prevRelic != -1 && _prevRelic != newAmount) hasChanged = true;
                    _prevRelic = newAmount;
                    if (hasChanged) TriggerPunchScale(_relicText);
                }
                break;
        }

        if (ResourceManager.Instance != null)
        {
            UpdateCapacityUI(ResourceManager.Instance.GetTotalPrimaryResources(), ResourceManager.Instance.GetMaxResourceCapacity());
        }
    }

    void Update()
    {
        // Legacy time update removed - Handled by HUDTimeUI.cs

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

        // Cập nhật trạng thái thiếu lương thực & Cảnh báo đói có transition/shake
        if (HungerSystem.Instance != null)
        {
            bool isShortage = HungerSystem.Instance.IsFoodShortage;
            if (isShortage != _wasFoodShortage)
            {
                _wasFoodShortage = isShortage;
                if (_foodWarningText != null)
                {
                    if (_foodWarningCoroutine != null) StopCoroutine(_foodWarningCoroutine);
                    _foodWarningCoroutine = StartCoroutine(FoodWarningTransitionRoutine(isShortage));
                }
            }
            if (_foodText != null)
            {
                _foodText.color = isShortage ? Color.red : Color.white;
            }
        }

        // Tạo nhịp nháy phát sáng nhẹ liên tục cho Relic text (vì đây là tài nguyên quý hiếm)
        if (_relicText != null)
        {
            float glow = 0.8f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.2f);
            _relicText.color = new Color(1f, 0.84f, 0f, glow);
        }
    }

    private void EnsurePopulationText()
    {
        if (_populationText != null)
        {
            return;
        }

        _populationText = FindTextComponent("PopulationText");
        if (_populationText != null)
        {
            return;
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
            ? $"{current}+{reserved}/{max}"
            : $"{current}/{max}";

        if (_prevPopulation != -1 && _prevPopulation != current)
        {
            TriggerPunchScale(_populationText);
        }
        _prevPopulation = current;
    }

    private void EnsureFoodWarningText()
    {
        if (_foodWarningText != null)
        {
            return;
        }

        _foodWarningText = FindTextComponent("FoodWarningText");
        if (_foodWarningText != null)
        {
            return;
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

        _relicText = FindTextComponent("RelicText");
        if (_relicText != null)
        {
            return;
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
        _relicText.text = "0";
    }

    private void EnsureBloodMoonBannerText()
    {
        if (_bloodMoonBannerText != null)
        {
            return;
        }

        _bloodMoonBannerText = FindTextComponent("BloodMoonBannerText");
        if (_bloodMoonBannerText != null)
        {
            return;
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

        _capacityText = FindTextComponent("CapacityText");
        if (_capacityText != null) return;

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
            if (_prevCapacity != -1 && _prevCapacity != current)
            {
                TriggerPunchScale(_capacityText);
            }
            _prevCapacity = current;
        }
    }

    // Các phương thức trợ giúp tạo hiệu ứng Animation cho HUD
    private void TriggerPunchScale(TextMeshProUGUI text)
    {
        if (text == null) return;
        if (_activePunchCoroutines.TryGetValue(text, out Coroutine active))
        {
            StopCoroutine(active);
        }
        _activePunchCoroutines[text] = StartCoroutine(PunchScaleRoutine(text));
    }

    private IEnumerator PunchScaleRoutine(TextMeshProUGUI text)
    {
        Transform textTrans = text.transform;
        Vector3 originalScale = Vector3.one;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Tạo nảy nhẹ dạng Parabol: 1.0 -> 1.25 -> 1.0
            float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
            textTrans.localScale = originalScale * scaleMultiplier;
            yield return null;
        }

        textTrans.localScale = originalScale;
        _activePunchCoroutines.Remove(text);
    }

    private IEnumerator FoodWarningTransitionRoutine(bool show)
    {
        if (!_hasStoredWarningPos)
        {
            _originalWarningPos = _foodWarningText.rectTransform.anchoredPosition;
            _hasStoredWarningPos = true;
        }

        if (show)
        {
            _foodWarningText.gameObject.SetActive(true);
            _foodWarningText.alpha = 0f;
            _foodWarningText.rectTransform.anchoredPosition = _originalWarningPos;

            // Fade in kết hợp rung lắc (Shake)
            float elapsed = 0f;
            float duration = 0.4f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                _foodWarningText.alpha = Mathf.Lerp(0f, 1f, t);
                
                float shakeOffset = Mathf.Sin(t * 30f) * 10f * (1f - t);
                _foodWarningText.rectTransform.anchoredPosition = _originalWarningPos + new Vector3(shakeOffset, 0f, 0f);
                yield return null;
            }

            _foodWarningText.alpha = 1f;
            _foodWarningText.rectTransform.anchoredPosition = _originalWarningPos;

            // Hiệu ứng nhấp nháy alpha nhịp nhàng khi đói
            while (_wasFoodShortage)
            {
                float pulse = 0.6f + Mathf.PingPong(Time.unscaledTime * 3f, 0.4f);
                _foodWarningText.alpha = pulse;
                yield return null;
            }
        }
        else
        {
            // Fade out
            float elapsed = 0f;
            float duration = 0.3f;
            float startAlpha = _foodWarningText.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                _foodWarningText.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }
            _foodWarningText.alpha = 0f;
            _foodWarningText.gameObject.SetActive(false);
        }
        _foodWarningCoroutine = null;
    }

    [Header("Kho đầy Warning")]
    [SerializeField] private TextMeshProUGUI _storageFullWarningText;
    private Coroutine _warningFadeCoroutine;

    private void EnsureStorageFullWarningText()
    {
        if (_storageFullWarningText != null) return;

        _storageFullWarningText = FindTextComponent("StorageFullWarningText");
        if (_storageFullWarningText != null) return;

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

    private TextMeshProUGUI FindTextComponent(string name)
    {
        TextMeshProUGUI[] textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
        // First pass: prefer active in hierarchy
        foreach (var text in textComponents)
        {
            if (text.gameObject.name == name && text.gameObject.activeInHierarchy)
            {
                return text;
            }
        }
        // Second pass: fall back to inactive
        foreach (var text in textComponents)
        {
            if (text.gameObject.name == name)
            {
                return text;
            }
        }
        return null;
    }

    private GameObject FindGameObjectInChildren(string name)
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        // First pass: prefer active in hierarchy
        foreach (var t in allChildren)
        {
            if (t.gameObject.name == name && t.gameObject.activeInHierarchy)
            {
                return t.gameObject;
            }
        }
        // Second pass: fall back to inactive
        foreach (var t in allChildren)
        {
            if (t.gameObject.name == name)
            {
                return t.gameObject;
            }
        }
        return null;
    }
}
