using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyWaveEntry
{
    public string name = "Enemy";
    public GameObject prefab;
    public int baseCount = 3;
    public int increasePerNight = 1;
    public int maxCount = 20;
}

[System.Serializable]
public class EnemyRosterEntry
{
    public string name = "Enemy";
    public GameObject prefab;
    [Min(1)] public int unlockDay = 1;
    [Min(0f)] public float weight = 1f;
    [Min(0)] public int maxCountPerWave = 20;
}

[System.Serializable]
public class DayWaveConfig
{
    [Tooltip("Đêm cụ thể áp dụng cấu hình này (1, 2, 3...). Nhập 0 nếu muốn làm wave mặc định cho các đêm không có cấu hình riêng.")]
    public int specificNight = 0;

    public string waveName = "Midnight Raid";

    [Tooltip("Thời điểm xuất hiện trong đêm (0.5 = đầu tối, 0.75 = nửa đêm, 0.9 = rạng sáng)")]
    [Range(0.5f, 0.99f)]
    public float spawnTimeRatio = 0.75f;

    [Tooltip("Hệ số nhân lượng quái cho wave này")]
    [Range(0.1f, 5f)]
    public float countMultiplier = 1f;

    [Tooltip("Danh sách các loại quái xuất hiện trong đợt này")]
    public List<EnemyWaveEntry> enemies = new List<EnemyWaveEntry>();
}

public class EnemyManager : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
{
    private static EnemyManager s_instance;
    public static EnemyManager Instance => s_instance;

    [Header("Enemy Data Assets")]
    [Tooltip("Optional roster asset. If assigned, this replaces the inline enemy roster at runtime.")]
    [SerializeField] private EnemyRosterData _enemyRosterData;
    [Tooltip("Optional designed wave override asset. Matching night waves replace auto progression when configured.")]
    [SerializeField] private WaveConfigData _waveConfigData;
    [Tooltip("Optional difficulty/settings asset. If assigned, it drives enemy count, scaling, events, and blood moon multipliers.")]
    [SerializeField] private EnemyDifficultySettings _difficultySettings;

    [Header("Fallback Prefabs")]
    [Tooltip("Chỉ dùng khi EnemyRoster asset thiếu prefab hoặc chưa được gán.")]
    [SerializeField] private GameObject _enemyPrefab;
    public GameObject EnemyPrefab
    {
        get => _enemyPrefab;
        set => _enemyPrefab = value;
    }

    [Tooltip("Chỉ dùng để tạo roster fallback khi EnemyRoster asset chưa có dữ liệu.")]
    [SerializeField] private GameObject _enemyArcherPrefab;
    public GameObject EnemyArcherPrefab
    {
        get => _enemyArcherPrefab;
        set => _enemyArcherPrefab = value;
    }


    private int _baseEnemyCount = 3;
    public int BaseEnemyCount
    {
        get => _baseEnemyCount;
        set => _baseEnemyCount = value;
    }

    private int _enemyIncreasePerNight = 2;
    public int EnemyIncreasePerNight
    {
        get => _enemyIncreasePerNight;
        set => _enemyIncreasePerNight = value;
    }

    private int _maxEnemiesPerWave = 30;
    public int MaxEnemiesPerWave
    {
        get => _maxEnemiesPerWave;
        set => _maxEnemiesPerWave = value;
    }

    private int _maxActiveEnemies = 60;
    public int MaxActiveEnemies
    {
        get => _maxActiveEnemies;
        set => _maxActiveEnemies = value;
    }

    private int _maxActiveSummonedEnemies = 18;
    public int MaxActiveSummonedEnemies
    {
        get => _maxActiveSummonedEnemies;
        set => _maxActiveSummonedEnemies = Mathf.Max(0, value);
    }

    private float _spawnRadius = 4f;
    public float SpawnRadius
    {
        get => _spawnRadius;
        set => _spawnRadius = value;
    }

    private int _spawnEdgeCandidateCount = 12;
    private float _minSpawnDistanceFromCamera = 60f;
    private bool _avoidRepeatingSpawnEdge = true;

    private float _groupRallyRadius = 5f;
    private float _groupRallyMinReadyRatio = 0.65f;
    private float _groupRallyMaxWait = 12f;

    private bool _useCustomWaveOverrides = false;
    private List<DayWaveConfig> _customWaves = new List<DayWaveConfig>();

