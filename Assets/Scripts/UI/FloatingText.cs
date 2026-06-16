using UnityEngine;

namespace MyGame.UI
{
    /// <summary>
    /// Hiển thị số nổi (Floating Text) chỉ thị lượng tài nguyên nhận được hoặc nộp được.
    /// Vẽ hoàn toàn tự động bằng TextMesh.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private TextMesh _textMesh;
        private MeshRenderer _meshRenderer;
        private float _duration = 1.2f;
        private float _floatSpeed = 1.5f;
        private float _timer = 0f;
        private Color _color;

        /// <summary>
        /// Sinh ra số nổi tài nguyên tại vị trí thế giới.
        /// </summary>
        /// <param name="position">Vị trí xuất hiện</param>
        /// <param name="text">Nội dung hiển thị (e.g. "+1 Gỗ")</param>
        /// <param name="color">Màu sắc chữ</param>
        /// <param name="duration">Thời gian tồn tại (giây)</param>
        /// <param name="floatSpeed">Tốc độ bay lên</param>
        public static void Spawn(Vector3 position, string text, Color color, float duration = 1.2f, float floatSpeed = 1.5f)
        {
            GameObject go = new GameObject("FloatingText_VFX");
            // Điều chỉnh vị trí cao hơn một chút so với gốc tọa độ của đối tượng gọi
            go.transform.position = position + Vector3.up * 1.5f;

            FloatingText ft = go.AddComponent<FloatingText>();
            ft.Initialize(text, color, duration, floatSpeed);
        }

        public void Initialize(string text, Color color, float duration, float floatSpeed)
        {
            _color = color;
            _duration = duration;
            _floatSpeed = floatSpeed;

            _textMesh = gameObject.AddComponent<TextMesh>();
            _meshRenderer = gameObject.GetComponent<MeshRenderer>();

            // Thử tải font chữ custom BoldPixels
            Font defaultFont = null;
#if UNITY_EDITOR
            defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/BoldPixels.ttf");
#endif

            if (defaultFont == null)
            {
                defaultFont = Resources.Load<Font>("BoldPixels");
            }

            if (defaultFont == null)
            {
                defaultFont = Resources.Load<Font>("Fonts/BoldPixels");
            }

            // Nếu không tải được BoldPixels, thiết lập font chữ mặc định của Unity (Thử LegacyRuntime.ttf trước cho Unity 6+, sau đó là Arial.ttf)
            if (defaultFont == null)
            {
                try
                {
                    defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.Exception)
                {
                    try
                    {
                        defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                    catch (System.Exception)
                    {
                        Debug.LogWarning("[FloatingText] Không thể tải font mặc định nào từ Resources!");
                    }
                }
            }

            if (defaultFont != null)
            {
                _textMesh.font = defaultFont;
                if (_meshRenderer != null)
                {
                    _meshRenderer.material = defaultFont.material;
                }
            }

            _textMesh.text = text;
            _textMesh.color = _color;
            _textMesh.fontSize = 24;
            _textMesh.characterSize = 0.12f; // Giới hạn kích thước vừa phải trong không gian 3D
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                Destroy(gameObject);
                return;
            }

            float t = _timer / _duration;

            // Di chuyển bay lên trên theo trục Y thế giới
            transform.position += Vector3.up * (_floatSpeed * Time.deltaTime);

            // Mờ dần theo thời gian (Fade out)
            if (_textMesh != null)
            {
                Color c = _color;
                c.a = Mathf.Lerp(_color.a, 0f, t);
                _textMesh.color = c;
            }

            // Billboard effect: Xoay chữ đối diện trực diện với camera chính
            if (Camera.main != null)
            {
                // Xoay sao cho chữ hướng về phía camera
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }
        }
    }
}
