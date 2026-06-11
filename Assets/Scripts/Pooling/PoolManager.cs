using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    private static PoolManager s_instance;
    public static PoolManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                PoolManager existing = FindAnyObjectByType<PoolManager>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    s_instance = existing;
                }
                else
                {
                    GameObject poolObject = new GameObject("PoolManager");
                    s_instance = poolObject.AddComponent<PoolManager>();
                }
            }

            return s_instance;
        }
    }

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (s_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Spawn(prefab.gameObject, position, rotation);
        return instance != null ? instance.GetComponent<T>() : null;
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools[prefab] = pool;
        }

        GameObject instance = null;
        while (pool.Count > 0 && instance == null)
        {
            instance = pool.Dequeue();
        }

        if (instance == null)
        {
            instance = Instantiate(prefab, position, rotation);
            PooledObject pooledObject = instance.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                pooledObject = instance.AddComponent<PooledObject>();
            }
            pooledObject.Initialize(prefab);
        }

        Transform instanceTransform = instance.transform;
        instanceTransform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        NotifySpawned(instance);
        return instance;
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools[prefab] = pool;
        }

        for (int i = pool.Count; i < count; i++)
        {
            GameObject instance = Instantiate(prefab);
            PooledObject pooledObject = instance.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                pooledObject = instance.AddComponent<PooledObject>();
            }
            pooledObject.Initialize(prefab);
            instance.SetActive(false);
            instance.transform.SetParent(transform);
            pool.Enqueue(instance);
        }
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledObject pooledObject = instance.GetComponent<PooledObject>();
        if (pooledObject == null || pooledObject.Prefab == null)
        {
            Destroy(instance);
            return;
        }

        NotifyReturned(instance);
        instance.SetActive(false);
        instance.transform.SetParent(transform);

        if (!pools.TryGetValue(pooledObject.Prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools[pooledObject.Prefab] = pool;
        }

        pool.Enqueue(instance);
    }

    private static void NotifySpawned(GameObject instance)
    {
        IPoolable[] poolables = instance.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
        {
            poolables[i].OnSpawnedFromPool();
        }
    }

    private static void NotifyReturned(GameObject instance)
    {
        IPoolable[] poolables = instance.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
        {
            poolables[i].OnReturnedToPool();
        }
    }
}