    private List<EnemyRosterEntry> _enemyRoster = new List<EnemyRosterEntry>();
    private int _waveIncreaseEveryNights = 3;
    private int _maxAutoWavesPerNight = 6;
    private float _autoWaveStartTimeRatio = 0.55f;
    private float _autoWaveEndTimeRatio = 0.9f;
    private float _bloodMoonWaveMultiplier = 1.75f;
    private float _bloodMoonCountMultiplier = 1.6f;

    private int _minorEventStartNight = 5;
    private int _dangerEventStartNight = 10;
    private float _minorNightEventChance = 0.12f;
    private float _dangerNightEventChance = 0.08f;
    private float _minorDayEventChance = 0.08f;
    private float _dangerDayEventChance = 0.05f;

    private int _enemyPoolPrewarmCount = 24;

    private GridSystem _gridSystem;
    private int _currentNightNumber = 0;
    public int CurrentNightNumber => _currentNightNumber;

    private List<EnemyUnitController> _activeEnemies = new List<EnemyUnitController>();
    public List<EnemyUnitController> ActiveEnemies => _activeEnemies;
    private float _nextCleanupTime = 0f;
    private const float CleanupCooldown = 0.5f;
    private bool _nightRaidPending = false;
    private bool[] _spawnedWavesThisNight = new bool[0];
    private bool _dayRainRaidSpawned = false;
    private bool _daySpecialEventSpawned = false;
    private List<DayWaveConfig> _activeNightWaves = new List<DayWaveConfig>();
    private EnemyStatsScaler _statsScaler;
    private EnemySpawnPositionFinder _spawnPositionFinder;
    private EnemyGroupRallyController _groupRallyController;
    private EnemyWaveScheduler _waveScheduler;


    // ===== Unity lifecycle =====
    void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSaveableEntity("Global_EnemyManager");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _gridSystem = FindAnyObjectByType<GridSystem>();
        EnsureSpawnPositionFinder();
        EnsureGroupRallyController();
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

