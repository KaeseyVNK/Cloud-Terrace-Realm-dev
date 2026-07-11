using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using CloudTerraceRealm.SaveSystem;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Điều khiển các tương tác trên giao diện Main Menu (Play, Quit).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private const string LoadingSceneName = "LoadingScene";

        [Header("Optional Continue Button")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _continueButton;

        private void Awake()
        {
            // Đảm bảo TimeScale trở lại bình thường khi ở Main Menu
            Time.timeScale = 1f;

            // Dọn dẹp các singleton game cũ trong scene DontDestroyOnLoad để tránh mang trạng thái sang ván mới,
            // giữ lại AudioManager để tránh ngắt nhạc nền và SaveGameRuntime cho tự động lưu.
            GameObject tempObj = new GameObject();
            DontDestroyOnLoad(tempObj);
            UnityEngine.SceneManagement.Scene dontDestroyScene = tempObj.scene;
            Destroy(tempObj);

            GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject obj = rootObjects[i];
                if (obj != null)
                {
                    if (obj.GetComponent<MyGame.Audio.AudioManager>() != null || 
                        obj.GetComponentInChildren<MyGame.Audio.AudioManager>() != null ||
                        obj.GetComponent<SaveGameRuntime>() != null ||
                        obj.GetComponentInChildren<SaveGameRuntime>() != null)
                    {
                        continue;
                    }
                    Destroy(obj);
                }
            }
        }

        private void Start()
        {
            EnsureContinueButton();
            RefreshContinueButton();
        }

        /// <summary>
        /// Bắt đầu chơi game, chuyển sang Loading Scene để tải bất đồng bộ màn chơi chính.
        /// </summary>
        public void PlayGame()
        {
            SaveGameSystem.DeleteSave();
            GameLog.Log("[MainMenu] Starting game. Loading LoadingScene...");
            SceneManager.LoadScene(LoadingSceneName);
        }

        public void ContinueGame()
        {
            if (!SaveGameSystem.HasSaveGame())
            {
                RefreshContinueButton();
                return;
            }

            SaveGameSystem.RequestResume();
            GameLog.Log("[MainMenu] Continuing saved game. Loading LoadingScene...");
            SetMenuButtonsInteractable(false);
            SceneManager.LoadScene(LoadingSceneName);
        }

        /// <summary>
        /// Thoát ứng dụng game.
        /// </summary>
        public void QuitGame()
        {
            GameLog.Log("[MainMenu] Quit Game requested.");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        private void EnsureContinueButton()
        {
            if (_continueButton != null)
            {
                ConfigureContinueButton(_continueButton);
                return;
            }

            if (_playButton == null)
            {
                _playButton = FindPlayButton();
            }

            _continueButton = FindExistingContinueButton();
            if (_continueButton != null)
            {
                ConfigureContinueButton(_continueButton);
                return;
            }

            if (_playButton == null)
            {
                return;
            }

            _continueButton = Instantiate(_playButton, _playButton.transform.parent);
            _continueButton.name = "ContinueButton";
            _continueButton.transform.SetSiblingIndex(_playButton.transform.GetSiblingIndex() + 1);
            ConfigureContinueButton(_continueButton);

            TextMeshProUGUI label = _continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "Continue";
            }
            else
            {
                Text legacyLabel = _continueButton.GetComponentInChildren<Text>(true);
                if (legacyLabel != null)
                {
                    legacyLabel.text = "Continue";
                }
            }
        }

        private void ConfigureContinueButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(ContinueGame);
            if (!HasPersistentContinueListener(button))
            {
                button.onClick.AddListener(ContinueGame);
            }
        }

        private Button FindExistingContinueButton()
        {
            if (_playButton != null && _playButton.transform.parent != null)
            {
                Button siblingMatch = FindContinueButtonInChildren(_playButton.transform.parent);
                if (siblingMatch != null)
                {
                    return siblingMatch;
                }
            }

            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button button in buttons)
            {
                if (IsContinueButton(button))
                {
                    return button;
                }
            }

            foreach (Button button in buttons)
            {
                if (HasPersistentContinueListener(button) && !LooksLikeDifferentMainMenuAction(button))
                {
                    return button;
                }
            }

            return null;
        }

        private Button FindContinueButtonInChildren(Transform parent)
        {
            Button[] buttons = parent.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (IsContinueButton(button))
                {
                    return button;
                }
            }

            return null;
        }

        private void RefreshContinueButton()
        {
            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(SaveGameSystem.HasSaveGame());
            }
        }

        private Button FindPlayButton()
        {
            Button fallback = null;
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = button;
                }

                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentMethodName(i) == nameof(PlayGame))
                    {
                        return button;
                    }
                }

                string lowerName = button.name.ToLowerInvariant();
                if (lowerName.Contains("play") || lowerName.Contains("new"))
                {
                    fallback = button;
                }
            }

            return fallback;
        }

        private bool IsContinueButton(Button button)
        {
            if (button == null || button == _playButton)
            {
                return false;
            }

            string name = button.name.ToLowerInvariant();
            string label = GetButtonLabelText(button).ToLowerInvariant();
            return name.Contains("continue") || label.Contains("continue");
        }

        private bool LooksLikeDifferentMainMenuAction(Button button)
        {
            string identity = $"{button.name} {GetButtonLabelText(button)}".ToLowerInvariant();
            return identity.Contains("play")
                || identity.Contains("new")
                || identity.Contains("quit")
                || identity.Contains("exit");
        }

        private string GetButtonLabelText(Button button)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                return label.text;
            }

            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            return legacyLabel != null ? legacyLabel.text : string.Empty;
        }

        private bool HasPersistentContinueListener(Button button)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) == nameof(ContinueGame))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetMenuButtonsInteractable(bool interactable)
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    button.interactable = interactable;
                }
            }
        }
    }
}
