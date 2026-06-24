using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Đại diện cho một thẻ nâng cấp đơn lẻ trong giao diện chọn thẻ.
/// Hỗ trợ các hiệu ứng phóng to khi hover và phát sáng viền.
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

    private void Awake()
    {
        _originalScale = transform.localScale;
        if (_glowHighlight != null)
        {
            _glowHighlight.SetActive(false);
        }
    }

    public void Setup(UpgradeCardData cardData, Action<UpgradeCardData> onClicked)
    {
        _cardData = cardData;
        _onClickedCallback = onClicked;

        // Reset visual state
        transform.localScale = Vector3.zero; // Bắt đầu bằng 0 để chạy hiệu ứng xuất hiện
        if (_glowHighlight != null)
        {
            _glowHighlight.SetActive(false);
        }

        if (cardData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

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

    public void AnimatePopIn(float delay)
    {
        StartCoroutine(PopInRoutine(delay));
    }

    private IEnumerator PopInRoutine(float delay)
    {
        transform.localScale = Vector3.zero;
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay); // Sử dụng Realtime vì game đang bị Pause
        }

        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Sử dụng unscaledDeltaTime để chạy mượt mà khi Pause
            float t = elapsed / duration;

            // Back Ease Out formula
            float tMinus1 = t - 1f;
            float s = 1.70158f;
            float scale = tMinus1 * tMinus1 * ((s + 1f) * tMinus1 + s) + 1f;

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
