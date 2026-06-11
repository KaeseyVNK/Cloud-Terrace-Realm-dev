using UnityEngine;

public class PooledObject : MonoBehaviour
{
    public GameObject Prefab { get; private set; }

    public void Initialize(GameObject prefab)
    {
        Prefab = prefab;
    }
}
