using UnityEngine;
using System.Collections;

/// <summary>
/// Controls the point light/torch for player units during Night or Rain.
/// Optimised using event-driven updates (no Update loop) and smooth fade-in/out.
/// </summary>
public class UnitLightController : MonoBehaviour
{
    private Light _pointLight;
    private float _targetIntensity = 5f;
    private Coroutine _fadeCoroutine;
    private bool _isLightOn = false;

    private void Start()
    {
        InitializeLight();
        RegisterEvents();
    }

    private void OnEnable()
    {
        // Khi được kích hoạt lại, cập nhật ngay lập tức trạng thái đèn không qua fade để khớp tức thì
        RefreshLightStateInstant();
    }

    private void OnDisable()
    {
        UnregisterEvents();
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        if (_pointLight != null)
        {
            _pointLight.intensity = 0f;
            _pointLight.enabled = false;
        }
        _isLightOn = false;
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    private void InitializeLight()
    {
        if (_pointLight == null)
        {
            // First check if there's already a Light component in children
            _pointLight = GetComponentInChildren<Light>(true);
            
            // If not found, dynamically spawn a point light
            if (_pointLight == null)
            {
                GameObject lightObj = new GameObject("UnitTorchLight");
                lightObj.transform.SetParent(transform, false);
                // Position slightly above the head / body center of standard unit prefabs
                lightObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                
                _pointLight = lightObj.AddComponent<Light>();
                _pointLight.type = LightType.Point;
                _pointLight.range = 6f; // Giảm xuống 6f để tối ưu vùng chiếu sáng ban đêm, giảm chồng chéo ánh sáng gây sụt FPS
                _pointLight.intensity = 0f; // Khởi đầu bằng 0 để fade
                _pointLight.color = new Color(1.0f, 0.65f, 0.3f); // Warm torch yellow/orange
                _pointLight.shadows = LightShadows.None; // Disabled for performance
            }
            else
            {
                _targetIntensity = _pointLight.intensity;
                _pointLight.intensity = 0f;
            }

            // Loại bỏ layer Grass khỏi cullingMask của đèn đuốc để tránh Additional Pass trên cỏ
            int grassLayer = LayerMask.NameToLayer("Grass");
            if (grassLayer != -1)
            {
                _pointLight.cullingMask &= ~(1 << grassLayer);
            }
        }
    }

    private void RegisterEvents()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.OnWeatherChanged += HandleWeatherChanged;
        }
    }

    private void UnregisterEvents()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.OnWeatherChanged -= HandleWeatherChanged;
        }
    }

    private void HandleDayNightChanged(bool isNight)
    {
        EvaluateAndTransitionLight();
    }

    private void HandleWeatherChanged(WeatherState state)
    {
        EvaluateAndTransitionLight();
    }

    private bool ShouldEnableLight()
    {
        bool isNight = TimeManager.Instance != null && TimeManager.Instance.IsNight;
        bool isRaining = WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain;
        return isNight || isRaining;
    }

    private void EvaluateAndTransitionLight()
    {
        if (_pointLight == null) return;

        bool shouldEnable = ShouldEnableLight();
        if (_isLightOn == shouldEnable) return;

        _isLightOn = shouldEnable;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        _fadeCoroutine = StartCoroutine(FadeLightRoutine(shouldEnable));
    }

    private void RefreshLightStateInstant()
    {
        if (_pointLight == null) return;

        bool shouldEnable = ShouldEnableLight();
        _isLightOn = shouldEnable;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _pointLight.enabled = shouldEnable;
        _pointLight.intensity = shouldEnable ? _targetIntensity : 0f;
    }

    private IEnumerator FadeLightRoutine(bool turnOn)
    {
        float duration = 1.2f; // Fade in/out trong 1.2 giây
        float elapsed = 0f;
        float startIntensity = _pointLight.intensity;
        float target = turnOn ? _targetIntensity : 0f;

        if (turnOn)
        {
            _pointLight.enabled = true;
        }

        while (elapsed < duration)
        {
            if (_pointLight == null) yield break;
            _pointLight.intensity = Mathf.Lerp(startIntensity, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_pointLight != null)
        {
            _pointLight.intensity = target;
            if (!turnOn)
            {
                _pointLight.enabled = false;
            }
        }
        _fadeCoroutine = null;
    }
}
