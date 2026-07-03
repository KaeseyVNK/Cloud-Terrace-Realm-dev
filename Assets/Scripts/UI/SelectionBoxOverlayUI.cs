using UnityEngine;
using UnityEngine.UI;

public class SelectionBoxOverlayUI : MonoBehaviour
{
    public static SelectionBoxOverlayUI Instance { get; private set; }

    [SerializeField] private RectTransform _fillRect;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _topBorder;
    [SerializeField] private Image _bottomBorder;
    [SerializeField] private Image _leftBorder;
    [SerializeField] private Image _rightBorder;
    [SerializeField] private float _borderThickness = 2f;

    private RectTransform _canvasRect;

    private void Awake()
    {
        Instance = this;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            _canvasRect = canvas.transform as RectTransform;
        }
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show(Vector2 startScreenPos, Vector2 currentScreenPos, Color fillColor, Color borderColor)
    {
        if (_canvasRect == null || _fillRect == null)
        {
            return;
        }

        Vector2 min = Vector2.Min(startScreenPos, currentScreenPos);
        Vector2 max = Vector2.Max(startScreenPos, currentScreenPos);
        Vector2 size = max - min;
        Vector2 center = min + size * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, center, null, out Vector2 localCenter);
        _fillRect.gameObject.SetActive(true);
        _fillRect.anchoredPosition = localCenter;
        _fillRect.sizeDelta = size;

        if (_fillImage != null)
        {
            _fillImage.color = fillColor;
        }

        SetBorder(_topBorder, new Vector2(0f, 0.5f), new Vector2(0f, size.y * 0.5f), new Vector2(size.x, _borderThickness), borderColor);
        SetBorder(_bottomBorder, new Vector2(0f, 0.5f), new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, _borderThickness), borderColor);
        SetBorder(_leftBorder, new Vector2(0.5f, 0f), new Vector2(-size.x * 0.5f, 0f), new Vector2(_borderThickness, size.y), borderColor);
        SetBorder(_rightBorder, new Vector2(0.5f, 0f), new Vector2(size.x * 0.5f, 0f), new Vector2(_borderThickness, size.y), borderColor);
    }

    public void Hide()
    {
        if (_fillRect != null)
        {
            _fillRect.gameObject.SetActive(false);
        }
    }

    private static void SetBorder(Image border, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        if (border == null)
        {
            return;
        }

        RectTransform rect = border.rectTransform;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        border.color = color;
    }
}
