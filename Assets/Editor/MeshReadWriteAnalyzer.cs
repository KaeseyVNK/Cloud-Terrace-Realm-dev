using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace CloudTerraceRealm.Editor
{
    public class MeshReadWriteAnalyzer
    {
        [MenuItem("Tools/Analyze Meshes Read-Write")]
        public static void AnalyzeMeshes()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model");
            List<string> rwEnabledMeshes = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && importer.isReadable)
                {
                    rwEnabledMeshes.Add(path);
                }
            }

            Debug.Log($"[MeshAnalyzer] Tìm thấy {rwEnabledMeshes.Count} model (.fbx/obj...) đang bật Read/Write:\n" + string.Join("\n", rwEnabledMeshes));
        }
    }
}
