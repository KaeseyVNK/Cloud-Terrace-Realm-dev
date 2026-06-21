using UnityEngine;
using System.Collections;

public class RiceField : MonoBehaviour
{
    public enum RiceFieldState
    {
        Empty,
        Growing,
        Ripe
    }

    [Header("Visual States")]
    [SerializeField] private GameObject _emptyVisual;
    [SerializeField] private GameObject _growingVisual;
    [SerializeField] private GameObject _ripeVisual;

    [Header("Growth Settings")]
    [SerializeField] private float _growingDuration = 15f;
    [SerializeField] private float _ripeDuration = 15f;

    [Header("Harvest Settings")]
    [SerializeField] private GameObject _harvestNodePrefab; // Prefab con chứa ResourceNode
    [SerializeField] private int _riceYield = 100; // Lượng thực phẩm thu hoạch mỗi đợt
    [SerializeField] private Transform _harvestNodeSpawnPoint; // Điểm xuất hiện node gặt (mặc định là tâm ruộng)

    private RiceFieldState _currentState = RiceFieldState.Empty;
    public RiceFieldState CurrentState => _currentState;

    private GameObject _activeHarvestNode;
    private float _growthProgress = 0f;
    private bool _isDroughtAffected = false;

    private void Update()
    {
        if (_currentState == RiceFieldState.Growing)
        {
            WeatherState weather = WeatherManager.Instance != null ? WeatherManager.Instance.CurrentWeather : WeatherState.Clear;
            if (weather == WeatherState.Rain)
            {
                // Tự động lớn gấp đôi khi trời mưa
                FarmProgress(Time.deltaTime * 2f, isAuto: true);
            }
            else if (weather == WeatherState.Drought)
            {
                // Ghi nhận bị ảnh hưởng bởi nắng hạn
                _isDroughtAffected = true;
            }
        }
    }

    private void Start()
    {
        SetState(RiceFieldState.Empty);
    }

    public void SetState(RiceFieldState newState)
    {
        _currentState = newState;
        _growthProgress = 0f;

        if (newState == RiceFieldState.Empty)
        {
            _isDroughtAffected = false;
        }

        // Bật/tắt Visual
        if (_emptyVisual != null) _emptyVisual.SetActive(true); // Đất bùn luôn luôn hiển thị
        if (_growingVisual != null) _growingVisual.SetActive(_currentState == RiceFieldState.Growing);
        if (_ripeVisual != null) _ripeVisual.SetActive(_currentState == RiceFieldState.Ripe);

        if (_currentState == RiceFieldState.Ripe)
        {
            SpawnHarvestNode();
        }
    }

    public void FarmProgress(float amount, bool isAuto = false)
    {
        WeatherState weather = WeatherManager.Instance != null ? WeatherManager.Instance.CurrentWeather : WeatherState.Clear;

        if (weather == WeatherState.Rain)
        {
            // Trời mưa: chỉ cho phép tự động lớn (isAuto = true)
            if (!isAuto) return;
        }
        else if (weather == WeatherState.Drought)
        {
            // Hạn hán:
            // Nguồn tự động (isAuto = true) không tăng tiến độ (đóng băng).
            // Dân làng chăm sóc (isAuto = false) tăng tiến độ với tốc độ giảm một nửa (0.5x).
            if (isAuto) return;
            amount *= 0.5f;
            _isDroughtAffected = true;
        }
        else
        {
            // Các thời tiết khác (Clear, BloodMoon):
            // Nguồn tự động không được tăng tiến độ.
            if (isAuto) return;
        }

        _growthProgress += amount;
        if (_currentState == RiceFieldState.Empty)
        {
            if (_growthProgress >= _growingDuration)
            {
                SetState(RiceFieldState.Growing);
            }
        }
        else if (_currentState == RiceFieldState.Growing)
        {
            if (_growthProgress >= _ripeDuration)
            {
                SetState(RiceFieldState.Ripe);
            }
        }
    }

    private void SpawnHarvestNode()
    {
        if (_activeHarvestNode != null)
        {
            Destroy(_activeHarvestNode);
        }

        if (_harvestNodePrefab == null)
        {
            Debug.LogError($"[RiceField] Chưa gán _harvestNodePrefab trên {gameObject.name}");
            return;
        }

        Vector3 spawnPos = _harvestNodeSpawnPoint != null ? _harvestNodeSpawnPoint.position : transform.position;
        _activeHarvestNode = Instantiate(_harvestNodePrefab, spawnPos, Quaternion.identity, transform);
        _activeHarvestNode.name = "RiceHarvestNode";

        ResourceNode node = _activeHarvestNode.GetComponent<ResourceNode>();
        if (node == null) node = _activeHarvestNode.GetComponentInChildren<ResourceNode>();

        if (node != null)
        {
            node.ResourceType = ResourceType.Food;
            int finalYield = _riceYield;
            if (_isDroughtAffected)
            {
                finalYield = Mathf.RoundToInt(_riceYield * 0.7f);
                Debug.Log($"[RiceField] Thu hoạch bị giảm 30% do nắng hạn trên {gameObject.name}. Sản lượng thực tế: {finalYield}");
            }
            node.CurrentQuantity = finalYield;
            // Đăng ký sự kiện khi thu hoạch xong
            node.OnDepleted += HandleHarvestCompleted;
        }
    }

    private void HandleHarvestCompleted(ResourceNode node)
    {
        if (node != null)
        {
            node.OnDepleted -= HandleHarvestCompleted;
        }
        _activeHarvestNode = null;
        Debug.Log($"[RiceField] Thu hoạch xong ruộng lúa {gameObject.name}. Bắt đầu vụ mùa mới!");
        SetState(RiceFieldState.Empty);
    }

    private void OnDestroy()
    {
        if (_activeHarvestNode != null)
        {
            ResourceNode node = _activeHarvestNode.GetComponent<ResourceNode>();
            if (node != null) node.OnDepleted -= HandleHarvestCompleted;
        }
    }
}
