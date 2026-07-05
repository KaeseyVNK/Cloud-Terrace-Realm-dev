using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Đại diện cho một đơn vị Ngựa thồ Thương nhân (Merchant Caravan Unit) trong sự kiện hộ tống.
/// Tự động di chuyển dọc theo NavMesh đến điểm đích được chỉ định và cập nhật hoạt họa cho Animator.
/// </summary>
public class MerchantCaravanUnit : BaseCombatUnitController
{
    private Vector3 _destination;
    private bool _hasDest = false;
    private float _checkTimer = 0.5f;

    public MerchantCaravanUnit()
    {
        faction = UnitFaction.Player; // Đặt phe Player để quái vật tự động tấn công
        unitName = "Merchant Caravan";
    }

    /// <summary>
    /// Gán điểm đích di chuyển cho đoàn thương nhân.
    /// </summary>
    public void SetDestination(Vector3 dest)
    {
        if (NavMesh.SamplePosition(dest, out NavMeshHit destHit, 30f, NavMesh.AllAreas))
        {
            _destination = destHit.position;
        }
        else
        {
            _destination = dest;
        }
        _hasDest = true;

        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
            navAgent.speed = 2.2f;
            navAgent.stoppingDistance = 1.0f;
            navAgent.SetDestination(_destination);
        }
    }

    protected override void Start()
    {
        base.Start();

        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
            navAgent.speed = 2.2f;
            navAgent.stoppingDistance = 1.0f;
            if (_hasDest)
            {
                if (NavMesh.SamplePosition(_destination, out NavMeshHit destHit, 30f, NavMesh.AllAreas))
                {
                    _destination = destHit.position;
                }
                navAgent.SetDestination(_destination);
            }
        }

        // Đảm bảo có FogVisibilityTarget để hiển thị theo tầm nhìn sương mù
        if (GetComponent<FogVisibilityTarget>() == null)
        {
            gameObject.AddComponent<FogVisibilityTarget>();
        }
    }

    protected override void Update()
    {
        // Bỏ qua logic tìm mục tiêu tấn công của lính chiến thông thường
        if (currentState == CombatState.Dead) return;

        // Giữ lộ trình di chuyển tới đích
        if (_hasDest && navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            // Chỉ đặt lại đích nếu agent thực sự không có lộ trình nào và không đang tính toán lộ trình
            if (!navAgent.hasPath && !navAgent.pathPending)
            {
                navAgent.SetDestination(_destination);
            }

            // Cập nhật Animator dựa trên vận tốc thực tế
            if (animator != null)
            {
                float speed = navAgent.velocity.magnitude;
                if (speed > 0.1f)
                {
                    // Horse_001 BlendTree: Vert = 1.0f (moving), State = 0.0f (walk)
                    animator.SetFloat("Vert", 1f);
                    animator.SetFloat("State", 0f);
                }
                else
                {
                    // Vert = 0.0f (idle)
                    animator.SetFloat("Vert", 0f);
                }
            }
        }

        // Định kỳ kiểm tra xem đã về tới đích chưa
        _checkTimer -= Time.deltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = 0.5f;
            if (_hasDest)
            {
                float dist = Vector3.Distance(transform.position, _destination);
                if (dist <= 6.0f)
                {
                    if (MapEventManager.Instance != null)
                    {
                        MapEventManager.Instance.OnCaravanEscorted(this);
                    }
                    Destroy(gameObject);
                }
            }
        }
    }

    protected override void OnDeath()
    {
        GameLog.Log("[MerchantCaravanUnit] Một ngựa thồ thương nhân đã bị tiêu diệt!");
        if (MapEventManager.Instance != null)
        {
            MapEventManager.Instance.OnCaravanFailed(this);
        }
        base.OnDeath();
    }

    [Header("Caravan Group Fields (Saved)")]
    [HideInInspector] public Vector3 startPos;
    [HideInInspector] public Vector3 destPos;
    [HideInInspector] public int caravanSize;
    [HideInInspector] public bool isFinished;
    [HideInInspector] public bool ambush30Triggered;
    [HideInInspector] public bool ambush60Triggered;
    [HideInInspector] public bool ambush90Triggered;

    [System.Serializable]
    private class CaravanUnitSaveState
    {
        public int currentHealth;
        public string unitName;
        public Vector3 destination;
        public bool hasDest;
        public Vector3 startPos;
        public Vector3 destPos;
        public int caravanSize;
        public bool isFinished;
        public bool ambush30Triggered;
        public bool ambush60Triggered;
        public bool ambush90Triggered;
    }

    public override string CaptureState()
    {
        var state = new CaravanUnitSaveState
        {
            currentHealth = this.currentHealth,
            unitName = this.unitName,
            destination = this._destination,
            hasDest = this._hasDest,
            startPos = this.startPos,
            destPos = this.destPos,
            caravanSize = this.caravanSize,
            isFinished = this.isFinished,
            ambush30Triggered = this.ambush30Triggered,
            ambush60Triggered = this.ambush60Triggered,
            ambush90Triggered = this.ambush90Triggered
        };

        if (MapEventManager.Instance != null)
        {
            var group = FindMyGroup();
            if (group != null)
            {
                state.startPos = group.startPos;
                state.destPos = group.destPos;
                state.caravanSize = group.size;
                state.isFinished = group.isFinished;
                state.ambush30Triggered = group.ambush30Triggered;
                state.ambush60Triggered = group.ambush60Triggered;
                state.ambush90Triggered = group.ambush90Triggered;
            }
        }

        return JsonUtility.ToJson(state);
    }

    public override void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        var state = JsonUtility.FromJson<CaravanUnitSaveState>(stateJson);
        if (state == null) return;

        this.unitName = state.unitName;
        this.currentHealth = Mathf.Clamp(state.currentHealth, 1, this.maxHealth);
        this._destination = state.destination;
        this._hasDest = state.hasDest;

        this.startPos = state.startPos;
        this.destPos = state.destPos;
        this.caravanSize = state.caravanSize;
        this.isFinished = state.isFinished;
        this.ambush30Triggered = state.ambush30Triggered;
        this.ambush60Triggered = state.ambush60Triggered;
        this.ambush90Triggered = state.ambush90Triggered;

        // Apply path destination to NavMeshAgent if destination is set
        if (_hasDest && navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.SetDestination(_destination);
        }
    }

    private MapEventManager.MerchantCaravanGroup FindMyGroup()
    {
        // Sử dụng reflection để lấy _activeCaravans từ MapEventManager
        var field = typeof(MapEventManager).GetField("_activeCaravans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var caravans = field.GetValue(MapEventManager.Instance) as List<MapEventManager.MerchantCaravanGroup>;
            if (caravans != null)
            {
                foreach (var group in caravans)
                {
                    if (group != null && group.units.Contains(this))
                    {
                        return group;
                    }
                }
            }
        }
        return null;
    }
}
