using UnityEngine;

public class WeatherVFXController : MonoBehaviour
{
    [Header("Rain")]
    [SerializeField] private GameObject _rainPrefab;
    [SerializeField] private Transform _rainAnchor;
    [SerializeField] private Vector3 _rainLocalOffset = new Vector3(0f, 8f, 12f);
    [SerializeField] private Vector3 _rainLocalEulerAngles = Vector3.zero;
    [SerializeField] private bool _spawnOnStart = true;

    [Header("Transition Settings")]
    [SerializeField] private float _weatherTransitionSpeed = 0.5f;

    private GameObject _rainInstance;
    private ParticleSystem[] _rainParticles;
    private float[] _originalEmissionRates;
    private WeatherManager _weatherManager;
    private float _currentRainIntensity = 0f;

    private void Start()
    {
        if (_spawnOnStart)
        {
            EnsureRainInstance();
        }

        TryBindWeatherManager();
    }

    private void Update()
    {
        TryBindWeatherManager();
        UpdateRainIntensity();
    }

    private void OnDestroy()
    {
        if (_weatherManager != null)
        {
            _weatherManager.OnWeatherChanged -= HandleWeatherChanged;
        }
    }

    private void HandleWeatherChanged(WeatherState weather)
    {
        // Giữ lại để tương thích event nhưng không thực hiện logic bật/tắt trực tiếp nữa
    }

    private void TryBindWeatherManager()
    {
        if (_weatherManager == WeatherManager.Instance)
        {
            return;
        }

        if (_weatherManager != null)
        {
            _weatherManager.OnWeatherChanged -= HandleWeatherChanged;
        }

        _weatherManager = WeatherManager.Instance;
        if (_weatherManager != null)
        {
            _weatherManager.OnWeatherChanged += HandleWeatherChanged;
        }
    }

    private void UpdateRainIntensity()
    {
        EnsureRainInstance();
        if (_rainInstance == null || _rainParticles == null) return;

        float targetRain = 0f;
        if (_weatherManager != null && _weatherManager.CurrentWeather == WeatherState.Rain)
        {
            targetRain = 1f;
        }

        if (Mathf.Approximately(_currentRainIntensity, targetRain) && targetRain == 0f && !_rainInstance.activeSelf)
        {
            return;
        }

        _currentRainIntensity = Mathf.MoveTowards(_currentRainIntensity, targetRain, _weatherTransitionSpeed * Time.deltaTime);

        if (!_rainInstance.activeSelf && _currentRainIntensity > 0f)
        {
            _rainInstance.SetActive(true);
        }

        for (int i = 0; i < _rainParticles.Length; i++)
        {
            ParticleSystem particle = _rainParticles[i];
            if (particle == null) continue;

            var emission = particle.emission;
            if (_currentRainIntensity > 0.01f)
            {
                if (!particle.isPlaying)
                {
                    particle.Play(true);
                }
                var rate = emission.rateOverTime;
                float originalRate = (_originalEmissionRates != null && i < _originalEmissionRates.Length) ? _originalEmissionRates[i] : 10f;
                rate.constant = originalRate * _currentRainIntensity;
                emission.rateOverTime = rate;
            }
            else
            {
                if (particle.isPlaying)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        if (_currentRainIntensity <= 0f && _rainInstance.activeSelf)
        {
            bool anyParticlesAlive = false;
            for (int i = 0; i < _rainParticles.Length; i++)
            {
                if (_rainParticles[i] != null && _rainParticles[i].particleCount > 0)
                {
                    anyParticlesAlive = true;
                    break;
                }
            }
            if (!anyParticlesAlive)
            {
                _rainInstance.SetActive(false);
            }
        }
    }

    private void EnsureRainInstance()
    {
        if (_rainInstance != null || _rainPrefab == null)
        {
            return;
        }

        Transform parent = _rainAnchor != null ? _rainAnchor : transform;
        _rainInstance = Instantiate(_rainPrefab, parent);
        _rainInstance.name = _rainPrefab.name.Trim() + " Instance";
        _rainInstance.transform.localPosition = _rainAnchor != null ? Vector3.zero : _rainLocalOffset;
        _rainInstance.transform.localRotation = _rainAnchor != null ? Quaternion.identity : Quaternion.Euler(_rainLocalEulerAngles);
        _rainInstance.transform.localScale = Vector3.one;
        
        _rainParticles = _rainInstance.GetComponentsInChildren<ParticleSystem>(true);
        _originalEmissionRates = new float[_rainParticles.Length];
        for (int i = 0; i < _rainParticles.Length; i++)
        {
            if (_rainParticles[i] != null)
            {
                _originalEmissionRates[i] = _rainParticles[i].emission.rateOverTime.constant;
            }
        }

        _rainInstance.SetActive(false);
    }
}
