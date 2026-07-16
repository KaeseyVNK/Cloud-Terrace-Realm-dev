using System.Collections.Generic;
using UnityEngine;

namespace MyGame.UI
{
    /// <summary>
    /// Hiển thị số nổi (Floating Text) chỉ thị lượng tài nguyên nhận được hoặc nộp được.
    /// Vẽ hoàn toàn tự động bằng TextMesh.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private const int MaxActiveTexts = 48;
        private const int MaxPoolSize = 64;

        private static readonly Stack<FloatingText> s_pool = new Stack<FloatingText>(MaxPoolSize);
        private static readonly List<FloatingText> s_active = new List<FloatingText>(MaxActiveTexts);
        private static Transform s_poolRoot;
        private static Font s_cachedFont;

        private TextMesh _textMesh;
        private MeshRenderer _meshRenderer;
        private float _duration = 1.2f;
        private float _floatSpeed = 1.5f;
        private float _timer = 0f;
        private Color _color;

        /// <summary>
        /// Sinh ra số nổi tài nguyên tại vị trí thế giới.
        /// </summary>
        public static void Spawn(Vector3 position, string text, Color color, float duration = 1.2f, float floatSpeed = 1.5f)
        {
            if (s_active.Count >= MaxActiveTexts)
            {
                return;
            }

            EnsurePoolRoot();
            FloatingText ft = AcquireInstance();
            if (ft == null)
            {
                return;
            }

            ft.transform.SetParent(s_poolRoot, false);
            ft.transform.position = position + Vector3.up * 1.5f;
            ft.gameObject.SetActive(true);
            ft.Initialize(text, color, duration, floatSpeed);
            s_active.Add(ft);
        }

        private static void EnsurePoolRoot()
        {
            if (s_poolRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("FloatingTextPool");
            DontDestroyOnLoad(root);
            s_poolRoot = root.transform;
        }

        private static FloatingText AcquireInstance()
        {
            while (s_pool.Count > 0)
            {
                FloatingText pooled = s_pool.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            GameObject go = new GameObject("FloatingText_VFX");
            return go.AddComponent<FloatingText>();
        }

        public void Initialize(string text, Color color, float duration, float floatSpeed)
        {
            _color = color;
            _duration = duration;
            _floatSpeed = floatSpeed;
            _timer = 0f;

            if (_textMesh == null)
            {
                _textMesh = gameObject.GetComponent<TextMesh>();
                if (_textMesh == null)
                {
                    _textMesh = gameObject.AddComponent<TextMesh>();
                }

                _meshRenderer = gameObject.GetComponent<MeshRenderer>();
                Font defaultFont = GetCachedFont();
                if (defaultFont != null)
                {
                    _textMesh.font = defaultFont;
                    if (_meshRenderer != null)
                    {
                        _meshRenderer.material = defaultFont.material;
                    }
                }

                _textMesh.fontSize = 24;
                _textMesh.characterSize = 0.12f;
                _textMesh.anchor = TextAnchor.MiddleCenter;
                _textMesh.alignment = TextAlignment.Center;
            }

            _textMesh.text = text;
            _textMesh.color = _color;
        }

        private static Font GetCachedFont()
        {
            if (s_cachedFont != null)
            {
                return s_cachedFont;
            }

#if UNITY_EDITOR
            s_cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/BoldPixels.ttf");
#endif
            if (s_cachedFont == null)
            {
                s_cachedFont = Resources.Load<Font>("BoldPixels");
            }

            if (s_cachedFont == null)
            {
                s_cachedFont = Resources.Load<Font>("Fonts/BoldPixels");
            }

            if (s_cachedFont == null)
            {
                try
                {
                    s_cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.Exception)
                {
                    try
                    {
                        s_cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                    catch (System.Exception)
                    {
                        GameLog.LogWarning("[FloatingText] Không thể tải font mặc định nào từ Resources!");
                    }
                }
            }

            return s_cachedFont;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                ReturnToPool();
                return;
            }

            float t = _timer / _duration;
            transform.position += Vector3.up * (_floatSpeed * Time.deltaTime);

            if (_textMesh != null)
            {
                Color c = _color;
                c.a = Mathf.Lerp(_color.a, 0f, t);
                _textMesh.color = c;
            }

            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }
        }

        private void ReturnToPool()
        {
            s_active.Remove(this);
            gameObject.SetActive(false);
            transform.SetParent(s_poolRoot, false);

            if (s_pool.Count < MaxPoolSize)
            {
                s_pool.Push(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
