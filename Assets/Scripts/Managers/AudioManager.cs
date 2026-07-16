using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MyGame.Audio
{
    /// <summary>
    /// Bộ quản lý âm thanh (AudioManager) quản lý nhạc nền (BGM) và hiệu ứng âm thanh (SFX).
    /// Hỗ trợ phát nhạc nền ngẫu nhiên thay phiên nhau, chuyển tiếp chéo (crossfade) mượt mà.
    /// Tuân thủ quy tắc thiết kế Unity của dự án.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Danh sách Nhạc Nền (BGM Playlist)")]
        [Tooltip("Các bản nhạc nền sẽ phát luân phiên trong quá trình chơi game")]
        [SerializeField] private AudioClip[] _bgmPlaylist;

        [Header("Medieval BGM Categories")]
        [SerializeField] private AudioClip[] _mainThemeClips;
        [SerializeField] private AudioClip[] _overworldTownClips;
        [SerializeField] private AudioClip[] _battleBossClips;

        public enum MusicCategory
        {
            MainMenu,
            OverworldTown,
            BattleBoss
        }

        private MusicCategory _currentCategory = MusicCategory.MainMenu;

        [Tooltip("Tự động trộn danh sách nhạc phát ngẫu nhiên không trùng lặp cho đến khi hết danh sách")]
        [SerializeField] private bool _shufflePlaylist = true;

        [Tooltip("Thời gian chuyển tiếp chéo giữa 2 bản nhạc (giây)")]
        [SerializeField] private float _crossfadeDuration = 2.0f;

        [Header("Cấu hình Âm Lượng (Volume Settings)")]
        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 0.5f;

        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 0.5f;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip _sfxChooseCard;
        [SerializeField] private AudioClip _sfxArrowHit;
        [SerializeField] private AudioClip _sfxBowShoot;
        [SerializeField] private AudioClip _sfxCaravanBell;
        [SerializeField] private AudioClip _sfxCardDraft;
        [SerializeField] private AudioClip _sfxCardSelect;
        [SerializeField] private AudioClip _sfxChopWood;
        [SerializeField] private AudioClip _sfxEnemySpawn;
        [SerializeField] private AudioClip _sfxExplosion;
        [SerializeField] private AudioClip _sfxHarvestCrop;
        [SerializeField] private AudioClip _sfxMarketTrade;
        [SerializeField] private AudioClip _sfxMineGold;
        [SerializeField] private AudioClip _sfxMineStone;
        [SerializeField] private AudioClip _sfxPortalHum;
        [SerializeField] private AudioClip _sfxPortalOpen;
        [SerializeField] private AudioClip _sfxSwordHit;
        [SerializeField] private AudioClip _sfxTrainingStart;
        [SerializeField] private AudioClip _sfxUiClick;
        [SerializeField] private AudioClip _sfxUiHover;
        [SerializeField] private AudioClip _sfxUiPanelOpen;
        [SerializeField] private AudioClip _sfxUnitMilitiaSelect;
        [SerializeField] private AudioClip _sfxVillagerMove1;
        [SerializeField] private AudioClip _sfxVillagerMove2;
        [SerializeField] private AudioClip _sfxVillagerSelect;

        private AudioSource _audioSourceA;
        private AudioSource _audioSourceB;
        private AudioSource _activeSource;
        private AudioSource _inactiveSource;

        private List<int> _playlistBag = new List<int>();
        private int _currentTrackIndex = -1;
        private bool _isTransitioning = false;
        private Coroutine _fadeCoroutine;

        // Anti-spam throttling
        private readonly Dictionary<string, float> _throttledSFXTimes = new Dictionary<string, float>();

        /// <summary>
        /// Thuộc tính âm lượng nhạc nền.
        /// </summary>
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                if (!_isTransitioning && _activeSource != null)
                {
                    _activeSource.volume = _musicVolume;
                }
            }
        }

        /// <summary>
        /// Thuộc tính âm lượng hiệu ứng âm thanh.
        /// </summary>
        public float SFXVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioSources();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Khởi tạo thể loại nhạc ban đầu dựa vào Scene hiện tại
            _currentCategory = MusicCategory.MainMenu;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName != "MainMenuScene" && sceneName != "LoadingScene")
            {
                _currentCategory = MusicCategory.OverworldTown;
            }

            // Gán BGM playlist tương ứng
            switch (_currentCategory)
            {
                case MusicCategory.MainMenu:
                    if (_mainThemeClips != null && _mainThemeClips.Length > 0) _bgmPlaylist = _mainThemeClips;
                    break;
                case MusicCategory.OverworldTown:
                    if (_overworldTownClips != null && _overworldTownClips.Length > 0) _bgmPlaylist = _overworldTownClips;
                    break;
                case MusicCategory.BattleBoss:
                    if (_battleBossClips != null && _battleBossClips.Length > 0) _bgmPlaylist = _battleBossClips;
                    break;
            }

            if (_bgmPlaylist != null && _bgmPlaylist.Length > 0)
            {
                PlayNextTrack();
            }
        }

        private void Update()
        {
            UpdateMusicQueue();
        }

        private void InitializeAudioSources()
        {
            // Tạo 2 AudioSource phục vụ cơ chế Crossfade (chuyển tiếp chéo mượt mà giữa 2 bài hát)
            _audioSourceA = gameObject.AddComponent<AudioSource>();
            _audioSourceB = gameObject.AddComponent<AudioSource>();

            ConfigureAudioSource(_audioSourceA);
            ConfigureAudioSource(_audioSourceB);

            _activeSource = _audioSourceA;
            _inactiveSource = _audioSourceB;
        }

        private void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false; // Tắt loop để tự kích hoạt bài hát tiếp theo ở cuối bài
            source.spatialBlend = 0f; // Nhạc nền 2D
            source.volume = 0f;
        }

        private void UpdateMusicQueue()
        {
            // Tự động kiểm tra và chuyển thể loại nhạc (BGM Category) dựa theo trạng thái game
            UpdateMusicCategory();

            if (_bgmPlaylist == null || _bgmPlaylist.Length == 0 || _isTransitioning) return;

            // Nếu không có nhạc đang phát
            if (!_activeSource.isPlaying)
            {
                PlayNextTrack();
                return;
            }

            // Nếu bài hát hiện tại sắp hết (chỉ còn lại thời gian chuyển tiếp chéo), bắt đầu chuyển bài tiếp theo
            if (_activeSource.clip != null)
            {
                float remainingTime = _activeSource.clip.length - _activeSource.time;
                if (remainingTime <= _crossfadeDuration)
                {
                    PlayNextTrack();
                }
            }
        }

        private void UpdateMusicCategory()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            MusicCategory targetCategory = MusicCategory.MainMenu;

            if (sceneName != "MainMenuScene" && sceneName != "LoadingScene")
            {
                bool isNight = TimeManager.Instance != null && TimeManager.Instance.IsNight;
                bool hasActiveEnemies = EnemyManager.Instance != null && EnemyManager.Instance.ActiveEnemies.Count > 0;

                if (isNight || hasActiveEnemies)
                {
                    targetCategory = MusicCategory.BattleBoss;
                }
                else
                {
                    targetCategory = MusicCategory.OverworldTown;
                }
            }

            if (targetCategory != _currentCategory)
            {
                MusicCategory oldCategory = _currentCategory;
                _currentCategory = targetCategory;

                AudioClip[] selectedPlaylist = null;
                switch (_currentCategory)
                {
                    case MusicCategory.MainMenu:
                        selectedPlaylist = _mainThemeClips;
                        break;
                    case MusicCategory.OverworldTown:
                        selectedPlaylist = _overworldTownClips;
                        break;
                    case MusicCategory.BattleBoss:
                        selectedPlaylist = _battleBossClips;
                        break;
                }

                // Nếu thể loại nhạc được chọn có dữ liệu, chuyển danh sách BGM và phát ngay
                if (selectedPlaylist != null && selectedPlaylist.Length > 0)
                {
                    _bgmPlaylist = selectedPlaylist;
                    _playlistBag.Clear();
                    _currentTrackIndex = -1;
                    PlayNextTrack();
                }
                else
                {
                    // Fallback nếu danh sách trống
                    _currentCategory = oldCategory;
                }
            }
        }

        private void PlayNextTrack()
        {
            if (_bgmPlaylist == null || _bgmPlaylist.Length == 0) return;

            int nextClipIndex = GetNextTrackIndex();
            if (nextClipIndex < 0 || nextClipIndex >= _bgmPlaylist.Length) return;

            AudioClip nextClip = _bgmPlaylist[nextClipIndex];
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(CrossfadeRoutine(nextClip));
        }

        private int GetNextTrackIndex()
        {
            if (_bgmPlaylist.Length == 1) return 0;

            // Cơ chế Shuffle Bag: Tạo danh sách chỉ số ngẫu nhiên không lặp lại cho tới khi phát hết danh sách
            if (_playlistBag.Count == 0)
            {
                for (int i = 0; i < _bgmPlaylist.Length; i++)
                {
                    // Tránh lặp lại bài hát vừa phát ngay khi trộn lại danh sách mới
                    if (_bgmPlaylist.Length > 1 && i == _currentTrackIndex) continue;
                    _playlistBag.Add(i);
                }

                // Nếu danh sách rỗng sau bước lọc trên (thường không xảy ra trừ khi danh sách quá ngắn)
                if (_playlistBag.Count == 0)
                {
                    _playlistBag.Add(_currentTrackIndex);
                }

                if (_shufflePlaylist)
                {
                    // Trộn ngẫu nhiên danh sách
                    for (int i = 0; i < _playlistBag.Count; i++)
                    {
                        int temp = _playlistBag[i];
                        int randomIndex = Random.Range(i, _playlistBag.Count);
                        _playlistBag[i] = _playlistBag[randomIndex];
                        _playlistBag[randomIndex] = temp;
                    }
                }
            }

            int index = _playlistBag[0];
            _playlistBag.RemoveAt(0);
            _currentTrackIndex = index;
            return index;
        }

        private IEnumerator CrossfadeRoutine(AudioClip newClip)
        {
            _isTransitioning = true;

            // Đổi vị trí nguồn phát Active và Inactive
            AudioSource oldSource = _activeSource;
            _activeSource = _inactiveSource;
            _inactiveSource = oldSource;

            // Gán bài mới và phát
            _activeSource.clip = newClip;
            _activeSource.volume = 0f;
            _activeSource.Play();

            float elapsed = 0f;
            float startActiveVol = _activeSource.volume;
            float startInactiveVol = _inactiveSource.volume;

            while (elapsed < _crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _crossfadeDuration;

                // Tăng âm lượng nguồn mới lên mục tiêu
                _activeSource.volume = Mathf.Lerp(startActiveVol, _musicVolume, t);
                // Giảm âm lượng nguồn cũ về 0
                _inactiveSource.volume = Mathf.Lerp(startInactiveVol, 0f, t);

                yield return null;
            }

            _activeSource.volume = _musicVolume;
            _inactiveSource.volume = 0f;
            _inactiveSource.Stop();
            _inactiveSource.clip = null;

            _isTransitioning = false;
            _fadeCoroutine = null;
        }

        /// <summary>
        /// Phát hiệu ứng âm thanh 2D (không có không gian 3D, thích hợp cho UI click, vv.)
        /// </summary>
        public void PlaySFX2D(AudioClip clip)
        {
            if (clip == null) return;
            
            // Tạo tạm một GameObject chứa AudioSource để phát và tự hủy
            GameObject sfxObj = new GameObject("TempSFX_2D");
            AudioSource source = sfxObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _sfxVolume;
            source.spatialBlend = 0f;
            source.Play();
            
            Destroy(sfxObj, clip.length + 0.1f);
        }

        /// <summary>
        /// Phát hiệu ứng âm thanh 3D tại tọa độ chỉ định.
        /// </summary>
        public void PlaySFX3D(AudioClip clip, Vector3 position, float maxDistance = 15f)
        {
            if (clip == null) return;

            GameObject sfxObj = new GameObject("TempSFX_3D");
            sfxObj.transform.position = position;
            
            AudioSource source = sfxObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _sfxVolume;
            source.spatialBlend = 1f; // Âm thanh 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.Play();

            Destroy(sfxObj, clip.length + 0.1f);
        }

        /// <summary>
        /// Phát hiệu ứng âm thanh 3D giãn cách thời gian để chống chồng chéo ồn ào.
        /// </summary>
        public void PlaySFX3DThrottled(AudioClip clip, Vector3 position, float cooldown = 0.15f, float maxDistance = 15f)
        {
            if (clip == null) return;
            string clipName = clip.name;

            if (_throttledSFXTimes.TryGetValue(clipName, out float lastTime))
            {
                if (Time.time - lastTime < cooldown) return;
            }
            _throttledSFXTimes[clipName] = Time.time;

            PlaySFX3DWithPitch(clip, position, Random.Range(0.9f, 1.1f), maxDistance);
        }

        /// <summary>
        /// Phát âm thanh 3D kèm cao độ (pitch) ngẫu nhiên.
        /// </summary>
        public void PlaySFX3DWithPitch(AudioClip clip, Vector3 position, float pitch, float maxDistance = 15f)
        {
            if (clip == null) return;

            GameObject sfxObj = new GameObject("TempSFX_3D_Pitch");
            sfxObj.transform.position = position;

            AudioSource source = sfxObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _sfxVolume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.pitch = pitch;
            source.Play();

            Destroy(sfxObj, (clip.length / Mathf.Max(0.1f, pitch)) + 0.1f);
        }

        #region Public SFX Players

        public void PlayChooseCard() => PlaySFX2D(_sfxChooseCard);
        public void PlayArrowHit(Vector3 pos) => PlaySFX3D(_sfxArrowHit, pos);
        public void PlayBowShoot(Vector3 pos) => PlaySFX3D(_sfxBowShoot, pos);
        public void PlayCaravanBell(Vector3 pos) => PlaySFX3D(_sfxCaravanBell, pos);
        public void PlayCardDraft() => PlaySFX2D(_sfxCardDraft);
        public void PlayCardSelect() => PlaySFX2D(_sfxCardSelect);
        public void PlayChopWood(Vector3 pos) => PlaySFX3DThrottled(_sfxChopWood, pos, 0.15f);
        public void PlayEnemySpawn(Vector3 pos) => PlaySFX3D(_sfxEnemySpawn, pos);
        public void PlayExplosion(Vector3 pos) => PlaySFX3D(_sfxExplosion, pos);
        public void PlayHarvestCrop(Vector3 pos) => PlaySFX3DThrottled(_sfxHarvestCrop, pos, 0.15f);
        public void PlayMarketTrade() => PlaySFX2D(_sfxMarketTrade);
        public void PlayMineGold(Vector3 pos) => PlaySFX3DThrottled(_sfxMineGold, pos, 0.15f);
        public void PlayMineStone(Vector3 pos) => PlaySFX3DThrottled(_sfxMineStone, pos, 0.15f);
        
        public void PlayPortalHum(AudioSource source)
        {
            if (source != null && _sfxPortalHum != null)
            {
                source.clip = _sfxPortalHum;
                source.loop = true;
                source.volume = _sfxVolume * 0.7f;
                source.spatialBlend = 1.0f;
                source.Play();
            }
        }

        public void PlayPortalOpen(Vector3 pos) => PlaySFX3D(_sfxPortalOpen, pos);
        public void PlaySwordHit(Vector3 pos) => PlaySFX3D(_sfxSwordHit, pos);
        public void PlayTrainingStart() => PlaySFX2D(_sfxTrainingStart);
        public void PlayUiClick() => PlaySFX2D(_sfxUiClick);
        public void PlayUiHover() => PlaySFX2D(_sfxUiHover);
        public void PlayUiPanelOpen() => PlaySFX2D(_sfxUiPanelOpen);
        public void PlayUnitMilitiaSelect() => PlaySFX2D(_sfxUnitMilitiaSelect);
        
        public void PlayVillagerMove(Vector3 pos)
        {
            AudioClip clip = Random.value > 0.5f ? _sfxVillagerMove1 : _sfxVillagerMove2;
            if (clip != null) PlaySFX3D(clip, pos);
        }

        public void PlayVillagerSelect() => PlaySFX2D(_sfxVillagerSelect);

        #endregion
    }
}
