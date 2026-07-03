using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MobileBuildControlsUI : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _rotateButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TextMeshProUGUI _rotateLabel;
    [SerializeField] private TextMeshProUGUI _cancelLabel;

    private void Awake()
    {
        if (_panelRoot == null)
        {
            _panelRoot = gameObject;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_rotateButton == null)
        {
            Transform rotate = transform.Find("RotateButton");
            if (rotate != null)
            {
                _rotateButton = rotate.GetComponent<Button>();
            }
        }

        if (_cancelButton == null)
        {
            Transform cancel = transform.Find("CancelButton");
            if (cancel != null)
            {
                _cancelButton = cancel.GetComponent<Button>();
            }
        }

        if (_rotateLabel == null && _rotateButton != null)
        {
            _rotateLabel = _rotateButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_cancelLabel == null && _cancelButton != null)
        {
            _cancelLabel = _cancelButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void OnEnable()
    {
        if (_rotateButton != null)
        {
            _rotateButton.onClick.AddListener(RotateBuilding);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(CancelBuildMode);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (_rotateButton != null)
        {
            _rotateButton.onClick.RemoveListener(RotateBuilding);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(CancelBuildMode);
        }
    }

    private void Update()
    {
        Refresh();
    }

    private void RotateBuilding()
    {
        if (BuildingManager.Instance == null)
        {
            return;
        }

        BuildingManager.Instance.RotateGhostBuilding();
        Refresh();
    }

    private void CancelBuildMode()
    {
        if (BuildingManager.Instance == null)
        {
            return;
        }

        BuildingManager.Instance.CancelBuildMode();
        Refresh();
    }

    private void Refresh()
    {
        BuildingManager manager = BuildingManager.Instance;
        bool showPanel = manager != null && (manager.IsBuildMode || manager.IsDeleteMode);
        bool canRotate = manager != null && manager.IsBuildMode && manager.CurrentSelectedBuilding != null;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = showPanel ? 1f : 0f;
            _canvasGroup.interactable = showPanel;
            _canvasGroup.blocksRaycasts = showPanel;
        }
        else if (_panelRoot != null && _panelRoot != gameObject && _panelRoot.activeSelf != showPanel)
        {
            _panelRoot.SetActive(showPanel);
        }

        if (_rotateButton != null)
        {
            _rotateButton.gameObject.SetActive(showPanel && manager != null && manager.IsBuildMode);
            _rotateButton.interactable = canRotate;
        }

        if (_cancelButton != null)
        {
            _cancelButton.interactable = showPanel;
        }

        if (_rotateLabel != null)
        {
            _rotateLabel.text = "Rotate";
        }

        if (_cancelLabel != null)
        {
            _cancelLabel.text = manager != null && manager.IsDeleteMode ? "Cancel Delete" : "Cancel";
        }
    }
}
