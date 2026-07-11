using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Giao diện thông báo dân làng rảnh rỗi (Idle Villagers HUD) và phím nóng Tab để đổi mục tiêu.
/// Tự động chạy và không cần kéo thả vào Scene (Plug & Play).
/// </summary>
public class IdleVillagerUI : MonoBehaviour
{
    private int _lastFocusedIdleIndex = -1;

    // Cache các style để tối ưu bộ nhớ
    private GUIStyle _hudButtonStyle;
    private Texture2D _hudButtonNormalTex;
    private Texture2D _hudButtonHoverTex;
    private bool _stylesInitialized = false;

    private static IdleVillagerUI _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name != "MainMenuScene" && scene.name != "LoadingScene")
            {
                EnsureInstance();
            }
        };
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        if (FindAnyObjectByType<IdleVillagerUI>() != null) return;

        GameObject go = new GameObject("IdleVillagerUI");
        _instance = go.AddComponent<IdleVillagerUI>();
        DontDestroyOnLoad(go);
        GameLog.Log("[IdleVillagerUI] Đã tự động khởi chạy Hệ thống chỉ báo Dân Rảnh Rỗi.");
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame
            && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
        {
            ReturnToPreviousCameraView();
            return;
        }

        // Nhấn phím Tab để chuyển nhanh qua các dân rảnh rỗi
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            CycleIdleVillager();
        }
    }

    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        // Tạo các texture màu sắc bán trong suốt (glassmorphic dark look)
        _hudButtonNormalTex = MakeSolidTex(1, 1, new Color(0.18f, 0.12f, 0.08f, 0.9f)); // Cam đất đậm mờ
        _hudButtonHoverTex = MakeSolidTex(1, 1, new Color(0.28f, 0.18f, 0.10f, 0.95f));  // Cam sáng hơn

        _hudButtonStyle = new GUIStyle();
        _hudButtonStyle.normal.background = _hudButtonNormalTex;
        _hudButtonStyle.hover.background = _hudButtonHoverTex;
        _hudButtonStyle.normal.textColor = new Color(1.0f, 0.75f, 0.2f); // Màu cam vàng rực rỡ
        _hudButtonStyle.hover.textColor = Color.white;
        _hudButtonStyle.alignment = TextAnchor.MiddleCenter;
        _hudButtonStyle.fontSize = 13;
        _hudButtonStyle.fontStyle = FontStyle.Bold;

        _stylesInitialized = true;
    }

    private void OnDestroy()
    {
        if (_hudButtonNormalTex != null) Destroy(_hudButtonNormalTex);
        if (_hudButtonHoverTex != null) Destroy(_hudButtonHoverTex);
    }

    private void OnGUI()
    {
        // Legacy IMGUI rendering is disabled.
    }

    /// <summary>
    /// Đếm số dân làng đang ở trạng thái Idle.
    /// </summary>
    private int GetIdleCount()
    {
        int count = 0;
        var list = VillagerController.AllVillagers;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].CurrentState == VillagerState.Idle)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Thực hiện việc hủy chọn toàn bộ, chọn dân rảnh rỗi tiếp theo và lia camera thẳng tới họ.
    /// </summary>
    private void CycleIdleVillager()
    {
        var list = VillagerController.AllVillagers;
        List<VillagerController> idleVillagers = new List<VillagerController>();

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].CurrentState == VillagerState.Idle)
            {
                idleVillagers.Add(list[i]);
            }
        }

        int idleCount = idleVillagers.Count;
        if (idleCount <= 0)
        {
            _lastFocusedIdleIndex = -1;
            return;
        }

        // Tăng index xoay vòng
        _lastFocusedIdleIndex = (_lastFocusedIdleIndex + 1) % idleCount;
        VillagerController nextIdle = idleVillagers[_lastFocusedIdleIndex];

        if (nextIdle == null) return;

        // 1. Hủy chọn toàn bộ lính/dân khác
        if (UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.DeselectAll();

            // 2. Chọn dân rảnh rỗi này
            SelectableUnit selectable = nextIdle.GetComponent<SelectableUnit>();
            if (selectable != null)
            {
                UnitSelectionManager.Instance.selectedUnits.Add(selectable);
                selectable.Select();
            }
        }

        // 3. Di chuyển Camera Controls thẳng tới vị trí dân làng rảnh rỗi
        CameraControls camControls = FindAnyObjectByType<CameraControls>();
        if (camControls != null)
        {
            // Cinemachine Camera theo dõi pivot của transform camControls
            camControls.FocusOnPosition(nextIdle.transform.position, true);
            GameLog.Log($"[IdleVillagerUI] Đã dịch chuyển Camera tới Cư dân rảnh rỗi: {nextIdle.gameObject.name}");
        }
    }

    private void ReturnToPreviousCameraView()
    {
        CameraControls camControls = FindAnyObjectByType<CameraControls>();
        if (camControls != null && camControls.ReturnToStoredView())
        {
            GameLog.Log("[IdleVillagerUI] Da dua camera ve vi tri truoc khi focus dan ranh.");
        }
    }

    private Texture2D MakeSolidTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
