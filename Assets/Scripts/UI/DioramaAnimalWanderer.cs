using UnityEngine;
using UnityEngine.AI;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script cho động vật hoang dã đi lại ngẫu nhiên trong Main Menu Diorama.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DioramaAnimalWanderer : MonoBehaviour
    {
        public float wanderRadius = 15f;
        public float waitTime = 5f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Vector3 _startPos;
        private float _timer;

        private void Start()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _startPos = transform.position;
            _timer = Random.Range(1f, waitTime);

            if (_agent != null)
            {
                _agent.speed = Random.Range(0.8f, 1.6f);
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                }
            }

            PickNewDestination();
        }

        private void Update()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            bool isMoving = _agent.velocity.magnitude > 0.1f;

            if (isMoving)
            {
                if (_animator != null)
                {
                    _animator.SetBool("IsMoving", true);
                    _animator.SetBool("IsIdle", false);
                }
            }
            else
            {
                if (_animator != null)
                {
                    _animator.SetBool("IsMoving", false);
                    _animator.SetBool("IsIdle", true);
                }

                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    PickNewDestination();
                    _timer = waitTime + Random.Range(-2f, 3f);
                }
            }
        }

        private void PickNewDestination()
        {
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir += _startPos;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }
    }
}
