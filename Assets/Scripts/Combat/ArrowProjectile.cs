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

    private BaseCombatUnitController target;
    private int damage;
    private float spawnTime;
    private float flightDuration;
    private Vector3 startPosition;
    private Vector3 lastPosition;

    public void Launch(BaseCombatUnitController newTarget, int newDamage)
    {
        target = newTarget;
        damage = newDamage;
        spawnTime = Time.time;
        startPosition = transform.position;
        lastPosition = startPosition;

        Vector3 targetPosition = GetTargetPosition();
        float distance = Vector3.Distance(startPosition, targetPosition);
        flightDuration = Mathf.Clamp(distance / Mathf.Max(0.1f, speed), minFlightDuration, maxFlightDuration);
    }

    private void Update()
    {
        if (target == null || target.currentState == CombatState.Dead || Time.time > spawnTime + maxLifetime)
        {
            Release();
            return;
        }

        Vector3 targetPosition = GetTargetPosition();
        float t = flightDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - spawnTime) / flightDuration);
        Vector3 flatPosition = Vector3.Lerp(startPosition, targetPosition, t);
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
        Vector3 nextPosition = flatPosition + Vector3.up * arc;

        if (t >= 1f || (targetPosition - nextPosition).sqrMagnitude <= hitDistance * hitDistance)
        {
            target.TakeDamage(damage);
            Release();
            return;
        }

        Vector3 direction = nextPosition - lastPosition;
        transform.position = nextPosition;
        lastPosition = nextPosition;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    public void OnSpawnedFromPool()
    {
        target = null;
        damage = 0;
        spawnTime = Time.time;
        flightDuration = 0f;
        startPosition = transform.position;
        lastPosition = transform.position;
    }

    public void OnReturnedToPool()
    {
        target = null;
        damage = 0;
        flightDuration = 0f;
    }

    private void Release()
    {
        PoolManager.Instance.Release(gameObject);
    }

    private Vector3 GetTargetPosition()
    {
        return target != null ? target.transform.position + targetOffset : transform.position;
    }
}
