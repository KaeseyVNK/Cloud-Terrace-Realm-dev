using FischlWorks_FogWar;
using UnityEngine;
using System.Collections.Generic;

public class FogVisibilityTarget : MonoBehaviour
{
    [SerializeField] private int additionalRadius = 0;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private bool visibleWhenFogDisabled = true;

    private csFogWar fogWar;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private float updateTimer;
    private bool currentVisible = true;

    public bool IsVisible => currentVisible;

    private void Awake()
    {
        GetComponentsInChildren<Renderer>(true, renderers);
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

        bool isVisible = fogWar.CheckWorldGridRange(transform.position) && 
                          fogWar.CheckVisibility(transform.position, additionalRadius);

        // Kiểm tra an toàn: Nếu mục tiêu đang ẩn, ta muốn chắc chắn không có renderer nào đang hiển thị.
        // Nếu phát hiện thấy có renderer đang enabled, ta ép buộc ẩn. Ta dọn dẹp các renderers bị null.
        if (!isVisible && !force)
        {
            renderers.RemoveAll(r => r == null);
            
            if (renderers.Count > 0)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i].enabled)
                    {
                        force = true;
                        break;
                    }
                }
            }
        }

        SetVisible(isVisible, force);
    }

    private void SetVisible(bool visible, bool force)
    {
        if (!force && currentVisible == visible)
        {
            return;
        }

        currentVisible = visible;

        // Chỉ quét lại toàn bộ renderers khi thực sự cần thiết (danh sách rỗng nhưng cần hiện)
        // Thay vì gọi GetComponentsInChildren vô điều kiện mỗi frame/mỗi lần force
        if (renderers.Count == 0 && visible)
        {
            GetComponentsInChildren<Renderer>(true, renderers);
        }
        else
        {
            renderers.RemoveAll(r => r == null);
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }
}
