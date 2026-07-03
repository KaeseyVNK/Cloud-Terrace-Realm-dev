using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Controller cho UI Production (nhà lính, trại quân, v.v.).
/// Tự động mở/đóng khi TestProductionUI chọn công trình.
/// Populate danh sách thẻ đơn vị và cập nhật thanh tiến trình hàng đợi.
/// </summary>
public class ProductionUIController : MonoBehaviour
{
    public static ProductionUIController Instance { get; private set; }

    // -------- Inspector --------
    [Header("Panel Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Header")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button   _rallyPointButton;

    [Header("Unit List")]
    [SerializeField] private Transform    _unitCardsContainer;   // Content bên trong ScrollView
    [SerializeField] private UnitCardUI   _unitCardPrefab;       // Prefab UnitCard_0

    [Header("Queue Area")]
    [SerializeField] private Slider       _progressBar;
    [SerializeField] private TMP_Text     _queueLabel;
    [SerializeField] private Transform    _queueSlotsParent;     // QueueSlots HorizontalLayoutGroup

    [Header("Close Button")]
    [SerializeField] private Button _closeButton;

    // -------- Private State --------
    private BuildingProduction _currentProduction;
    private readonly List<UnitCardUI> _spawnedCards = new List<UnitCardUI>();
    private CanvasGroup _canvasGroup;
    private GameObject _slotTemplate;
    private readonly List<GameObject> _activeSlots = new List<GameObject>();
    private UnitCardUI _unitCardTemplate;
    private Coroutine _fadeCoroutine;
    private BuildingProduction _productionOverride;


    // -------- Lifecycle --------

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (_panelRoot != null)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
            _panelRoot.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        // Cache unit card template and reparent out of Content if it resides in the scene
        if (_unitCardPrefab != null)
        {
            _unitCardTemplate = _unitCardPrefab;
            bool isPrefabAsset = string.IsNullOrEmpty(_unitCardPrefab.gameObject.scene.name);
            if (!isPrefabAsset)
            {
                _unitCardTemplate.gameObject.SetActive(false);
                _unitCardTemplate.transform.SetParent(transform, false);
            }
        }

        // Khởi tạo template hàng đợi và dọn các slot mẫu dư thừa
        if (_queueSlotsParent != null && _queueSlotsParent.childCount > 0)
        {
            _slotTemplate = _queueSlotsParent.GetChild(0).gameObject;
            _slotTemplate.SetActive(false);

            for (int i = _queueSlotsParent.childCount - 1; i >= 1; i--)
            {
                var child = _queueSlotsParent.GetChild(i).gameObject;
                if (child != null)
                {
                    child.SetActive(false);
                    Destroy(child);
                }
            }
        }

        // Đăng ký sự kiện nút
        if (_rallyPointButton != null)
            _rallyPointButton.onClick.AddListener(OnRallyPointButtonClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseButtonClicked);

        // Lắng nghe thay đổi tài nguyên để refresh trạng thái card
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged += OnResourceChanged;

        if (TechnologyManager.Instance != null)
            TechnologyManager.Instance.OnTechnologyUnlocked += OnTechnologyUnlocked;
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;

        if (TechnologyManager.HasInstance)
            TechnologyManager.Instance.OnTechnologyUnlocked -= OnTechnologyUnlocked;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Theo dõi TestProductionUI để mở/đóng panel
        BuildingProduction activeProduction = _productionOverride != null ? _productionOverride : TestProductionUI.Instance?.SelectedProduction;

        // Bỏ qua nếu là Nhà Chính (Main Building)
        if (activeProduction != null && 
            (activeProduction.GetComponent<MainBuildingCombatTarget>() != null || 
             activeProduction.GetComponentInChildren<MainBuildingCombatTarget>() != null))
        {
            activeProduction = null;
        }

        if (activeProduction != _currentProduction)
            SetProduction(activeProduction);

        // Cập nhật thanh tiến trình sản xuất mỗi frame
        if (_currentProduction != null && _progressBar != null)
            UpdateProgressBar();

        // Cập nhật slot hàng đợi
        if (_currentProduction != null)
            UpdateQueueSlots();
    }

    // -------- Public API --------

    /// <summary>Gắn dữ liệu công trình vào UI.</summary>
    public void SetProduction(BuildingProduction production)
    {
        if (_currentProduction == production)
        {
            return;
        }

        _currentProduction = production;

        if (_currentProduction != null)
            OpenPanel();
        else
            ClosePanel();
    }

    public void SetProductionOverride(BuildingProduction production)
    {
        _productionOverride = production;
        SetProduction(production);
    }

    public void ClearProductionOverride()
    {
        _productionOverride = null;
    }

    // -------- Private Helpers --------

