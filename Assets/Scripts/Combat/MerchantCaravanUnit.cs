using UnityEngine;
using UnityEngine.AI;

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
        Debug.Log("[MerchantCaravanUnit] Một ngựa thồ thương nhân đã bị tiêu diệt!");
        if (MapEventManager.Instance != null)
        {
            MapEventManager.Instance.OnCaravanFailed(this);
        }
        base.OnDeath();
    }
}
