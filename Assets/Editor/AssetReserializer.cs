using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

namespace MyGame.Editor
{
    /// <summary>
    /// Helper editor utility to force reserialization of selected assets to apply text formatting.
    /// </summary>
    public static class AssetReserializer
    {
        [MenuItem("Assets/Force Reserialize Selected Assets")]
        public static void ForceReserializeSelected()
        {
            var paths = new List<string>();
            Debug.Log($"[AssetReserializer] Current serialization mode: {EditorSettings.serializationMode}");
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (path.EndsWith(".unity"))
                {
                    // For scene files, open, mark dirty, and save them via EditorSceneManager to force serialization mode
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    EditorSceneManager.MarkSceneDirty(scene);
                    bool saved = EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[AssetReserializer] Opened and Saved scene: {path}, success: {saved}");
                }
                else
                {
                    paths.Add(path);
                }
            }

            if (paths.Count > 0)
            {
                AssetDatabase.ForceReserializeAssets(paths);
                Debug.Log($"[AssetReserializer] Reserialized {paths.Count} assets.");
            }
        }
    }
}