    private void OpenPanel()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(true, 0.2f));

        // Tiêu đề
        if (_titleText != null && _currentProduction.BuildingData != null)
            _titleText.text = _currentProduction.BuildingData.buildingName;

        PopulateUnitCards();
        UpdateProgressBar();
        UpdateQueueSlots();
    }

    private void ClosePanel()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(false, 0.15f));
    }

    private System.Collections.IEnumerator FadeRoutine(bool show, float duration)
    {
        if (_panelRoot == null) yield break;

        if (show)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(true);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;

        Vector3 startScale = show ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;
        Vector3 targetScale = show ? Vector3.one : new Vector3(0.95f, 0.95f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tSmooth = t * t * (3f - 2f * t);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, tSmooth);
            }

            _panelRoot.transform.localScale = Vector3.Lerp(startScale, targetScale, tSmooth);
            yield return null;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = targetAlpha;
        }
        _panelRoot.transform.localScale = targetScale;

        if (!show)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
        }

        _fadeCoroutine = null;
    }

    private void PopulateUnitCards()
    {
        ClearUnitCards();

        if (_currentProduction?.BuildingData?.producibleUnits == null) return;

        UnitCardUI templateToUse = _unitCardTemplate != null ? _unitCardTemplate : _unitCardPrefab;
        if (templateToUse == null || _unitCardsContainer == null) return;

        foreach (var unitData in _currentProduction.BuildingData.producibleUnits)
        {
            if (unitData == null) continue;

            // Ẩn đơn vị lính nếu chưa được khám phá/phát hiện qua thẻ nâng cấp
            if (CardManager.Instance != null && !CardManager.Instance.IsUnitDiscovered(unitData))
                continue;

            UnitCardUI card = Instantiate(templateToUse, _unitCardsContainer, false);
            card.gameObject.SetActive(true);
            UnitData capturedUnit = unitData; // Capture for lambda
            card.Setup(unitData, () => _currentProduction?.RequestProduceUnit(capturedUnit));
            _spawnedCards.Add(card);
        }
    }

    private void ClearUnitCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        _spawnedCards.Clear();

        // Xóa sạch các child còn lại trong container (đề phòng các card mẫu tĩnh từ editor)
        if (_unitCardsContainer != null)
        {
            foreach (Transform child in _unitCardsContainer)
            {
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }
    }

    private void ClearQueueSlots()
    {
        foreach (var slot in _activeSlots)
        {
            if (slot != null)
                Destroy(slot);
        }
        _activeSlots.Clear();
    }

    private void UpdateProgressBar()
    {
        if (_progressBar == null || _currentProduction == null) return;

        bool isProducing = _currentProduction.CurrentProducingUnit != null;
        _progressBar.gameObject.SetActive(isProducing);

        if (isProducing)
        {
            float totalTime = _currentProduction.CurrentProducingUnit.productionTime;
            float remaining = _currentProduction.CurrentProductionTimer;
            _progressBar.value = totalTime > 0f ? 1f - (remaining / totalTime) : 0f;

            if (_queueLabel != null)
                _queueLabel.text = $"Queue: {_currentProduction.CurrentProducingUnit.unitName} ({(_progressBar.value * 100f):F0}%)";
        }
        else
        {
            if (_queueLabel != null)
                _queueLabel.text = "Queue";
        }
    }

    private void UpdateQueueSlots()
    {
        if (_queueSlotsParent == null || _currentProduction == null || _slotTemplate == null) return;

        var queueList = new List<UnitData>(_currentProduction.ProductionQueue);

        // 1. Đồng bộ số lượng slot hoạt động với hàng đợi
        while (_activeSlots.Count < queueList.Count)
        {
            GameObject newSlot = Instantiate(_slotTemplate, _queueSlotsParent);
            newSlot.SetActive(true);
            _activeSlots.Add(newSlot);
        }

        while (_activeSlots.Count > queueList.Count)
        {
            GameObject slotToDestroy = _activeSlots[_activeSlots.Count - 1];
            _activeSlots.RemoveAt(_activeSlots.Count - 1);
            if (slotToDestroy != null)
                Destroy(slotToDestroy);
        }

        // 2. Cập nhật hình ảnh và sự kiện Click cho từng slot
        for (int i = 0; i < queueList.Count; i++)
        {
            var unit = queueList[i];
            var slotGO = _activeSlots[i];

            if (slotGO == null) continue;

            if (slotGO.transform.childCount > 0)
            {
                var iconChild = slotGO.transform.GetChild(0);
                var img = iconChild.GetComponent<Image>();
                if (img != null)
                {
                    if (unit != null && unit.portraitIcon != null)
                    {
                        img.sprite = unit.portraitIcon;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = new Color(1, 1, 1, 0);
                    }
                }
            }

            var btn = slotGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int capturedIndex = i;
                btn.onClick.AddListener(() => CancelQueueItem(capturedIndex));
            }
        }
    }

    private void CancelQueueItem(int index)
    {
        if (_currentProduction != null)
        {
            bool success = _currentProduction.CancelQueueItem(index);
            if (success)
            {
                UpdateQueueSlots();
                RefreshAllCardStates();
            }
        }
    }

    private void RefreshAllCardStates()
    {
        foreach (var card in _spawnedCards)
            card?.RefreshState();
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        RefreshAllCardStates();
    }


    private void OnTechnologyUnlocked(TechnologyData tech)
    {
        RefreshAllCardStates();
    }

    private void OnRallyPointButtonClicked()
    {
        // Thông báo cho TestProductionUI kích hoạt chế độ đặt rally point
        Debug.Log("[ProductionUI] Nhấn nút Rally Point");
        // Hiện tại TestProductionUI quản lý trạng thái _isRallyTargetingMode nội bộ.
        // Tìm field qua reflection để toggle
        if (TestProductionUI.Instance != null)
        {
            TestProductionUI.Instance.ToggleRallyTargeting(_currentProduction);
        }
    }

    private void OnCloseButtonClicked()
    {
        TestProductionUI.Instance?.DeselectProduction();
    }
}
