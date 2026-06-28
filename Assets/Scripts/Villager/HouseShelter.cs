using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Component attached to houses (like homeblue) to handle villager sheltering.
/// </summary>
public class HouseShelter : MonoBehaviour
{
    #region Serialized Fields

    [Tooltip("Maximum capacity of villagers this house can shelter")]
    [SerializeField] private int _capacity = 5;

    [Tooltip("Distance threshold at which a villager is considered arrived and enters the shelter")]
    [SerializeField] private float _enterDistance = 1.5f;

    #endregion

    #region Private Fields

    private readonly List<VillagerController> _shelteredVillagers = new();
    private readonly List<VillagerController> _incomingVillagers = new();
    private ConstructibleBuilding _constructibleBuilding;

    #endregion

    #region Static Fields

    /// <summary>
    /// Global flag indicating if the emergency shelter mode is active.
    /// </summary>
    public static bool IsEmergencyShelterActive { get; set; } = false;

    public static readonly List<HouseShelter> Registry = new List<HouseShelter>();

    #endregion

    #region Public Properties

    /// <summary>
    /// The maximum capacity of this house.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Current number of sheltered villagers in this house.
    /// </summary>
    public int OccupantCount => _shelteredVillagers.Count;

    /// <summary>
    /// True if there is space in the shelter including incoming reservations.
    /// </summary>
    public bool HasSpace => (_shelteredVillagers.Count + _incomingVillagers.Count) < _capacity;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void Awake()
    {
        _constructibleBuilding = GetComponent<ConstructibleBuilding>();
    }

    private void Update()
    {
        if (!IsOperational()) return;

        // Auto-eject villagers if weather/night is clear and emergency is not active
        bool isNightShelterNeeded = VillagerController.ShouldShelterAtNight && TimeManager.Instance != null && TimeManager.Instance.IsNight;
        if (!IsEmergencyShelterActive && 
            !isNightShelterNeeded && 
            WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather != WeatherState.Rain)
        {
            if (_shelteredVillagers.Count > 0)
            {
                EjectAll();
            }
        }

        // Process incoming reserved villagers
        float enterDistanceSqr = _enterDistance * _enterDistance;
        for (int i = _incomingVillagers.Count - 1; i >= 0; i--)
        {
            VillagerController villager = _incomingVillagers[i];
            if (villager == null)
            {
                _incomingVillagers.RemoveAt(i);
                continue;
            }

            // Check if they reached the house
            if ((villager.transform.position - transform.position).sqrMagnitude <= enterDistanceSqr)
            {
                _incomingVillagers.RemoveAt(i);
                EnterShelter(villager);
            }
        }
    }

    private void OnDisable()
    {
        Registry.Remove(this);
        EjectAll();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the capacity of this house (used dynamically when added).
    /// </summary>
    /// <param name="capacity">Capacity value.</param>
    public void SetCapacity(int capacity)
    {
        _capacity = capacity;
    }

    /// <summary>
    /// Checks if the house is operational (i.e. not under construction).
    /// </summary>
    public bool IsOperational()
    {
        if (_constructibleBuilding == null)
        {
            _constructibleBuilding = GetComponent<ConstructibleBuilding>();
            if (_constructibleBuilding == null)
            {
                _constructibleBuilding = GetComponentInParent<ConstructibleBuilding>();
            }
        }

        if (_constructibleBuilding != null)
        {
            return _constructibleBuilding.IsCompleted;
        }

        return true;
    }

    /// <summary>
    /// Tries to reserve a spot for a villager.
    /// </summary>
    public bool TryReserveSpot(VillagerController villager)
    {
        if (villager == null || !IsOperational() || !HasSpace) return false;
        if (_shelteredVillagers.Contains(villager) || _incomingVillagers.Contains(villager)) return true;

        _incomingVillagers.Add(villager);
        return true;
    }

    /// <summary>
    /// Cancels reservation for a villager.
    /// </summary>
    public void CancelReservation(VillagerController villager)
    {
        if (villager != null)
        {
            _incomingVillagers.Remove(villager);
        }
    }

    /// <summary>
    /// Force a villager to enter the shelter.
    /// </summary>
    public void EnterShelter(VillagerController villager)
    {
        if (villager == null) return;
        
        if (!_shelteredVillagers.Contains(villager))
        {
            _shelteredVillagers.Add(villager);
        }

        _incomingVillagers.Remove(villager);

        // Deselect the unit if selected
        var selectable = villager.GetComponent<SelectableUnit>();
        if (selectable != null && UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.DeselectUnit(selectable);
        }

        // Clean up assigned shelter in the villager before disabling
        villager.ClearAssignedShelter();

        // Change state and disable game object
        villager.ChangeState(VillagerState.Sheltered);
        villager.gameObject.SetActive(false);
        
        Debug.Log($"[HouseShelter] Villager {villager.gameObject.name} entered shelter {gameObject.name}. Occupants: {_shelteredVillagers.Count}/{_capacity}");
    }

    /// <summary>
    /// Eject all villagers in the shelter.
    /// </summary>
    public void EjectAll()
    {
        if (gameObject.activeInHierarchy && Application.isPlaying)
        {
            StartCoroutine(EjectAllRoutine());
        }
        else
        {
            // Fallback khi tắt/xóa đối tượng
            for (int i = _shelteredVillagers.Count - 1; i >= 0; i--)
            {
                EjectVillagerImmediate(_shelteredVillagers[i], i);
            }
            _shelteredVillagers.Clear();
            _incomingVillagers.Clear();
        }
    }

    private System.Collections.IEnumerator EjectAllRoutine()
    {
        // Tạo bản sao danh sách để tránh sửa đổi bộ sưu tập khi đang duyệt
        List<VillagerController> toEject = new List<VillagerController>(_shelteredVillagers);
        _shelteredVillagers.Clear();
        _incomingVillagers.Clear();

        for (int i = 0; i < toEject.Count; i++)
        {
            if (toEject[i] != null)
            {
                EjectVillagerImmediate(toEject[i], i);
                // Giãn cách 0.08 giây (khoảng 5-6 frame ở 60fps) giữa các dân làng để tránh dồn ứ pathfinding
                yield return new WaitForSeconds(0.08f);
            }
        }
    }

    #endregion

    #region Private Methods

    private void EjectVillagerImmediate(VillagerController villager, int index)
    {
        if (villager == null) return;

        // Ước lượng bán kính vật lý của công trình từ Collider hoặc Renderer để tránh dân làng spawn trong tường
        float ejectSpacing = 1.5f;
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            ejectSpacing = Mathf.Max(ejectSpacing, col.bounds.extents.x + 1.2f);
        }
        else
        {
            Renderer ren = GetComponentInChildren<Renderer>();
            if (ren != null)
            {
                ejectSpacing = Mathf.Max(ejectSpacing, ren.bounds.extents.x + 1.2f);
            }
        }

        float angle = index * 360f / Mathf.Max(1, _capacity);
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * ejectSpacing;
        Vector3 ejectPos = transform.position + offset;

        if (NavMesh.SamplePosition(ejectPos, out NavMeshHit hit, 5f, ~2))
        {
            ejectPos = hit.position;
        }

        // Activate and resume state
        villager.transform.position = ejectPos;
        villager.gameObject.SetActive(true);

        NavMeshAgent agent = villager.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(ejectPos);
        }

        villager.ResumePostShelterState();

        Debug.Log($"[HouseShelter] Ejected villager {villager.gameObject.name} from {gameObject.name}.");
    }

    #endregion
}
