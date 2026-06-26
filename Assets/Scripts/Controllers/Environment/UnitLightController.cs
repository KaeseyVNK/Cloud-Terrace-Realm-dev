using UnityEngine;
using System.Collections;

/// <summary>
/// Controls the point light/torch for player units during Night or Rain.
/// Optimised using event-driven updates (no Update loop) and smooth fade-in/out.
/// </summary>
public class UnitLightController : MonoBehaviour
{
    [Header("Fake Light (Vòng Sáng Dưới Đất)")]
    [SerializeField] private bool _useFakeLight = true;
    [SerializeField] private GameObject _fakeLightVisual;

    [Header("Character Light (Chiếu Sáng Nhân Vật)")]
    [SerializeField] private bool _useCharacterPointLight = true;
    [SerializeField] private float _characterLightRange = 3f;
    [SerializeField] private float _characterLightIntensity = 2f;

    private Light _pointLight;
    private float _targetIntensity = 2f;
    private Coroutine _fadeCoroutine;
    private bool _isLightOn = false;
    private Vector3 _baseFakeLightScale = Vector3.one;

    private Camera _mainCamera;
    private bool _isInCameraView = true;
    private Coroutine _cullingCoroutine;

    private void Awake()
    {
        InitializeLight();
    }

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        RegisterEvents();
        _isLightOn = false;
        _isInCameraView = true;
        EvaluateAndTransitionLight();
        
