using UnityEngine;

namespace MyGame.UI
{
    /// <summary>
    /// Hiển thị vòng tròn chỉ thị điểm đến (Move Indicator) khi click chuột phải ra lệnh.
    /// Vẽ hoàn toàn bằng code (procedural) thông qua LineRenderer.
    /// </summary>
    public class MoveIndicator : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private float _maxRadius = 1.2f;
        private float _duration = 0.4f;
        private float _timer = 0f;
        private Color _color;

        /// <summary>
        /// Sinh ra vòng tròn chỉ thị điểm đến tại tọa độ click.
        /// </summary>
        /// <param name="position">Vị trí click</param>
        /// <param name="normal">Vector pháp tuyến của bề mặt để áp sát độ dốc</param>
        /// <param name="color">Màu sắc chỉ thị (Xanh cho di chuyển, Đỏ cho tấn công)</param>
        /// <param name="maxRadius">Bán kính bung tối đa</param>
        /// <param name="duration">Thời gian tồn tại (giây)</param>
        public static void Spawn(Vector3 position, Vector3 normal, Color color, float maxRadius = 1.2f, float duration = 0.4f)
        {
            GameObject go = new GameObject("MoveIndicator_VFX");
            // Đẩy cao hơn mặt đất một chút để tránh z-fighting
            go.transform.position = position + normal * 0.05f;
            // Xoay sao cho trục Z cục bộ (Z-forward) hướng theo pháp tuyến bề mặt (normal)
            go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, normal);

            MoveIndicator indicator = go.AddComponent<MoveIndicator>();
            indicator.Initialize(color, maxRadius, duration);
        }

        public void Initialize(Color color, float maxRadius, float duration)
        {
            _color = color;
            _maxRadius = maxRadius;
            _duration = duration;

            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.loop = true;

            // Sử dụng Sprites/Default để hỗ trợ alpha blend màu sắc
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                _lineRenderer.material = new Material(spriteShader);
            }
            else
            {
                _lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
            }

            _lineRenderer.startWidth = 0.12f;
            _lineRenderer.endWidth = 0.12f;
            _lineRenderer.positionCount = 32;

            // Tính toán tọa độ vòng tròn trong hệ trục X-Y (phẳng trên mặt đất nhờ góc xoay của transform)
            Vector3[] points = new Vector3[32];
            for (int i = 0; i < 32; i++)
            {
                float angle = (i / 32f) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            }
            _lineRenderer.SetPositions(points);

            _lineRenderer.startColor = _color;
            _lineRenderer.endColor = _color;
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

            // Bung rộng vòng tròn theo thời gian
            float currentScale = Mathf.Lerp(0.1f, _maxRadius, t);
            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            // Mờ dần theo thời gian
            Color c = _color;
            c.a = Mathf.Lerp(_color.a, 0f, t);
            _lineRenderer.startColor = c;
            _lineRenderer.endColor = c;

            // Sợi đường mảnh dần khi bung ra
            float currentWidth = Mathf.Lerp(0.12f, 0.01f, t);
            _lineRenderer.startWidth = currentWidth;
            _lineRenderer.endWidth = currentWidth;
        }
    }
}
