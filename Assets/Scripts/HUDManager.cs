using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Tài nguyên")]
    [UnityEngine.Serialization.FormerlySerializedAs("woodText")]
    [SerializeField] private TextMeshProUGUI _woodText;
    
    [UnityEngine.Serialization.FormerlySerializedAs("stoneText")]
    [SerializeField] private TextMeshProUGUI _stoneText;
    
    [UnityEngine.Serialization.FormerlySerializedAs("foodText")]
    [SerializeField] private TextMeshProUGUI _foodText;
    
    [UnityEngine.Serialization.FormerlySerializedAs("goldText")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Thời gian")]
    [UnityEngine.Serialization.FormerlySerializedAs("timeText")]
    [SerializeField] private TextMeshProUGUI _timeText;

    void Start()
    {
        // Đăng ký lắng nghe sự kiện tài nguyên thay đổi từ ResourceManager
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            
            // Cập nhật UI lần đầu tiên
            UpdateResourceUI(ResourceType.Wood, ResourceManager.Instance.GetResourceAmount(ResourceType.Wood));
            UpdateResourceUI(ResourceType.Stone, ResourceManager.Instance.GetResourceAmount(ResourceType.Stone));
            UpdateResourceUI(ResourceType.Food, ResourceManager.Instance.GetResourceAmount(ResourceType.Food));
            UpdateResourceUI(ResourceType.Gold, ResourceManager.Instance.GetResourceAmount(ResourceType.Gold));
        }
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
        }
    }

    private void UpdateResourceUI(ResourceType type, int newAmount)
    {
        switch (type)
        {
            case ResourceType.Wood:
                if (_woodText != null) _woodText.text = "Go: " + newAmount;
                break;
            case ResourceType.Stone:
                if (_stoneText != null) _stoneText.text = "Da: " + newAmount;
                break;
            case ResourceType.Food:
                if (_foodText != null) _foodText.text = "Luong: " + newAmount;
                break;
            case ResourceType.Gold:
                if (_goldText != null) _goldText.text = "Vang: " + newAmount;
                break;
        }
    }

    void Update()
    {
        if (TimeManager.Instance != null && _timeText != null)
        {
            if (TimeManager.Instance.IsNight)
                _timeText.text = "Ban Dem";
            else
                _timeText.text = "Ban Ngay";
        }
    }
}