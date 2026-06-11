using UnityEngine;

public class WeatherVFXController : MonoBehaviour
{
    [Header("Rain")]
    [SerializeField] private GameObject _rainPrefab;
    [SerializeField] private Transform _rainAnchor;
    [SerializeField] private Vector3 _rainLocalOffset = new Vector3(0f, 8f, 12f);
    [SerializeField] private Vector3 _rainLocalEulerAngles = Vector3.zero;
    [SerializeField] private bool _spawnOnStart = true;

    private GameObject _rainInstance;
    private ParticleSystem[] _rainParticles;
    private WeatherManager _weatherManager;
    private WeatherState _lastAppliedWeather;
    private bool _hasAppliedWeather;

    private void Start()
    {
        if (_spawnOnStart)
        {
            EnsureRainInstance();
        }

        TryBindWeatherManager();
        ApplyCurrentWeather();
    }

    private void Update()
    {
        TryBindWeatherManager();
        ApplyCurrentWeather();
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
        ApplyWeatherState(weather);
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

    private void ApplyCurrentWeather()
    {
        if (_weatherManager == null)
        {
            SetRainActive(false);
            return;
        }

        WeatherState currentWeather = _weatherManager.CurrentWeather;
        if (_hasAppliedWeather && _lastAppliedWeather == currentWeather)
        {
            return;
        }

        ApplyWeatherState(currentWeather);
    }

    private void ApplyWeatherState(WeatherState weather)
    {
        _lastAppliedWeather = weather;
        _hasAppliedWeather = true;
        SetRainActive(weather == WeatherState.Rain);
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
        SetRainActive(false);
    }

    private void SetRainActive(bool active)
    {
        EnsureRainInstance();
        if (_rainInstance == null)
        {
            return;
        }

        if (!_rainInstance.activeSelf)
        {
            _rainInstance.SetActive(true);
        }

        for (int i = 0; i < _rainParticles.Length; i++)
        {
            ParticleSystem particle = _rainParticles[i];
            if (particle == null)
            {
                continue;
            }

            if (active)
            {
                if (!particle.isPlaying)
                {
                    particle.Play(true);
                }
            }
            else
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
