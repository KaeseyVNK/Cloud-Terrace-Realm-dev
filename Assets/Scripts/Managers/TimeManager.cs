using System;
using UnityEngine;

public class TimeManager : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
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
            EnsureSaveableEntity("Global_TimeManager");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EnsureSaveableEntity(string saveID)
    {
        var saveable = GetComponent<CloudTerraceRealm.SaveSystem.SaveableEntity>();
        if (saveable == null)
        {
            saveable = gameObject.AddComponent<CloudTerraceRealm.SaveSystem.SaveableEntity>();
            var field = typeof(CloudTerraceRealm.SaveSystem.SaveableEntity).GetField("_saveID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(saveable, saveID);
            }
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

    [Serializable]
    private class TimeSaveState
    {
        public float currentTime;
        public int dayCount;
        public bool isNight;
    }

    public string CaptureState()
    {
        TimeSaveState state = new TimeSaveState
        {
            currentTime = this.currentTime,
            dayCount = this.dayCount,
            isNight = this.IsNight
        };
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        TimeSaveState state = JsonUtility.FromJson<TimeSaveState>(stateJson);
        if (state == null) return;

        this.currentTime = state.currentTime;
        this.dayCount = state.dayCount;
        this.IsNight = state.isNight;
        
        OnDayChanged?.Invoke(dayCount);
        OnDayNightChanged?.Invoke(IsNight);
    }
}