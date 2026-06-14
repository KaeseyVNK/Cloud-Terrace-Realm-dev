using UnityEngine;

/// <summary>
/// Shows a lightweight carried-resource prop on a villager.
/// Custom prefabs can be assigned later; otherwise small runtime primitives are used.
/// </summary>
public class VillagerCarryVisuals : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private Transform _carryAnchor;
    [SerializeField] private Vector3 _defaultAnchorLocalPosition = new Vector3(0f, 1.05f, -0.25f);
    [SerializeField] private Vector3 _defaultAnchorLocalEuler = new Vector3(12f, 0f, 0f);

    [Header("Optional Prefabs")]
    [SerializeField] private GameObject _woodPrefab;
    [SerializeField] private GameObject _stonePrefab;
    [SerializeField] private GameObject _foodPrefab;
    [SerializeField] private GameObject _goldPrefab;

    [Header("Scale")]
    [SerializeField] private Vector3 _woodScale = new Vector3(0.32f, 0.16f, 0.16f);
    [SerializeField] private Vector3 _bagScale = new Vector3(0.28f, 0.28f, 0.22f);

    private GameObject _currentVisual;
    private ResourceType _currentType;
    private int _currentAmount;
    private Vector3 _currentBaseScale = Vector3.one;

    private void Awake()
    {
        EnsureAnchor();
        Hide();
    }

    public void SetCarry(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            Hide();
            return;
        }

        EnsureAnchor();

        if (_currentVisual != null && _currentType == type)
        {
            _currentAmount = amount;
            ApplyAmountScale(amount);
            return;
        }

        ClearCurrentVisual();
        _currentType = type;
        _currentAmount = amount;

        GameObject prefab = GetPrefab(type);
        _currentVisual = prefab != null ? Instantiate(prefab, _carryAnchor) : CreateDefaultVisual(type);
        _currentVisual.name = $"Carry_{type}";
        _currentVisual.transform.localPosition = Vector3.zero;
        _currentVisual.transform.localRotation = Quaternion.identity;
        _currentBaseScale = _currentVisual.transform.localScale;

        ApplyAmountScale(amount);
        _currentVisual.SetActive(true);
    }

    public void Hide()
    {
        _currentType = default;
        _currentAmount = 0;
        ClearCurrentVisual();
    }

    private void EnsureAnchor()
    {
        if (_carryAnchor != null)
        {
            return;
        }

        Transform existing = transform.Find("CarryVisualAnchor");
        if (existing != null)
        {
            _carryAnchor = existing;
            return;
        }

        GameObject anchor = new GameObject("CarryVisualAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = _defaultAnchorLocalPosition;
        anchor.transform.localRotation = Quaternion.Euler(_defaultAnchorLocalEuler);
        _carryAnchor = anchor.transform;
    }

    private GameObject GetPrefab(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return _woodPrefab;
            case ResourceType.Stone:
                return _stonePrefab;
            case ResourceType.Food:
                return _foodPrefab;
            case ResourceType.Gold:
                return _goldPrefab;
            default:
                return null;
        }
    }

    private GameObject CreateDefaultVisual(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return CreateDefaultWood();
            case ResourceType.Gold:
                return CreateDefaultBag(new Color(1f, 0.78f, 0.18f));
            case ResourceType.Food:
                return CreateDefaultBag(new Color(0.76f, 0.55f, 0.28f));
            case ResourceType.Stone:
            default:
                return CreateDefaultBag(new Color(0.45f, 0.47f, 0.48f));
        }
    }

    private GameObject CreateDefaultWood()
    {
        GameObject root = new GameObject("Carry_WoodBundle");
        root.transform.SetParent(_carryAnchor, false);

        for (int i = 0; i < 3; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = $"Log_{i + 1}";
            log.transform.SetParent(root.transform, false);
            log.transform.localPosition = new Vector3((i - 1) * 0.08f, 0f, 0f);
            log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            log.transform.localScale = _woodScale;
            SetColor(log, new Color(0.45f, 0.24f, 0.11f));
            RemoveCollider(log);
        }

        root.transform.localScale = Vector3.one;
        return root;
    }

    private GameObject CreateDefaultBag(Color color)
    {
        GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bag.transform.SetParent(_carryAnchor, false);
        bag.transform.localScale = _bagScale;
        SetColor(bag, color);
        RemoveCollider(bag);
        return bag;
    }

    private void SetColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        renderer.sharedMaterial = material;
    }

    private void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }

    private void ClearCurrentVisual()
    {
        if (_currentVisual == null)
        {
            return;
        }

        _currentVisual.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(_currentVisual);
        }
        else
        {
            DestroyImmediate(_currentVisual);
        }

        _currentVisual = null;
    }

    private void ApplyAmountScale(int amount)
    {
        if (_currentVisual == null)
        {
            return;
        }

        float fullness = Mathf.Clamp01(amount / 10f);
        float scaleMultiplier = Mathf.Lerp(0.85f, 1.2f, fullness);
        _currentVisual.transform.localScale = _currentBaseScale * scaleMultiplier;
    }
}
