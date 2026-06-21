using UnityEngine;

public class FreezableBuilding : MonoBehaviour, IFreezable
{
    [Header("Freeze Settings")]
    public Material frozenMaterial;
    public Material normalMaterial;

    private MeshRenderer _renderer;
    private bool _isFrozen = false;

    public bool IsFrozen => _isFrozen;

    void Awake()
    {
        _renderer = GetComponentInChildren<MeshRenderer>();
        if (normalMaterial == null && _renderer != null)
            normalMaterial = _renderer.sharedMaterial;
    }

    public void Freeze()
    {
        if (_isFrozen) return;
        _isFrozen = true;

        if (_renderer != null && frozenMaterial != null)
            _renderer.sharedMaterial = frozenMaterial;

        FreezeManager.Instance.RegisterFrozen(this);
        Debug.Log($"[Freeze] {gameObject.name} bị đóng băng!");
    }

    public void Unfreeze()
    {
        if (!_isFrozen) return;
        _isFrozen = false;

        if (_renderer != null && normalMaterial != null)
            _renderer.sharedMaterial = normalMaterial;

        FreezeManager.Instance.UnregisterFrozen(this);
        Debug.Log($"[Freeze] {gameObject.name} đã rã đông!");
    }

    public Vector3 GetPosition() => transform.position;
}