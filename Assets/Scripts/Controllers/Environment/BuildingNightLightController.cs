using System.Collections;
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
    private Dictionary<Light, float> _lightTargetIntensities = new Dictionary<Light, float>();
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _constructibleBuilding = GetComponent<ConstructibleBuilding>();
    }

    private void OnEnable()
    {
        SetupNightLights();

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
    }

    private void Update()
    {
        // Vẫn giữ check Update để cập nhật trạng thái khi công trình xây xong,
        // nhưng sẽ giảm chi phí tính toán bằng cách cache trạng thái
        RefreshNightLights();
    }

    private void SetupNightLights()
    {
        // Tắt hoàn toàn việc quản lý đèn point light của công trình để tối ưu hóa hiệu năng, tránh lag
        _controlChildPointLights = false;
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

        // Cache lại cường độ sáng ban đầu của từng đèn để làm đích fade
        _lightTargetIntensities.Clear();
        int grassLayer = LayerMask.NameToLayer("Grass");
        for (int i = 0; i < _controlledPointLights.Length; i++)
        {
            Light l = _controlledPointLights[i];
            if (l != null)
            {
                _lightTargetIntensities[l] = l.intensity;
                l.intensity = 0f; // Bắt đầu bằng 0
                l.shadows = LightShadows.None; // Tắt shadow của đèn công trình ban đêm để tối ưu hiệu năng
                
                if (grassLayer != -1)
                {
                    l.cullingMask &= ~(1 << grassLayer); // Loại bỏ layer Grass để tránh chiếu sáng cỏ
                }
            }
        }
    }

    private void HandleDayNightChanged(bool isNight)
    {
        RefreshNightLights(true);
    }

    private void RefreshNightLights(bool force = false)
    {
        if (!gameObject.activeInHierarchy || !enabled)
        {
            return;
        }

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

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        _fadeCoroutine = StartCoroutine(FadeLightsRoutine(shouldEnable));

        _lastLightState = shouldEnable;
        _hasAppliedLightState = true;
    }

    private IEnumerator FadeLightsRoutine(bool turnOn)
    {
        float duration = 1.5f; // Thời gian chuyển sắc mượt mà của đèn công trình (1.5s)
        float elapsed = 0f;

        // Bật component Light trước khi fade in
        if (turnOn)
        {
            for (int i = 0; i < _controlledPointLights.Length; i++)
            {
                if (_controlledPointLights[i] != null)
                {
                    _controlledPointLights[i].enabled = true;
                }
            }
        }

        // Lấy cường độ hiện tại của các đèn làm điểm bắt đầu
        Dictionary<Light, float> startIntensities = new Dictionary<Light, float>();
        for (int i = 0; i < _controlledPointLights.Length; i++)
        {
            Light l = _controlledPointLights[i];
            if (l != null)
            {
                startIntensities[l] = l.intensity;
            }
        }

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < _controlledPointLights.Length; i++)
            {
                Light l = _controlledPointLights[i];
                if (l != null && _lightTargetIntensities.TryGetValue(l, out float targetIntensity))
                {
                    float start = startIntensities.ContainsKey(l) ? startIntensities[l] : 0f;
                    float target = turnOn ? targetIntensity : 0f;
                    l.intensity = Mathf.Lerp(start, target, t);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Đảm bảo cường độ sáng đạt giá trị cuối cùng và tắt component nếu fade out
        for (int i = 0; i < _controlledPointLights.Length; i++)
        {
            Light l = _controlledPointLights[i];
            if (l != null)
            {
                float target = turnOn && _lightTargetIntensities.TryGetValue(l, out float targetIntensity) ? targetIntensity : 0f;
                l.intensity = target;
                if (!turnOn)
                {
                    l.enabled = false;
                }
            }
        }

        _fadeCoroutine = null;
    }
}
