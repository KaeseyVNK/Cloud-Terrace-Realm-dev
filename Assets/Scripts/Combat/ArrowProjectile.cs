using UnityEngine;

public class ArrowProjectile : MonoBehaviour, IPoolable
{
    [SerializeField] private float speed = 28f;
    [SerializeField] private float hitDistance = 0.35f;
    [SerializeField] private float maxLifetime = 4f;
    [SerializeField] private float arcHeight = 2.5f;
    [SerializeField] private float minFlightDuration = 0.25f;
    [SerializeField] private float maxFlightDuration = 2.4f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Missed Arrow - Stick In Ground")]
    [Tooltip("Thời gian mũi tên cắm dưới đất trước khi biến mất (0 = tắt hiệu ứng)")]
    [SerializeField] private float stickDuration = 2.5f;
    [Tooltip("Góc nghiêng mũi tên khi cắm xuống đất (độ so với trục -Y)")]
    [SerializeField] private float stickAngleDegrees = 75f;

    private BaseCombatUnitController _target;
    private Vector3 _fixedTargetPosition;
    private int _damage;
    private float _spawnTime;
    private float _flightDuration;
    private Vector3 _startPosition;
    private Vector3 _lastPosition;
    private bool _useFixedTargetPosition;
    private BaseCombatUnitController _launcher;

    // Trạng thái mũi tên đang cắm xuống đất
    private bool _isStuck;
    private float _stuckUntilTime;

    public void Launch(BaseCombatUnitController newTarget, int newDamage, BaseCombatUnitController newLauncher = null)
    {
        _target = newTarget;
        _fixedTargetPosition = transform.position;
        _damage = newDamage;
        _launcher = newLauncher;
        _spawnTime = Time.time;
        _startPosition = transform.position;
        _lastPosition = _startPosition;
        _useFixedTargetPosition = false;
        _isStuck = false;

        Vector3 targetPosition = GetTargetPosition();
        float distance = Vector3.Distance(_startPosition, targetPosition);
        _flightDuration = Mathf.Clamp(distance / Mathf.Max(0.1f, speed), minFlightDuration, maxFlightDuration);
    }

    public void LaunchAtPosition(BaseCombatUnitController originalTarget, Vector3 targetPosition, int newDamage, BaseCombatUnitController newLauncher = null)
    {
        _target = originalTarget;
        _fixedTargetPosition = targetPosition + targetOffset;
        _damage = newDamage;
        _launcher = newLauncher;
        _spawnTime = Time.time;
        _startPosition = transform.position;
        _lastPosition = _startPosition;
        _useFixedTargetPosition = true;
        _isStuck = false;

        float distance = Vector3.Distance(_startPosition, _fixedTargetPosition);
        _flightDuration = Mathf.Clamp(distance / Mathf.Max(0.1f, speed), minFlightDuration, maxFlightDuration);
    }

    private void Update()
    {
        // --- Mũi tên đang cắm xuống đất ---
        if (_isStuck)
        {
            if (Time.time >= _stuckUntilTime)
            {
                Release();
            }
            return;
        }

        // --- Hết thời gian tối đa → cắm đất hoặc biến mất ---
        if (Time.time > _spawnTime + maxLifetime)
        {
            StickInGround(transform.position);
            return;
        }

        // --- Target biến mất giữa chừng khi đang bay → tiếp tục bay đến vị trí cuối rồi cắm ---
        if (!_useFixedTargetPosition && (_target == null || _target.currentState == CombatState.Dead))
        {
            // Chuyển sang fixed để bay đến điểm cuối cùng rồi cắm đất
            _fixedTargetPosition = GetTargetPosition();
            _useFixedTargetPosition = true;
        }

        Vector3 targetPosition = GetTargetPosition();
        float t = _flightDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - _spawnTime) / _flightDuration);
        Vector3 flatPosition = Vector3.Lerp(_startPosition, targetPosition, t);
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
        Vector3 nextPosition = flatPosition + Vector3.up * arc;

        if (t >= 1f || (targetPosition - nextPosition).sqrMagnitude <= hitDistance * hitDistance)
        {
            bool didHit = TryDamageTargetAtImpact(targetPosition);

            if (didHit)
            {
                // Trúng đích → biến mất ngay
                Release();
            }
            else
            {
                // Bắn hụt → cắm xuống đất tại điểm đó
                StickInGround(nextPosition);
            }
            return;
        }

        Vector3 direction = nextPosition - _lastPosition;
        transform.position = nextPosition;
        _lastPosition = nextPosition;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// Dừng mũi tên lại và cắm xuống đất tại vị trí chỉ định.
    /// </summary>
    private void StickInGround(Vector3 position)
    {
        if (stickDuration <= 0f)
        {
            Release();
            return;
        }

        _isStuck = true;
        _stuckUntilTime = Time.time + stickDuration;

        // Hạ mũi tên xuống mặt đất
        Vector3 groundPos = position;
        groundPos.y = GetGroundY(position);
        transform.position = groundPos;

        // Giữ nguyên góc quay thực tế lúc bay rơi xuống đất (không xoay cưỡng ép theo góc tĩnh nữa)
        // Điều này giúp mũi tên cắm xiên một góc hoàn hảo khớp với quỹ đạo bay vòng cung.
    }

    /// <summary>
    /// Lấy chiều cao mặt đất tại vị trí đó (Terrain hoặc Raycast).
    /// </summary>
    private float GetGroundY(Vector3 pos)
    {
        // Ưu tiên Terrain
        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
        }

        // Fallback: raycast xuống
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
        {
            return hit.point.y;
        }

        return pos.y - 0.3f;
    }

    public void OnSpawnedFromPool()
    {
        _target = null;
        _launcher = null;
        _fixedTargetPosition = transform.position;
        _damage = 0;
        _spawnTime = Time.time;
        _flightDuration = 0f;
        _startPosition = transform.position;
        _lastPosition = transform.position;
        _useFixedTargetPosition = false;
        _isStuck = false;
        _stuckUntilTime = 0f;
    }

    public void OnReturnedToPool()
    {
        _target = null;
        _launcher = null;
        _fixedTargetPosition = transform.position;
        _damage = 0;
        _flightDuration = 0f;
        _useFixedTargetPosition = false;
        _isStuck = false;
        _stuckUntilTime = 0f;
    }

    private void Release()
    {
        PoolManager.Instance.Release(gameObject);
    }

    private Vector3 GetTargetPosition()
    {
        if (_useFixedTargetPosition)
        {
            return _fixedTargetPosition;
        }

        return _target != null ? _target.transform.position + targetOffset : transform.position;
    }

    /// <summary>
    /// Cố gắng gây sát thương khi mũi tên đến vị trí mục tiêu.
    /// </summary>
    /// <returns>True nếu đã gây sát thương thành công (trúng đích), false nếu bắn hụt.</returns>
    private bool TryDamageTargetAtImpact(Vector3 impactPosition)
    {
        if (_target == null || _target.currentState == CombatState.Dead || !_target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 targetHitPosition = _target.transform.position + targetOffset;
        if ((targetHitPosition - impactPosition).sqrMagnitude > hitDistance * hitDistance)
        {
            return false;
        }

        _target.TakeDamage(_damage, _launcher);
        return true;
    }
}
