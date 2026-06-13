using UnityEngine;

public class MagicProjectile : MonoBehaviour, IPoolable
{
    [SerializeField] private float speed = 18f;
    [SerializeField] private float hitDistance = 0.45f;
    [SerializeField] private float maxLifetime = 4f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    private BaseCombatUnitController target;
    private Vector3 targetPosition;
    private int damage;
    private float spawnTime;
    private bool hasLaunched;
    private BaseCombatUnitController launcher;

    public void LaunchAtPosition(BaseCombatUnitController originalTarget, Vector3 castTargetPosition, int newDamage, BaseCombatUnitController newLauncher = null)
    {
        target = originalTarget;
        targetPosition = castTargetPosition + targetOffset;
        damage = Mathf.Max(1, newDamage);
        launcher = newLauncher;
        spawnTime = Time.time;
        hasLaunched = true;

        FaceTravelDirection();
    }

    private void Update()
    {
        if (!hasLaunched)
        {
            return;
        }

        if (Time.time > spawnTime + maxLifetime)
        {
            Release();
            return;
        }

        Vector3 direction = targetPosition - transform.position;
        float step = speed * Time.deltaTime;
        if (direction.sqrMagnitude <= Mathf.Max(hitDistance * hitDistance, step * step))
        {
            transform.position = targetPosition;
            TryDamageTargetAtImpact();
            Release();
            return;
        }

        transform.position += direction.normalized * step;
        FaceTravelDirection();
    }

    public void OnSpawnedFromPool()
    {
        target = null;
        launcher = null;
        targetPosition = transform.position;
        damage = 0;
        spawnTime = Time.time;
        hasLaunched = false;
    }

    public void OnReturnedToPool()
    {
        target = null;
        launcher = null;
        damage = 0;
        hasLaunched = false;
    }

    private void TryDamageTargetAtImpact()
    {
        if (target == null || target.currentState == CombatState.Dead || !target.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 targetHitPosition = target.transform.position + targetOffset;
        if ((targetHitPosition - targetPosition).sqrMagnitude > hitDistance * hitDistance)
        {
            return;
        }

        target.TakeDamage(damage, launcher);
    }

    private void FaceTravelDirection()
    {
        Vector3 direction = targetPosition - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private void Release()
    {
        PoolManager.Instance.Release(gameObject);
    }
}
