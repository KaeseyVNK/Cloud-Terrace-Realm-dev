using UnityEngine;
using System.Collections;

/// <summary>
/// Điều khiển cột đuốc TorchStand. Tự động bật sáng khi đêm xuống hoặc trời mưa,
/// đi kèm hiệu ứng đuốc lửa bập bùng (flicker) để tạo cảm giác chân thực.
/// </summary>
public class TorchStandController : MonoBehaviour
{
    [Header("Cấu hình Đèn")]
    [SerializeField] private Light _torchLight;
    [SerializeField] private GameObject _fireVisual; // Hiệu ứng lửa hạt (particle)
    [SerializeField] private bool _useFakeLight = true;
    [SerializeField] private GameObject _fakeLightVisual; // Vòng sáng giả dưới mặt đất

    [Header("Đèn Đuốc Tối Ưu (Chiếu Thân & Unit)")]
    [SerializeField] private bool _useBuildingPointLight = true;
    [SerializeField] private float _buildingLightRange = 20f;
    [SerializeField] private float _buildingLightIntensity = 50f;

    [Header("Hiệu ứng Bập bùng (Flicker)")]
    [SerializeField] private float _flickerSpeed = 6f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _flickerRange = 0.15f;

    private ConstructibleBuilding _building;
    private float _baseIntensity = 4.5f;
    private bool _isCompletedCached = false;
    private Coroutine _flickerCoroutine;
    private Vector3 _baseFakeLightScale = Vector3.one;
    private bool _isLightOn = false;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _building = GetComponent<ConstructibleBuilding>();
        if (_torchLight == null)
        {
            _torchLight = GetComponentInChildren<Light>(true);
        }

        if (_torchLight != null)
        {
            _baseIntensity = _torchLight.intensity;
            _torchLight.enabled = false;

            // Cấu hình culling mask tối ưu cho Đuốc:
            // CHỈ chiếu sáng Building (layer 10), Unit (layer 6) và Unit Enemy (layer 8).
            // Loại bỏ Terrain (Default), Grass để giữ FPS cực cao.
            int buildingLayer = LayerMask.NameToLayer("Buiding"); // Spelling check: "Buiding"
            int unitLayer = LayerMask.NameToLayer("Unit");
            int enemyLayer = LayerMask.NameToLayer("Unit Enemy");
            int mask = 0;
            if (buildingLayer != -1) mask |= (1 << buildingLayer);
            if (unitLayer != -1) mask |= (1 << unitLayer);
            if (enemyLayer != -1) mask |= (1 << enemyLayer);

            _torchLight.cullingMask = mask;

            if (_useBuildingPointLight)
            {
                _torchLight.range = _buildingLightRange;
                _baseIntensity = _buildingLightIntensity;
            }
        }

        if (_fireVisual != null)
        {
            _fireVisual.SetActive(false);
        }

        if (_fakeLightVisual != null)
        {
            _baseFakeLightScale = _fakeLightVisual.transform.localScale;
            _fakeLightVisual.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.OnWeatherChanged += HandleWeatherChanged;
        }

