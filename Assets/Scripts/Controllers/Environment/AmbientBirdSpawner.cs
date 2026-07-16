using UnityEngine;
using System.Collections;

namespace MyGame.Environment
{
    /// <summary>
    /// Periodic camera-relative spawner that flies ambient birds across the player's screen.
    /// </summary>
    public class AmbientBirdSpawner : MonoBehaviour
    {
        [Header("Prefab Reference")]
        [Tooltip("Prefab chú chim bay (low_poly_bird_animated.prefab)")]
        [SerializeField] private GameObject _birdPrefab;

        [Header("Spawn Interval")]
        [SerializeField] private float _minSpawnInterval = 25f;
        [SerializeField] private float _maxSpawnInterval = 55f;

        [Header("Flight Settings")]
        [SerializeField] private float _minFlightSpeed = 7f;
        [SerializeField] private float _maxFlightSpeed = 12f;
        [Tooltip("Khoảng cách (mét) trước camera chim sẽ bay qua")]
        [SerializeField] private float _cameraDistance = 22f;

        private void Start()
        {
            if (_birdPrefab == null)
            {
                GameLog.LogWarning("[AmbientBirdSpawner] Bird Prefab chưa được gán! Đang tự động tìm kiếm...");
                _birdPrefab = Resources.Load<GameObject>("Prefabs/low_poly_bird_animated");
                if (_birdPrefab == null)
                {
                    // Thử tìm trong Assets/Prefabs
                    #if UNITY_EDITOR
                    _birdPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/low_poly_bird_animated.prefab");
                    #endif
                }
            }

            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            // Chờ một khoảng thời gian đầu trước khi đợt chim đầu tiên bay qua
            yield return new WaitForSeconds(Random.Range(10f, 20f));

            while (true)
            {
                SpawnBird();
                float nextInterval = Random.Range(_minSpawnInterval, _maxSpawnInterval);
                yield return new WaitForSeconds(nextInterval);
            }
        }

        private void SpawnBird()
        {
            Camera cam = Camera.main;
            if (cam == null || _birdPrefab == null) return;

            // Quyết định hướng bay: 50% từ Trái sang Phải, 50% từ Phải sang Trái
            bool leftToRight = Random.value > 0.5f;

            // Xác định tọa độ X ngoài màn hình (X = -0.2f là ngoài lề trái, X = 1.2f là ngoài lề phải)
            float startViewportX = leftToRight ? -0.2f : 1.2f;
            float targetViewportX = leftToRight ? 1.2f : -0.2f;

            // Xác định độ cao Y trên màn hình (0f = đáy màn hình, 1f = đỉnh màn hình)
            // Lấy khoảng 0.25f đến 0.55f để bay ở phần nửa dưới màn hình ("dưới camera một chút")
            float startViewportY = Random.Range(0.25f, 0.55f);
            float targetViewportY = startViewportY + Random.Range(-0.08f, 0.08f); // Nghiêng nhẹ khi bay

            // Chuyển sang tọa độ thế giới (World Position)
            Vector3 startViewportPos = new Vector3(startViewportX, startViewportY, _cameraDistance);
            Vector3 targetViewportPos = new Vector3(targetViewportX, targetViewportY, _cameraDistance);

            Vector3 startWorld = cam.ViewportToWorldPoint(startViewportPos);
            Vector3 targetWorld = cam.ViewportToWorldPoint(targetViewportPos);

            // Bù trừ độ cao dựa trên Terrain để tránh chim bay dưới lòng đất hoặc quá thấp vướng ngọn cây
            if (Terrain.activeTerrain != null)
            {
                float terrainHeightAtStart = Terrain.activeTerrain.SampleHeight(startWorld) + Terrain.activeTerrain.transform.position.y;
                float terrainHeightAtTarget = Terrain.activeTerrain.SampleHeight(targetWorld) + Terrain.activeTerrain.transform.position.y;

                // Chim phải bay cao hơn địa hình ít nhất 4.5m
                float minFlyHeight = 4.5f;
                startWorld.y = Mathf.Max(startWorld.y, terrainHeightAtStart + minFlyHeight);
                targetWorld.y = Mathf.Max(targetWorld.y, terrainHeightAtTarget + minFlyHeight);
            }
            else
            {
                startWorld.y = Mathf.Max(startWorld.y, 5f);
                targetWorld.y = Mathf.Max(targetWorld.y, 5f);
            }

            // Sinh chú chim từ Prefab
            GameObject birdObj = Instantiate(_birdPrefab, startWorld, Quaternion.identity);
            
            // Gán component điều khiển bay động
            AmbientBirdController controller = birdObj.GetComponent<AmbientBirdController>();
            if (controller == null)
            {
                controller = birdObj.AddComponent<AmbientBirdController>();
            }

            float speed = Random.Range(_minFlightSpeed, _maxFlightSpeed);
            controller.Initialize(startWorld, targetWorld, speed);
        }
    }
}
