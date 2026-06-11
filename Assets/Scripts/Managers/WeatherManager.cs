using System;
using UnityEngine;

public enum WeatherState
{
    Clear,
    Rain,
    BloodMoon
}

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    [Header("Weather Settings")]
    [SerializeField] private WeatherState _currentWeather = WeatherState.Clear;
    public WeatherState CurrentWeather => _currentWeather;

    [Tooltip("Tỉ lệ đổ mưa vào ban ngày (0.25 = 25%)")]
    [Range(0f, 1f)]
    [SerializeField] private float _rainChance = 0.25f;

    [Tooltip("Chu kỳ đêm trăng máu (mỗi 5 đêm)")]
    [SerializeField] private int _bloodMoonCycle = 5;

    public event Action<WeatherState> OnWeatherChanged;

    private int _nightCount = 0;
    public int NightCount => _nightCount;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }
        else
        {
            Debug.LogError("[WeatherManager] Không tìm thấy TimeManager.Instance!");
        }
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }
    }

    private void HandleDayNightChanged(bool isNight)
    {
        if (isNight)
        {
            _nightCount++;
            if (_nightCount % _bloodMoonCycle == 0)
            {
                SetWeather(WeatherState.BloodMoon);
            }
            else
            {
                SetWeather(WeatherState.Clear);
            }
        }
        else
        {
            // Ban ngày: Có tỉ lệ đổ mưa
            if (UnityEngine.Random.value <= _rainChance)
            {
                SetWeather(WeatherState.Rain);
            }
            else
            {
                SetWeather(WeatherState.Clear);
            }
        }
    }

    public void SetWeather(WeatherState newWeather)
    {
        if (_currentWeather == newWeather) return;

        _currentWeather = newWeather;
        Debug.Log($"[WeatherManager] Thời tiết chuyển sang: {_currentWeather}");
        OnWeatherChanged?.Invoke(_currentWeather);
    }

    [ContextMenu("Force Blood Moon")]
    public void ForceBloodMoon()
    {
        SetWeather(WeatherState.BloodMoon);
    }

    [ContextMenu("Force Rain")]
    public void ForceRain()
    {
        SetWeather(WeatherState.Rain);
    }

    [ContextMenu("Force Clear")]
    public void ForceClear()
    {
        SetWeather(WeatherState.Clear);
    }
}
