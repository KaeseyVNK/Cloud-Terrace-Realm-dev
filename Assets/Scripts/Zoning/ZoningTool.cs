using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;

public class ZoningTool : MonoBehaviour
{
    [Header("Zone Settings")]
    public ZoneType selectedZone = ZoneType.Logging;
    public Color loggingColor = new Color(0.3f, 0.8f, 0.3f, 0.4f);
    public Color miningColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
    public Color farmingColor = new Color(0.9f, 0.8f, 0.2f, 0.4f);

    private GridSystem gridSystem;
    private bool isDragging = false;
    public bool toolActive = false;

    // UI Box
    private Vector2 startMousePos;
    private Texture2D whiteTexture;

    void Start()
    {
        gridSystem = FindAnyObjectByType<GridSystem>();

        // Tạo texture trắng để vẽ UI Marquee bằng OnGUI
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }

    void Update()
    {
        // Hệ thống Zoning đã được vô hiệu hóa để nhường chỗ cho RTS
    }

    void OnGUI()
    {
        // Legacy IMGUI zoning UI is disabled.
    }

    void ConfirmZone(Vector2 startPos, Vector2 endPos)
    {
        // Tạo khung chữ nhật ảo (Tính theo tọa độ màn hình từ góc DƯỚI TRÁI)
        Rect selectionRect = new Rect(
            Mathf.Min(startPos.x, endPos.x),
            Mathf.Min(startPos.y, endPos.y),
            Mathf.Abs(startPos.x - endPos.x),
            Mathf.Abs(startPos.y - endPos.y)
        );

        // Hỗ trợ trường hợp người chơi không kéo mà chỉ Click 1 cái
        if (selectionRect.width < 10 && selectionRect.height < 10)
        {
            selectionRect.width = 20;
            selectionRect.height = 20;
            selectionRect.x -= 10;
            selectionRect.y -= 10;
        }

        int addedJobs = 0;

        // Quét toàn bộ tài nguyên có trên bản đồ (có thể tối ưu bằng QuadTree sau này, nhưng mảng 2D rất nhanh)
        for (int x = 0; x < gridSystem.GetWidth(); x++)
        {
            for (int z = 0; z < gridSystem.GetLength(); z++)
            {
                GridCell cell = gridSystem.GetCell(x, z);

                if (cell != null && cell.hasResource && IsResourceMatchZone(selectedZone, cell.resourceType))
                {
                    Vector3 worldPos = gridSystem.GetWorldPosition(x, z);
                    
                    // Chuyển vị trí ngọn cây lên màn hình 2D
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 1f); 

                    // Nếu đối tượng nằm trước Camera (z > 0) và lọt vào khung quét chuột
                    if (screenPos.z > 0 && selectionRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                    {
                        JobBroker.Instance.AddJob(selectedZone, cell.resourceType, worldPos);
                        addedJobs++;

                        // Tạo hiệu ứng để báo hiệu đã chọn trúng
                        HighlightResource(cell.resourceObject);
                    }
                }
            }
        }

        Debug.Log($"[Zoning] Đã khoanh vùng được {addedJobs} công việc.");
    }

    void HighlightResource(GameObject resourceObj)
    {
        if (resourceObj == null) return;
        
        // Nháy to lên một chút rồi thu nhỏ lại
        StartCoroutine(BounceEffect(resourceObj.transform));
    }

    IEnumerator BounceEffect(Transform target)
    {
        Vector3 originalScale = target.localScale;
        target.localScale = originalScale * 1.3f; // Phình to 30%
        
        float timer = 0;
        while(timer < 0.2f)
        {
            if (target == null) yield break;
            target.localScale = Vector3.Lerp(originalScale * 1.3f, originalScale, timer / 0.2f);
            timer += Time.deltaTime;
            yield return null;
        }

        if (target != null) target.localScale = originalScale;
    }

    private bool IsResourceMatchZone(ZoneType zone, ResourceType res)
    {
        if (zone == ZoneType.Logging && res == ResourceType.Wood) return true;
        if (zone == ZoneType.Mining && (res == ResourceType.Stone || res == ResourceType.Gold)) return true;
        if (zone == ZoneType.Farming && res == ResourceType.Food) return true;
        return false;
    }

    Color GetZoneColor()
    {
        switch (selectedZone)
        {
            case ZoneType.Logging: return loggingColor;
            case ZoneType.Mining: return miningColor;
            case ZoneType.Farming: return farmingColor;
            default: return Color.white;
        }
    }
}