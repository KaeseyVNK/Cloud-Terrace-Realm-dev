#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.Events;
using CloudTerraceRealm.UI;

namespace CloudTerraceRealm.Editor
{
    /// <summary>
    /// Script tiện ích Editor để tự động cấu hình các sự kiện click cho các nút trong MainMenuScene.
    /// Tránh được giới hạn chiều dài command line trên Windows của MCP.
    /// </summary>
    public static class SetupMainMenu
    {
        [MenuItem("Tools/Setup Main Menu UI")]
        public static void Setup()
        {
            var playBtnGo = GameObject.Find("/MainMenuCanvas/PlayButton");
            var quitBtnGo = GameObject.Find("/MainMenuCanvas/QuitButton");
            var controllerGo = GameObject.Find("/MainMenuController");

            if (playBtnGo == null || quitBtnGo == null || controllerGo == null)
            {
                Debug.LogError("[SetupMainMenu] Không tìm thấy các GameObject cần thiết trong scene!");
                return;
            }

            var playBtn = playBtnGo.GetComponent<Button>();
            var quitBtn = quitBtnGo.GetComponent<Button>();
            var menuUI = controllerGo.GetComponent<MainMenuUI>();

            if (playBtn == null || quitBtn == null || menuUI == null)
            {
                Debug.LogError("[SetupMainMenu] Không tìm thấy các Component (Button hoặc MainMenuUI) tương ứng!");
                return;
            }

            // Xóa sự kiện cũ nếu có
            while (playBtn.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(playBtn.onClick, 0);
            }
            while (quitBtn.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(quitBtn.onClick, 0);
            }

            // Gán sự kiện click trong Editor
            UnityEventTools.AddPersistentListener(playBtn.onClick, menuUI.PlayGame);
            UnityEventTools.AddPersistentListener(quitBtn.onClick, menuUI.QuitGame);

            // Lưu scene
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SetupMainMenu] Gán sự kiện Main Menu thành công và đã lưu Scene.");
        }

        [MenuItem("Tools/Log Canvas Hierarchy")]
        public static void LogCanvasHierarchy()
        {
            var canvas = GameObject.Find("/Canvas");
            if (canvas == null)
            {
                Debug.LogError("[LogCanvasHierarchy] Không tìm thấy GameObject '/Canvas' trong scene!");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[LogCanvasHierarchy] Các UI elements trong /Canvas (Tổng số con: {canvas.transform.childCount}):");
            
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i);
                sb.AppendLine($"- {child.name} (Active: {child.gameObject.activeSelf})");
                for (int j = 0; j < child.childCount; j++)
                {
                    var grandChild = child.GetChild(j);
                    sb.AppendLine($"  + {grandChild.name} (Active: {grandChild.gameObject.activeSelf})");
                }
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Log Canvas Components")]
        public static void LogCanvasComponents()
        {
            var canvas = GameObject.Find("/Canvas");
            if (canvas == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[LogCanvasComponents] Components on Canvas Children:");
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i);
                sb.Append($"- {child.name}: ");
                var components = child.GetComponents<Component>();
                var compNames = new System.Collections.Generic.List<string>();
                foreach (var c in components)
                {
                    if (c == null) continue;
                    compNames.Add(c.GetType().Name);
                }
                sb.AppendLine(string.Join(", ", compNames));
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif
