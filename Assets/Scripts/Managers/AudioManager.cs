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

        [Tooltip("Tự động trộn danh sách nhạc phát ngẫu nhiên không trùng lặp cho đến khi hết danh sách")]
        [SerializeField] private bool _shufflePlaylist = true;

        [Tooltip("Thời gian chuyển tiếp chéo giữa 2 bản nhạc (giây)")]
        [SerializeField] private float _crossfadeDuration = 2.0f;

        [Header("Cấu hình Âm Lượng (Volume Settings)")]
        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 0.5f;

        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 0.5f;

        private AudioSource _audioSourceA;
        private AudioSource _audioSourceB;
        private AudioSource _activeSource;
        private AudioSource _inactiveSource;

        private List<int> _playlistBag = new List<int>();
        private int _currentTrackIndex = -1;
        private bool _isTransitioning = false;
        private Coroutine _fadeCoroutine;

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
    }
}
