using UnityEngine;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script gắn vào Main Camera trong Main Menu để xoay quanh 1 target (thường là Nhà chính),
    /// tạo ra một cảnh Diorama 3D sống động.
    /// </summary>
    public class MainMenuDioramaCamera : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("Đối tượng trung tâm để Camera xoay quanh (vd: Nhà chính)")]
        [SerializeField] private Transform _target;

        [Header("Orbit Settings")]
        [Tooltip("Tốc độ xoay (độ/giây)")]
        [SerializeField] private float _rotationSpeed = 5f;
        
        [Tooltip("Khoảng cách từ Camera tới Target")]
        [SerializeField] private float _distance = 25f;
        
        [Tooltip("Độ cao của Camera so với Target")]
        [SerializeField] private float _height = 15f;

        [Tooltip("Có tự động nội suy mượt mà (Smooth damping) không?")]
        [SerializeField] private bool _smooth = true;
        
        [Tooltip("Độ mượt (càng lớn càng mượt nhưng bám đuôi chậm hơn)")]
        [SerializeField] private float _smoothFactor = 2f;

        private float _currentAngle = 0f;

        private void Start()
        {
            if (_target == null)
            {
                GameLog.LogWarning("[MainMenuDioramaCamera] Chưa gán Target để xoay! Sẽ xoay quanh gốc tọa độ (0,0,0).");
            }
            else
            {
                // Khởi tạo góc hiện tại dựa trên hướng hiện tại của camera so với target
                Vector3 dir = transform.position - _target.position;
                _currentAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }
        }

        private void LateUpdate()
        {
            Vector3 targetPos = _target != null ? _target.position : Vector3.zero;

            // Tăng góc xoay đều đặn mỗi frame
            _currentAngle += _rotationSpeed * Time.deltaTime;

            // Tính toán vị trí mong muốn của Camera trên vòng tròn quanh Target
            float rad = _currentAngle * Mathf.Deg2Rad;
            float x = targetPos.x + _distance * Mathf.Sin(rad);
            float z = targetPos.z + _distance * Mathf.Cos(rad);
            float y = targetPos.y + _height;

            Vector3 desiredPosition = new Vector3(x, y, z);

            if (_smooth)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * _smoothFactor);
            }
            else
            {
                transform.position = desiredPosition;
            }

            // Luôn luôn nhìn vào Target
            transform.LookAt(targetPos + Vector3.up * (_height * 0.2f)); // Nhìn hơi cao lên 1 chút để ko bị cắm mặt xuống đất quá nhiều
        }

        /// <summary>
        /// Gán mục tiêu xoay mới qua Code (sử dụng trong Editor Setup script)
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            _target = newTarget;
            if (newTarget != null)
            {
                // Khởi tạo góc ngay lập tức
                _currentAngle = 45f; // Mặc định xoay từ góc chéo
                LateUpdate(); // Force update vị trí ngay lập tức
            }
        }

        /// <summary>
        /// Gán các thông số thiết lập tự động
        /// </summary>
        public void SetupParameters(float distance, float height, float speed)
        {
            _distance = distance;
            _height = height;
            _rotationSpeed = speed;
        }
    }
}
