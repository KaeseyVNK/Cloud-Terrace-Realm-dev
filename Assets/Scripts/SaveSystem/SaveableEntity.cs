using UnityEngine;
using System.Collections.Generic;
using System;

namespace CloudTerraceRealm.SaveSystem
{
    [DisallowMultipleComponent]
    public class SaveableEntity : MonoBehaviour
    {
        [SerializeField] private string _saveID = "";
        [SerializeField] private string _prefabID = "";

        public string SaveID => _saveID;
        public string PrefabID => _prefabID;

        private void Awake()
        {
            if (string.IsNullOrEmpty(_saveID))
            {
                _saveID = Guid.NewGuid().ToString();
            }
        }

        [ContextMenu("Generate Unique ID")]
        public void GenerateID()
        {
            _saveID = Guid.NewGuid().ToString();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public string CaptureState()
        {
            List<ComponentStateEntry> list = new List<ComponentStateEntry>();
            
            // Lấy tất cả các component triển khai ISaveable trên GameObject này
            foreach (var saveable in GetComponents<ISaveable>())
            {
                string compType = saveable.GetType().AssemblyQualifiedName;
                string state = saveable.CaptureState();
                
                list.Add(new ComponentStateEntry 
                { 
                    componentType = compType, 
                    json = state 
                });
            }

            ComponentStateWrapper wrapper = new ComponentStateWrapper { components = list };
            return JsonUtility.ToJson(wrapper);
        }

        public void RestoreState(string stateJson)
        {
            if (string.IsNullOrEmpty(stateJson)) return;

            ComponentStateWrapper wrapper = JsonUtility.FromJson<ComponentStateWrapper>(stateJson);
            if (wrapper == null || wrapper.components == null) return;

            // Tìm các component ISaveable trên đối tượng này
            var saveables = GetComponents<ISaveable>();
            
            foreach (var entry in wrapper.components)
            {
                Type type = Type.GetType(entry.componentType);
                if (type == null) continue;

                foreach (var saveable in saveables)
                {
                    if (saveable.GetType() == type)
                    {
                        saveable.RestoreState(entry.json);
                        break;
                    }
                }
            }
        }
    }

    [Serializable]
    public class ComponentStateEntry
    {
        public string componentType;
        public string json;
    }

    [Serializable]
    public class ComponentStateWrapper
    {
        public List<ComponentStateEntry> components;
    }
}
