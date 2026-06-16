using UnityEngine;
using UnityEngine.Rendering;

public class DayNightCycle : MonoBehaviour
{
    private static readonly int TintProperty = Shader.PropertyToID("_Tint");
    private static readonly int SkyTintProperty = Shader.PropertyToID("_SkyTint");
    private static readonly int ExposureProperty = Shader.PropertyToID("_Exposure");
    private static readonly int RotationProperty = Shader.PropertyToID("_Rotation");
    private static readonly int ZenithColorProperty = Shader.PropertyToID("_ZenithColor");
    private static readonly int HorizonColorProperty = Shader.PropertyToID("_HorizonColor");
    private static readonly int AtmosphereThicknessProperty = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int EnableStarsProperty = Shader.PropertyToID("_EnableStars");

    private Light sun;
    private Material runtimeSkyboxMaterial;
    private Material previousSkyboxMaterial;

    [Header("Sky")]
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private bool instantiateSkyboxMaterial = true;

    [Header("Day")]
    [SerializeField] private Color dayZenith = new Color(0.53f, 0.81f, 0.98f);
    [SerializeField] private Color dayHorizon = new Color(0.73f, 0.89f, 1f);
    [SerializeField] private float dayIntensity = 1f;

    [Header("Night")]
    [SerializeField] private Color nightZenith = new Color(0.01f, 0.01f, 0.07f);
    [SerializeField] private Color nightHorizon = new Color(0.01f, 0.01f, 0.05f);
    [SerializeField] private Color moonlightColor = new Color(0.55f, 0.65f, 1f);
    [SerializeField] private float nightIntensity = 0.32f; // Tăng từ 0.18f để đêm sáng hơn
    [SerializeField] private float moonAmbientIntensity = 0.52f; // Tăng từ 0.35f để shadow bớt tối đen

    [Header("Golden Hour (Dawn/Dusk)")]
    [SerializeField] private Color dawnDuskZenith = new Color(0.85f, 0.45f, 0.25f);
    [SerializeField] private Color dawnDuskHorizon = new Color(0.95f, 0.65f, 0.45f);

    [Header("Sun Rotation")]
    [SerializeField] private float minRotationX = -90f;
    [SerializeField] private float baseRotationY = 50f;

    [Header("Weather Transitions")]
    [SerializeField] private float weatherTransitionSpeed = 0.5f;

    [Header("Fog Settings")]
    [SerializeField] private bool _enableFog = false;
    [SerializeField] private Color _clearFogColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private float _clearFogDensity = 0.02f;
    [SerializeField] private Color _rainFogColor = new Color(0.28f, 0.32f, 0.36f);
    [SerializeField] private float _rainFogDensity = 0.035f;
    [SerializeField] private Color _bloodMoonFogColor = new Color(0.45f, 0.06f, 0.06f);
    [SerializeField] private float _bloodMoonFogDensity = 0.028f;

    private float _currentRainIntensity = 0f;
    private float _currentBloodMoonIntensity = 0f;

    private void Start()
    {
        sun = GetComponent<Light>();
        if (sun == null)
        {
            Debug.LogError("DayNightCycle must be attached to a Directional Light!");
            return;
        }

        SetupSkyboxMaterial();
    }

