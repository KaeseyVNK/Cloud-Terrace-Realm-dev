using UnityEngine;
using UnityEngine.AI;
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

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager s_instance;
    public static EnemyManager Instance => s_instance;

    [Header("Spawner Settings")]
    [Tooltip("Prefab của kẻ địch (mặc định sẽ tải Skeleton_Warrior.prefab nếu để trống)")]
    [SerializeField] private GameObject _enemyPrefab;
    public GameObject EnemyPrefab
    {
        get => _enemyPrefab;
        set => _enemyPrefab = value;
    }

    [Tooltip("Prefab của cung thủ địch (mặc định sẽ tải EnemyArcher.prefab nếu để trống)")]
    [SerializeField] private GameObject _enemyArcherPrefab;
    public GameObject EnemyArcherPrefab
    {
        get => _enemyArcherPrefab;
        set => _enemyArcherPrefab = value;
    }


    [Tooltip("Số lượng kẻ địch xuất hiện ở đêm đầu tiên")]
    [SerializeField] private int _baseEnemyCount = 3;
    public int BaseEnemyCount
    {
        get => _baseEnemyCount;
        set => _baseEnemyCount = value;
    }

    [Tooltip("Số lượng kẻ địch tăng thêm sau mỗi đêm")]
    [SerializeField] private int _enemyIncreasePerNight = 2;
    public int EnemyIncreasePerNight
    {
        get => _enemyIncreasePerNight;
        set => _enemyIncreasePerNight = value;
    }

    [Tooltip("Giới hạn số kẻ địch tối đa trong một wave ban đêm")]
    [SerializeField] private int _maxEnemiesPerWave = 30;
    public int MaxEnemiesPerWave
    {
        get => _maxEnemiesPerWave;
        set => _maxEnemiesPerWave = value;
    }

    [Tooltip("Giới hạn tổng số kẻ địch đang hoạt động để bảo vệ FPS")]
    [SerializeField] private int _maxActiveEnemies = 60;
    public int MaxActiveEnemies
    {
        get => _maxActiveEnemies;
        set => _maxActiveEnemies = value;
    }

    [Tooltip("Bán kính cụm spawn lính")]
    [SerializeField] private float _spawnRadius = 4f;
    public float SpawnRadius
    {
        get => _spawnRadius;
        set => _spawnRadius = value;
    }

    [Header("Smart Spawn")]
    [Tooltip("Số vị trí ứng viên được thử khi chọn cạnh spawn. Cao hơn giúp spawn thông minh hơn nhưng tốn thêm rất ít CPU.")]
    [SerializeField] private int _spawnEdgeCandidateCount = 12;

    [Tooltip("Khoảng cách tối thiểu mong muốn từ vị trí spawn tới camera hiện tại.")]
    [SerializeField] private float _minSpawnDistanceFromCamera = 60f;

    [Tooltip("Giảm điểm cạnh vừa spawn ở wave trước để tránh địch luôn tới từ cùng một hướng.")]
    [SerializeField] private bool _avoidRepeatingSpawnEdge = true;

    [Header("Group Rally")]
    [Tooltip("Bán kính quanh rally point được xem là đã tập hợp xong.")]
    [SerializeField] private float _groupRallyRadius = 5f;

    [Tooltip("Tỷ lệ quân trong group phải tới rally trước khi cả nhóm bắt đầu tấn công.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _groupRallyMinReadyRatio = 0.65f;

    [Tooltip("Thời gian tối đa chờ tập hợp trước khi release group.")]
    [SerializeField] private float _groupRallyMaxWait = 12f;

    [Tooltip("Time ratio for the night raid spawn. 0.5 = night start, 0.75 = midnight.")]
    [Range(0.5f, 0.99f)]
    [SerializeField] private float _nightRaidSpawnTimeRatio = 0.75f;

    [Header("Multi Wave Settings")]
    [SerializeField] private bool _useMultiWaveConfig = true;
    [SerializeField] private List<DayWaveConfig> _customWaves = new List<DayWaveConfig>();

    [Header("Pooling")]
    [SerializeField] private int _enemyPoolPrewarmCount = 24;

    private GridSystem _gridSystem;
    private int _currentNightNumber = 0;
    public int CurrentNightNumber => _currentNightNumber;

    private List<EnemyUnitController> _activeEnemies = new List<EnemyUnitController>();
    public List<EnemyUnitController> ActiveEnemies => _activeEnemies;
    private bool _nightRaidPending = false;
    private bool[] _spawnedWavesThisNight = new bool[0];
    private bool _dayRainRaidSpawned = false;
    private List<DayWaveConfig> _activeNightWaves = new List<DayWaveConfig>();
    private int _lastSpawnEdgeChoice = -1;


    void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _gridSystem = FindAnyObjectByType<GridSystem>();
    }

    void Start()
    {
        // Đảm bảo có enemyPrefab
        if (_enemyPrefab == null)
        {
#if UNITY_EDITOR
            _enemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Skeleton_Warrior.prefab");
            if (_enemyPrefab != null)
            {
                Debug.Log($"[EnemyManager] Tự động tải thành công prefab kẻ địch: {_enemyPrefab.name}");
            }
            else
            {
                Debug.LogWarning("[EnemyManager] Không tìm thấy prefab Skeleton_Warrior tại Assets/Prefabs/Unit/Skeleton_Warrior.prefab!");
            }
#endif
        }

        // Đảm bảo có enemyArcherPrefab
        if (_enemyArcherPrefab == null)
        {
#if UNITY_EDITOR
            _enemyArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/EnemyArcher.prefab");
            if (_enemyArcherPrefab != null)
            {
                Debug.Log($"[EnemyManager] Tự động tải thành công prefab EnemyArcher: {_enemyArcherPrefab.name}");
            }
            else
            {
                Debug.LogWarning("[EnemyManager] Không tìm thấy prefab EnemyArcher tại Assets/Prefabs/Unit/EnemyArcher.prefab!");
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

        EnsureDefaultWaveConfig();
        AddEnemyArcherToWavesIfNeeded();
        PrewarmConfiguredEnemies();

        // Đăng ký lắng nghe sự kiện đổi Ngày/Đêm từ TimeManager
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
            Debug.Log("[EnemyManager] Đăng ký lắng nghe sự kiện OnDayNightChanged thành công.");
        }
        else
        {
            Debug.LogError("[EnemyManager] Không tìm thấy TimeManager.Instance!");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaultWaveConfig();
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
        }
    }

    [ContextMenu("Spawn Night Raid Test")]
    public void SpawnNightRaid()
    {
        _currentNightNumber++;
        CleanupActiveEnemies();
        
        if (_enemyPrefab == null)
        {
            Debug.LogError("[EnemyManager] Không có enemyPrefab để sinh kẻ địch!");
            return;
        }

        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
            if (_gridSystem == null)
            {
                Debug.LogError("[EnemyManager] Không tìm thấy GridSystem trong cảnh!");
                return;
            }
        }

        int width = _gridSystem.GetWidth();
        int length = _gridSystem.GetLength();

        if (width <= 0 || length <= 0)
        {
            Debug.LogError($"[EnemyManager] Kích thước GridSystem không hợp lệ: {width}x{length}");
            return;
        }

        // Chọn ngẫu nhiên 1 trong 4 rìa bản đồ để spawn cả nhóm lính
        int edgeChoice = Random.Range(0, 4);
        int edgeX = 0;
        int edgeZ = 0;
        string edgeName = "";

        switch (edgeChoice)
        {
            case 0: // Rìa trên (Z tối đa)
                edgeX = Random.Range(0, width);
                edgeZ = length - 1;
                edgeName = "Bắc (Rìa Trên)";
                break;
            case 1: // Rìa dưới (Z = 0)
                edgeX = Random.Range(0, width);
                edgeZ = 0;
                edgeName = "Nam (Rìa Dưới)";
                break;
            case 2: // Rìa trái (X = 0)
                edgeX = 0;
                edgeZ = Random.Range(0, length);
                edgeName = "Tây (Rìa Trái)";
                break;
            case 3: // Rìa phải (X tối đa)
                edgeX = width - 1;
                edgeZ = Random.Range(0, length);
                edgeName = "Đông (Rìa Phải)";
                break;
        }

        Vector3 centerSpawnPos = _gridSystem.GetWorldPosition(edgeX, edgeZ);
        
        PrepareNightRaidSchedule();

        int successfulSpawns = 0;
        int requestedEnemyCount = GetEnemyCountForNight(_currentNightNumber);
        if (_useMultiWaveConfig && _activeNightWaves.Count > 0)
        {
            for (int waveIndex = 0; waveIndex < _activeNightWaves.Count; waveIndex++)
            {
                successfulSpawns += SpawnWave(_currentNightNumber, waveIndex, centerSpawnPos, edgeName);
            }
        }
        else
        {
            successfulSpawns = SpawnEnemyGroup(_enemyPrefab, "Enemy", requestedEnemyCount, _currentNightNumber, 0, centerSpawnPos);
        }

        Debug.Log($"[EnemyManager] Đêm {_currentNightNumber}: Spawn test thành công {successfulSpawns} kẻ địch từ hướng {edgeName}.");
    }

    public int GetEnemyCountForNight(int nightNumber)
    {
        int safeNightNumber = Mathf.Max(1, nightNumber);
        int scaledCount = Mathf.Max(0, _baseEnemyCount) + (safeNightNumber - 1) * Mathf.Max(0, _enemyIncreasePerNight);
        return Mathf.Clamp(scaledCount, 0, Mathf.Max(0, _maxEnemiesPerWave));
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
        Debug.Log($"[EnemyManager] Force wave: night {_currentNightNumber}, wave {waveIndex + 1}, spawned {successfulSpawns}, edge {edgeName}.");
    }

    /// <summary>
    /// Hàm dọn dẹp các kẻ địch đã bị tiêu diệt
    /// </summary>
    void Update()
    {
        // Kiểm tra đột kích ban ngày nếu trời mưa sương mù âm u
        if (TimeManager.Instance != null && !TimeManager.Instance.IsNight)
        {
            if (WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain)
            {
                if (!_dayRainRaidSpawned && TimeManager.Instance.GetTimeRatio() >= 0.2f)
                {
                    _dayRainRaidSpawned = true;
                    SpawnDayRainRaid();
                }
            }
        }

        if (_nightRaidPending && TimeManager.Instance != null && TimeManager.Instance.IsNight && TimeManager.Instance.GetTimeRatio() >= _nightRaidSpawnTimeRatio)
        {
            if (!_useMultiWaveConfig || _activeNightWaves.Count == 0)
            {
                _nightRaidPending = false;
                SpawnLegacyNightWave();
            }
        }

        if (_nightRaidPending && TimeManager.Instance != null && TimeManager.Instance.IsNight && _useMultiWaveConfig && _activeNightWaves.Count > 0)
        {
            ProcessScheduledNightWaves(TimeManager.Instance.GetTimeRatio());
        }

        CleanupActiveEnemies();
    }

    private void SpawnDayRainRaid()
    {
        if (!TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
        {
            return;
        }

        // Spawn một nhóm nhỏ kẻ địch quấy rối kinh tế ban ngày (2 - 8 con tùy theo đêm hiện tại)
        int count = Mathf.Clamp(2 + (_currentNightNumber / 2), 2, 8);
        int successfulSpawns = SpawnEnemyGroup(_enemyPrefab, "RainyRaid_Skeleton", count, _currentNightNumber, 99, centerSpawnPos);

        Debug.Log($"[EnemyManager] Đột kích ngày mưa: Spawn thành công {successfulSpawns} Skeleton từ hướng {edgeName} lúc sáng sớm.");
    }


    [ContextMenu("Reset to Preset Waves")]
    public void ResetToPresetWaves()
    {
        _customWaves.Clear();
        EnsureDefaultWaveConfig();
        Debug.Log("[EnemyManager] Đã khôi phục danh sách các wave mẫu thành công.");
    }

    private void EnsureDefaultWaveConfig()
    {
        // Nếu chỉ có 1 phần tử mặc định chưa cấu hình (tên trống và không có quái), tự động dọn dẹp để tải preset
        if (_customWaves.Count == 1 && string.IsNullOrEmpty(_customWaves[0].waveName) && (_customWaves[0].enemies == null || _customWaves[0].enemies.Count == 0))
        {
            _customWaves.Clear();
        }

        if (_customWaves.Count > 0)
        {
            return;
        }

        // 1. Đêm 1: Lính Skeleton cơ bản, giới thiệu game
        DayWaveConfig waveNight1 = new DayWaveConfig
        {
            specificNight = 1,
            waveName = "Đột kích Đêm 1 (Giới thiệu)",
            spawnTimeRatio = 0.65f, // Xuất hiện hơi sớm
            countMultiplier = 1f
        };
        waveNight1.enemies.Add(new EnemyWaveEntry
        {
            name = "Skeleton",
            prefab = _enemyPrefab,
            baseCount = 3,
            increasePerNight = 0,
            maxCount = 5
        });
        _customWaves.Add(waveNight1);

        // 2. Đêm 2: Hai wave chồng nhau (Archer tiên phong, Skeleton theo sau)
        DayWaveConfig waveNight2Vanguard = new DayWaveConfig
        {
            specificNight = 2,
            waveName = "Đột kích Đêm 2 (Cung thủ Tiên phong)",
            spawnTimeRatio = 0.58f,
            countMultiplier = 1f
        };
        waveNight2Vanguard.enemies.Add(new EnemyWaveEntry
        {
            name = "EnemyArcher",
            prefab = _enemyArcherPrefab,
            baseCount = 2,
            increasePerNight = 0,
            maxCount = 5
        });
        _customWaves.Add(waveNight2Vanguard);

        DayWaveConfig waveNight2Main = new DayWaveConfig
        {
            specificNight = 2,
            waveName = "Đột kích Đêm 2 (Quân chủ lực)",
            spawnTimeRatio = 0.78f,
            countMultiplier = 1f
        };
        waveNight2Main.enemies.Add(new EnemyWaveEntry
        {
            name = "Skeleton",
            prefab = _enemyPrefab,
            baseCount = 5,
            increasePerNight = 0,
            maxCount = 10
        });
        _customWaves.Add(waveNight2Main);

        // 3. Đêm 3: Đợt tấn công lớn kết hợp
        DayWaveConfig waveNight3 = new DayWaveConfig
        {
            specificNight = 3,
            waveName = "Đột kích Đêm 3 (Hỗn hợp Cận chiến & Tầm xa)",
            spawnTimeRatio = 0.7f,
            countMultiplier = 1.1f
        };
        waveNight3.enemies.Add(new EnemyWaveEntry
        {
            name = "Skeleton",
            prefab = _enemyPrefab,
            baseCount = 6,
            increasePerNight = 1,
            maxCount = 12
        });
        waveNight3.enemies.Add(new EnemyWaveEntry
        {
            name = "EnemyArcher",
            prefab = _enemyArcherPrefab,
            baseCount = 3,
            increasePerNight = 0,
            maxCount = 6
        });
        _customWaves.Add(waveNight3);

        // 4. Đêm 5: Đêm Trăng Máu - Đợt càn quét kinh hoàng
        DayWaveConfig waveNight5 = new DayWaveConfig
        {
            specificNight = 5,
            waveName = "Đột kích Đêm 5 (Đại quân Trăng Máu)",
            spawnTimeRatio = 0.75f,
            countMultiplier = 1.5f
        };
        waveNight5.enemies.Add(new EnemyWaveEntry
        {
            name = "Skeleton",
            prefab = _enemyPrefab,
            baseCount = 12,
            increasePerNight = 2,
            maxCount = 25
        });
        waveNight5.enemies.Add(new EnemyWaveEntry
        {
            name = "EnemyArcher",
            prefab = _enemyArcherPrefab,
            baseCount = 6,
            increasePerNight = 1,
            maxCount = 15
        });
        _customWaves.Add(waveNight5);

        // 5. Cấu hình mặc định cho các đêm khác (specificNight = 0)
        DayWaveConfig defaultWave = new DayWaveConfig
        {
            specificNight = 0,
            waveName = "Đột kích Đêm (Mặc định)",
            spawnTimeRatio = _nightRaidSpawnTimeRatio,
            countMultiplier = 1f
        };
        defaultWave.enemies.Add(new EnemyWaveEntry
        {
            name = "Skeleton",
            prefab = _enemyPrefab,
            baseCount = _baseEnemyCount,
            increasePerNight = _enemyIncreasePerNight,
            maxCount = _maxEnemiesPerWave
        });
        defaultWave.enemies.Add(new EnemyWaveEntry
        {
            name = "EnemyArcher",
            prefab = _enemyArcherPrefab,
            baseCount = 1,
            increasePerNight = 1,
            maxCount = 10
        });
        _customWaves.Add(defaultWave);
    }

    private void AddEnemyArcherToWavesIfNeeded()
    {
        if (_enemyArcherPrefab == null) return;

        bool hasArcher = false;
        foreach (var wave in _customWaves)
        {
            foreach (var enemy in wave.enemies)
            {
                if (enemy.prefab == _enemyArcherPrefab || enemy.name == "EnemyArcher" || (enemy.prefab != null && enemy.prefab.name == "EnemyArcher"))
                {
                    hasArcher = true;
                    break;
                }
            }
            if (hasArcher) break;
        }

        if (!hasArcher && _customWaves.Count > 0)
        {
            _customWaves[0].enemies.Add(new EnemyWaveEntry
            {
                name = "EnemyArcher",
                prefab = _enemyArcherPrefab,
                baseCount = 1,
                increasePerNight = 1,
                maxCount = 10
            });
            Debug.Log("[EnemyManager] Tự động thêm EnemyArcher vào wave 1.");
        }
    }


    private void PrewarmConfiguredEnemies()
    {
        if (_enemyPoolPrewarmCount <= 0 || PoolManager.Instance == null)
        {
            return;
        }

        HashSet<GameObject> prewarmedPrefabs = new HashSet<GameObject>();
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

    private void PrepareNightRaidSchedule()
    {
        int night = Mathf.Max(1, _currentNightNumber);
        WeatherState weather = WeatherState.Clear;
        if (WeatherManager.Instance != null)
        {
            weather = WeatherManager.Instance.CurrentWeather;
        }

        _activeNightWaves.Clear();

        // 1. Search for custom waves matching the current night
        foreach (var wave in _customWaves)
        {
            if (wave.specificNight == night)
            {
                _activeNightWaves.Add(wave);
            }
        }

        // 2. If none, search for default custom waves (specificNight <= 0)
        if (_activeNightWaves.Count == 0)
        {
            foreach (var wave in _customWaves)
            {
                if (wave.specificNight <= 0)
                {
                    _activeNightWaves.Add(wave);
                }
            }
        }

        // 3. If still none, fallback to procedural wave generation
        if (_activeNightWaves.Count == 0)
        {
            int baseWaveCount = 1 + (night / 3);
            baseWaveCount = Mathf.Clamp(baseWaveCount, 1, 4);

            int waveCount = baseWaveCount;
            float multiplier = 1f;

            if (weather == WeatherState.BloodMoon)
            {
                waveCount = baseWaveCount * 2;
                multiplier = 1.6f;
            }

            for (int i = 0; i < waveCount; i++)
            {
                DayWaveConfig wave = new DayWaveConfig
                {
                    specificNight = 0,
                    waveName = $"{(weather == WeatherState.BloodMoon ? "Blood Moon" : "Night")} Wave {i + 1}",
                    countMultiplier = multiplier
                };

                if (waveCount == 1)
                {
                    wave.spawnTimeRatio = 0.7f;
                }
                else
                {
                    wave.spawnTimeRatio = 0.55f + ((float)i / (waveCount - 1)) * 0.35f;
                }

                wave.enemies.Add(new EnemyWaveEntry
                {
                    name = "Skeleton",
                    prefab = _enemyPrefab,
                    baseCount = _baseEnemyCount,
                    increasePerNight = _enemyIncreasePerNight,
                    maxCount = _maxEnemiesPerWave
                });

                if (night >= 2 || weather == WeatherState.BloodMoon)
                {
                    wave.enemies.Add(new EnemyWaveEntry
                    {
                        name = "EnemyArcher",
                        prefab = _enemyArcherPrefab,
                        baseCount = 1,
                        increasePerNight = 1,
                        maxCount = 10
                    });
                }

                _activeNightWaves.Add(wave);
            }
            Debug.Log($"[EnemyManager] Đêm {night} ({weather}): Không tìm thấy cấu hình custom, tự động sinh {waveCount} wave procedural.");
        }
        else
        {
            Debug.Log($"[EnemyManager] Đêm {night} ({weather}): Đã tải {_activeNightWaves.Count} wave cấu hình sẵn từ customWaves.");
        }

        _spawnedWavesThisNight = new bool[_activeNightWaves.Count];
    }

    private void ProcessScheduledNightWaves(float timeRatio)
    {
        if (_spawnedWavesThisNight.Length != _activeNightWaves.Count)
        {
            PrepareNightRaidSchedule();
        }

        bool anyPending = false;
        for (int i = 0; i < _activeNightWaves.Count; i++)
        {
            if (_spawnedWavesThisNight[i])
            {
                continue;
            }

            anyPending = true;
            if (timeRatio < _activeNightWaves[i].spawnTimeRatio)
            {
                continue;
            }

            if (TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
            {
                int successfulSpawns = SpawnWave(_currentNightNumber, i, centerSpawnPos, edgeName);
                Debug.Log($"[EnemyManager] Đêm {_currentNightNumber}: Wave {i + 1}/{_activeNightWaves.Count} spawned {successfulSpawns} enemies.");
            }

            _spawnedWavesThisNight[i] = true;
        }

        if (!anyPending || AllWavesSpawned())
        {
            _nightRaidPending = false;
        }
    }

    private bool AllWavesSpawned()
    {
        for (int i = 0; i < _spawnedWavesThisNight.Length; i++)
        {
            if (!_spawnedWavesThisNight[i])
            {
                return false;
            }
        }
        return true;
    }

    private int GetNextUnspawnedWaveIndex()
    {
        EnsureDefaultWaveConfig();
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

    private void SpawnLegacyNightWave()
    {
        CleanupActiveEnemies();
        if (!TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName))
        {
            return;
        }

        int requestedEnemyCount = GetEnemyCountForNight(_currentNightNumber);
        int successfulSpawns = SpawnEnemyGroup(_enemyPrefab, "Enemy", requestedEnemyCount, _currentNightNumber, 0, centerSpawnPos);
        Debug.Log($"[EnemyManager] Đêm {_currentNightNumber}: Legacy wave spawned {successfulSpawns}/{requestedEnemyCount} enemies from {edgeName}.");
    }

    private int SpawnWave(int nightNumber, int waveIndex, Vector3 centerSpawnPos, string edgeName)
    {
        EnsureDefaultWaveConfig();
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

        Debug.Log($"[EnemyManager] Đêm {nightNumber}: {wave.waveName} tại {edgeName}, spawned {totalSpawned}/{totalRequested}, active {_activeEnemies.Count}/{_maxActiveEnemies}.");
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
            finalMultiplier *= 1.6f;
        }
        
        return Mathf.RoundToInt(cappedCount * Mathf.Max(0f, finalMultiplier));
    }

    private GameObject GetEntryPrefab(EnemyWaveEntry entry)
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
            Debug.Log($"[EnemyManager] Đêm {nightNumber}: Bỏ qua group {enemyName} vì đã đạt giới hạn active enemies ({_activeEnemies.Count}/{_maxActiveEnemies}).");
            return 0;
        }

        // Tinh toan vi tri rally point cho group nay (huong vao tam ban do 15 met)
        Vector3 mapCenter = Vector3.zero;
        if (_gridSystem != null)
        {
            mapCenter = _gridSystem.GetWorldPosition(_gridSystem.GetWidth() / 2, _gridSystem.GetLength() / 2);
        }
        Vector3 toCenterDir = (mapCenter - centerSpawnPos).normalized;
        Vector3 rallyPos = centerSpawnPos + toCenterDir * 15f;
        if (NavMesh.SamplePosition(rallyPos, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            rallyPos = hit.position;
        }

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
            enemyController.SetRallyPoint(rallyPos); // Bat dau di chuyen den rally point truoc

            // Áp dụng tăng chỉ số nếu là đêm Trăng Máu (Blood Moon)
            if (WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.BloodMoon)
            {
                enemyController.ApplyStatMultipliers(1.5f, 1.3f, 1.15f);
            }

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
            StartCoroutine(ReleaseGroupWhenReady(spawnedGroup, rallyPos));
        }

        return successfulSpawns;
    }

    private System.Collections.IEnumerator ReleaseGroupWhenReady(List<EnemyUnitController> group, Vector3 rallyPosition)
    {
        float elapsed = 0f;
        float maxWait = Mathf.Max(0.5f, _groupRallyMaxWait);
        float readyRadiusSqr = _groupRallyRadius * _groupRallyRadius;

        while (elapsed < maxWait)
        {
            int activeCount = 0;
            int readyCount = 0;

            foreach (EnemyUnitController enemy in group)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.currentState == CombatState.Dead)
                {
                    continue;
                }

                activeCount++;
                if ((enemy.transform.position - rallyPosition).sqrMagnitude <= readyRadiusSqr)
                {
                    readyCount++;
                }
            }

            if (activeCount == 0 || (float)readyCount / activeCount >= _groupRallyMinReadyRatio)
            {
                break;
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        foreach (var enemy in group)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.currentState != CombatState.Dead)
            {
                enemy.ReleaseRally();
            }
        }
    }

    private Vector3 GetSpawnPositionNear(Vector3 centerSpawnPos)
    {
        Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
        Vector3 spawnPos = centerSpawnPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (Terrain.activeTerrain != null)
        {
            spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos);
        }

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, _spawnRadius * 2f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        return spawnPos;
    }

    private bool TryGetSpawnEdge(out Vector3 centerSpawnPos, out string edgeName)
    {
        centerSpawnPos = Vector3.zero;
        edgeName = "";

        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
            if (_gridSystem == null)
            {
                Debug.LogError("[EnemyManager] Không tìm thấy GridSystem trong cảnh!");
                return false;
            }
        }

        int width = _gridSystem.GetWidth();
        int length = _gridSystem.GetLength();
        if (width <= 0 || length <= 0)
        {
            Debug.LogError($"[EnemyManager] Kích thước GridSystem không hợp lệ: {width}x{length}");
            return false;
        }

        int bestEdgeChoice = 0;
        int bestEdgeX = 0;
        int bestEdgeZ = length - 1;
        float bestScore = float.NegativeInfinity;
        int candidateCount = Mathf.Max(4, _spawnEdgeCandidateCount);

        for (int i = 0; i < candidateCount; i++)
        {
            int edgeChoice = Random.Range(0, 4);
            GetRandomPointOnEdge(edgeChoice, width, length, out int edgeX, out int edgeZ);

            Vector3 candidatePos = _gridSystem.GetWorldPosition(edgeX, edgeZ);
            float score = GetSpawnCandidateScore(candidatePos, edgeChoice);
            if (score > bestScore)
            {
                bestScore = score;
                bestEdgeChoice = edgeChoice;
                bestEdgeX = edgeX;
                bestEdgeZ = edgeZ;
            }
        }

        centerSpawnPos = _gridSystem.GetWorldPosition(bestEdgeX, bestEdgeZ);
        edgeName = GetSpawnEdgeName(bestEdgeChoice);
        _lastSpawnEdgeChoice = bestEdgeChoice;
        return true;
    }

    private void GetRandomPointOnEdge(int edgeChoice, int width, int length, out int edgeX, out int edgeZ)
    {
        edgeX = 0;
        edgeZ = 0;

        switch (edgeChoice)
        {
            case 0:
                edgeX = Random.Range(0, width);
                edgeZ = length - 1;
                break;
            case 1:
                edgeX = Random.Range(0, width);
                edgeZ = 0;
                break;
            case 2:
                edgeX = 0;
                edgeZ = Random.Range(0, length);
                break;
            default:
                edgeX = width - 1;
                edgeZ = Random.Range(0, length);
                break;
        }
    }

    private float GetSpawnCandidateScore(Vector3 candidatePos, int edgeChoice)
    {
        float score = Random.Range(0f, 5f);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float cameraDistance = Vector3.Distance(candidatePos, mainCamera.transform.position);
            score += Mathf.Min(cameraDistance, _minSpawnDistanceFromCamera) * 0.5f;
            if (cameraDistance < _minSpawnDistanceFromCamera)
            {
                score -= (_minSpawnDistanceFromCamera - cameraDistance) * 2f;
            }
        }

        BuildingManager buildingManager = BuildingManager.Instance != null ? BuildingManager.Instance : FindAnyObjectByType<BuildingManager>();
        if (buildingManager != null && buildingManager.MainBuildingInstance != null)
        {
            float mainBuildingDistance = Vector3.Distance(candidatePos, buildingManager.MainBuildingInstance.transform.position);
            score += mainBuildingDistance * 0.15f;
        }

        if (_avoidRepeatingSpawnEdge && edgeChoice == _lastSpawnEdgeChoice)
        {
            score -= 25f;
        }

        return score;
    }

    private string GetSpawnEdgeName(int edgeChoice)
    {
        switch (edgeChoice)
        {
            case 0:
                return "North";
            case 1:
                return "South";
            case 2:
                return "West";
            default:
                return "East";
        }
    }

    private void CleanupActiveEnemies()
    {
        _activeEnemies.RemoveAll(enemy => enemy == null || enemy.currentState == CombatState.Dead || !enemy.gameObject.activeInHierarchy);
    }
}
