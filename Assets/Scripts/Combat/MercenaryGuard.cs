using UnityEngine;

/// <summary>
/// Hộ tống quân thuê tại Chợ để bảo vệ xe hàng Caravan vào ban đêm.
/// Khi ở trạng thái Idle, lính sẽ tự động di chuyển đi theo xe hàng.
/// Khi phát hiện kẻ địch, lính tự động lao ra đánh và sau đó quay lại đi theo xe hàng.
/// </summary>
public class MercenaryGuard : MonoBehaviour
{
    private Transform _caravanTransform;
    private BaseCombatUnitController _combatUnit;
    private UnityEngine.AI.NavMeshAgent _agent;
    private float _checkInterval = 0.5f;
    private float _nextCheckTime;
    private Vector3 _localOffset;

    public void Initialize(Transform caravan)
    {
        _caravanTransform = caravan;
        _combatUnit = GetComponent<BaseCombatUnitController>();
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (_combatUnit != null)
        {
            _combatUnit.faction = UnitFaction.Player;
            _combatUnit.unitName = "Mercenary Guard";
            _combatUnit.ApplyPlayerTechnologyStats(); // Áp dụng các chỉ số buff công nghệ/thẻ của người chơi
        }

        // Tăng tốc độ chạy của vệ sĩ để nhanh hơn xe hàng (3.5f), giúp đuổi kịp khi hộ tống hoặc sau khi đánh quái
        if (_agent != null)
        {
            _agent.speed = 4.5f; // Xe hàng chạy 3.5f, lính chạy 4.5f để theo sát và quay về kịp thời
        }

        // Cố định một offset ngẫu nhiên xung quanh xe hàng để tránh các vệ sĩ chồng chéo lên nhau hoặc đi zig-zag
        _localOffset = new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-2.2f, 2.2f));
    }

    private void Update()
    {
        if (_caravanTransform == null)
        {
            // Xe hàng đã biến mất (do giao hàng thành công hoặc bị tiêu diệt) -> Tự hủy sau khi dọn dẹp xong
            Destroy(gameObject, 2f);
            return;
        }

        if (Time.time >= _nextCheckTime)
        {
            _nextCheckTime = Time.time + _checkInterval;
            
            if (_combatUnit != null)
            {
                // Chỉ đi theo xe hàng nếu đang Idle hoặc đang di chuyển thường (không bận rượt đuổi/đánh quái)
                if (_combatUnit.currentState == CombatState.Idle || _combatUnit.currentState == CombatState.Moving)
                {
                    float dist = Vector3.Distance(transform.position, _caravanTransform.position);
                    if (dist > 3.5f)
                    {
                        Vector3 targetPos = _caravanTransform.position + _localOffset;
                        bool shouldSetDestination = false;

                        if (_combatUnit.currentState == CombatState.Idle)
                        {
                            shouldSetDestination = true;
                        }
                        else if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                        {
                            // Chỉ cập nhật đích đến mới khi điểm đến hiện tại lệch quá 1.5m so với vị trí Caravan mới.
                            // Tránh việc gọi SetDestination liên tục làm NavMeshAgent bị khựng/reset tốc độ mỗi 0.5s.
                            float destDist = Vector3.Distance(_agent.destination, targetPos);
                            if (destDist > 1.5f)
                            {
                                shouldSetDestination = true;
                            }
                        }
                        else
                        {
                            shouldSetDestination = true;
                        }

                        if (shouldSetDestination)
                        {
                            _combatUnit.CommandAttackMove(targetPos);
                        }
                    }
                }
            }
        }
    }
}
