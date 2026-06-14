using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Enemy/Wave Config")]
public class WaveConfigData : ScriptableObject
{
    [Tooltip("When true, matching designed waves replace the generated auto waves for that night.")]
    public bool replaceAutoProgression = true;

    [Tooltip("Designed waves. If any entry matches the current night, these waves replace the automatic schedule for that night.")]
    public List<DayWaveConfig> waves = new List<DayWaveConfig>();
}
