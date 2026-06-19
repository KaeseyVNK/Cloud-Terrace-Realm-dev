using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the new day-night cycle HUD UI, animating the timeline pointer/slider 
/// and updating clock/day/state information in pixel-art style.
/// </summary>
public class HUDTimeUI : MonoBehaviour
{
    [Header("UI Text Components")]
    [SerializeField] private TMP_Text _dayText;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _stateText;

    [Header("Timeline Slider (Slider Component)")]
    [SerializeField] private Slider _timeSlider;

    [Header("Timeline Manual Setup (Fallback)")]
    [SerializeField] private RectTransform _progressBarRect;
    [SerializeField] private RectTransform _pointerHandleRect;

    [Header("State Colors")]
    [SerializeField] private Color _dayColor = new Color(0.95f, 0.8f, 0.2f);      // Golden yellow
    [SerializeField] private Color _nightColor = new Color(0.6f, 0.8f, 1.0f);     // Soft ice blue
    [SerializeField] private Color _bloodMoonColor = new Color(1.0f, 0.15f, 0.15f); // Vibrant red

    [Header("State Text Values")]
    [SerializeField] private string _dayStateText = "DAY";
    [SerializeField] private string _nightStateText = "NIGHT";
    [SerializeField] private string _bloodMoonStateText = "TRĂNG MÁU";

    private readonly System.Collections.Generic.Dictionary<TMP_Text, Coroutine> _activePunchCoroutines = new System.Collections.Generic.Dictionary<TMP_Text, Coroutine>();

    private void Start()
    {
        // Enforce correct anchoring for the manual pointer handle if used
        if (_pointerHandleRect != null)
        {
            _pointerHandleRect.anchorMin = new Vector2(0f, 0.5f);
            _pointerHandleRect.anchorMax = new Vector2(0f, 0.5f);
            _pointerHandleRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // Configure slider settings if used
        if (_timeSlider != null)
        {
            _timeSlider.minValue = 0f;
            _timeSlider.maxValue = 1f;
            _timeSlider.wholeNumbers = false;
        }

        // Register to time and weather events
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }

        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.OnWeatherChanged += HandleWeatherChanged;
        }

        UpdateDayText();
        UpdateStateText();
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }

        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.OnWeatherChanged -= HandleWeatherChanged;
        }
    }

    private void Update()
    {
        if (TimeManager.Instance == null) return;

        float ratio = TimeManager.Instance.GetTimeRatio();
        
        // 1. Update the Clock Text (HH:MM AM/PM, e.g. 17:06PM or 05:06PM)
        float clockHoursRaw = (ratio * 24f + 6f) % 24f; // Begins day at 6:00 AM
        int hours = Mathf.FloorToInt(clockHoursRaw);
        int minutes = Mathf.FloorToInt((clockHoursRaw - hours) * 60f);
        
        if (_timeText != null)
        {
            string ampm = hours >= 12 ? "PM" : "AM";
            int displayHours = hours % 12;
            if (displayHours == 0) displayHours = 12;
            _timeText.text = $"{displayHours:00}:{minutes:00}{ampm}";
        }

        // 2. Animate the Pointer Handle position or Slider value
        if (_timeSlider != null)
        {
            _timeSlider.value = ratio;
        }
        else if (_progressBarRect != null && _pointerHandleRect != null)
        {
            float width = _progressBarRect.rect.width;
            float targetX = ratio * width;
            _pointerHandleRect.anchoredPosition = new Vector2(targetX, _pointerHandleRect.anchoredPosition.y);
        }

        // 3. Pulsate the Blood Moon text (scale and alpha pulse) if active
        if (IsBloodMoonActive() && _stateText != null)
        {
            float pulse = 0.6f + Mathf.PingPong(Time.time * 1.5f, 0.4f);
            _stateText.color = new Color(_bloodMoonColor.r, _bloodMoonColor.g, _bloodMoonColor.b, pulse);

            // Tăng nhịp đập scale theo nhịp tim (nhịp 1.5s nhấp nhô từ 1.0 -> 1.08)
            float scalePulse = 1f + Mathf.PingPong(Time.time * 0.75f, 0.08f);
            _stateText.transform.localScale = Vector3.one * scalePulse;
        }
        else if (_stateText != null && !_activePunchCoroutines.ContainsKey(_stateText))
        {
            _stateText.transform.localScale = Vector3.one;
        }
    }

    private void HandleDayChanged(int day)
    {
        UpdateDayText();
        TriggerTimePunchScale(_dayText);
    }

    private void HandleDayNightChanged(bool isNight)
    {
        UpdateStateText();
    }

    private void HandleWeatherChanged(WeatherState state)
    {
        UpdateStateText();
    }

    private void UpdateDayText()
    {
        if (_dayText != null && TimeManager.Instance != null)
        {
            _dayText.text = $"Day {TimeManager.Instance.dayCount}";
        }
    }

    private void UpdateStateText()
    {
        if (_stateText == null) return;

        bool stateChanged = false;
        string newText = "";
        Color targetColor = Color.white;

        if (IsBloodMoonActive())
        {
            newText = _bloodMoonStateText;
            targetColor = _bloodMoonColor;
        }
        else if (TimeManager.Instance != null && TimeManager.Instance.IsNight)
        {
            newText = _nightStateText;
            targetColor = _nightColor;
        }
        else
        {
            newText = _dayStateText;
            targetColor = _dayColor;
        }

        if (_stateText.text != newText)
        {
            stateChanged = true;
        }

        _stateText.text = newText;
        if (!IsBloodMoonActive())
        {
            _stateText.color = targetColor;
        }

        if (stateChanged)
        {
            TriggerTimePunchScale(_stateText);
        }
    }

    private void TriggerTimePunchScale(TMP_Text text)
    {
        if (text == null) return;
        if (_activePunchCoroutines.TryGetValue(text, out Coroutine active))
        {
            StopCoroutine(active);
        }
        _activePunchCoroutines[text] = StartCoroutine(TimePunchScaleRoutine(text));
    }

    private IEnumerator TimePunchScaleRoutine(TMP_Text text)
    {
        Transform textTrans = text.transform;
        Vector3 originalScale = Vector3.one;
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            textTrans.localScale = originalScale * scaleMultiplier;
            yield return null;
        }

        textTrans.localScale = originalScale;
        _activePunchCoroutines.Remove(text);
    }

    private bool IsBloodMoonActive()
    {
        return WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.BloodMoon;
    }
}
