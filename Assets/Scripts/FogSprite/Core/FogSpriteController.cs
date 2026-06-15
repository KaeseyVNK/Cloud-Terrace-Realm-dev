using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FogSpriteController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;

    [Header("Detection")]
    public float lightDetectRadius = 2f;
    public LayerMask lightSourceLayer;

    [Header("Stealing")]
    public int stealAmount = 10;

    [Header("References")]
    public Transform fogSpawnPoint;

    // Internal
    public FogSpriteStateMachine StateMachine { get; private set; }
    public IResourceStorage TargetStorage { get; set; }
    public SpriteRenderer SR { get; private set; }

    // Movement
    private Vector3 _destination;
    private bool _isMoving;

    public void MoveTo(Vector3 destination)
    {
        _destination = destination;
        _isMoving = true;
    }

    public void StopMoving()
    {
        _isMoving = false;
    }

    public bool HasReached(float threshold = 0.3f)
    {
        return Vector3.Distance(transform.position, _destination) <= threshold;
    }

    void Awake()
    {
        SR = GetComponent<SpriteRenderer>();
        StateMachine = new FogSpriteStateMachine();
    }

    void Start()
    {
        StateMachine.ChangeState(new SpawnState(this));
    }

    void Update()
    {
        if (_isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, _destination, moveSpeed * Time.deltaTime);

            if (HasReached())
                _isMoving = false;
        }

        StateMachine.Tick();
    }

    public bool IsInLight()
    {
        return Physics.CheckSphere(transform.position, lightDetectRadius, lightSourceLayer);
    }
}