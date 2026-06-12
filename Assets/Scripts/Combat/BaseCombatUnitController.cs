using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseCombatUnitController : MonoBehaviour
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [Header("Faction & Identity")]
    public UnitFaction faction;
    public string unitName = "Combat Unit";

    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    public int attackDamage = 15;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;
    public float scanRange = 15f; // Tầm tự động phát hiện kẻ địch

    [Header("Scan Optimization")]
    [SerializeField] protected float idleScanInterval = 0.25f;
    [SerializeField] protected float movingScanInterval = 0.35f;

    [Header("State")]
    public CombatState currentState = CombatState.Idle;
    public bool autoAggroDuringMove = true; // Tự động tự vệ tấn công địch khi hành quân ngang qua
    public virtual int TargetPriorityPenalty => 0;

    [Header("Chase Leash")]
    [SerializeField] protected bool useAutoChaseLeash = true;
    [SerializeField] protected float maxAutoChaseDistance = 12f;
    [SerializeField] private float attackRangeExitBuffer = 0.35f;
    [SerializeField] private float knockupRecoveryDuration = 0.25f;
    [SerializeField] private float movingAnimationVelocityThreshold = 0.08f;
    [SerializeField] private float movingAnimationHoldTime = 0.18f;

    protected NavMeshAgent navAgent;
    protected Animator animator;
    protected BaseCombatUnitController currentTarget;
    protected float lastAttackTime = 0f;
    protected bool isStunned = false;
    protected Coroutine staggerCoroutine;
    protected Coroutine flashCoroutine;
    private readonly Dictionary<Renderer, Color> _hitFlashOriginalColors = new Dictionary<Renderer, Color>();
    private readonly Dictionary<Renderer, int> _hitFlashColorProperties = new Dictionary<Renderer, int>();
    protected float blockedTimer = 0f;
    protected bool isManualMoveCommand = false;
    protected bool returnToPoolOnDeath = false;
    private Vector3 chaseAnchorPosition;
    private bool hasChaseAnchor;
    private bool isManualAttackTarget;
    private float nextIdleScanTime;
    private float nextMovingScanTime;
    private BaseCombatUnitController cachedIdleScanTarget;
    private BaseCombatUnitController cachedMovingScanTarget;
    private float knockupRecoveryUntil;
    private float movingAnimationHoldUntil;

    // Base stats caching to avoid accumulated multipliers on pool recycle
    protected int baseMaxHealth = -1;
    protected int baseAttackDamage = -1;
    protected float baseSpeed = -1f;

    protected void InitializeBaseStatsIfNeeded()
    {
        if (baseMaxHealth == -1)
        {
            baseMaxHealth = maxHealth;
            baseAttackDamage = attackDamage;
            if (navAgent == null)
            {
                navAgent = GetComponent<NavMeshAgent>();
            }
            if (navAgent != null)
            {
                baseSpeed = navAgent.speed;
            }
        }
    }

    public void ApplyStatMultipliers(float healthMult, float damageMult, float speedMult)
    {
        InitializeBaseStatsIfNeeded();
        
        maxHealth = Mathf.RoundToInt(baseMaxHealth * healthMult);
        currentHealth = maxHealth;
        attackDamage = Mathf.RoundToInt(baseAttackDamage * damageMult);
        
        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }
        if (navAgent != null && baseSpeed > 0)
        {
            navAgent.speed = baseSpeed * speedMult;
        }
    }

    public void RestoreBaseStats()
    {
        if (baseMaxHealth != -1)
        {
            maxHealth = baseMaxHealth;
            currentHealth = maxHealth;
            attackDamage = baseAttackDamage;
            if (navAgent != null && baseSpeed > 0)
            {
                navAgent.speed = baseSpeed;
            }
        }
    }

    protected virtual void Start()
    {
        InitializeBaseStatsIfNeeded();
        currentHealth = maxHealth;
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        
        // Cấu hình ban đầu cho NavMeshAgent
        if (navAgent != null)
        {
            navAgent.avoidancePriority = Random.Range(30, 45);
        }

        // Tự động gắn đèn cho unit của người chơi
        if (faction == UnitFaction.Player)
        {
            if (gameObject.GetComponent<UnitLightController>() == null)
            {
                gameObject.AddComponent<UnitLightController>();
            }
        }
    }

    protected virtual void Update()
    {
        if (currentState == CombatState.Dead) return;
        if (isStunned) return;

        if (IsKnockupActive())
        {
            UpdateKnockupAnimationState();
            return;
        }

        if (IsPostKnockupRecovering())
        {
            UpdatePostKnockupRecoveryAnimationState();
            return;
        }

        switch (currentState)
        {
            case CombatState.Idle:
                HandleIdleState();
                break;
            case CombatState.Moving:
                HandleMovingState();
                break;
            case CombatState.Chasing:
                HandleChasingState();
                break;
            case CombatState.Attacking:
                HandleAttackingState();
                break;
        }

        UpdateAvoidancePriority();
        UpdateAnimationState();
    }

    // --- LOGIC CHO TỪNG TRẠNG THÁI ---

    protected virtual void HandleIdleState()
    {
        // Tự động quét tìm mục tiêu đối địch xung quanh
        BaseCombatUnitController nearestEnemy = GetThrottledNearestEnemy(ref nextIdleScanTime, ref cachedIdleScanTarget, idleScanInterval, scanRange);
        if (nearestEnemy != null)
        {
            AttackTarget(nearestEnemy);
        }
    }

    protected virtual void HandleMovingState()
    {
        if (!IsNavAgentReady()) return;

        // Tự động quét tự vệ khi đang di chuyển hành quân
        if (autoAggroDuringMove && !isManualMoveCommand)
        {
            BaseCombatUnitController nearestEnemy = GetThrottledNearestEnemy(ref nextMovingScanTime, ref cachedMovingScanTarget, movingScanInterval, scanRange);
            if (nearestEnemy != null)
            {
                // Tầm quét chủ động khi đang di chuyển hẹp hơn tầm quét rảnh rỗi một chút để tránh lệch quá xa lộ trình
                float dist = Vector3.Distance(transform.position, nearestEnemy.transform.position);
                if (dist <= scanRange * 0.6f)
                {
                    AttackTarget(nearestEnemy);
                    return;
                }
            }
        }

        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
        {
            if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude == 0f)
            {
                isManualMoveCommand = false;
                ChangeState(CombatState.Idle);
            }
        }
    }

    protected virtual float GetDistanceToTarget(BaseCombatUnitController target)
    {
        if (target == null) return 0f;
        Collider col = GetActiveTargetCollider(target);
        if (col != null && target.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            Vector3 closestPoint = col.ClosestPoint(transform.position);
            closestPoint.y = transform.position.y;
            return Vector3.Distance(transform.position, closestPoint);
        }
        
        float dist = Vector3.Distance(transform.position, target.transform.position);
        
        // Trừ đi bán kính của bản thân và mục tiêu để tính khoảng cách thực tế giữa 2 vỏ vật lý
        float selfRadius = 0f;
        if (navAgent != null)
        {
            selfRadius = navAgent.radius;
        }
        else if (TryGetComponent(out Collider c))
        {
            selfRadius = c.bounds.extents.x;
        }

        float targetRadius = 0f;
        if (target.TryGetComponent(out NavMeshAgent targetAgent))
        {
            targetRadius = targetAgent.radius;
        }
        else if (target.TryGetComponent(out Collider targetCol))
        {
            targetRadius = targetCol.bounds.extents.x;
        }

        return Mathf.Max(0f, dist - selfRadius - targetRadius);
    }

    protected virtual float GetAttackRangeForTarget(BaseCombatUnitController target)
    {
        return attackRange;
    }

    protected virtual Collider GetActiveTargetCollider(BaseCombatUnitController target)
    {
        if (target == null) return null;

        Collider[] colliders = target.GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (col != null && col.enabled && col.gameObject.activeInHierarchy && !col.isTrigger)
            {
                return col;
            }
        }

        foreach (Collider col in colliders)
        {
            if (col != null && col.enabled && col.gameObject.activeInHierarchy)
            {
                return col;
            }
        }

        return null;
    }

    protected virtual void HandleChasingState()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        if (TrySwitchToBetterTarget())
        {
            return;
        }

        if (HasExceededAutoChaseLeash())
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        float effectiveAttackRange = GetAttackRangeForTarget(currentTarget);

        if (IsNavAgentReady())
        {
            navAgent.stoppingDistance = effectiveAttackRange * 0.5f; // Đảm bảo dừng lại khi nằm trong tầm chém
        }

        float distance = GetDistanceToTarget(currentTarget);

        if (distance <= effectiveAttackRange)
        {
            // Trong tầm đánh -> Dừng lại và bắt đầu tấn công
            if (IsNavAgentReady())
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            ChangeState(CombatState.Attacking);
            blockedTimer = 0f;
        }
        else
        {
            // Ngoài tầm đánh -> Tiếp tục đuổi theo mục tiêu
            if (IsNavAgentReady())
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(GetChaseDestination());

                // Chống kẹt: Nếu đang di chuyển đuổi theo nhưng bị các đồng đội đi trước chặn đường (vận tốc ~ 0)
                if (navAgent.velocity.sqrMagnitude < 0.05f && navAgent.hasPath)
                {
                    blockedTimer += Time.deltaTime;
                    if (blockedTimer > 1.5f) // Bị kẹt quá 1.5 giây
                    {
                        // Tìm một kẻ địch khác gần nhất để đánh thay thế
                        BaseCombatUnitController alternativeEnemy = ScanForNearestEnemy();
                        if (alternativeEnemy != null && alternativeEnemy != currentTarget)
                        {
                            AttackTarget(alternativeEnemy);
                            blockedTimer = 0f;
                        }
                    }
                }
                else
                {
                    blockedTimer = 0f;
                }
            }
        }
    }

    protected virtual Vector3 GetChaseDestination()
    {
        if (currentTarget == null) return transform.position;

        Collider col = GetActiveTargetCollider(currentTarget);
        if (col != null && currentTarget.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            // Đối với mục tiêu tĩnh (như nhà cửa), di chuyển trực tiếp đến điểm gần nhất trên Collider của nó
            Vector3 closestPoint = col.ClosestPoint(transform.position);
            closestPoint.y = transform.position.y;
            return closestPoint;
        }

        // Sử dụng GetHashCode để tính toán góc lệch duy nhất cho từng unit (tránh chụm vào 1 điểm)
        int id = GetHashCode();
        float angle = (id % 8) * 45f * Mathf.Deg2Rad; // Phân phối thành 8 hướng quanh mục tiêu
        
        // Vị trí đứng chém tối ưu (cách tâm mục tiêu bằng 65% tầm đánh)
        float offsetDistance = attackRange * 0.65f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * offsetDistance;
        
        return currentTarget.transform.position + offset;
    }

    protected virtual void HandleAttackingState()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        if (TrySwitchToBetterTarget())
        {
            return;
        }

        if (HasExceededAutoChaseLeash())
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        float distance = GetDistanceToTarget(currentTarget);

        // Nếu mục tiêu di chuyển ra xa quá tầm đánh -> Đuổi theo
        if (distance > GetAttackRangeForTarget(currentTarget) + attackRangeExitBuffer)
        {
            ChangeState(CombatState.Chasing);
            return;
        }

        // Xoay mặt mượt mà về phía mục tiêu (Chỉ xoay khi khoảng cách đủ lớn để tránh bị xoay vòng vòng khi đứng quá sát)
        Vector3 direction = currentTarget.transform.position - transform.position;
        direction.y = 0; // Giữ thăng bằng trục Y
        
        if (direction.magnitude > 0.25f)
        {
            Vector3 normDirection = direction.normalized;
            if (normDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(normDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }

        // Tấn công dựa trên Cooldown
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformAttack();
        }
    }

    // --- HÀNH ĐỘNG TẤN CÔNG & SÁT THƯƠNG ---

    protected virtual void PerformAttack()
    {
        lastAttackTime = Time.time;
        
        // Kích hoạt trigger animation tấn công
        if (animator != null)
        {
            SetAnimatorTriggerIfExists("Attack");
        }

        // Gây sát thương lên mục tiêu
        if (currentTarget != null)
        {
            BaseCombatUnitController attackTarget = currentTarget;
            string targetName = attackTarget.unitName;
            attackTarget.TakeDamage(attackDamage);
            Debug.Log($"[Combat] {unitName} tấn công {targetName} gây {attackDamage} sát thương.");
        }
    }

    public virtual void TakeDamage(int damage)
    {
        if (currentState == CombatState.Dead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"[Combat] {unitName} nhận {damage} sát thương. Máu còn lại: {currentHealth}/{maxHealth}");

        // Kích hoạt nhấp nháy đỏ phản hồi thị giác (không làm gián đoạn hành động/animation)
        TriggerHitFlash();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual IEnumerator StaggerCoroutine(float duration)
    {
        isStunned = true;
        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
        }

        yield return new WaitForSeconds(duration);

        isStunned = false;
        
        // Nếu vẫn còn sống và đang đi/đuổi quái, tiếp tục di chuyển
        if (currentState != CombatState.Dead && IsNavAgentReady())
        {
            if (currentState == CombatState.Moving || currentState == CombatState.Chasing)
            {
                navAgent.isStopped = false;
            }
        }
    }

    protected virtual void TriggerHitFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            RestoreHitFlashColors();
        }
        flashCoroutine = StartCoroutine(FlashRedCoroutine());
    }

    private IEnumerator FlashRedCoroutine()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r != null && r.material != null)
            {
                // Kiểm tra thuộc tính màu cho cả URP và Standard shader
                if (r.material.HasProperty(BaseColorProperty))
                {
                    StoreOriginalFlashColor(r, BaseColorProperty);
                    r.material.SetColor(BaseColorProperty, Color.red);
                }
                else if (r.material.HasProperty(ColorProperty))
                {
                    StoreOriginalFlashColor(r, ColorProperty);
                    r.material.SetColor(ColorProperty, Color.red);
                }
            }
        }

        yield return new WaitForSeconds(0.12f);

        // Trả lại màu gốc mượt mà
        RestoreHitFlashColors();
        flashCoroutine = null;
    }

    private void StoreOriginalFlashColor(Renderer targetRenderer, int colorProperty)
    {
        if (_hitFlashOriginalColors.ContainsKey(targetRenderer))
        {
            return;
        }

        _hitFlashOriginalColors[targetRenderer] = targetRenderer.material.GetColor(colorProperty);
        _hitFlashColorProperties[targetRenderer] = colorProperty;
    }

    private void RestoreHitFlashColors()
    {
        foreach (var pair in _hitFlashOriginalColors)
        {
            Renderer targetRenderer = pair.Key;
            if (targetRenderer == null || targetRenderer.material == null)
            {
                continue;
            }

            int colorProperty = _hitFlashColorProperties[targetRenderer];
            if (targetRenderer.material.HasProperty(colorProperty))
            {
                targetRenderer.material.SetColor(colorProperty, pair.Value);
            }
        }

        _hitFlashOriginalColors.Clear();
        _hitFlashColorProperties.Clear();
    }

    protected virtual void Die()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        RestoreHitFlashColors();

        currentTarget = null;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        isManualAttackTarget = false;
        hasChaseAnchor = false;
        ChangeState(CombatState.Dead);
        
        if (navAgent != null)
        {
            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
            }
            navAgent.enabled = false;
        }

        // Đóng băng vật lý Rigidbody (nếu có) để tránh rơi tự do xuyên đất ngay lập tức
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (animator != null)
        {
            SetAnimatorBoolIfExists("IsDead", true);
        }

        Debug.Log($"[Combat] {unitName} đã tử trận!");
        
        OnDeath();
    }

    protected virtual void OnDeath()
    {
        // Cho nằm trên đất 4 giây, sau đó chìm xuống đất 1.5 giây rồi biến mất hẳn (tổng cộng 5.5 giây)
        StartCoroutine(DestroyAfterDelay(4f, 1.5f));
    }

    private IEnumerator DestroyAfterDelay(float lieDelay, float sinkDuration)
    {
        // 1. Cho xác nằm im trên mặt đất
        yield return new WaitForSeconds(lieDelay);

        // 2. Chìm dần xuống mặt đất (Sink Effect) để biến mất mượt mà
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * 1.5f; // Chìm sâu xuống 1.5m

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / sinkDuration);
            yield return null;
        }

        // 3. Tiến hành giải phóng bộ nhớ hoặc trả về pool nếu unit này được pool quản lý
        if (returnToPoolOnDeath)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- ĐIỀU KHIỂN COMMAND (API CHO RTS CHUỘT PHẢI) ---

    public virtual void CommandMove(Vector3 position)
    {
        if (currentState == CombatState.Dead) return;

        currentTarget = null;
        isManualMoveCommand = true;
        isManualAttackTarget = false;
        hasChaseAnchor = false;
        blockedTimer = 0f;

        if (animator != null)
        {
            ResetAnimatorTriggerIfExists("Attack");
            SetAnimatorBoolIfExists("IsAttacking", false);
        }

        if (IsNavAgentReady())
        {
            navAgent.stoppingDistance = 0.2f; // Reset về mặc định khi di chuyển thường
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
        }
        ChangeState(CombatState.Moving);
    }

    public virtual void CommandAttack(BaseCombatUnitController target)
    {
        if (currentState == CombatState.Dead) return;
        if (target == null || target.currentState == CombatState.Dead) return;

        isManualMoveCommand = false;
        AttackTarget(target, true);
    }

    protected void AttackTarget(BaseCombatUnitController target, bool manualAttack = false)
    {
        isManualMoveCommand = false;
        isManualAttackTarget = manualAttack;
        chaseAnchorPosition = transform.position;
        hasChaseAnchor = true;
        currentTarget = target;
        ChangeState(CombatState.Chasing);
    }

    protected void ClearCurrentTargetAndIdle()
    {
        currentTarget = null;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        isManualAttackTarget = false;
        hasChaseAnchor = false;

        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        ChangeState(CombatState.Idle);
    }

    // --- TIỆN ÍCH DÒ TÌM KẺ ĐỊCH ---

    protected virtual BaseCombatUnitController ScanForNearestEnemy()
    {
        // Quét tất cả các Collider trong bán kính scanRange
        Collider[] colliders = Physics.OverlapSphere(transform.position, scanRange);
        BaseCombatUnitController nearest = null;
        float minDistance = float.MaxValue;
        int bestPenalty = int.MaxValue;

        foreach (var col in colliders)
        {
            BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
            if (unit != null && unit.currentState != CombatState.Dead && unit.faction != this.faction)
            {
                float dist = GetDistanceToTarget(unit);
                int penalty = Mathf.Max(0, unit.TargetPriorityPenalty);
                if (penalty < bestPenalty || (penalty == bestPenalty && dist < minDistance))
                {
                    bestPenalty = penalty;
                    minDistance = dist;
                    nearest = unit;
                }
            }
        }

        return nearest;
    }

    protected bool TrySwitchToBetterTarget()
    {
        if (currentTarget == null || currentTarget.TargetPriorityPenalty <= 0)
        {
            return false;
        }

        BaseCombatUnitController betterTarget = ScanForNearestEnemy();
        if (betterTarget == null || betterTarget == currentTarget)
        {
            return false;
        }

        if (!IsBetterTargetThanCurrent(betterTarget, currentTarget))
        {
            return false;
        }

        AttackTarget(betterTarget);
        return true;
    }

    private bool IsBetterTargetThanCurrent(BaseCombatUnitController candidate, BaseCombatUnitController current)
    {
        int candidatePenalty = Mathf.Max(0, candidate.TargetPriorityPenalty);
        int currentPenalty = Mathf.Max(0, current.TargetPriorityPenalty);
        if (candidatePenalty < currentPenalty)
        {
            return true;
        }

        if (candidatePenalty > currentPenalty)
        {
            return false;
        }

        return GetDistanceToTarget(candidate) + 1f < GetDistanceToTarget(current);
    }

    private bool HasExceededAutoChaseLeash()
    {
        if (!useAutoChaseLeash || isManualAttackTarget || !hasChaseAnchor)
        {
            return false;
        }

        if (maxAutoChaseDistance <= 0f)
        {
            return false;
        }

        Vector3 fromAnchor = transform.position - chaseAnchorPosition;
        fromAnchor.y = 0f;
        return fromAnchor.sqrMagnitude > maxAutoChaseDistance * maxAutoChaseDistance;
    }

    protected BaseCombatUnitController GetThrottledNearestEnemy(
        ref float nextScanTime,
        ref BaseCombatUnitController cachedTarget,
        float interval,
        float maxRange)
    {
        if (IsValidScanTarget(cachedTarget, maxRange) && Time.time < nextScanTime)
        {
            return cachedTarget;
        }

        if (Time.time < nextScanTime)
        {
            return null;
        }

        cachedTarget = ScanForNearestEnemy();
        nextScanTime = Time.time + Mathf.Max(0.05f, interval) + Random.Range(0f, 0.05f);

        if (!IsValidScanTarget(cachedTarget, maxRange))
        {
            cachedTarget = null;
        }

        return cachedTarget;
    }

    protected bool IsValidScanTarget(BaseCombatUnitController target, float maxRange)
    {
        if (target == null || target == this || target.currentState == CombatState.Dead || target.faction == faction)
        {
            return false;
        }

        if (!target.gameObject.activeInHierarchy)
        {
            return false;
        }

        return GetDistanceToTarget(target) <= maxRange;
    }

    // --- QUẢN LÝ TRẠNG THÁI & ANIMATOR ---

    public void ChangeState(CombatState newState)
    {
        if (currentState == CombatState.Dead && newState != CombatState.Dead && currentHealth <= 0)
        {
            return;
        }

        currentState = newState;

        if (currentState == CombatState.Moving || currentState == CombatState.Chasing)
        {
            movingAnimationHoldUntil = Time.time + Mathf.Max(0f, movingAnimationHoldTime);
        }

        if (IsNavAgentReady())
        {
            if (currentState == CombatState.Idle || currentState == CombatState.Attacking || currentState == CombatState.Dead)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.updateRotation = false; // Tắt tự động xoay của NavMesh khi không di chuyển
            }
            else
            {
                navAgent.isStopped = false;
                navAgent.updateRotation = true;  // Bật lại tự động xoay khi di chuyển/đuổi theo mục tiêu
            }
        }

        if (currentState == CombatState.Dead)
        {
            ForceDeadAnimationState();
        }
    }

    protected virtual void UpdateAnimationState()
    {
        if (animator == null) return;

        if (currentState == CombatState.Dead)
        {
            ForceDeadAnimationState();
            return;
        }

        bool useMovingAnimation = ShouldUseMovingAnimation();

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", currentState == CombatState.Idle && !useMovingAnimation);
        SetAnimatorBoolIfExists("IsMoving", useMovingAnimation);
        SetAnimatorBoolIfExists("IsAttacking", currentState == CombatState.Attacking);
    }

    private bool ShouldUseMovingAnimation()
    {
        if (currentState == CombatState.Chasing)
        {
            movingAnimationHoldUntil = Time.time + Mathf.Max(0f, movingAnimationHoldTime);
            return true;
        }

        if (Time.time < movingAnimationHoldUntil && currentState != CombatState.Attacking && currentState != CombatState.Dead)
        {
            return true;
        }

        if (currentState != CombatState.Moving)
        {
            return false;
        }

        if (!IsNavAgentReady())
        {
            return false;
        }

        if (navAgent.velocity.sqrMagnitude > movingAnimationVelocityThreshold * movingAnimationVelocityThreshold)
        {
            return true;
        }

        return navAgent.pathPending || (navAgent.hasPath && navAgent.remainingDistance > navAgent.stoppingDistance + 0.05f);
    }

    protected bool IsKnockupActive()
    {
        CombatKnockupMotion knockupMotion = GetComponent<CombatKnockupMotion>();
        return knockupMotion != null && knockupMotion.IsActive;
    }

    protected bool IsPostKnockupRecovering()
    {
        return Time.time < knockupRecoveryUntil;
    }

    protected void UpdateKnockupAnimationState()
    {
        if (animator == null) return;

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        ResetAnimatorTriggerIfExists("Attack");
    }

    protected void UpdatePostKnockupRecoveryAnimationState()
    {
        if (animator == null) return;

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        ResetAnimatorTriggerIfExists("Attack");
    }

    public void RecoverFromKnockup()
    {
        if (currentState == CombatState.Dead)
        {
            return;
        }

        blockedTimer = 0f;
        isManualMoveCommand = false;
        knockupRecoveryUntil = Time.time + Mathf.Max(0f, knockupRecoveryDuration);

        if (animator != null)
        {
            UpdatePostKnockupRecoveryAnimationState();
        }

        if (currentTarget != null && currentTarget.currentState != CombatState.Dead && currentTarget.gameObject.activeInHierarchy)
        {
            float distance = GetDistanceToTarget(currentTarget);
            float effectiveAttackRange = GetAttackRangeForTarget(currentTarget);
            ChangeState(distance <= effectiveAttackRange ? CombatState.Attacking : CombatState.Chasing);
            return;
        }

        ClearCurrentTargetAndIdle();
    }

    protected void ForceDeadAnimationState()
    {
        if (animator == null) return;

        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        SetAnimatorBoolIfExists("IsDead", true);
        ResetAnimatorTriggerIfExists("Attack");
    }

    protected void SetAnimatorBoolIfExists(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    protected void SetAnimatorTriggerIfExists(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
        }
    }

    protected void ResetAnimatorTriggerIfExists(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(parameterName);
        }
    }

    protected bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void UpdateAvoidancePriority()
    {
        if (navAgent == null || !navAgent.enabled) return;

        if (currentState == CombatState.Dead)
        {
            navAgent.avoidancePriority = 99; // Thấp nhất khi chết
            return;
        }

        // Thêm offset duy nhất dựa trên HashCode để tránh việc các unit có cùng độ ưu tiên đẩy nhau vô tận
        int uniqueOffset = Mathf.Abs(GetHashCode()) % 5;
        
        bool isRanged = (this is RangedCombatUnitController) || (this.GetType().Name.Contains("Archer"));

        if (currentState == CombatState.Attacking)
        {
            // Đang trực tiếp chiến đấu: Cận chiến ưu tiên rất cao (5-9) để đứng vững chém; Archer ưu tiên thấp hơn (35-39)
            navAgent.avoidancePriority = (isRanged ? 35 : 5) + uniqueOffset;
        }
        else if (currentState == CombatState.Chasing && currentTarget != null)
        {
            // Đang đuổi theo địch: Cận chiến ưu tiên cực cao (10-14) để chen qua đám đông; Archer ưu tiên vừa phải (45-49)
            navAgent.avoidancePriority = (isRanged ? 45 : 10) + uniqueOffset;
        }
        else if (currentState == CombatState.Moving)
        {
            // Đang di chuyển hành quân: Cận chiến ưu tiên trung-cao (20-24); Archer ưu tiên (50-54)
            navAgent.avoidancePriority = (isRanged ? 50 : 20) + uniqueOffset;
        }
        else // Idle (Đang đứng yên)
        {
            // Rảnh rỗi/đứng yên: Cận chiến (70-74); Archer (85-89) để dễ dàng nhường đường
            navAgent.avoidancePriority = (isRanged ? 85 : 70) + uniqueOffset;
        }
    }

    protected bool IsNavAgentReady()
    {
        return navAgent != null && navAgent.enabled && navAgent.isOnNavMesh;
    }
}
