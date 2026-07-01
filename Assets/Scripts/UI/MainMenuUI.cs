using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Điều khiển các tương tác trên giao diện Main Menu (Play, Quit).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private const string GameplaySceneName = "GameScene";

        /// <summary>
        /// Bắt đầu chơi game, chuyển cảnh sang Gameplay Scene bất đồng bộ.
        /// </summary>
        public void PlayGame()
        {
            Debug.Log("[MainMenu] Starting game. Loading GameScene...");
            SceneManager.LoadSceneAsync(GameplaySceneName);
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
