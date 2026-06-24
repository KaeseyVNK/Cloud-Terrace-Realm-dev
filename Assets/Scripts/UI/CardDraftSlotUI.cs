using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Đại diện cho một thẻ nâng cấp đơn lẻ trong giao diện chọn thẻ.
/// Hỗ trợ hiệu ứng quay trục đứng (Slot Machine / Bingo Roll) khi xuất hiện.
/// </summary>
public class CardDraftSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visual Elements")]
    [SerializeField] private TMP_Text _cardNameText;
    [SerializeField] private TMP_Text _cardTypeText;
    [SerializeField] private TMP_Text _cardDescriptionText;
    [SerializeField] private Image _cardIconImage;
    [SerializeField] private GameObject _glowHighlight;

    [Header("Hover Settings")]
    [SerializeField] private float _hoverScaleFactor = 1.06f;
    [SerializeField] private float _scaleDuration = 0.15f;

    private UpgradeCardData _cardData;
    private Action<UpgradeCardData> _onClickedCallback;
    private Coroutine _scaleCoroutine;
    private Vector3 _originalScale = Vector3.one;

    private RectTransform _rollingContainer;

    private void Awake()
    {
        _originalScale = transform.localScale;
        if (_glowHighlight != null)
        {
            _glowHighlight.SetActive(false);
        }

        CreateRollingContainer();
    }

    private void CreateRollingContainer()
    {
        if (_rollingContainer != null) return;

        // Tạo container trung gian để trượt toàn bộ UI con theo trục Y
        GameObject containerObj = new GameObject("RollingContainer");
        _rollingContainer = containerObj.AddComponent<RectTransform>();
        _rollingContainer.SetParent(this.transform, false);

        // Giãn nở phủ kín toàn bộ thẻ
        _rollingContainer.anchorMin = Vector2.zero;
        _rollingContainer.anchorMax = Vector2.one;
        _rollingContainer.sizeDelta = Vector2.zero;
        _rollingContainer.anchoredPosition = Vector2.zero;

        // Di chuyển toàn bộ các GameObject con (ngoại trừ GlowHighlight và RollingContainer chính nó) vào container trượt
        int childCount = transform.childCount;
        Transform[] children = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            children[i] = transform.GetChild(i);
        }

        foreach (var child in children)
        {
            if (child == _rollingContainer || (_glowHighlight != null && child == _glowHighlight.transform))
            {
                continue;
            }
            child.SetParent(_rollingContainer, false);
        }
    }

    public void Setup(UpgradeCardData cardData, Action<UpgradeCardData> onClicked)
    {
        _cardData = cardData;
        _onClickedCallback = onClicked;

        // Reset tỷ lệ về 0 để chạy hiệu ứng xuất hiện từ từ
        transform.localScale = Vector3.zero;
        if (_glowHighlight != null)
        {
            _glowHighlight.SetActive(false);
        }

        // Hiển thị giao diện thẻ ngẫu nhiên ngay từ đầu để tránh người chơi nhìn thấy kết quả trước
        ShowRandomCardVisuals();
    }

    private void SetupValues(UpgradeCardData cardData)
    {
        if (cardData == null) return;

        if (_cardNameText != null)
        {
            _cardNameText.text = cardData.cardName;
        }

        if (_cardTypeText != null)
        {
            _cardTypeText.text = cardData.cardType switch
            {
                UpgradeCardType.StatBuff => "STAT BUFF",
                UpgradeCardType.Unlock => "UNLOCK",
                UpgradeCardType.Instant => "INSTANT",
                _ => "CARD"
            };
        }

        if (_cardDescriptionText != null)
        {
            _cardDescriptionText.text = cardData.description;
        }

        if (_cardIconImage != null)
        {
            if (cardData.icon != null)
            {
                _cardIconImage.sprite = cardData.icon;
                _cardIconImage.enabled = true;
            }
            else
            {
                _cardIconImage.enabled = false;
            }
        }
    }

    private void ShowRandomCardVisuals()
    {
        if (CardManager.Instance == null || CardManager.Instance.AllCards.Count == 0) return;

        var pool = CardManager.Instance.AllCards;
        int idx = UnityEngine.Random.Range(0, pool.Count);
        SetupValues(pool[idx]);
    }

    public void AnimatePopIn(float delay, int slotIndex)
    {
        StartCoroutine(PopInRoutine(delay, slotIndex));
    }

    private IEnumerator PopInRoutine(float delay, int slotIndex)
    {
        transform.localScale = Vector3.zero;
        if (_rollingContainer != null)
        {
            _rollingContainer.anchoredPosition = Vector2.zero;
        }

        // Thiết lập hiển thị ngẫu nhiên ngay từ đầu
        ShowRandomCardVisuals();

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay); // Chờ thời gian thực vì game đang pause
        }

        // Bắt đầu chạy cả 2 hiệu ứng cùng lúc: Pop-in scale và Bingo Roll!
        float elapsed = 0f;
        float rollDuration = 1.0f + slotIndex * 0.4f; // Các cột dừng so le nhau
        float startSpeed = 2600f; // Vận tốc ban đầu
        float currentY = 0f;
        float popDuration = 0.15f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            // 1. Cập nhật tỷ lệ (Pop-in Scale)
            if (elapsed < popDuration)
            {
                transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, elapsed / popDuration);
            }
            else
            {
                transform.localScale = _originalScale;
            }

            // 2. Tính tốc độ và di chuyển cuộn dọc
            // Giảm tốc nhẹ trong lúc quay để tăng tính chân thực
            float t = Mathf.Clamp01(elapsed / rollDuration);
            float currentSpeed = startSpeed * (1f - t * t * 0.4f);

            currentY -= currentSpeed * dt;

            // Kiểm tra bọc viền (wrap) hoàn toàn ngoài vùng Mask (chiều cao slot là 563.6, dùng 650f để ẩn hoàn toàn)
            if (currentY <= -650f)
            {
                currentY = 650f;

                // Nếu đã quay đủ thời gian -> Load card thật và thoát vòng lặp để chuyển sang giai đoạn Snap
                if (elapsed >= rollDuration)
                {
                    SetupValues(_cardData);
                    break;
                }
                else
                {
                    ShowRandomCardVisuals();
                }
            }

            if (_rollingContainer != null)
            {
                _rollingContainer.anchoredPosition = new Vector2(0f, currentY);
            }

            yield return null;
        }

        // Đảm bảo scale chuẩn sau khi xong vòng lặp trượt
        transform.localScale = _originalScale;

        // 3. Thực hiện trượt mượt mà từ vị trí bọc trên cùng (650f) về tâm (0f) với hiệu ứng nẩy cơ học (Back Ease Out)
        float snapElapsed = 0f;
        float snapDuration = 0.5f;
        Vector2 startPos = new Vector2(0f, 650f);
        Vector2 targetPos = Vector2.zero;

        while (snapElapsed < snapDuration)
        {
            snapElapsed += Time.unscaledDeltaTime;
            float t = snapElapsed / snapDuration;

            // Công thức Back Ease Out tạo độ nẩy nhẹ khi dừng cơ cấu bánh quay
            float tMinus1 = t - 1f;
            float s = 1.70158f;
            float easeT = tMinus1 * tMinus1 * ((s + 1f) * tMinus1 + s) + 1f;

            if (_rollingContainer != null)
            {
                _rollingContainer.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, easeT);
            }
            yield return null;
        }

        if (_rollingContainer != null)
        {
            _rollingContainer.anchoredPosition = Vector2.zero;
        }

        // Phát âm thanh dừng nếu có
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            // MyGame.Audio.AudioManager.Instance.PlaySFX(...) - tuỳ chọn
        }

        // 4. Hiệu ứng Squash & Stretch nhẹ trên thẻ root để tạo cảm giác cơ học đàn hồi
        float bounceElapsed = 0f;
        float bounceDuration = 0.22f;
        while (bounceElapsed < bounceDuration)
        {
            bounceElapsed += Time.unscaledDeltaTime;
            float t = bounceElapsed / bounceDuration;
            float scale = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            transform.localScale = _originalScale * scale;
            yield return null;
        }
        transform.localScale = _originalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_glowHighlight != null)
        {
            _glowHighlight.SetActive(true);
        }

        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale * _hoverScaleFactor, _scaleDuration));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_glowHighlight != null)
        {
            _glowHighlight.SetActive(false);
        }

        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale, _scaleDuration));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _onClickedCallback?.Invoke(_cardData);
        }
    }

    private IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            yield return null;
        }

        transform.localScale = targetScale;
        _scaleCoroutine = null;
    }
}