        if (_cullingCoroutine != null)
        {
            StopCoroutine(_cullingCoroutine);
        }
        _cullingCoroutine = StartCoroutine(CameraCullingRoutine());
    }

    private void OnDisable()
    {
        UnregisterEvents();
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        if (_cullingCoroutine != null)
        {
            StopCoroutine(_cullingCoroutine);
            _cullingCoroutine = null;
        }
        if (_pointLight != null)
        {
            _pointLight.intensity = 0f;
            _pointLight.enabled = false;
        }
        if (_fakeLightVisual != null)
        {
            _fakeLightVisual.transform.localScale = Vector3.zero;
            _fakeLightVisual.SetActive(false);
        }
        _isLightOn = false;
    }

    private IEnumerator CameraCullingRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0f, 0.25f));
        while (true)
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera != null)
            {
                Vector3 viewportPos = _mainCamera.WorldToViewportPoint(transform.position);
                bool inView = viewportPos.x >= -0.1f && viewportPos.x <= 1.1f && viewportPos.y >= -0.1f && viewportPos.y <= 1.1f && viewportPos.z > 0f;
                if (_isInCameraView != inView)
                {
                    _isInCameraView = inView;
                    EvaluateCullState();
                }
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void EvaluateCullState()
    {
        bool shouldEnable = ShouldEnableLight() && _isInCameraView;
        if (_isLightOn == shouldEnable) return;
        _isLightOn = shouldEnable;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_useFakeLight && _fakeLightVisual != null)
        {
            _fakeLightVisual.SetActive(shouldEnable);
            _fakeLightVisual.transform.localScale = shouldEnable ? _baseFakeLightScale : Vector3.zero;
        }

        if (_pointLight != null)
        {
            bool enableRealLight = shouldEnable && _useCharacterPointLight;
            _pointLight.enabled = enableRealLight;
            _pointLight.intensity = enableRealLight ? _targetIntensity : 0f;
        }
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    private void InitializeLight()
    {
        // Khởi tạo vòng sáng giả dưới đất
        if (_fakeLightVisual == null)
        {
            Transform child = transform.Find("FakeLight");
            if (child != null)
            {
                _fakeLightVisual = child.gameObject;
            }
        }

        if (_fakeLightVisual != null)
        {
            _baseFakeLightScale = _fakeLightVisual.transform.localScale;
            _fakeLightVisual.SetActive(false);
        }

        // Khởi tạo đèn Point Light chiếu sáng riêng cho model nhân vật
        if (_pointLight == null)
        {
            _pointLight = GetComponentInChildren<Light>(true);
            
            if (_pointLight == null && _useCharacterPointLight)
            {
                GameObject lightObj = new GameObject("UnitTorchLight");
                lightObj.transform.SetParent(transform, false);
                // Đặt đèn ngang tầm ngực/thân trên nhân vật
                lightObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                
                _pointLight = lightObj.AddComponent<Light>();
                _pointLight.type = LightType.Point;
                _pointLight.range = _characterLightRange;
                _pointLight.intensity = 0f;
                _pointLight.color = new Color(1.0f, 0.7f, 0.4f); // Vàng ấm
                _pointLight.shadows = LightShadows.None; // Không đổ bóng để tối ưu FPS
            }
            
            if (_pointLight != null)
            {
                _targetIntensity = _useCharacterPointLight ? _characterLightIntensity : _pointLight.intensity;
                _pointLight.intensity = 0f;
                _pointLight.range = _useCharacterPointLight ? _characterLightRange : _pointLight.range;

                // Tối ưu hóa cullingMask của đèn trên nhân vật:
                // CHỈ chiếu sáng các layer Unit (layer 6) và Unit Enemy (layer 8).
                // Loại bỏ hoàn toàn Default (Terrain), Grass, Resource, Buiding để tránh Additional Pass cực nặng.
                int unitLayer = LayerMask.NameToLayer("Unit");
                int enemyLayer = LayerMask.NameToLayer("Unit Enemy");
                int mask = 0;
                if (unitLayer != -1) mask |= (1 << unitLayer);
                if (enemyLayer != -1) mask |= (1 << enemyLayer);
                
                _pointLight.cullingMask = mask;
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
        bool shouldEnable = ShouldEnableLight() && _isInCameraView;
        if (_isLightOn == shouldEnable) return;

        _isLightOn = shouldEnable;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (shouldEnable)
        {
            _fadeCoroutine = StartCoroutine(FadeLightRoutine(true));
        }
        else
        {
            // Tắt từ từ khi trời sáng
            _fadeCoroutine = StartCoroutine(FadeLightRoutine(false));
        }
    }

    private void RefreshLightStateInstant()
    {
        bool shouldEnable = ShouldEnableLight() && _isInCameraView;
        _isLightOn = shouldEnable;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_useFakeLight && _fakeLightVisual != null)
        {
            _fakeLightVisual.SetActive(shouldEnable);
            _fakeLightVisual.transform.localScale = shouldEnable ? _baseFakeLightScale : Vector3.zero;
        }
        if (_pointLight != null)
        {
            bool enableRealLight = shouldEnable && _useCharacterPointLight;
            _pointLight.enabled = enableRealLight;
            _pointLight.intensity = enableRealLight ? _targetIntensity : 0f;
        }
    }

    private IEnumerator FadeLightRoutine(bool turnOn)
    {
        float duration = 1.2f; // Fade in/out trong 1.2 giây
        float elapsed = 0f;
        float startValue = turnOn ? 0f : 1f;
        float targetValue = turnOn ? 1f : 0f;

        float startIntensity = _pointLight != null ? _pointLight.intensity : 0f;
        float targetIntensity = turnOn ? _targetIntensity : 0f;

        if (turnOn)
        {
            if (_useFakeLight && _fakeLightVisual != null)
            {
                _fakeLightVisual.SetActive(true);
                _fakeLightVisual.transform.localScale = Vector3.zero;
            }
            if (_useCharacterPointLight && _pointLight != null)
            {
                _pointLight.enabled = true;
            }
        }

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float currentFactor = Mathf.Lerp(startValue, targetValue, progress);

            if (_useFakeLight && _fakeLightVisual != null)
            {
                _fakeLightVisual.transform.localScale = _baseFakeLightScale * currentFactor;
            }
            if (_useCharacterPointLight && _pointLight != null)
            {
                _pointLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, progress);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_useFakeLight && _fakeLightVisual != null)
        {
            _fakeLightVisual.transform.localScale = _baseFakeLightScale * targetValue;
            if (!turnOn)
            {
                _fakeLightVisual.SetActive(false);
            }
        }
        if (_pointLight != null)
        {
            if (_useCharacterPointLight)
            {
                _pointLight.intensity = targetIntensity;
                if (!turnOn)
                {
                    _pointLight.enabled = false;
                }
            }
            else
            {
                _pointLight.enabled = false;
                _pointLight.intensity = 0f;
            }
        }
        _fadeCoroutine = null;
    }
}
