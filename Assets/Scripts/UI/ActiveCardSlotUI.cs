using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Quản lý hiển thị cho từng ô Icon Thẻ Nâng Cấp trên HUD.
/// Hỗ trợ đổi màu viền theo độ hiếm và hiển thị Tooltip thông tin chi tiết khi rê chuột vào.
/// </summary>
public class ActiveCardSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _rarityBorderImage;

    [Header("Tooltip References")]
    [SerializeField] private GameObject _tooltipRoot;
    [SerializeField] private TextMeshProUGUI _tooltipTitleText;
    [SerializeField] private TextMeshProUGUI _tooltipRarityText;
    [SerializeField] private TextMeshProUGUI _tooltipDescriptionText;

    private UpgradeCardData _cardData;

    // Các biến phục vụ việc chuyển cha để Tooltip không bị đè bởi các icon khác
    private Transform _originalParent;
    private Vector2 _originalAnchoredPosition;
    private Vector3 _originalScale;

    private void Awake()
    {
        // Ẩn Tooltip mặc định khi khởi tạo
        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);

            // Lưu giữ thông tin ban đầu của Tooltip
            _originalParent = _tooltipRoot.transform.parent;
            RectTransform rt = _tooltipRoot.transform as RectTransform;
            if (rt != null)
            {
                _originalAnchoredPosition = rt.anchoredPosition;
            }
            _originalScale = _tooltipRoot.transform.localScale;
        }
    }

    /// <summary>
    /// Cấu hình dữ liệu thẻ cho ô hiển thị này.
    /// </summary>
    /// <param name="card">Dữ liệu thẻ nâng cấp</param>
    public void Setup(UpgradeCardData card)
    {
        _cardData = card;

        if (_cardData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // 1. Cập nhật Icon hình ảnh
        if (_iconImage != null)
        {
            _iconImage.sprite = _cardData.icon;
            _iconImage.enabled = _cardData.icon != null;
        }

        // 2. Cập nhật màu viền theo độ hiếm
        if (_rarityBorderImage != null)
        {
            _rarityBorderImage.color = _cardData.RarityColor;
        }

        // 3. Cập nhật nội dung văn bản Tooltip sẵn
        UpdateTooltipTexts();
    }

    private void UpdateTooltipTexts()
    {
        if (_cardData == null) return;

        if (_tooltipTitleText != null)
        {
            _tooltipTitleText.text = _cardData.cardName;
            _tooltipTitleText.color = _cardData.RarityColor; // Màu sắc tiêu đề theo độ hiếm
        }

        if (_tooltipRarityText != null)
        {
            _tooltipRarityText.text = _cardData.RarityDisplayName;
            _tooltipRarityText.color = _cardData.RarityColor;
        }

        if (_tooltipDescriptionText != null)
        {
            _tooltipDescriptionText.text = _cardData.description;
        }
    }

    #region Event Systems Pointer Interface
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_cardData == null) return;

        // Hiển thị Tooltip khi hover chuột vào
        if (_tooltipRoot != null)
        {
            // 1. Tìm Canvas gốc chứa toàn bộ UI để làm cha mới tạm thời cho Tooltip
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                RectTransform canvasRT = rootCanvas.transform as RectTransform;
                RectTransform tooltipRect = _tooltipRoot.transform as RectTransform;

                if (canvasRT != null && tooltipRect != null)
                {
                    // Chuyển cha của Tooltip về Canvas chính để đè lên mọi thứ trong game
                    _tooltipRoot.transform.SetParent(rootCanvas.transform, false);
                    _tooltipRoot.transform.SetAsLastSibling(); // Vẽ cuối cùng để đè lên mọi UI khác

                    // 2. Tính toán tọa độ của ô Slot đối với Canvas chính
                    Vector3 slotWorldPos = transform.position;
                    Vector2 localPosOnCanvas = canvasRT.InverseTransformPoint(slotWorldPos);

                    // 3. Lấy kích thước thực tế của Tooltip
                    Vector2 tooltipSize = tooltipRect.rect.size;
                    if (tooltipSize.x <= 0f || tooltipSize.y <= 0f)
                    {
                        tooltipSize = tooltipRect.sizeDelta; // Fallback
                    }

                    // 4. Định vị Tooltip mặc định nằm phía trên ô Slot
                    float slotHeight = (transform as RectTransform).rect.height;
                    float spacing = 15f;
                    float targetY = localPosOnCanvas.y + (slotHeight * 0.5f) + (tooltipSize.y * 0.5f) + spacing;

                    // Nếu Tooltip nhô ra ngoài cạnh trên màn hình, lật xuống hiển thị phía dưới ô Slot
                    float margin = 12f;
                    float topEdge = targetY + (tooltipSize.y * 0.5f);
                    if (topEdge > canvasRT.rect.yMax - margin)
                    {
                        targetY = localPosOnCanvas.y - (slotHeight * 0.5f) - (tooltipSize.y * 0.5f) - spacing;
                    }

                    float targetX = localPosOnCanvas.x;

                    // 5. Giới hạn (clamp) để đảm bảo Tooltip không bị lòi ra ngoài 4 góc màn hình
                    float minX = canvasRT.rect.xMin + (tooltipSize.x * 0.5f) + margin;
                    float maxX = canvasRT.rect.xMax - (tooltipSize.x * 0.5f) - margin;
                    float minY = canvasRT.rect.yMin + (tooltipSize.y * 0.5f) + margin;
                    float maxY = canvasRT.rect.yMax - (tooltipSize.y * 0.5f) - margin;

                    targetX = Mathf.Clamp(targetX, minX, maxX);
                    targetY = Mathf.Clamp(targetY, minY, maxY);

                    // Thiết lập Pivot, Anchor và vị trí mới
                    tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
                    tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
                    tooltipRect.pivot = new Vector2(0.5f, 0.5f);
                    tooltipRect.anchoredPosition = new Vector2(targetX, targetY);
                }
            }

            _tooltipRoot.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Ẩn Tooltip và khôi phục cha cũ
        ResetTooltipState();
    }

    private void OnDisable()
    {
        // Khôi phục cha và ẩn Tooltip nếu bị vô hiệu hóa
        ResetTooltipState();
    }

    private void ResetTooltipState()
    {
        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);

            // Trả cha cũ và tọa độ ban đầu về cho ô slot
            if (_originalParent != null && _tooltipRoot.transform.parent != _originalParent)
            {
                _tooltipRoot.transform.SetParent(_originalParent, false);

                RectTransform tooltipRect = _tooltipRoot.transform as RectTransform;
                if (tooltipRect != null)
                {
                    tooltipRect.anchorMin = new Vector2(0.5f, 1f);
                    tooltipRect.anchorMax = new Vector2(0.5f, 1f);
                    tooltipRect.pivot = new Vector2(0.5f, 0f);
                    tooltipRect.anchoredPosition = _originalAnchoredPosition;
                }
                _tooltipRoot.transform.localScale = _originalScale;
            }
        }
    }

    #endregion
}
