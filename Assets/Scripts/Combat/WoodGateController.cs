using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls the wooden gate building. Automatically opens when player units (villagers/soldiers)
/// are nearby, disabling the NavMeshObstacle to allow passage. Closes when no player units are nearby.
/// </summary>
public class WoodGateController : MonoBehaviour
{
    [Header("Gate Settings")]
    [Tooltip("Transform of the moving gate door object. If null, auto-finds 'fence_wood_straight_gate'")]
    [SerializeField] private Transform _gateDoorTransform;

    [Tooltip("Distance at which the gate opens for player units")]
    [SerializeField] private float _detectionRadius = 4.5f;

    [Tooltip("Time interval between checks (to optimize performance)")]
    [SerializeField] private float _checkInterval = 0.2f;

    [Tooltip("Speed of opening/closing rotation animation")]
    [SerializeField] private float _rotateSpeed = 5f;

    private NavMeshObstacle _navObstacle;
    private BoxCollider _boxCollider;
    private ConstructibleBuilding _building;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private bool _isOpen = false;
    private float _nextCheckTime;

    void Start()
    {
        _building = GetComponent<ConstructibleBuilding>();
        _navObstacle = GetComponentInChildren<NavMeshObstacle>();
        _boxCollider = GetComponentInChildren<BoxCollider>();

        // Auto-find gate door if not assigned
        if (_gateDoorTransform == null)
        {
            _gateDoorTransform = transform.Find("fence_wood_straight_gate");
        }

        if (_gateDoorTransform != null)
        {
            // Store closed rotation as the initial rotation of the gate door
            _closedRotation = _gateDoorTransform.localRotation;
            // Rotate 90 degrees outward around Y axis to open
            _openRotation = _closedRotation * Quaternion.Euler(0f, 90f, 0f);
        }
        else
        {
            GameLog.LogWarning($"[WoodGateController] Gate door child 'fence_wood_straight_gate' not found on {gameObject.name}!");
        }
    }

    void Update()
    {
        // Do not manage gate until construction is fully complete (or if component is missing/ghost)
        if (_building == null || !_building.IsCompleted)
        {
            return;
        }

        // Periodic distance check to optimize CPU usage
        if (Time.time >= _nextCheckTime)
        {
            _nextCheckTime = Time.time + _checkInterval;
            CheckNearbyPlayerUnits();
        }

        // Smoothly animate the gate door opening/closing
        if (_gateDoorTransform != null)
        {
            Quaternion targetRot = _isOpen ? _openRotation : _closedRotation;
            _gateDoorTransform.localRotation = Quaternion.Slerp(_gateDoorTransform.localRotation, targetRot, Time.deltaTime * _rotateSpeed);
        }
    }

    private void CheckNearbyPlayerUnits()
    {
        bool hasFriendlyNearby = false;
        float radiusSqr = _detectionRadius * _detectionRadius;
        Vector3 myPos = transform.position;

        // 1. Check Combat Units Registry
        if (BaseCombatUnitController.Registry != null)
        {
            for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
            {
                var unit = BaseCombatUnitController.Registry[i];
                if (unit != null && unit.faction == UnitFaction.Player && unit.currentState != CombatState.Dead)
                {
                    // Ignore self and other static buildings (fences, towers, main building, etc.)
                    if (unit.gameObject == gameObject || unit is BuildingCombatTarget || unit is MainBuildingCombatTarget)
                    {
                        continue;
                    }

                    if (Vector3.SqrMagnitude(unit.transform.position - myPos) <= radiusSqr)
                    {
                        hasFriendlyNearby = true;
                        break;
                    }
                }
            }
        }

        // 2. Check Villagers if no combat unit is nearby
        if (!hasFriendlyNearby && VillagerController.AllVillagers != null)
        {
            for (int i = 0; i < VillagerController.AllVillagers.Count; i++)
            {
                var villager = VillagerController.AllVillagers[i];
                if (villager != null && villager.gameObject.activeInHierarchy)
                {
                    if (Vector3.SqrMagnitude(villager.transform.position - myPos) <= radiusSqr)
                    {
                        hasFriendlyNearby = true;
                        break;
                    }
                }
            }
        }

        // Set open/close state based on presence of player units
        if (hasFriendlyNearby)
        {
            if (!_isOpen)
            {
                OpenGate();
            }
        }
        else
        {
            if (_isOpen)
            {
                CloseGate();
            }
        }
    }

    private void OpenGate()
    {
        _isOpen = true;
        
        // Disable NavMeshObstacle and BoxCollider so friendly units can pass through
        if (_navObstacle != null)
        {
            _navObstacle.enabled = false;
        }
        if (_boxCollider != null)
        {
            _boxCollider.enabled = false;
        }

        GameLog.Log($"[WoodGateController] Opening gate: {gameObject.name}");
    }

    private void CloseGate()
    {
        _isOpen = false;
        
        // Re-enable NavMeshObstacle and BoxCollider to block pathfinding and attacks
        if (_navObstacle != null)
        {
            _navObstacle.enabled = true;
        }
        if (_boxCollider != null)
        {
            _boxCollider.enabled = true;
        }

        GameLog.Log($"[WoodGateController] Closing gate: {gameObject.name}");
    }
}
