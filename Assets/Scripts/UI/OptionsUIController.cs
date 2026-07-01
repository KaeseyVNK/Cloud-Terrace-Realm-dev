using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Controller for the Options/Settings UI panel.
/// Handles Master, Music, and SFX Volume sliders, Graphic Quality level,
/// Screen Resolution, and Fullscreen toggle.
/// </summary>
public class OptionsUIController : MonoBehaviour
{
    public static OptionsUIController Instance { get; private set; }

    #region Serialized Fields

    [Header("Panel Roots")]
    [SerializeField] private GameObject _optionsRoot;
    [SerializeField] private GameObject _pauseMenuCenterBox;

    [Header("Volume Controls")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;

    [Header("Graphics Controls")]
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Dropdown _resolutionDropdown;
    [SerializeField] private Dropdown _qualityDropdown;

    [Header("Navigation")]
    [SerializeField] private Button _backButton;

    #endregion

    #region Private Fields

    private List<Resolution> _filteredResolutions;

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

        // Hide options screen at start
        if (_optionsRoot != null)
        {
            _optionsRoot.SetActive(false);
        }
    }

    private void Start()
    {
        InitVolumeSliders();
        InitGraphicsSettings();
        InitResolutions();

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(CloseOptions);
        }
    }

    #endregion

    #region Public Methods

    public void OpenOptions()
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }

        // Hide pause main box and show options panel
        if (_pauseMenuCenterBox != null)
            _pauseMenuCenterBox.SetActive(false);

        if (_optionsRoot != null)
            _optionsRoot.SetActive(true);

        UpdateSliderUIValues();
    }

    public void CloseOptions()
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }

        // Hide options panel and show pause main box
        if (_optionsRoot != null)
            _optionsRoot.SetActive(false);

        if (_pauseMenuCenterBox != null)
            _pauseMenuCenterBox.SetActive(true);
    }

    #endregion

    #region Initializers & Listeners

    private void InitVolumeSliders()
    {
        // Initial values
        float initialMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = initialMaster;
        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.value = initialMaster;
            _masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        float initialMusic = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.MusicVolume = initialMusic;
        }
        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.value = initialMusic;
            _musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        float initialSFX = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.SFXVolume = initialSFX;
        }
        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.value = initialSFX;
            _sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        }
    }

    private void UpdateSliderUIValues()
    {
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.value = AudioListener.volume;

        if (_musicVolumeSlider != null && MyGame.Audio.AudioManager.Instance != null)
            _musicVolumeSlider.value = MyGame.Audio.AudioManager.Instance.MusicVolume;

        if (_sfxVolumeSlider != null && MyGame.Audio.AudioManager.Instance != null)
            _sfxVolumeSlider.value = MyGame.Audio.AudioManager.Instance.SFXVolume;
    }

    private void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    private void SetMusicVolume(float value)
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.MusicVolume = value;
        }
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    private void SetSFXVolume(float value)
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.SFXVolume = value;
        }
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    private void InitGraphicsSettings()
    {
        if (_fullscreenToggle != null)
        {
            _fullscreenToggle.isOn = Screen.fullScreen;
            _fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        if (_qualityDropdown != null)
        {
            _qualityDropdown.ClearOptions();
            List<string> qualities = new List<string>(QualitySettings.names);
            _qualityDropdown.AddOptions(qualities);
            _qualityDropdown.value = QualitySettings.GetQualityLevel();
            _qualityDropdown.RefreshShownValue();
            _qualityDropdown.onValueChanged.AddListener(SetQualityLevel);
        }
    }

    private void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }
    }

    private void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index);
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }
    }

    private void InitResolutions()
    {
        if (_resolutionDropdown == null) return;

        _resolutionDropdown.ClearOptions();
        Resolution[] allResolutions = Screen.resolutions;
        _filteredResolutions = new List<Resolution>();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;
        HashSet<string> seenResolutions = new HashSet<string>();

        // Sort descending so highest resolutions are at the top, or default ascending order
        for (int i = 0; i < allResolutions.Length; i++)
        {
            string resString = allResolutions[i].width + " x " + allResolutions[i].height;
            if (!seenResolutions.Contains(resString))
            {
                seenResolutions.Add(resString);
                _filteredResolutions.Add(allResolutions[i]);
                options.Add(resString);

                if (allResolutions[i].width == Screen.width &&
                    allResolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = _filteredResolutions.Count - 1;
                }
            }
        }

        _resolutionDropdown.AddOptions(options);
        _resolutionDropdown.value = currentResolutionIndex;
        _resolutionDropdown.RefreshShownValue();
        _resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void SetResolution(int index)
    {
        if (index < 0 || index >= _filteredResolutions.Count) return;
        Resolution res = _filteredResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }
    }

    #endregion
}
