using UnityEditor;
using UnityEngine;

namespace CloudTerraceRealm.Editor
{
    /// <summary>
    /// Cửa sổ Editor hiển thị danh sách các con trỏ chuột trong thư mục Wenrexa.
    /// Giúp lập trình viên và người dùng dễ dàng chọn số thứ tự cho từng lệnh.
    /// </summary>
    public class CursorPreviewWindow : EditorWindow
    {
        private Texture2D[] _cursors;
        private Vector2 _scrollPosition;

        [MenuItem("Window/Cloud Terrace Realm/Cursor Preview")]
        public static void ShowWindow()
        {
            GetWindow<CursorPreviewWindow>("Cursor Preview");
        }

        private void OnEnable()
        {
            LoadCursors();
        }

        private void LoadCursors()
        {
            _cursors = new Texture2D[20];
            for (int i = 1; i <= 20; i++)
            {
                string path = $"Assets/ThirdAssets/StoneCursorWenrexa/PNG/{i:00}.png";
                _cursors[i - 1] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Danh sách Cursors (Wenrexa Pack)", EditorStyles.boldLabel);
            if (GUILayout.Button("Tải lại (Reload Cursors)"))
            {
                LoadCursors();
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            int columns = 5;
            int width = 100;
            int height = 120;

            GUILayout.BeginVertical();
            for (int i = 0; i < 20; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < columns; j++)
                {
                    int index = i + j;
                    if (index >= 20) break;

                    Texture2D tex = _cursors[index];
                    GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height));
                    
                    GUILayout.Label($"Ảnh {index + 1:00}", EditorStyles.centeredGreyMiniLabel);
                    
                    if (tex != null)
                    {
                        // Hiển thị ảnh
                        Rect rect = GUILayoutUtility.GetRect(64, 64);
                        GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUILayout.Label("Không tìm thấy", EditorStyles.centeredGreyMiniLabel);
                    }

                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            
            GUILayout.EndScrollView();
        }
    }
}
