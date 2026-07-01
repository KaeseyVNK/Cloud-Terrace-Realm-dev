using UnityEngine;
using UnityEngine.AI;
using MyGame.UI;

/// <summary>
/// Controls the trade caravan movement, delivery logic, floating text, and loss on death.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BaseCombatUnitController))]
public class TradeCaravanController : MonoBehaviour
{
    #region Private Fields

    private ResourceType _resourceType;
    private int _amount;
    private Transform _destination;
    private NavMeshAgent _navAgent;
    private BaseCombatUnitController _combatController;
    private bool _hasArrived = false;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the trade caravan with delivery details.
    /// </summary>
    /// <param name="type">Type of the resource to deliver.</param>
    /// <param name="amount">Amount of the resource.</param>
    /// <param name="destination">Target destination transform.</param>
    public void Initialize(ResourceType type, int amount, Transform destination)
    {
        _resourceType = type;
        _amount = amount;
        _destination = destination;
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _combatController = GetComponent<BaseCombatUnitController>();

        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayCaravanBell(transform.position);
        }

        if (_navAgent != null)
        {
            _navAgent.speed = 3.5f;
            _navAgent.stoppingDistance = 1.5f;

            if (_destination != null)
            {
                _navAgent.SetDestination(_destination.position);
            }
        }
    }

    private void Update()
    {
        if (_hasArrived || _destination == null || _navAgent == null)
        {
            return;
        }

        // Check if the caravan is dead
        if (_combatController != null && _combatController.currentState == CombatState.Dead)
        {
            return;
        }

        // Update destination position dynamically in case the destination moves
        if (_navAgent.destination != _destination.position)
        {
            _navAgent.SetDestination(_destination.position);
        }

        // Check if reached destination
        if (!_navAgent.pathPending && _navAgent.remainingDistance <= _navAgent.stoppingDistance + 0.2f)
        {
            Arrive();
        }
    }

    #endregion

    #region Private Methods

    private void Arrive()
    {
        _hasArrived = true;

        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayCaravanBell(transform.position);
        }

        // Add resource to player storage
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(_resourceType, _amount);
        }

        // Determine floating text color based on resource type
        Color textColor = Color.yellow;
        switch (_resourceType)
        {
            case ResourceType.Wood:
                textColor = new Color(0.6f, 0.4f, 0.2f);
                break;
            case ResourceType.Stone:
                textColor = Color.gray;
                break;
            case ResourceType.Food:
                textColor = Color.green;
                break;
            case ResourceType.Gold:
                textColor = Color.yellow;
                break;
        }

        // Spawn floating text above the arrival position
        FloatingText.Spawn(transform.position, $"+{_amount} {_resourceType}", textColor, 2.0f, 1.2f);

        // Destroy the caravan GameObject
        Destroy(gameObject);
    }

    #endregion
}
