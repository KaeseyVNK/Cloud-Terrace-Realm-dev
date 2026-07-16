using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using CloudTerraceRealm.SaveSystem;
using CloudTerraceRealm.UI;

/// <summary>
/// Controller for the Pause Menu UGUI panel.
/// Handles pausing/resuming, reloading the scene, and quitting.
/// </summary>
public class PauseUIController : MonoBehaviour
{
    public static PauseUIController Instance { get; private set; }

    #region Serialized Fields

    [Header("Panel Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Buttons")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _optionsButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _quitButton;

    #endregion

    #region Private Fields

    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;
    private bool _isPaused;

    #endregion

    #region Properties

    public bool IsPaused => _isPaused;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Initialize HUD/panel state (hide at start)
        if (_panelRoot != null)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
            _panelRoot.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(ResumeGame);

        if (_optionsButton != null)
            _optionsButton.onClick.AddListener(OpenOptions);

        if (_restartButton != null)
            _restartButton.onClick.AddListener(RestartGame);

        if (_quitButton != null)
            _quitButton.onClick.AddListener(QuitGame);
    }

    private void Update()
    {
        // Toggle pause with Escape or P key
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (IsOtherBlockingUIActive())
            {
                return;
            }

            if (_isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    #endregion

    #region Public Methods

    public void PauseGame()
    {
        if (_isPaused) return;
        _isPaused = true;

        Time.timeScale = 0f;

        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiPanelOpen();
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(true, 0.2f));
    }

    public void ResumeGame()
    {
        if (!_isPaused) return;
        _isPaused = false;

        Time.timeScale = 1f;

        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }

        if (OptionsUIController.Instance != null)
        {
            OptionsUIController.Instance.CloseOptions();
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(false, 0.15f));
    }

    private void OpenOptions()
    {
        if (OptionsUIController.Instance != null)
        {
            OptionsUIController.Instance.OpenOptions();
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }

        // Lưu game trước khi về Main Menu
        SaveGameSystem.SaveCurrentGame();

        // Đảm bảo TimeScale trở về bình thường trước khi chuyển scene
        // (game đang bị pause, nếu không reset sẽ gây MainMenuScene bị đóng băng)
        Time.timeScale = 1f;
        _isPaused = false;

        GameLog.Log("[PauseUI] Returning to Main Menu...");
        MainMenuUI.CleanupGameSingletons();
        SceneManager.LoadScene("MainMenuScene");
    }

    #endregion

    #region Private Helpers

    private bool IsOtherBlockingUIActive()
    {
        // Don't open pause menu if card draft is active
        if (CardDraftUIController.Instance != null && CardDraftUIController.Instance.gameObject.activeInHierarchy)
        {
            CanvasGroup cg = CardDraftUIController.Instance.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha > 0f) return true;
        }

        // Don't open pause menu if end game screens (victory or defeat) are active
        if (HUDManager.Instance != null && HUDManager.Instance.IsEndScreenActive)
        {
            return true;
        }

        return false;
    }

    private System.Collections.IEnumerator FadeRoutine(bool show, float duration)
    {
        if (_panelRoot == null) yield break;

        if (show)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(true);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;

        Vector3 startScale = show ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;
        Vector3 targetScale = show ? Vector3.one : new Vector3(0.95f, 0.95f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tSmooth = t * t * (3f - 2f * t);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, tSmooth);
            }

            _panelRoot.transform.localScale = Vector3.Lerp(startScale, targetScale, tSmooth);
            yield return null;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = targetAlpha;
        }
        _panelRoot.transform.localScale = targetScale;

        if (!show)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
        }

        _fadeCoroutine = null;
    }

    #endregion
}
