using UnityEngine;
using System.Collections;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Hiệu ứng nhấp nháy phóng to/thu nhỏ (Bounce Effect) của đối tượng visual 3D
    /// khi được chạm/chỉ định ra lệnh tương tác (Tài nguyên, Nhà, Phế tích...).
    /// </summary>
    public class TargetBounceEffect : MonoBehaviour
    {
        private Vector3 _originalScale;
        private bool _isScaleCached = false;
        private Transform _visualTarget;
        private Coroutine _bounceCoroutine;

        private void CacheScale()
        {
            if (_isScaleCached) return;

            // 1. Tìm con có tên "Visual" hoặc "Model"
            Transform modelChild = transform.Find("Visual");
            if (modelChild == null) modelChild = transform.Find("Model");

            // 2. Nếu không tìm thấy, lấy con đầu tiên có Renderer (không phải root)
            if (modelChild == null)
            {
                Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in childRenderers)
                {
                    if (r.transform != transform)
                    {
                        modelChild = r.transform;
                        break;
                    }
                }
            }

            // 3. Nếu vẫn không có con nào, lấy chính transform này
            _visualTarget = (modelChild != null) ? modelChild : transform;
            _originalScale = _visualTarget.localScale;
            _isScaleCached = true;
        }

        /// <summary>
        /// Kích hoạt hiệu ứng nháy phóng to đối tượng.
        /// </summary>
        public void TriggerBounce()
        {
            CacheScale();
            if (_bounceCoroutine != null)
            {
                StopCoroutine(_bounceCoroutine);
            }
            _bounceCoroutine = StartCoroutine(BounceRoutine());
        }

        private IEnumerator BounceRoutine()
        {
            Vector3 targetScale = _originalScale * 1.25f;
            float duration = 0.1f;
            float elapsed = 0f;

            // Phóng to
            while (elapsed < duration)
            {
                if (_visualTarget != null)
                {
                    _visualTarget.localScale = Vector3.Lerp(_originalScale, targetScale, elapsed / duration);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            // Thu nhỏ lại
            while (elapsed < duration)
            {
                if (_visualTarget != null)
                {
                    _visualTarget.localScale = Vector3.Lerp(targetScale, _originalScale, elapsed / duration);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_visualTarget != null)
            {
                _visualTarget.localScale = _originalScale;
            }
            _bounceCoroutine = null;
        }

        private void OnDisable()
        {
            // Reset scale khi bị disable
            if (_isScaleCached && _visualTarget != null)
            {
                _visualTarget.localScale = _originalScale;
            }
            _bounceCoroutine = null;
        }
    }
}
