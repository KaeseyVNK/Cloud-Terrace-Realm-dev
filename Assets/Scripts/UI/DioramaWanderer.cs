using UnityEngine;
using UnityEngine.AI;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script cho dân làng đi lại ngẫu nhiên xung quanh khu vực nhà chính trong Main Menu Diorama.
    /// Giúp tạo cảm giác ngôi làng đang hoạt động mà không cần logic game nặng nề.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DioramaWanderer : MonoBehaviour
    {
        public float wanderRadius = 15f;
        public float waitTime = 4f;
        
        private NavMeshAgent _agent;
        private Animator _animator;
        private Vector3 _startPos;
        private float _timer;
        private bool _isPerformingActivity;

        // Tool references
        private GameObject _woodTool;
        private GameObject _miningTool;

        // Animator parameter presence
        private bool _hasIsIdle;
        private bool _hasIsMoving;
        private bool _hasIsGathering;
        private bool _hasIsBuilding;
        private bool _hasInteractType;

        private enum DioramaActivity
        {
            Idle,
            ChopWood,
            MineStone,
            Build
        }

        private DioramaActivity _currentActivity = DioramaActivity.Idle;
        private Transform _currentTargetTransform;

        public void SetupTools(GameObject woodTool, GameObject miningTool)
        {
            _woodTool = woodTool;
            _miningTool = miningTool;

            // Đảm bảo ban đầu tắt hết
            if (_woodTool != null) _woodTool.SetActive(false);
            if (_miningTool != null) _miningTool.SetActive(false);
        }

        private void Start()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _startPos = transform.position;
            _timer = waitTime;
            _isPerformingActivity = false;
            
            if (_agent != null)
            {
                _agent.speed = Random.Range(2.0f, 3.2f);
                
                // Warp agent to the nearest valid NavMesh position if slightly off
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                }
            }

            if (_animator != null)
            {
                foreach (var param in _animator.parameters)
                {
                    if (param.name == "IsIdle") _hasIsIdle = true;
                    if (param.name == "IsMoving") _hasIsMoving = true;
                    if (param.name == "IsGathering") _hasIsGathering = true;
                    if (param.name == "IsBuilding") _hasIsBuilding = true;
                    if (param.name == "InteractType") _hasInteractType = true;
                }
            }

            // Chọn mục tiêu di chuyển đầu tiên
            PickNewDestination();
        }

        private void Update()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            bool isMoving = _agent.velocity.magnitude > 0.1f;

            if (isMoving)
            {
                _isPerformingActivity = false;
                UpdateAnimator(true, false, false, false, 0);
            }
            else
            {
                if (!_isPerformingActivity)
                {
                    // Vừa đi đến đích -> Chuyển sang trạng thái làm việc tại chỗ
                    _isPerformingActivity = true;
                    _timer = waitTime + Random.Range(-1.0f, 2.5f);

                    // Quay mặt về phía đối tượng để làm việc cho đẹp mắt
                    if (_currentTargetTransform != null)
                    {
                        Vector3 lookPos = _currentTargetTransform.position;
                        lookPos.y = transform.position.y;
                        transform.LookAt(lookPos);
                    }
                }

                // Cập nhật Animator dựa trên hoạt động được chọn bởi PickNewDestination()
                switch (_currentActivity)
                {
                    case DioramaActivity.Idle:
                        UpdateAnimator(false, true, false, false, 0);
                        break;
                    case DioramaActivity.ChopWood:
                        UpdateAnimator(false, false, true, false, 0); // InteractType = 0 (Chặt gỗ)
                        break;
                    case DioramaActivity.MineStone:
                        UpdateAnimator(false, false, true, false, 1); // InteractType = 1 (Đào đá/vàng)
                        break;
                    case DioramaActivity.Build:
                        UpdateAnimator(false, false, false, true, 3); // InteractType = 3 (Xây dựng)
                        break;
                }

                // Đếm ngược thời gian làm việc tại chỗ
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    PickNewDestination();
                    _isPerformingActivity = false;
                }
            }
        }

        private void UpdateAnimator(bool moving, bool idle, bool gathering, bool building, int interactType)
        {
            if (_animator == null) return;

            if (_hasIsMoving) _animator.SetBool("IsMoving", moving);
            if (_hasIsIdle) _animator.SetBool("IsIdle", idle);
            if (_hasIsGathering) _animator.SetBool("IsGathering", gathering);
            if (_hasIsBuilding) _animator.SetBool("IsBuilding", building);
            if (_hasInteractType) _animator.SetInteger("InteractType", interactType);

            // Bật/Tắt công cụ cầm tay tương ứng
            if (_woodTool != null) _woodTool.SetActive(gathering && interactType == 0 || building);
            if (_miningTool != null) _miningTool.SetActive(gathering && interactType == 1);
        }

        private void PickNewDestination()
        {
            var targets = new System.Collections.Generic.List<TargetInfo>();

            // 1. Thu thập cây cối
            var treeGroup = GameObject.Find("Diorama_Trees");
            if (treeGroup != null)
            {
                foreach (Transform t in treeGroup.transform)
                {
                    targets.Add(new TargetInfo { transform = t, activity = DioramaActivity.ChopWood });
                }
            }

            // 2. Khai thác đá
            var stoneGroup = GameObject.Find("Diorama_Stones");
            if (stoneGroup != null)
            {
                foreach (Transform t in stoneGroup.transform)
                {
                    targets.Add(new TargetInfo { transform = t, activity = DioramaActivity.MineStone });
                }
            }

            // 3. Khai thác vàng
            var goldGroup = GameObject.Find("Diorama_Gold");
            if (goldGroup != null)
            {
                foreach (Transform t in goldGroup.transform)
                {
                    targets.Add(new TargetInfo { transform = t, activity = DioramaActivity.MineStone });
                }
            }

            // 4. Xây dựng công trình dang dở (móng nhà)
            var foundationGroup = GameObject.Find("Diorama_Foundations");
            if (foundationGroup != null)
            {
                foreach (Transform t in foundationGroup.transform)
                {
                    targets.Add(new TargetInfo { transform = t, activity = DioramaActivity.Build });
                }
            }

            // 4b. Nhà dân (Houses)
            var houseGroup = GameObject.Find("Diorama_Houses");
            if (houseGroup != null)
            {
                foreach (Transform t in houseGroup.transform)
                {
                    targets.Add(new TargetInfo { transform = t, activity = Random.value < 0.4f ? DioramaActivity.Build : DioramaActivity.Idle });
                }
            }

            // 4c. Trại gỗ (Wood Camp)
            var woodCampGroup = GameObject.Find("Diorama_WoodCamps");
            if (woodCampGroup != null)
            {
                foreach (Transform t in woodCampGroup.transform)
                {
                    targets.Add(new TargetInfo { transform = t, activity = Random.value < 0.5f ? DioramaActivity.Build : DioramaActivity.Idle });
                }
            }

            // 5. Nếu không tìm thấy gì hoặc ngẫu nhiên 20% thì đi dạo tự do
            if (targets.Count == 0 || Random.value < 0.2f)
            {
                Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
                randomDir += _startPos;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, NavMesh.AllAreas))
                {
                    _currentTargetTransform = null;
                    _currentActivity = DioramaActivity.Idle;
                    _agent.SetDestination(hit.position);
                    return;
                }
            }

            // Chọn ngẫu nhiên một mục tiêu trong danh sách
            if (targets.Count > 0)
            {
                int index = Random.Range(0, targets.Count);
                var selected = targets[index];
                _currentTargetTransform = selected.transform;
                _currentActivity = selected.activity;

                // Tránh đi thẳng vào giữa tâm vật thể (nhất là đá lớn hay móng nhà)
                Vector3 targetPos = selected.transform.position;
                Vector3 direction = (transform.position - targetPos).normalized;
                if (direction == Vector3.zero) direction = Vector3.forward;

                float stoppingOffset = 1.6f;
                if (selected.activity == DioramaActivity.Build) stoppingOffset = 2.8f; // Móng nhà to hơn nên đứng xa hơn

                Vector3 destination = targetPos + direction * stoppingOffset;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(destination, out hit, 4f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                }
                else
                {
                    _agent.SetDestination(targetPos);
                }
            }
        }

        private struct TargetInfo
        {
            public Transform transform;
            public DioramaActivity activity;
        }
    }
}
