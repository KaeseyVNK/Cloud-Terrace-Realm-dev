using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls the wooden gate building. Automatically opens when player units (villagers/soldiers)
/// are nearby, disabling NavMeshObstacles to allow passage. Closes when no player units are nearby.
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

    private NavMeshObstacle[] _navObstacles;
    private BoxCollider _rootBoxCollider;
    private ConstructibleBuilding _building;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private bool _isOpen = false;
    private bool _initializedClosedState;
    private float _nextCheckTime;

    void Start()
    {
        _building = GetComponent<ConstructibleBuilding>();
        EnsureObstacleCache();

        if (_gateDoorTransform == null)
        {
            _gateDoorTransform = transform.Find("fence_wood_straight_gate");
        }

        if (_gateDoorTransform != null)
        {
            _closedRotation = _gateDoorTransform.localRotation;
            _openRotation = _closedRotation * Quaternion.Euler(0f, 90f, 0f);
        }
        else
        {
            GameLog.LogWarning($"[WoodGateController] Gate door child 'fence_wood_straight_gate' not found on {gameObject.name}!");
        }

        TryInitializeClosedState();
    }

    void Update()
    {
        if (_building == null || !_building.IsCompleted)
        {
            return;
        }

        TryInitializeClosedState();

        if (Time.time >= _nextCheckTime)
        {
            _nextCheckTime = Time.time + _checkInterval;
            CheckNearbyPlayerUnits();
        }

        if (_gateDoorTransform != null)
        {
            Quaternion targetRot = _isOpen ? _openRotation : _closedRotation;
            _gateDoorTransform.localRotation = Quaternion.Slerp(_gateDoorTransform.localRotation, targetRot, Time.deltaTime * _rotateSpeed);
        }
    }

    private void TryInitializeClosedState()
    {
        if (_initializedClosedState)
        {
            return;
        }

        if (_building != null && !_building.IsCompleted)
        {
            return;
        }

        _initializedClosedState = true;
        CloseGate();
    }

    private void CheckNearbyPlayerUnits()
    {
        bool hasFriendlyNearby = false;
        float radiusSqr = _detectionRadius * _detectionRadius;
        Vector3 myPos = transform.position;

        if (BaseCombatUnitController.Registry != null)
        {
            for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
            {
                var unit = BaseCombatUnitController.Registry[i];
                if (unit != null && unit.faction == UnitFaction.Player && unit.currentState != CombatState.Dead)
                {
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

        if (hasFriendlyNearby)
        {
            if (!_isOpen)
            {
                OpenGate();
            }
        }
        else if (_isOpen)
        {
            CloseGate();
        }
    }

    public void ForceClosedState()
    {
        EnsureObstacleCache();
        _initializedClosedState = true;
        CloseGate();
    }

    private void EnsureObstacleCache()
    {
        if (_navObstacles == null || _navObstacles.Length == 0)
        {
            _navObstacles = GetComponentsInChildren<NavMeshObstacle>(true);
        }

        if (_rootBoxCollider == null)
        {
            _rootBoxCollider = GetComponent<BoxCollider>();
        }
    }

    private void OpenGate()
    {
        EnsureObstacleCache();
        _isOpen = true;
        SetGateBlockingColliders(false);
        GameLog.LogVerbose($"[WoodGateController] Opening gate: {gameObject.name}");
    }

    private void CloseGate()
    {
        EnsureObstacleCache();
        _isOpen = false;
        SetGateBlockingColliders(true);
        GameLog.LogVerbose($"[WoodGateController] Closing gate: {gameObject.name}");
    }

    private void SetGateBlockingColliders(bool block)
    {
        if (_navObstacles != null)
        {
            for (int i = 0; i < _navObstacles.Length; i++)
            {
                NavMeshObstacle obstacle = _navObstacles[i];
                if (obstacle != null)
                {
                    obstacle.enabled = block;
                }
            }
        }

        if (_rootBoxCollider != null)
        {
            _rootBoxCollider.enabled = block;
        }
    }
}
