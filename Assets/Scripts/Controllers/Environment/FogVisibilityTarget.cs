using FischlWorks_FogWar;
using UnityEngine;

public class FogVisibilityTarget : MonoBehaviour
{
    [SerializeField] private int additionalRadius = 0;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private bool visibleWhenFogDisabled = true;

    private csFogWar fogWar;
    private Renderer[] renderers;
    private float updateTimer;
    private bool currentVisible = true;

    public bool IsVisible => currentVisible;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        fogWar = FindAnyObjectByType<csFogWar>(FindObjectsInactive.Include);
        updateTimer = Random.Range(0f, updateInterval);
        RefreshVisibility(true);
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval)
        {
            return;
        }

        updateTimer = 0f;
        RefreshVisibility(false);
    }

    private void OnDisable()
    {
        SetVisible(true, true);
    }

    private void RefreshVisibility(bool force)
    {
        if (fogWar == null)
        {
            fogWar = FindAnyObjectByType<csFogWar>(FindObjectsInactive.Include);
        }

        if (fogWar == null || !fogWar.enabled)
        {
            SetVisible(visibleWhenFogDisabled, force);
            return;
        }

        if (!fogWar.CheckWorldGridRange(transform.position))
        {
            SetVisible(false, force);
            return;
        }

        SetVisible(fogWar.CheckVisibility(transform.position, additionalRadius), force);
    }

    private void SetVisible(bool visible, bool force)
    {
        if (!force && currentVisible == visible)
        {
            return;
        }

        currentVisible = visible;

        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }
}
