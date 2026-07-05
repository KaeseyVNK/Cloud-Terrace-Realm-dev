using System;
using UnityEngine;

public enum WeatherState
{
    Clear,
    Rain,
    BloodMoon,
    Drought
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

    [Tooltip("Tỉ lệ hạn hán vào ban ngày (0.15 = 15%)")]
    [Range(0f, 1f)]
    [SerializeField] private float _droughtChance = 0.15f;

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
            GameLog.LogError("[WeatherManager] Không tìm thấy TimeManager.Instance!");
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
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowBloodMoonAlert(
                        "⚠️ ĐÊM TRĂNG MÁU BẮT ĐẦU ⚠️",
                        "Kẻ địch trở nên to lớn hơn, di chuyển nhanh hơn và cực kỳ hung dữ!",
                        5f
                    );
                }
            }
            else
            {
                SetWeather(WeatherState.Clear);
            }
        }
        else
        {
            // Kết thúc đêm: Nếu đêm vừa qua là Trăng Máu -> Trao thưởng
            if (_currentWeather == WeatherState.BloodMoon)
            {
                AwardBloodMoonSurvivalReward();
            }

            // Ban ngày: Có tỉ lệ đổ mưa hoặc nắng hạn
            float rand = UnityEngine.Random.value;
            if (rand <= _rainChance)
            {
                SetWeather(WeatherState.Rain);
            }
            else if (rand <= _rainChance + _droughtChance)
            {
                SetWeather(WeatherState.Drought);
            }
            else
            {
                SetWeather(WeatherState.Clear);
            }

            // Cảnh báo sớm nếu đêm tiếp theo là Trăng Máu
            if ((_nightCount + 1) % _bloodMoonCycle == 0)
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowBloodMoonAlert(
                        "CẢNH BÁO TRĂNG MÁU",
                        "Đêm nay Trăng Máu sẽ xuất hiện! Hãy chuẩn bị tháp canh và lính phòng thủ ngay lập tức!",
                        6f
                    );
                }
            }
        }
    }

    private void AwardBloodMoonSurvivalReward()
    {
        GameLog.Log("[WeatherManager] Đã sống sót qua Trăng Máu! Kích hoạt chọn thẻ nâng cấp.");
        
        if (CardManager.Instance != null)
        {
            CardManager.Instance.TriggerCardDraft();
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                "SỐNG SÓT THÀNH CÔNG",
                "Bạn đã vượt qua đêm Trăng Máu! Hãy chọn một Thẻ Nâng Cấp bổ sung! 🏆",
                5f
            );
        }
    }

    public void SetWeather(WeatherState newWeather)
    {
        if (_currentWeather == newWeather) return;

        _currentWeather = newWeather;
        GameLog.Log($"[WeatherManager] Thời tiết chuyển sang: {_currentWeather}");
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

    [ContextMenu("Force Drought")]
    public void ForceDrought()
    {
        SetWeather(WeatherState.Drought);
    }
}
