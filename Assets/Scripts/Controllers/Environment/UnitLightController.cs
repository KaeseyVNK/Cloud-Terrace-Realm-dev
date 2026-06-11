using UnityEngine;

/// <summary>
/// Controls the point light/torch for player units during Night or Rain.
/// </summary>
public class UnitLightController : MonoBehaviour
{
    private Light _pointLight;
    private bool _hasAppliedLightState = false;
    private bool _lastLightState = false;

    private void Start()
    {
        InitializeLight();
    }

    private void OnEnable()
    {
        // Force state refresh when the unit is reactivated (e.g. exiting watchtower or pool)
        RefreshLightState(true);
    }

    private void InitializeLight()
    {
        if (_pointLight == null)
        {
            // First check if there's already a Light component in children
            _pointLight = GetComponentInChildren<Light>(true);
            
            // If not found, dynamically spawn a point light
            if (_pointLight == null)
            {
                GameObject lightObj = new GameObject("UnitTorchLight");
                lightObj.transform.SetParent(transform, false);
                // Position slightly above the head / body center of standard unit prefabs
                lightObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                
                _pointLight = lightObj.AddComponent<Light>();
                _pointLight.type = LightType.Point;
                _pointLight.range = 8f;
                _pointLight.intensity = 5f;
                _pointLight.color = new Color(1.0f, 0.65f, 0.3f); // Warm torch yellow/orange
                _pointLight.shadows = LightShadows.None; // Disabled for performance
            }
        }

        RefreshLightState(true);
    }

    private void Update()
    {
        RefreshLightState();
    }

    private void RefreshLightState(bool force = false)
    {
        if (_pointLight == null) return;

        bool isNight = TimeManager.Instance != null && TimeManager.Instance.IsNight;
        bool isRaining = WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain;
        bool shouldEnable = isNight || isRaining;

        if (!force && _hasAppliedLightState && _lastLightState == shouldEnable)
        {
            return;
        }

        _pointLight.enabled = shouldEnable;
        _lastLightState = shouldEnable;
        _hasAppliedLightState = true;
    }
}
