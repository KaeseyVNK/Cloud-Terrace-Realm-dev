using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RainSoundAssigner
{
    private const string RainSoundPath = "Assets/Audio/SFX/sfx_rain_sound.mp3";

    [MenuItem("Tools/Cloud Terrace/Audio/Assign Rain Sound")]
    public static void AssignRainSound()
    {
        AudioClip rainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(RainSoundPath);
        if (rainSound == null)
        {
            Debug.LogError($"[RainSoundAssigner] Rain sound not found: {RainSoundPath}");
            return;
        }

        WeatherVFXController[] controllers = Object.FindObjectsByType<WeatherVFXController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int assignedCount = 0;
        bool needsSave = false;
        foreach (WeatherVFXController controller in controllers)
        {
            SerializedObject serializedObject = new SerializedObject(controller);
            SerializedProperty rainSoundProperty = serializedObject.FindProperty("_rainSound");
            if (rainSoundProperty == null)
            {
                Debug.LogError("[RainSoundAssigner] WeatherVFXController._rainSound was not found.");
                continue;
            }

            if (rainSoundProperty.objectReferenceValue != rainSound)
            {
                rainSoundProperty.objectReferenceValue = rainSound;
                serializedObject.ApplyModifiedProperties();
                assignedCount++;
            }

            EditorUtility.SetDirty(controller);
            needsSave = true;
        }

        if (needsSave)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log($"[RainSoundAssigner] Assigned rain sound to {assignedCount} WeatherVFXController instance(s), verified {controllers.Length} controller(s).");
    }
}