        _isCompletedCached = _building != null && _building.IsCompleted;
        _isLightOn = false;
        UpdateTorchState();
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }
        if (WeatherManager.Instance != null)
        {
            WeatherManager.Instance.OnWeatherChanged -= HandleWeatherChanged;
        }
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        StopFlicker();
        _isLightOn = false;
    }

    private void Update()
    {
        // Tối ưu: Chỉ chạy Update để quét tiến độ nếu công trình chưa hoàn thành
        if (!_isCompletedCached)
        {
            if (_building == null || _building.IsCompleted)
            {
                _isCompletedCached = true;
                UpdateTorchState();
            }
        }
    }

    private void HandleDayNightChanged(bool isNight)
    {
        UpdateTorchState();
    }

    private void HandleWeatherChanged(WeatherState state)
    {
        UpdateTorchState();
    }

    private void UpdateTorchState()
    {
        bool isCompleted = _building == null || _building.IsCompleted;
        bool isNight = TimeManager.Instance != null && TimeManager.Instance.IsNight;
        bool isRaining = WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain;

        bool shouldLight = isCompleted && (isNight || isRaining);

        if (_isLightOn == shouldLight) return;
        _isLightOn = shouldLight;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        StopFlicker();

        if (shouldLight)
        {
            _fadeCoroutine = StartCoroutine(FadeLightRoutine(true));
        }
        else
        {
            // Tắt từ từ khi trời sáng
            _fadeCoroutine = StartCoroutine(FadeLightRoutine(false));
        }
    }

    private IEnumerator FadeLightRoutine(bool turnOn)
    {
        float duration = 1.5f; // Rộng ra từ từ trong 1.5 giây
        float elapsed = 0f;
        float startFactor = turnOn ? 0f : 1f;
        float targetFactor = turnOn ? 1f : 0f;

        float startIntensity = _torchLight != null ? _torchLight.intensity : 0f;
        float targetIntensity = turnOn ? _baseIntensity : 0f;

        if (turnOn)
        {
            if (_useFakeLight && _fakeLightVisual != null)
            {
                _fakeLightVisual.SetActive(true);
                _fakeLightVisual.transform.localScale = Vector3.zero;
            }
            if (_useBuildingPointLight && _torchLight != null)
            {
                _torchLight.enabled = true;
                _torchLight.intensity = 0f;
            }
            if (_fireVisual != null)
            {
                _fireVisual.SetActive(true);
            }
        }

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float t = Mathf.SmoothStep(0f, 1f, progress);
            float currentFactor = Mathf.Lerp(startFactor, targetFactor, t);

            if (_useFakeLight && _fakeLightVisual != null)
            {
                _fakeLightVisual.transform.localScale = _baseFakeLightScale * currentFactor;
            }
            if (_useBuildingPointLight && _torchLight != null)
            {
                _torchLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_useFakeLight && _fakeLightVisual != null)
        {
            _fakeLightVisual.transform.localScale = _baseFakeLightScale * targetFactor;
            if (!turnOn)
            {
                _fakeLightVisual.SetActive(false);
            }
        }
        if (_torchLight != null)
        {
            if (_useBuildingPointLight)
            {
                _torchLight.intensity = targetIntensity;
                if (!turnOn)
                {
                    _torchLight.enabled = false;
                }
            }
            else
            {
                _torchLight.enabled = false;
                _torchLight.intensity = 0f;
            }
        }
        if (_fireVisual != null && !turnOn)
        {
            _fireVisual.SetActive(false);
        }

        _fadeCoroutine = null;

        if (turnOn)
        {
            StartFlicker();
        }
    }

    private void StartFlicker()
    {
        // Bảo vệ: Không chạy coroutine trên đối tượng không hoạt động (tránh lỗi Active/Inactive)
        if (!gameObject.activeInHierarchy || !enabled) return;

        if (_flickerCoroutine == null)
        {
            _flickerCoroutine = StartCoroutine(FlickerRoutine());
        }
    }

    private void StopFlicker()
    {
        if (_flickerCoroutine != null)
        {
            StopCoroutine(_flickerCoroutine);
            _flickerCoroutine = null;
        }
        if (_torchLight != null)
        {
            _torchLight.intensity = _baseIntensity;
        }
        if (_fakeLightVisual != null)
        {
            _fakeLightVisual.transform.localScale = _baseFakeLightScale;
        }
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // Tạo noise ngẫu nhiên mô phỏng ngọn lửa dao động trước gió
            float noise = Mathf.PerlinNoise(Time.time * _flickerSpeed, 0f);
            float factor = 1f + Mathf.Lerp(-_flickerRange, _flickerRange, noise);

            if (_useFakeLight && _fakeLightVisual != null && _fakeLightVisual.activeSelf)
            {
                // Bập bùng cho Fake Light bằng cách đổi nhẹ Scale của Quad
                _fakeLightVisual.transform.localScale = _baseFakeLightScale * factor;
            }
            else if (_torchLight != null && _torchLight.enabled)
            {
                _torchLight.intensity = _baseIntensity * factor;
            }

            yield return new WaitForSeconds(0.08f); // Quét ở chu kỳ 12.5 FPS để tối ưu hóa hiệu năng
        }
    }
}
