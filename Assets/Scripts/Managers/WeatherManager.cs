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

            // Ban ngày: Có tỉ lệ đổ mưa
            if (UnityEngine.Random.value <= _rainChance)
            {
                SetWeather(WeatherState.Rain);
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
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.AncientRelic, 1);
            Debug.Log("[WeatherManager] Đã sống sót qua Trăng Máu! Nhận 1 Cổ vật Cổ đại.");
            
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowBloodMoonAlert(
                    "SỐNG SÓT THÀNH CÔNG",
                    "Bạn đã vượt qua đêm Trăng Máu và nhận được 1 Cổ Vật Cổ Đại! 🏆",
                    5f
                );
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
