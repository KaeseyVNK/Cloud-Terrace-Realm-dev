using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Quản lý việc chuyển cảnh bất đồng bộ giữa Main Menu và GameScene1.
    /// Hiển thị tiến trình tải (Progress Bar) và các mẹo chơi game ngẫu nhiên.
    /// </summary>
    public class LoadingSceneManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _tipText;

        [Header("Settings")]
        [SerializeField] private string _targetSceneName = "GameScene1";
        [SerializeField] private float _minLoadingTime = 2.0f; // Thời gian tải tối thiểu để người chơi kịp đọc Tip

        // Danh sách các mẹo chơi game RTS
        private readonly string[] _gameTips = new string[]
        {
            "Villagers gather Wood faster than mining Stone and Gold.",
            "Build additional Houses to increase your maximum population capacity.",
            "Place Logging Camps near forests to reduce villagers' walking distance.",
            "Watch Towers can automatically fire arrows to defend the village from monsters.",
            "Always monitor your Food supply, villagers will not spawn if food is scarce.",
            "When gold mines are depleted, task villagers to excavate ancient Ruins for resources.",
            "Villagers can repair damaged buildings using construction hammers.",
            "Upgrade your military units to protect the village before Void Portals open."
        };

        private void Start()
        {
            // Reset UI
            if (_progressBar != null) _progressBar.value = 0f;
            if (_progressText != null) _progressText.text = "Loading... 0%";

            // Chọn ngẫu nhiên 1 Tip để hiển thị
            if (_tipText != null && _gameTips.Length > 0)
            {
                int randIndex = Random.Range(0, _gameTips.Length);
                _tipText.text = "TIP: " + _gameTips[randIndex];
            }

            // Bắt đầu Coroutine tải cảnh bất đồng bộ
            StartCoroutine(LoadSceneAsyncCoroutine());
        }

        private IEnumerator LoadSceneAsyncCoroutine()
        {
            yield return new WaitForSeconds(0.5f); // Chờ nhẹ trước khi bắt đầu tải

            float startTime = Time.time;
            AsyncOperation op = SceneManager.LoadSceneAsync(_targetSceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingSceneManager] Cannot load scene '{_targetSceneName}'. Make sure it is enabled in Build Settings.");
                if (_progressText != null) _progressText.text = "Loading failed";
                yield break;
            }

            op.allowSceneActivation = false; // Ngăn chuyển cảnh ngay lập tức để làm hiệu ứng mượt

            float progressValue = 0f;

            while (!op.isDone)
            {
                // Unity AsyncOperation.progress chỉ chạy từ 0 đến 0.9 khi chưa cho phép active scene
                float targetProgress = Mathf.Clamp01(op.progress / 0.9f);
                
                // Lerp mượt thanh tiến trình
                while (progressValue < targetProgress)
                {
                    progressValue += Time.deltaTime * 1.5f; // Tốc độ chạy thanh tiến trình
                    progressValue = Mathf.Min(progressValue, targetProgress);
                    
                    if (_progressBar != null) _progressBar.value = progressValue;
                    if (_progressText != null) _progressText.text = $"Loading assets... {Mathf.RoundToInt(progressValue * 100)}%";
                    
                    yield return null;
                }

                // Kiểm tra nếu đã tải xong ở background và đạt thời gian tải tối thiểu
                float elapsed = Time.time - startTime;
                if (op.progress >= 0.9f && elapsed >= _minLoadingTime)
                {
                    // Chạy nốt thanh tiến trình lên 100%
                    while (progressValue < 1.0f)
                    {
                        progressValue += Time.deltaTime * 2.0f;
                        progressValue = Mathf.Min(progressValue, 1.0f);
                        if (_progressBar != null) _progressBar.value = progressValue;
                        if (_progressText != null) _progressText.text = "Completed! 100%";
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.3f); // Chờ ngắn
                    op.allowSceneActivation = true; // Chuyển cảnh chính thức!
                }

                yield return null;
            }
        }
    }
}
