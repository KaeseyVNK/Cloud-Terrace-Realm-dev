using UnityEngine;

namespace MyGame.Gameplay.Environment
{
    /// <summary>
    /// Điều khiển quay cánh quạt của cối xay gió / kho lương thực.
    /// </summary>
    public class WindmillFanRotator : MonoBehaviour
    {
        [Header("Cấu hình Quay")]
        [Tooltip("Tốc độ quay của cánh quạt (độ/giây)")]
        [SerializeField] private float _rotationSpeed = 90f;

        [Tooltip("Trục quay của cánh quạt (Local Space)")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.forward;

        private void Update()
        {
            // Tự động xoay quanh trục cục bộ
            transform.Rotate(_rotationAxis * (_rotationSpeed * Time.deltaTime), Space.Self);
        }
    }
}