    void Start()
    {
        // Đảm bảo có enemyPrefab
        if (_enemyPrefab == null)
        {
#if UNITY_EDITOR
            _enemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Enemy/Skeleton_Warrior.prefab");
            if (_enemyPrefab != null)
            {
                GameLog.Log($"[EnemyManager] Tự động tải thành công prefab kẻ địch: {_enemyPrefab.name}");
            }
            else
            {
                GameLog.LogWarning("[EnemyManager] Không tìm thấy prefab Skeleton_Warrior tại Assets/Prefabs/Unit/Enemy/Skeleton_Warrior.prefab!");
            }
#endif
        }

        // Đảm bảo có enemyArcherPrefab
        if (_enemyArcherPrefab == null)
        {
#if UNITY_EDITOR
            _enemyArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Enemy/EnemyArcher.prefab");
            if (_enemyArcherPrefab != null)
            {
                GameLog.Log($"[EnemyManager] Tự động tải thành công prefab EnemyArcher: {_enemyArcherPrefab.name}");
            }
            else
            {
                GameLog.LogWarning("[EnemyManager] Không tìm thấy prefab EnemyArcher tại Assets/Prefabs/Unit/Enemy/EnemyArcher.prefab!");
            }
#endif
        }

        if (_enemyPrefab != null && _enemyPoolPrewarmCount > 0)
        {
            PoolManager.Instance.Prewarm(_enemyPrefab, _enemyPoolPrewarmCount);
        }

        if (_enemyArcherPrefab != null && _enemyPoolPrewarmCount > 0)
        {
            PoolManager.Instance.Prewarm(_enemyArcherPrefab, _enemyPoolPrewarmCount);
        }

        ApplyEnemyDataAssets();
        EnsureDefaultEnemyRoster();
        PrewarmConfiguredEnemies();

        // Đăng ký lắng nghe sự kiện đổi Ngày/Đêm từ TimeManager
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
            GameLog.Log("[EnemyManager] Đăng ký lắng nghe sự kiện OnDayNightChanged thành công.");
        }
        else
        {
            GameLog.LogError("[EnemyManager] Không tìm thấy TimeManager.Instance!");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyEnemyDataAssets();
        EnsureDefaultEnemyRoster();
    }
#endif

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
            _nightRaidPending = true;
            _currentNightNumber++;
            PrepareNightRaidSchedule();
        }
        else
        {
            _nightRaidPending = false;
            _spawnedWavesThisNight = new bool[0];
            _dayRainRaidSpawned = false;
            _daySpecialEventSpawned = false;
        }
    }

    // ===== Public debug and query API =====
    [ContextMenu("Spawn Night Raid Test")]
    public void SpawnNightRaid()
    {
        _currentNightNumber++;
        CleanupActiveEnemies();

        if (!TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
        {
            return;
        }

        PrepareNightRaidSchedule();

        int successfulSpawns = 0;
        for (int waveIndex = 0; waveIndex < _activeNightWaves.Count; waveIndex++)
        {
            successfulSpawns += SpawnWave(_currentNightNumber, waveIndex, centerSpawnPos, edgeName);
        }

        GameLog.Log($"[EnemyManager] Đêm {_currentNightNumber}: Spawn test thành công {successfulSpawns} kẻ địch từ hướng {edgeName}.");
    }

    public int GetEnemyCountForNight(int nightNumber)
    {
        EnsureWaveScheduler();
        return _waveScheduler.GetEnemyCountForNight(BuildWaveSchedulerSettings(), nightNumber);
    }

    [ContextMenu("Force Spawn Wave")]
    public void ForceSpawnWave()
    {
        CleanupActiveEnemies();

        if (!TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
        {
            return;
        }

        if (_currentNightNumber <= 0)
        {
            _currentNightNumber = 1;
        }

        if (_activeNightWaves.Count == 0)
        {
            PrepareNightRaidSchedule();
        }

        int waveIndex = GetNextUnspawnedWaveIndex();
        int successfulSpawns = SpawnWave(_currentNightNumber, waveIndex, centerSpawnPos, edgeName);
        GameLog.Log($"[EnemyManager] Force wave: night {_currentNightNumber}, wave {waveIndex + 1}, spawned {successfulSpawns}, edge {edgeName}.");
    }

    /// <summary>
    /// Hàm dọn dẹp các kẻ địch đã bị tiêu diệt
    /// </summary>
    void Update()
    {
        TimeManager timeManager = TimeManager.Instance;
        WeatherManager weatherManager = WeatherManager.Instance;
        bool hasTimeManager = timeManager != null;
        float timeRatio = hasTimeManager ? timeManager.GetTimeRatio() : 0f;

        // Kiểm tra đột kích ban ngày nếu trời mưa sương mù âm u
        if (hasTimeManager && !timeManager.IsNight)
        {
            if (weatherManager != null && weatherManager.CurrentWeather == WeatherState.Rain)
            {
                if (!_dayRainRaidSpawned && timeRatio >= 0.2f)
                {
                    _dayRainRaidSpawned = true;
                    SpawnDayRainRaid();
                }
            }

            if (!_daySpecialEventSpawned && timeRatio >= 0.35f)
            {
                _daySpecialEventSpawned = true;
                TrySpawnDaySpecialEvent();
            }
        }

        if (_nightRaidPending && hasTimeManager && timeManager.IsNight)
        {
            ProcessScheduledNightWaves(timeRatio);
        }

        if (Time.time >= _nextCleanupTime)
        {
            _nextCleanupTime = Time.time + CleanupCooldown;
            CleanupActiveEnemies();
        }
    }

    // ===== Daytime raid events =====
    private void SpawnDayRainRaid()
    {
        if (!TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
        {
            return;
        }

        // Spawn một nhóm nhỏ kẻ địch quấy rối kinh tế ban ngày (2 - 8 con tùy theo đêm hiện tại)
        int count = Mathf.Clamp(2 + (_currentNightNumber / 2), 2, 8);
        int successfulSpawns = SpawnEnemyGroup(_enemyPrefab, "RainyRaid_Skeleton", count, _currentNightNumber, 99, centerSpawnPos);

        GameLog.Log($"[EnemyManager] Đột kích ngày mưa: Spawn thành công {successfulSpawns} Skeleton từ hướng {edgeName} lúc sáng sớm.");
    }

    private void TrySpawnDaySpecialEvent()
    {
        if (!TryRollSpecialEvent(_currentNightNumber, _minorDayEventChance, _dangerDayEventChance, out string eventName, out int _, out float countMultiplier))
        {
            return;
        }

        if (!TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
        {
            return;
        }

        int baseCount = Mathf.Clamp(Mathf.RoundToInt(GetEnemyCountForNight(_currentNightNumber) * 0.5f * countMultiplier), 2, Mathf.Max(2, _maxEnemiesPerWave));
        List<EnemyWaveEntry> entries = BuildWeightedEnemyEntries(_currentNightNumber, baseCount);
        int totalSpawned = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            GameObject prefab = GetEntryPrefab(entries[i]);
            totalSpawned += SpawnEnemyGroup(prefab, $"{eventName}_{entries[i].name}", entries[i].baseCount, _currentNightNumber, 98, centerSpawnPos);
        }

        GameLog.Log($"[EnemyManager] Sự kiện ban ngày {eventName}: spawned {totalSpawned} enemies từ {edgeName}.");
    }


    // ===== Wave setup: roster and optional custom presets =====
    private void ApplyEnemyDataAssets()
    {
        EnsureStatsScaler();
        ApplyDifficultySettings();

        if (_enemyRosterData != null && _enemyRosterData.roster != null && _enemyRosterData.roster.Count > 0)
        {
            _enemyRoster = CloneEnemyRoster(_enemyRosterData.roster);
        }

        if (_waveConfigData != null && _waveConfigData.waves != null)
        {
            _customWaves = CloneDayWaves(_waveConfigData.waves);
            _useCustomWaveOverrides = _waveConfigData.replaceAutoProgression && _customWaves.Count > 0;
        }
    }

    private void ApplyDifficultySettings()
    {
        EnsureStatsScaler();
        _statsScaler.SetSettings(_difficultySettings);

        if (_difficultySettings == null)
        {
            return;
        }

        _baseEnemyCount = Mathf.Max(0, _difficultySettings.baseEnemyCount);
        _enemyIncreasePerNight = Mathf.Max(0, _difficultySettings.enemyIncreasePerNight);
        _maxEnemiesPerWave = Mathf.Max(1, _difficultySettings.maxEnemiesPerWave);
        _maxActiveEnemies = Mathf.Max(1, _difficultySettings.maxActiveEnemies);
        _waveIncreaseEveryNights = Mathf.Max(1, _difficultySettings.waveIncreaseEveryNights);
        _maxAutoWavesPerNight = Mathf.Max(1, _difficultySettings.maxAutoWavesPerNight);
        _autoWaveStartTimeRatio = Mathf.Clamp(_difficultySettings.autoWaveStartTimeRatio, 0.5f, 0.95f);
        _autoWaveEndTimeRatio = Mathf.Clamp(_difficultySettings.autoWaveEndTimeRatio, 0.55f, 0.99f);
        _bloodMoonWaveMultiplier = Mathf.Max(1f, _difficultySettings.bloodMoonWaveMultiplier);
        _bloodMoonCountMultiplier = Mathf.Max(1f, _difficultySettings.bloodMoonCountMultiplier);
        _minorEventStartNight = Mathf.Max(1, _difficultySettings.minorEventStartNight);
        _dangerEventStartNight = Mathf.Max(1, _difficultySettings.dangerEventStartNight);
        _minorNightEventChance = Mathf.Clamp01(_difficultySettings.minorNightEventChance);
        _dangerNightEventChance = Mathf.Clamp01(_difficultySettings.dangerNightEventChance);
        _minorDayEventChance = Mathf.Clamp01(_difficultySettings.minorDayEventChance);
        _dangerDayEventChance = Mathf.Clamp01(_difficultySettings.dangerDayEventChance);
        _maxActiveSummonedEnemies = Mathf.Max(0, _difficultySettings.maxActiveSummonedEnemies);
    }

    private void EnsureStatsScaler()
    {
        if (_statsScaler == null)
        {
            _statsScaler = new EnemyStatsScaler(_difficultySettings);
        }
    }

    private void EnsureWaveScheduler()
    {
        if (_waveScheduler == null)
        {
            _waveScheduler = new EnemyWaveScheduler();
        }
    }

    private EnemyWaveSchedulerSettings BuildWaveSchedulerSettings()
    {
        return new EnemyWaveSchedulerSettings
        {
            baseEnemyCount = _baseEnemyCount,
            enemyIncreasePerNight = _enemyIncreasePerNight,
            maxEnemiesPerWave = _maxEnemiesPerWave,
            waveIncreaseEveryNights = _waveIncreaseEveryNights,
            maxAutoWavesPerNight = _maxAutoWavesPerNight,
            autoWaveStartTimeRatio = _autoWaveStartTimeRatio,
            autoWaveEndTimeRatio = _autoWaveEndTimeRatio,
            bloodMoonWaveMultiplier = _bloodMoonWaveMultiplier,
            minorEventStartNight = _minorEventStartNight,
            dangerEventStartNight = _dangerEventStartNight
        };
    }

    private List<EnemyRosterEntry> CloneEnemyRoster(List<EnemyRosterEntry> source)
    {
        List<EnemyRosterEntry> clone = new List<EnemyRosterEntry>();
        for (int i = 0; i < source.Count; i++)
        {
            EnemyRosterEntry entry = source[i];
            if (entry == null)
            {
                continue;
            }

            clone.Add(new EnemyRosterEntry
            {
                name = entry.name,
                prefab = entry.prefab,
                unlockDay = entry.unlockDay,
                weight = entry.weight,
                maxCountPerWave = entry.maxCountPerWave
            });
        }

        return clone;
    }

    private List<DayWaveConfig> CloneDayWaves(List<DayWaveConfig> source)
    {
        List<DayWaveConfig> clone = new List<DayWaveConfig>();
        for (int i = 0; i < source.Count; i++)
        {
            DayWaveConfig wave = source[i];
            if (wave == null)
            {
                continue;
            }

            DayWaveConfig waveClone = new DayWaveConfig
            {
                specificNight = wave.specificNight,
                waveName = wave.waveName,
                spawnTimeRatio = wave.spawnTimeRatio,
                countMultiplier = wave.countMultiplier,
                enemies = CloneEnemyWaveEntries(wave.enemies)
            };
            clone.Add(waveClone);
        }

        return clone;
    }

    private List<EnemyWaveEntry> CloneEnemyWaveEntries(List<EnemyWaveEntry> source)
    {
        List<EnemyWaveEntry> clone = new List<EnemyWaveEntry>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            EnemyWaveEntry entry = source[i];
            if (entry == null)
            {
                continue;
            }

            clone.Add(new EnemyWaveEntry
            {
                name = entry.name,
                prefab = entry.prefab,
                baseCount = entry.baseCount,
                increasePerNight = entry.increasePerNight,
                maxCount = entry.maxCount
            });
        }

        return clone;
    }

    private void EnsureDefaultEnemyRoster()
    {
        if (_enemyRoster.Count == 1 && _enemyRoster[0].prefab == null && string.IsNullOrEmpty(_enemyRoster[0].name))
        {
            _enemyRoster.Clear();
        }

        if (_enemyRoster.Count > 0)
        {
            return;
        }

        if (_enemyPrefab != null)
        {
            _enemyRoster.Add(new EnemyRosterEntry
            {
                name = "Skeleton",
                prefab = _enemyPrefab,
                unlockDay = 1,
                weight = 3f,
                maxCountPerWave = _maxEnemiesPerWave
            });
        }

        if (_enemyArcherPrefab != null)
        {
            _enemyRoster.Add(new EnemyRosterEntry
            {
                name = "EnemyArcher",
                prefab = _enemyArcherPrefab,
                unlockDay = 4,
                weight = 1f,
                maxCountPerWave = 12
            });
        }
    }

    private void PrewarmConfiguredEnemies()
    {
        if (_enemyPoolPrewarmCount <= 0 || PoolManager.Instance == null)
        {
            return;
        }

        HashSet<GameObject> prewarmedPrefabs = new HashSet<GameObject>();
        for (int rosterIndex = 0; rosterIndex < _enemyRoster.Count; rosterIndex++)
        {
            GameObject rosterPrefab = GetRosterPrefab(_enemyRoster[rosterIndex]);
            if (rosterPrefab != null && prewarmedPrefabs.Add(rosterPrefab))
            {
                PoolManager.Instance.Prewarm(rosterPrefab, _enemyPoolPrewarmCount);
            }
        }

        for (int waveIndex = 0; waveIndex < _customWaves.Count; waveIndex++)
        {
            DayWaveConfig wave = _customWaves[waveIndex];
            for (int entryIndex = 0; entryIndex < wave.enemies.Count; entryIndex++)
            {
                GameObject prefab = GetEntryPrefab(wave.enemies[entryIndex]);
                if (prefab != null && prewarmedPrefabs.Add(prefab))
                {
                    PoolManager.Instance.Prewarm(prefab, _enemyPoolPrewarmCount);
                }
            }
        }
    }

    // ===== Night wave scheduling =====
    private void PrepareNightRaidSchedule()
    {
        ApplyEnemyDataAssets();
        EnsureWaveScheduler();

        int night = Mathf.Max(1, _currentNightNumber);
        WeatherState weather = WeatherState.Clear;
        if (WeatherManager.Instance != null)
        {
            weather = WeatherManager.Instance.CurrentWeather;
        }

        _activeNightWaves = _waveScheduler.PrepareNightRaidSchedule(
            BuildWaveSchedulerSettings(),
            _useCustomWaveOverrides,
            _customWaves,
            _enemyRoster,
            _enemyPrefab,
            night,
            weather);

        bool usingCustomWaves = _useCustomWaveOverrides && HasCustomWaveForNight(night);
        if (!usingCustomWaves)
        {
            _waveScheduler.ApplyNightSpecialEvent(
                BuildWaveSchedulerSettings(),
                night,
                _minorNightEventChance,
                _dangerNightEventChance,
                _activeNightWaves);
        }

        GameLog.Log(usingCustomWaves
            ? $"[EnemyManager] Đêm {night} ({weather}): Đã tải {_activeNightWaves.Count} wave cấu hình sẵn từ customWaves."
            : $"[EnemyManager] Đêm {night} ({weather}): Tự sinh {_activeNightWaves.Count} wave từ Enemy Roster.");

        _spawnedWavesThisNight = new bool[_activeNightWaves.Count];
    }

    private bool HasCustomWaveForNight(int night)
    {
        if (!_useCustomWaveOverrides || _customWaves == null || _customWaves.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _customWaves.Count; i++)
        {
            DayWaveConfig wave = _customWaves[i];
            if (wave != null && wave.specificNight == night)
            {
                return true;
            }
        }

        for (int i = 0; i < _customWaves.Count; i++)
        {
            DayWaveConfig wave = _customWaves[i];
            if (wave != null && wave.specificNight <= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRollSpecialEvent(int night, float minorChance, float dangerChance, out string eventName, out int bonusWaves, out float countMultiplier)
    {
        EnsureWaveScheduler();
        return _waveScheduler.TryRollSpecialEvent(
            BuildWaveSchedulerSettings(),
            night,
            minorChance,
            dangerChance,
            out eventName,
            out bonusWaves,
            out countMultiplier);
    }

    private List<EnemyWaveEntry> BuildWeightedEnemyEntries(int night, int totalCount)
    {
        EnsureWaveScheduler();
        return _waveScheduler.BuildWeightedEnemyEntries(
            BuildWaveSchedulerSettings(),
            _enemyRoster,
            _enemyPrefab,
            night,
            totalCount);
    }

    private void ProcessScheduledNightWaves(float timeRatio)
    {
        EnsureWaveScheduler();

        if (_spawnedWavesThisNight.Length != _activeNightWaves.Count)
        {
            PrepareNightRaidSchedule();
        }

        List<int> dueWaveIndices = _waveScheduler.GetDueWaveIndices(_activeNightWaves, _spawnedWavesThisNight, timeRatio);
        for (int i = 0; i < dueWaveIndices.Count; i++)
        {
            int waveIndex = dueWaveIndices[i];

            if (TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
            {
                int successfulSpawns = SpawnWave(_currentNightNumber, waveIndex, centerSpawnPos, edgeName);
                GameLog.Log($"[EnemyManager] Đêm {_currentNightNumber}: Wave {waveIndex + 1}/{_activeNightWaves.Count} spawned {successfulSpawns} enemies.");
            }

            _spawnedWavesThisNight[waveIndex] = true;
        }

        if (_waveScheduler.AllWavesSpawned(_activeNightWaves, _spawnedWavesThisNight))
        {
            _nightRaidPending = false;
        }
    }

    private int GetNextUnspawnedWaveIndex()
    {
        if (_spawnedWavesThisNight.Length != _activeNightWaves.Count)
        {
            _spawnedWavesThisNight = new bool[_activeNightWaves.Count];
        }

        for (int i = 0; i < _spawnedWavesThisNight.Length; i++)
        {
            if (!_spawnedWavesThisNight[i])
            {
                _spawnedWavesThisNight[i] = true;
                return i;
            }
        }

        _spawnedWavesThisNight = new bool[_activeNightWaves.Count];
        _spawnedWavesThisNight[0] = true;
        return 0;
    }

    // ===== Spawn execution =====
    private int SpawnWave(int nightNumber, int waveIndex, Vector3 centerSpawnPos, string edgeName)
    {
        if (waveIndex < 0 || waveIndex >= _activeNightWaves.Count)
        {
            return 0;
        }

        DayWaveConfig wave = _activeNightWaves[waveIndex];
        int totalRequested = 0;
        int totalSpawned = 0;

        for (int entryIndex = 0; entryIndex < wave.enemies.Count; entryIndex++)
        {
            EnemyWaveEntry entry = wave.enemies[entryIndex];
            GameObject prefab = GetEntryPrefab(entry);
            if (prefab == null)
            {
                continue;
            }

            int requestedCount = GetEnemyCountForEntry(entry, nightNumber, wave.countMultiplier);
            totalRequested += requestedCount;
            totalSpawned += SpawnEnemyGroup(prefab, entry.name, requestedCount, nightNumber, waveIndex, centerSpawnPos);
        }

        GameLog.Log($"[EnemyManager] Đêm {nightNumber}: {wave.waveName} tại {edgeName}, spawned {totalSpawned}/{totalRequested}, active {_activeEnemies.Count}/{_maxActiveEnemies}.");
        return totalSpawned;
    }

    private int GetEnemyCountForEntry(EnemyWaveEntry entry, int nightNumber, float waveMultiplier)
    {
        int safeNightNumber = Mathf.Max(1, nightNumber);
        int scaledCount = Mathf.Max(0, entry.baseCount) + (safeNightNumber - 1) * Mathf.Max(0, entry.increasePerNight);
        int cappedCount = Mathf.Clamp(scaledCount, 0, Mathf.Max(0, entry.maxCount));
        
        float finalMultiplier = waveMultiplier;
        if (WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.BloodMoon)
        {
            finalMultiplier *= Mathf.Max(1f, _bloodMoonCountMultiplier);
        }
        
        return Mathf.RoundToInt(cappedCount * Mathf.Max(0f, finalMultiplier));
    }

    private GameObject GetEntryPrefab(EnemyWaveEntry entry)
    {
        return entry != null && entry.prefab != null ? entry.prefab : _enemyPrefab;
    }

    private GameObject GetRosterPrefab(EnemyRosterEntry entry)
    {
        return entry != null && entry.prefab != null ? entry.prefab : _enemyPrefab;
    }

    private int SpawnEnemyGroup(GameObject prefab, string enemyName, int requestedCount, int nightNumber, int waveIndex, Vector3 centerSpawnPos)
    {
        if (prefab == null || requestedCount <= 0)
        {
            return 0;
        }

        int activeEnemySlots = Mathf.Max(0, _maxActiveEnemies - _activeEnemies.Count);
        int enemyCount = Mathf.Min(requestedCount, activeEnemySlots);
        if (enemyCount <= 0)
        {
            GameLog.Log($"[EnemyManager] Đêm {nightNumber}: Bỏ qua group {enemyName} vì đã đạt giới hạn active enemies ({_activeEnemies.Count}/{_maxActiveEnemies}).");
            return 0;
        }

        EnsureGroupRallyController();
        Vector3 rallyPos = _groupRallyController.GetRallyPosition(centerSpawnPos);

        List<EnemyUnitController> spawnedGroup = new List<EnemyUnitController>();
        int successfulSpawns = 0;
        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = GetSpawnPositionNear(centerSpawnPos);
            GameObject enemyObj = PoolManager.Instance.Spawn(prefab, spawnPos, Quaternion.identity);
            enemyObj.name = $"{enemyName}_Night{nightNumber}_Wave{waveIndex + 1}_{i + 1}";

            EnemyUnitController enemyController = enemyObj.GetComponent<EnemyUnitController>();
            if (enemyController == null)
            {
                enemyController = enemyObj.AddComponent<EnemyUnitController>();
            }

            enemyController.OnSpawnedFromPool();
            enemyController.prefabName = prefab.name;
            enemyController.SetRallyPoint(rallyPos); // Bat dau di chuyen den rally point truoc

            GetEnemyStatMultipliers(nightNumber, out float healthMultiplier, out float damageMultiplier, out float speedMultiplier);
            enemyController.ApplyStatMultipliers(healthMultiplier, damageMultiplier, speedMultiplier);

            if (!_activeEnemies.Contains(enemyController))
            {
                _activeEnemies.Add(enemyController);
            }

            spawnedGroup.Add(enemyController);
            successfulSpawns++;
        }

        // Giai phong rally khi group da tap hop du hoac het thoi gian cho.
        if (spawnedGroup.Count > 0)
        {
            float rallyMaxWait = (TimeManager.Instance != null && TimeManager.Instance.IsNight) ? 2.5f : _groupRallyMaxWait;
            StartCoroutine(_groupRallyController.ReleaseGroupWhenReady(
                spawnedGroup,
                rallyPos,
                _groupRallyRadius,
                _groupRallyMinReadyRatio,
                rallyMaxWait));
        }

        return successfulSpawns;
    }

    public void GetEnemyStatMultipliers(int nightNumber, out float healthMultiplier, out float damageMultiplier, out float speedMultiplier)
    {
        EnsureStatsScaler();
        WeatherState weather = WeatherManager.Instance != null ? WeatherManager.Instance.CurrentWeather : WeatherState.Clear;
        _statsScaler.GetMultipliers(nightNumber, weather, out healthMultiplier, out damageMultiplier, out speedMultiplier);

        // Nếu Sắc lệnh "Trinh sát đi đêm" đang hoạt động -> Tăng sức mạnh quái vật đêm lên 50%
        if (CardManager.Instance != null && CardManager.Instance.IsDecreeActive("decree_night_scout"))
        {
            healthMultiplier *= 1.5f;
            damageMultiplier *= 1.5f;
        }
    }

    private void EnsureGroupRallyController()
    {
        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }

        if (_groupRallyController == null)
        {
            _groupRallyController = new EnemyGroupRallyController(_gridSystem);
        }
        else
        {
            _groupRallyController.SetGridSystem(_gridSystem);
        }
    }

    // ===== Spawn position selection =====
    private void EnsureSpawnPositionFinder()
    {
        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }

        if (_spawnPositionFinder == null)
        {
            _spawnPositionFinder = new EnemySpawnPositionFinder(_gridSystem);
        }
        else
        {
            _spawnPositionFinder.SetGridSystem(_gridSystem);
        }
    }

    private Vector3 GetSpawnPositionNear(Vector3 centerSpawnPos)
    {
        EnsureSpawnPositionFinder();
        return _spawnPositionFinder.GetSpawnPositionNear(centerSpawnPos, _spawnRadius);
    }

    private bool TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName)
    {
        EnsureSpawnPositionFinder();
        return _spawnPositionFinder.TryGetSpawnEdge(
            _spawnEdgeCandidateCount,
            _minSpawnDistanceFromCamera,
            _avoidRepeatingSpawnEdge,
            out centerSpawnPos,
            out edgeName);
    }

    public GameObject GetEnemyPrefabByName(string name)
    {
        if (_enemyPrefab != null && _enemyPrefab.name == name) return _enemyPrefab;
        if (_enemyArcherPrefab != null && _enemyArcherPrefab.name == name) return _enemyArcherPrefab;

        if (_enemyRoster != null)
        {
            foreach (var entry in _enemyRoster)
            {
                if (entry.prefab != null && entry.prefab.name == name)
                {
                    return entry.prefab;
                }
            }
        }

        if (_enemyRosterData != null && _enemyRosterData.roster != null)
        {
            foreach (var entry in _enemyRosterData.roster)
            {
                if (entry.prefab != null && entry.prefab.name == name)
                {
                    return entry.prefab;
                }
            }
        }

        return null;
    }

    public List<GameObject> GetUnlockedEnemyPrefabs(int night)
    {
        List<GameObject> unlocked = new List<GameObject>();

        if (_enemyRosterData != null && _enemyRosterData.roster != null)
        {
            foreach (var entry in _enemyRosterData.roster)
            {
                if (entry.prefab != null && night >= entry.unlockDay && !unlocked.Contains(entry.prefab))
                {
                    unlocked.Add(entry.prefab);
                }
            }
        }

        if (_enemyRoster != null)
        {
            foreach (var entry in _enemyRoster)
            {
                if (entry.prefab != null && night >= entry.unlockDay && !unlocked.Contains(entry.prefab))
                {
                    unlocked.Add(entry.prefab);
                }
            }
        }

        if (unlocked.Count == 0)
        {
            if (_enemyPrefab != null) unlocked.Add(_enemyPrefab);
            if (_enemyArcherPrefab != null) unlocked.Add(_enemyArcherPrefab);
        }

        return unlocked;
    }


    private void CleanupActiveEnemies()
    {
        _activeEnemies.RemoveAll(enemy => enemy == null || enemy.currentState == CombatState.Dead || !enemy.gameObject.activeInHierarchy);
    }

    [System.Serializable]
    private class EnemyManagerSaveState
    {
        public int currentNightNumber;
        public bool nightRaidPending;
        public bool dayRainRaidSpawned;
        public bool daySpecialEventSpawned;
    }

    public string CaptureState()
    {
        EnemyManagerSaveState state = new EnemyManagerSaveState
        {
            currentNightNumber = this._currentNightNumber,
            nightRaidPending = this._nightRaidPending,
            dayRainRaidSpawned = this._dayRainRaidSpawned,
            daySpecialEventSpawned = this._daySpecialEventSpawned
        };
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        EnemyManagerSaveState state = JsonUtility.FromJson<EnemyManagerSaveState>(stateJson);
        if (state == null) return;

        this._currentNightNumber = state.currentNightNumber;
        this._nightRaidPending = state.nightRaidPending;
        this._dayRainRaidSpawned = state.dayRainRaidSpawned;
        this._daySpecialEventSpawned = state.daySpecialEventSpawned;
        
        GameLog.Log($"[EnemyManager] Restored currentNightNumber to: {this._currentNightNumber}");
    }
}
