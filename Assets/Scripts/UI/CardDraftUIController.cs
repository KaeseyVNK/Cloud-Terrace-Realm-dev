using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;



/// <summary>
/// Quản lý giao diện chọn thẻ nâng cấp.
/// Hỗ trợ mờ nền và hiệu ứng xuất hiện các thẻ.
/// </summary>
public class CardDraftUIController : MonoBehaviour
{
    public static CardDraftUIController Instance { get; private set; }

    [Header("Panel Settings")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Card Slots")]
    [SerializeField] private List<CardDraftSlotUI> _cardSlots = new List<CardDraftSlotUI>();

    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        SetPanelActive(false);
    }

    public void OpenMenu(List<UpgradeCardData> cards)
    {
        SetPanelActive(true);

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(true, 0.3f));

        // Play card draft/roll sound
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayCardDraft();
        }

        // Populate slots
        int numSlots = _cardSlots.Count;
        for (int i = 0; i < numSlots; i++)
        {
            if (i < cards.Count)
            {
                _cardSlots[i].gameObject.SetActive(true);
                _cardSlots[i].Setup(cards[i], OnCardSelected, i);
                _cardSlots[i].AnimatePopIn(i * 0.08f, i); // Hiệu ứng quay thẻ so le
            }
            else
            {
                _cardSlots[i].gameObject.SetActive(false);
            }
        }
    }

    public void CloseMenu()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(false, 0.2f));
    }

    private void OnCardSelected(UpgradeCardData selectedCard)
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayCardSelect();
        }

        if (CardManager.Instance != null)
        {
            CardManager.Instance.ApplyCard(selectedCard);
        }
        CloseMenu();
    }

    private void SetPanelActive(bool active)
    {
        if (_panelRoot != null)
        {
            if (_panelRoot != gameObject || active)
            {
                _panelRoot.SetActive(active);
            }
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f; // Luôn khởi đầu bằng 0f để FadeRoutine thực hiện hiệu ứng mờ/tỏ
            _canvasGroup.interactable = active;
            _canvasGroup.blocksRaycasts = active;
        }
    }

    private IEnumerator FadeRoutine(bool show, float duration)
    {
        if (show)
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
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
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Unscaled vì đang Pause
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
            }
            yield return null;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = targetAlpha;
        }

        if (!show && _panelRoot != null)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
        }

        _fadeCoroutine = null;
    }
}