    private void OnDestroy()
    {
        if (runtimeSkyboxMaterial == null)
        {
            return;
        }

        if (RenderSettings.skybox == runtimeSkyboxMaterial)
        {
            RenderSettings.skybox = previousSkyboxMaterial;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeSkyboxMaterial);
        }
        else
        {
            DestroyImmediate(runtimeSkyboxMaterial);
        }
    }

    private void Update()
    {
        if (TimeManager.Instance == null || sun == null)
        {
            return;
        }

        float timeRatio = TimeManager.Instance.GetTimeRatio();
        float nightBlend = GetNightBlend(timeRatio);

        // Get current weather
        WeatherState weather = WeatherState.Clear;
        if (WeatherManager.Instance != null)
        {
            weather = WeatherManager.Instance.CurrentWeather;
        }

        // Smoothly interpolate weather intensities
        float targetRain = (weather == WeatherState.Rain) ? 1f : 0f;
        float targetBloodMoon = (weather == WeatherState.BloodMoon) ? 1f : 0f;

        _currentRainIntensity = Mathf.MoveTowards(_currentRainIntensity, targetRain, weatherTransitionSpeed * Time.deltaTime);
        _currentBloodMoonIntensity = Mathf.MoveTowards(_currentBloodMoonIntensity, targetBloodMoon, weatherTransitionSpeed * Time.deltaTime);

        // Golden hour: 1 at sunrise/sunset, 0 at midday/midnight
        float dawnDuskBlend = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(timeRatio * Mathf.PI * 2f)));

        // Base sun color and intensity
        float clearSunIntensity = Mathf.Lerp(dayIntensity, nightIntensity, nightBlend);
        clearSunIntensity = Mathf.Lerp(clearSunIntensity, dayIntensity * 0.6f, dawnDuskBlend);

        Color clearSunColor = Color.Lerp(dayHorizon, moonlightColor, nightBlend);
        clearSunColor = Color.Lerp(clearSunColor, dawnDuskHorizon, dawnDuskBlend);

        // Blood Moon targets
        Color bloodRed = new Color(0.85f, 0.08f, 0.08f);
        Color bloodMoonSunColor = Color.Lerp(dayHorizon, bloodRed, nightBlend);
        float bloodMoonSunIntensity = Mathf.Lerp(dayIntensity, nightIntensity * 1.5f, nightBlend);

        // Rain targets
        Color rainyColor = new Color(0.4f, 0.45f, 0.5f);
        Color rainSunColor = Color.Lerp(dayHorizon * 0.5f, rainyColor * 0.4f, nightBlend);
        float rainSunIntensity = Mathf.Lerp(dayIntensity * 0.4f, nightIntensity * 0.3f, nightBlend);

        // Interpolate final sun state
        Color targetSunColor = Color.Lerp(clearSunColor, bloodMoonSunColor, _currentBloodMoonIntensity);
        targetSunColor = Color.Lerp(targetSunColor, rainSunColor, _currentRainIntensity);

        float targetSunIntensity = Mathf.Lerp(clearSunIntensity, bloodMoonSunIntensity, _currentBloodMoonIntensity);
        targetSunIntensity = Mathf.Lerp(targetSunIntensity, rainSunIntensity, _currentRainIntensity);

        sun.intensity = targetSunIntensity;
        sun.color = targetSunColor;

        float currentXRotation;
        float currentYRotation = baseRotationY;
        if (timeRatio < 0.5f)
        {
            // Ban ngày: Mặt trời mọc từ 0 đến 180 độ
            currentXRotation = timeRatio * 2f * 180f;
        }
        else
        {
            // Ban đêm: Mặt trăng mọc từ 0 đến 180 độ, chiếu từ góc đối diện (baseRotationY + 180)
            currentXRotation = (timeRatio - 0.5f) * 2f * 180f;
            currentYRotation = baseRotationY + 180f;
        }
        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);

        ApplyEnvironment(timeRatio, nightBlend, _currentRainIntensity, _currentBloodMoonIntensity);
    }

    private void ApplyEnvironment(float timeRatio, float nightBlend, float rainIntensity, float bloodMoonIntensity)
    {
        float dawnDuskBlend = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(timeRatio * Mathf.PI * 2f)));

        Color clearZenith = Color.Lerp(dayZenith, nightZenith, nightBlend);
        clearZenith = Color.Lerp(clearZenith, dawnDuskZenith, dawnDuskBlend);

        Color clearHorizon = Color.Lerp(dayHorizon, nightHorizon, nightBlend);
        clearHorizon = Color.Lerp(clearHorizon, dawnDuskHorizon, dawnDuskBlend);

        Color clearTint = Color.Lerp(Color.white, nightZenith, nightBlend);
        clearTint = Color.Lerp(clearTint, dawnDuskZenith, dawnDuskBlend);

        Color clearAmbient = Color.Lerp(dayHorizon * 0.75f, moonlightColor * moonAmbientIntensity, nightBlend);
        clearAmbient = Color.Lerp(clearAmbient, dawnDuskHorizon * 0.75f, dawnDuskBlend);

        float clearReflection = Mathf.Lerp(1f, 0.15f, nightBlend);
        float clearExposure = Mathf.Lerp(1.2f, 0.2f, nightBlend);
        clearExposure = Mathf.Lerp(clearExposure, 0.8f, dawnDuskBlend);

        // Blood Moon targets
        Color bloodRedAmbient = new Color(0.45f, 0.04f, 0.04f);
        Color bloodMoonAmbient = Color.Lerp(dayHorizon * 0.75f, bloodRedAmbient, nightBlend);
        Color bloodRedZenith = new Color(0.12f, 0.015f, 0.015f);
        Color bloodRedHorizon = new Color(0.32f, 0.035f, 0.03f);
        Color bloodMoonZenith = Color.Lerp(dayZenith, bloodRedZenith, nightBlend);
        Color bloodMoonHorizon = Color.Lerp(dayHorizon, bloodRedHorizon, nightBlend);
        Color bloodMoonTint = Color.Lerp(Color.white, bloodRedZenith, nightBlend);

        // Rain targets
        Color rainyAmbient = new Color(0.25f, 0.28f, 0.32f);
        Color rainAmbient = Color.Lerp(dayHorizon * 0.3f, rainyAmbient * 0.25f, nightBlend);
        Color rainyZenith = new Color(0.2f, 0.22f, 0.25f);
        Color rainyHorizon = new Color(0.35f, 0.38f, 0.42f);
        Color rainZenith = Color.Lerp(rainyZenith, rainyZenith * 0.2f, nightBlend);
        Color rainHorizon = Color.Lerp(rainyHorizon, rainyHorizon * 0.2f, nightBlend);
        Color rainTint = Color.Lerp(Color.gray, rainyZenith * 0.5f, nightBlend);
        float rainReflection = Mathf.Lerp(0.3f, 0.05f, nightBlend);
        float rainExposure = clearExposure * 0.5f;

        // Blended outputs
        Color zenith = Color.Lerp(clearZenith, bloodMoonZenith, bloodMoonIntensity);
        zenith = Color.Lerp(zenith, rainZenith, rainIntensity);

        Color horizon = Color.Lerp(clearHorizon, bloodMoonHorizon, bloodMoonIntensity);
        horizon = Color.Lerp(horizon, rainHorizon, rainIntensity);

        Color tint = Color.Lerp(clearTint, bloodMoonTint, bloodMoonIntensity);
        tint = Color.Lerp(tint, rainTint, rainIntensity);

        Color targetAmbient = Color.Lerp(clearAmbient, bloodMoonAmbient, bloodMoonIntensity);
        targetAmbient = Color.Lerp(targetAmbient, rainAmbient, rainIntensity);

        float targetReflection = Mathf.Lerp(clearReflection, clearReflection, bloodMoonIntensity);
        targetReflection = Mathf.Lerp(targetReflection, rainReflection, rainIntensity);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = targetAmbient;
        RenderSettings.reflectionIntensity = targetReflection;
        ApplyWeatherFog(rainIntensity, bloodMoonIntensity);

        Material skybox = runtimeSkyboxMaterial != null ? runtimeSkyboxMaterial : RenderSettings.skybox;
        if (skybox == null)
        {
            return;
        }

        if (skybox.HasProperty(ZenithColorProperty))
        {
            skybox.SetColor(ZenithColorProperty, zenith);
        }

        if (skybox.HasProperty(HorizonColorProperty))
        {
            skybox.SetColor(HorizonColorProperty, horizon);
        }

        if (skybox.HasProperty(AtmosphereThicknessProperty))
        {
            skybox.SetFloat(AtmosphereThicknessProperty, Mathf.Lerp(0.5f, 1f, nightBlend));
        }

        if (skybox.HasProperty(EnableStarsProperty))
        {
            // Che sao dan khi troi mua
            skybox.SetFloat(EnableStarsProperty, Mathf.Lerp(nightBlend, 0f, rainIntensity));
        }

        if (skybox.HasProperty(TintProperty))
        {
            skybox.SetColor(TintProperty, tint);
        }

        if (skybox.HasProperty(SkyTintProperty))
        {
            skybox.SetColor(SkyTintProperty, tint);
        }

        if (skybox.HasProperty(ExposureProperty))
        {
            float targetExposure = Mathf.Lerp(clearExposure, rainExposure, rainIntensity);
            skybox.SetFloat(ExposureProperty, targetExposure);
        }

        if (skybox.HasProperty(RotationProperty))
        {
            skybox.SetFloat(RotationProperty, timeRatio * 360f);
        }
    }

    private void ApplyWeatherFog(float rainIntensity, float bloodMoonIntensity)
    {
        bool weatherFogActive = rainIntensity > 0.01f || bloodMoonIntensity > 0.01f;
        RenderSettings.fog = _enableFog || weatherFogActive;

        Color fogColor = Color.Lerp(_clearFogColor, _bloodMoonFogColor, bloodMoonIntensity);
        fogColor = Color.Lerp(fogColor, _rainFogColor, rainIntensity);

        float fogDensity = Mathf.Lerp(_clearFogDensity, _bloodMoonFogDensity, bloodMoonIntensity);
        fogDensity = Mathf.Lerp(fogDensity, _rainFogDensity, rainIntensity);

        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private void SetupSkyboxMaterial()
    {
        previousSkyboxMaterial = RenderSettings.skybox;

        Material sourceMaterial = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
        if (sourceMaterial == null)
        {
            return;
        }

        if (!instantiateSkyboxMaterial)
        {
            RenderSettings.skybox = sourceMaterial;
            return;
        }

        runtimeSkyboxMaterial = new Material(sourceMaterial)
        {
            name = sourceMaterial.name + " (Runtime)"
        };
        RenderSettings.skybox = runtimeSkyboxMaterial;
    }

    private static float GetNightBlend(float timeRatio)
    {
        return Mathf.Clamp01(Mathf.Sin((timeRatio - 0.5f) * Mathf.PI * 2f));
    }
}
