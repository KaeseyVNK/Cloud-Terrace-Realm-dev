using UnityEngine;

public class ParticleEffectAutoRelease : MonoBehaviour, IPoolable
{
    [SerializeField] private float fallbackLifetime = 2f;

    private ParticleSystem[] particleSystems;
    private float releaseTime;
    private bool releaseScheduled;

    public void PlayAndRelease()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        float lifetime = fallbackLifetime;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = ps.main;
            ps.Clear(true);
            ps.Play(true);

            if (!main.loop)
            {
                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }
        }

        releaseTime = Time.time + Mathf.Max(0.05f, lifetime);
        releaseScheduled = true;
    }

    private void Update()
    {
        if (!releaseScheduled || Time.time < releaseTime)
        {
            return;
        }

        releaseScheduled = false;
        PooledObject pooledObject = GetComponent<PooledObject>();
        if (pooledObject != null && pooledObject.Prefab != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnSpawnedFromPool()
    {
        releaseScheduled = false;
    }

    public void OnReturnedToPool()
    {
        releaseScheduled = false;
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null)
            {
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
