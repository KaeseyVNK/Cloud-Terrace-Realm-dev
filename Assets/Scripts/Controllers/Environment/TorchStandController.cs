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

    [Header("Hiệu ứng Bập bùng (Flicker)")]
    [SerializeField] private float _flickerSpeed = 6f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _flickerRange = 0.15f;

    private ConstructibleBuilding _building;
    private float _baseIntensity = 4.5f;
    private bool _isCompletedCached = false;
    private Coroutine _flickerCoroutine;

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
        }

        if (_fireVisual != null)
        {
            _fireVisual.SetActive(false);
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
        StopFlicker();
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

        if (_torchLight != null)
        {
            if (_torchLight.enabled != shouldLight)
            {
                _torchLight.enabled = shouldLight;
                _torchLight.intensity = _baseIntensity;
            }

            if (shouldLight)
            {
                StartFlicker();
            }
            else
            {
                StopFlicker();
            }
        }

        if (_fireVisual != null && _fireVisual.activeSelf != shouldLight)
        {
            _fireVisual.SetActive(shouldLight);
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
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            if (_torchLight != null && _torchLight.enabled)
            {
                // Tạo noise ngẫu nhiên mô phỏng ngọn lửa dao động trước gió
                float noise = Mathf.PerlinNoise(Time.time * _flickerSpeed, 0f);
                float factor = 1f + Mathf.Lerp(-_flickerRange, _flickerRange, noise);
                _torchLight.intensity = _baseIntensity * factor;
            }
            yield return new WaitForSeconds(0.08f); // Quét ở chu kỳ 12.5 FPS để tối ưu hóa hiệu năng
        }
    }
}
