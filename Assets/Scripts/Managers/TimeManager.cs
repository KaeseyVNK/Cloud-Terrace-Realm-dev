using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings")]
    public float dayDuration = 120f; // 1 ngày = 120 giây
    public float currentTime = 0f;
    public int dayCount = 1; // Số ngày sinh tồn
    [Range(0f, 1f)]
    public float nightStartRatio = 0.55f; // Đêm bắt đầu trễ hơn (55% chu kỳ ngày)

    // Sự kiện khi chuyển đổi Ngày / Đêm
    public event Action<bool> OnDayNightChanged;
    
    // Sự kiện khi ngày mới bắt đầu
    public event Action<int> OnDayChanged;
    
    public bool IsNight { get; private set; } = false;

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

    void Update()
    {
        currentTime += Time.deltaTime;
        if (currentTime >= dayDuration) 
        {
            currentTime = 0f;
            dayCount++;
            OnDayChanged?.Invoke(dayCount);
        }

        // Cập nhật trạng thái Ngày/Đêm
        float ratio = currentTime / dayDuration;
        bool currentlyIsNight = (ratio >= nightStartRatio);

        if (currentlyIsNight != IsNight)
        {
            IsNight = currentlyIsNight;
            OnDayNightChanged?.Invoke(IsNight);
        }
    }

    public float GetTimeRatio()
    {
        return currentTime / dayDuration;
    }
}