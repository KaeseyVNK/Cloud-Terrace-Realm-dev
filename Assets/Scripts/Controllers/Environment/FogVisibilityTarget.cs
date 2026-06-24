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
        RefreshVisibility(true);
    }

    private void OnEnable()
    {
        FogVisibilityManager.Register(this);
    }

    private void OnDisable()
    {
        FogVisibilityManager.Unregister(this);
        SetVisible(true, true);
    }

    /// <summary>
    /// Called by FogVisibilityManager to batch visibility updates.
    /// </summary>
    public void ManualUpdate()
    {
        RefreshVisibility(false);
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

        // Dọn dẹp các renderer bị null trước tiên (do trích xuất mesh hoặc hủy đối tượng con)
        renderers.RemoveAll(r => r == null);

        // Nếu danh sách rỗng, ta tìm lại tất cả renderers con (đảm bảo ẩn/hiện chính xác kể cả khi mesh được trích xuất động)
        if (renderers.Count == 0)
        {
            GetComponentsInChildren<Renderer>(true, renderers);
            if (renderers.Count > 0)
            {
                force = true; // Ép buộc cập nhật trạng thái hiển thị cho các renderer mới tìm thấy
            }
        }

        bool isVisible = fogWar.CheckWorldGridRange(transform.position) && 
                          fogWar.CheckVisibility(transform.position, additionalRadius);

        // Kiểm tra an toàn: Nếu mục tiêu đang ẩn, ta muốn chắc chắn không có renderer nào đang hiển thị.
        // Nếu phát hiện thấy có renderer đang enabled, ta ép buộc ẩn.
        if (!isVisible && !force)
        {
            if (renderers.Count > 0)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] != null && renderers[i].enabled)
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

        // Dọn dẹp các renderer đã bị hủy (null) do trích xuất Mesh hoặc hủy đối tượng con
        renderers.RemoveAll(r => r == null);

        // Nếu danh sách rỗng, tìm lại tất cả renderers con (đảm bảo ẩn/hiện chính xác kể cả khi mesh được trích xuất động)
        if (renderers.Count == 0)
        {
            GetComponentsInChildren<Renderer>(true, renderers);
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
