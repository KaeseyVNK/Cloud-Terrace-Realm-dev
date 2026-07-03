using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Điều khiển các tương tác trên giao diện Main Menu (Play, Quit).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private const string LoadingSceneName = "LoadingScene";

        /// <summary>
        /// Bắt đầu chơi game, chuyển sang Loading Scene để tải bất đồng bộ màn chơi chính.
        /// </summary>
        public void PlayGame()
        {
            Debug.Log("[MainMenu] Starting game. Loading LoadingScene...");
            SceneManager.LoadScene(LoadingSceneName);
        }

        /// <summary>
        /// Thoát ứng dụng game.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[MainMenu] Quit Game requested.");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
