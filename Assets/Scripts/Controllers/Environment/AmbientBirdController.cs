using UnityEngine;

namespace MyGame.Environment
{
    /// <summary>
    /// Controls the movement of an ambient bird flying from a spawn point to a target point, then destroying itself.
    /// </summary>
    public class AmbientBirdController : MonoBehaviour
    {
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _speed;
        private float _flightDuration;
        private float _elapsedTime;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes the bird's flight path and speed.
        /// </summary>
        public void Initialize(Vector3 start, Vector3 target, float speed)
        {
            _startPosition = start;
            _targetPosition = target;
            _speed = speed;

            transform.position = start;
            
            // Face the target
            Vector3 direction = (target - start).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            float distance = Vector3.Distance(start, target);
            _flightDuration = distance / Mathf.Max(0.1f, speed);
            _elapsedTime = 0f;
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            _elapsedTime += Time.deltaTime;
            float t = _elapsedTime / _flightDuration;

            Vector3 currentPos;
            if (t < 1f)
            {
                // Di chuyển theo tuyến đường bình thường với hiệu ứng sóng hình sin
                currentPos = Vector3.Lerp(_startPosition, _targetPosition, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI * 3f) * 0.35f;
            }
            else
            {
                // Nếu đã đến đích nhưng vẫn đang được nhìn thấy, tiếp tục bay thẳng cùng hướng với tốc độ không đổi
                Vector3 direction = (_targetPosition - _startPosition).normalized;
                currentPos = _targetPosition + direction * (_speed * (_elapsedTime - _flightDuration));
            }

            transform.position = currentPos;

            // Cập nhật góc quay hướng bay
            Vector3 forward = (_targetPosition - _startPosition).normalized;
            Vector3 velocity = forward * _speed;
            if (t < 1f)
            {
                float waveDeriv = Mathf.Cos(t * Mathf.PI * 3f) * (Mathf.PI * 3f) * 0.35f / _flightDuration;
                velocity += Vector3.up * waveDeriv;
            }
            if (velocity != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
            }

            // Chỉ hủy đối tượng khi chú chim đã bay xong hành trình và không còn nằm trong khung hình camera
            if (t >= 1f && !IsVisibleToCamera())
            {
                Destroy(gameObject);
            }
        }

        private bool IsVisibleToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 viewportPos = cam.WorldToViewportPoint(transform.position);
            
            // Thêm lề (margin = 0.1) để tránh việc chim bị biến mất đột ngột ngay sát mép pixel màn hình
            float margin = 0.1f;
            return (viewportPos.x >= -margin && viewportPos.x <= 1f + margin &&
                    viewportPos.y >= -margin && viewportPos.y <= 1f + margin &&
                    viewportPos.z > 0f);
        }
    }
}
