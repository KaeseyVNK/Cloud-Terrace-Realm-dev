using System.Collections.Generic;
using UnityEngine;

public class BuildingNightLightController : MonoBehaviour
{
    [SerializeField] private bool _controlChildPointLights = true;
    [SerializeField] private bool _lightsRequireCompletedBuilding = true;
    [SerializeField] private Light[] _controlledPointLights;

    private ConstructibleBuilding _constructibleBuilding;
    private bool _lastLightState;
    private bool _hasAppliedLightState;

    private void Start()
    {
        _constructibleBuilding = GetComponent<ConstructibleBuilding>();
        SetupNightLights();

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }
    }

    private void Update()
    {
        RefreshNightLights();
    }

    private void SetupNightLights()
    {
        if (!_controlChildPointLights)
        {
            return;
        }

        if (_controlledPointLights == null || _controlledPointLights.Length == 0)
        {
            Light[] childLights = GetComponentsInChildren<Light>(true);
            List<Light> pointLights = new List<Light>();

            for (int i = 0; i < childLights.Length; i++)
            {
                if (childLights[i] != null && childLights[i].type == LightType.Point)
                {
                    pointLights.Add(childLights[i]);
                }
            }

            _controlledPointLights = pointLights.ToArray();
        }

        RefreshNightLights(true);
    }

    private void HandleDayNightChanged(bool isNight)
    {
        RefreshNightLights(true);
    }

    private void RefreshNightLights(bool force = false)
    {
        if (!_controlChildPointLights || _controlledPointLights == null || _controlledPointLights.Length == 0)
        {
            return;
        }

        bool isNight = TimeManager.Instance != null && TimeManager.Instance.IsNight;
        bool isRaining = WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain;
        bool isCompleted = _constructibleBuilding == null || _constructibleBuilding.IsCompleted;
        bool shouldEnable = (isNight || isRaining) && (!_lightsRequireCompletedBuilding || isCompleted);

        if (!force && _hasAppliedLightState && _lastLightState == shouldEnable)
        {
            return;
        }

        for (int i = 0; i < _controlledPointLights.Length; i++)
        {
            if (_controlledPointLights[i] != null)
            {
                _controlledPointLights[i].enabled = shouldEnable;
            }
        }

        _lastLightState = shouldEnable;
        _hasAppliedLightState = true;
    }
}
