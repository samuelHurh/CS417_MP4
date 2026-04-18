using UnityEngine;
using UnityEngine.AI;
using TMPro;

public enum EnemyAwarenessState
{
    Idle,
    Alerted,
    Searching,
    Resetting
}

public enum EnemyActionState
{
    Idle,
    MoveToPlayer,
    Attack,
    MoveToSearchArea,
    Search,
    Reposition,
    ReturnToAnchor,
    Support,
    Dead
}

public class EnemyAIController : MonoBehaviour
{
    [Header("Scene References")]
    // This reference stores the squad manager that coordinates this enemy with the rest of the encounter.
    [SerializeField] private SquadManager squadManager;
    // This reference stores the shared blackboard this enemy reads from and writes to.
    [SerializeField] private SquadBlackboard squadBlackboard;
    // This reference points to the role brain that chooses chaser, shooter, or support behavior.
    [SerializeField] private EnemyRoleBrain roleBrain;
    // This reference stores the player transform this enemy should perceive and navigate toward.
    [SerializeField] private Transform playerTarget;
    // This reference stores the home position the enemy should return to after a reset.
    [SerializeField] private Transform anchorPoint;
    // This reference stores the transform used as the origin for vision checks.
    [SerializeField] private Transform eyePoint;

    [Header("Movement")]
    // This reference stores the NavMeshAgent used for navigation and pathfinding.
    [SerializeField] private NavMeshAgent navMeshAgent;
    // This distance defines when the enemy considers itself close enough to attack.
    [SerializeField] private float attackRange = 1.5f;
    // This distance defines when the enemy considers itself to have arrived at a search point.
    [SerializeField] private float searchArrivalDistance = 1f;

    [Header("Perception")]
    // This distance limits how far away the enemy can directly detect the player.
    [SerializeField] private float sightRange = 20f;
    // This angle limits the enemy's forward field of view for line-of-sight detection.
    [SerializeField] private float sightAngle = 120f;
    // This mask defines which colliders can block or satisfy the enemy's line-of-sight raycast.
    [SerializeField] private LayerMask sightBlockers = Physics.DefaultRaycastLayers;

    public SquadManager SquadManager => squadManager;
    public SquadBlackboard SquadBlackboard => squadBlackboard;
    public EnemyRoleBrain RoleBrain => roleBrain;
    public Transform PlayerTarget => playerTarget;
    public Transform AnchorPoint => anchorPoint;
    public Vector3 AnchorPosition => anchorPoint != null ? anchorPoint.position : fallbackAnchorPosition;
    public Transform EyePoint => eyePoint != null ? eyePoint : transform;
    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public float AttackRange => attackRange;
    public float SearchArrivalDistance => searchArrivalDistance;

    public EnemyAwarenessState AwarenessState { get; private set; } = EnemyAwarenessState.Idle;
    public EnemyActionState ActionState { get; private set; } = EnemyActionState.Idle;

    // This position stores the local copy of the squad's last known player position for role-brain decisions.
    public Vector3 LastKnownPlayerPosition { get; private set; }
    // This flag tracks whether the controller currently has a meaningful last-known player position.
    public bool HasLastKnownPlayerPosition { get; private set; }
    // This flag tracks whether this specific enemy currently sees the player directly.
    public bool HasLineOfSight { get; private set; }
    // This flag tracks whether the enemy has entered its terminal dead state.
    public bool IsDead { get; private set; }

    // This fallback position stores the spawn point used when no explicit anchor transform exists.
    private Vector3 fallbackAnchorPosition;

    // This reference stores the world-space text object used to display the enemy's current debug state.
    [SerializeField] private TextMeshPro stateText;

    // This function auto-fills component references and initializes the attached role brain.
    private void Awake()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (roleBrain == null)
        {
            roleBrain = GetComponent<EnemyRoleBrain>();
        }

        if (eyePoint == null)
        {
            eyePoint = transform;
        }

        fallbackAnchorPosition = transform.position;

        if (squadManager == null)
        {
            squadManager = GetComponentInParent<SquadManager>();
        }

        if (squadBlackboard == null && squadManager != null)
        {
            squadBlackboard = squadManager.GetComponent<SquadBlackboard>();
        }

        if (roleBrain != null)
        {
            roleBrain.Initialize(this);
        }

        RefreshStateText();
    }

    // This function runs the enemy's perception sync and role-brain decision loop every frame.
    private void Update()
    {
        if (IsDead || roleBrain == null)
        {
            return;
        }

        UpdatePerception();
        SyncWithSquadBlackboard();

        roleBrain.Tick();
    }

    // This function performs delayed squad registration after Awake so the manager can inject shared refs.
    private void Start()
    {
        if (squadManager != null)
        {
            squadManager.RegisterEnemy(this);
        }
    }

    // This function injects the squad, blackboard, player, and anchor references used by this controller.
    public void Initialize(SquadManager manager, SquadBlackboard blackboard, Transform player, Transform anchor)
    {
        squadManager = manager;
        squadBlackboard = blackboard;
        playerTarget = player;
        anchorPoint = anchor;

        if (roleBrain != null)
        {
            roleBrain.Initialize(this);
        }

        RefreshStateText();
    }

    // This function replaces the player target used for perception and navigation.
    public void SetPlayerTarget(Transform target)
    {
        playerTarget = target;
    }

    // This function swaps the active role brain and re-initializes it against this controller.
    public void SetRoleBrain(EnemyRoleBrain newRoleBrain)
    {
        roleBrain = newRoleBrain;

        if (roleBrain != null)
        {
            roleBrain.Initialize(this);
        }
    }

    // This function updates the enemy's direct sight flag and refreshes last-known player data when sight is confirmed.
    public void SetLineOfSight(bool hasSight, Vector3 visiblePlayerPosition)
    {
        HasLineOfSight = hasSight;

        if (hasSight)
        {
            LastKnownPlayerPosition = visiblePlayerPosition;
            HasLastKnownPlayerPosition = true;
        }
    }

    // This function clears any remembered player location once the squad has fully reset.
    public void ClearLastKnownPlayerPosition()
    {
        HasLastKnownPlayerPosition = false;
    }

    // This function assigns a new anchor transform and updates the fallback anchor position to match it.
    public void SetAnchorPoint(Transform anchor)
    {
        anchorPoint = anchor;

        if (anchor != null)
        {
            fallbackAnchorPosition = anchor.position;
        }
    }

    // This function replaces the current action-state label used by the active role brain.
    public void ChangeActionState(EnemyActionState newState)
    {
        ActionState = newState;
        RefreshStateText();
    }

    // This function sends the NavMeshAgent toward a destination if the enemy is currently on a valid navmesh.
    public void MoveTo(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(destination);
    }

    // This function halts the NavMeshAgent and clears any active path.
    public void StopMoving()
    {
        if (navMeshAgent == null)
        {
            return;
        }

        navMeshAgent.isStopped = true;

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
        }
    }

    // This function checks whether the player is currently within this enemy's configured attack range.
    public bool IsWithinAttackRange()
    {
        if (playerTarget == null)
        {
            return false;
        }

        return Vector3.Distance(transform.position, playerTarget.position) <= attackRange;
    }

    // This function checks whether the enemy has arrived within a threshold of a target position.
    public bool HasReachedPosition(Vector3 targetPosition, float distanceThreshold = -1f)
    {
        float threshold = distanceThreshold > 0f ? distanceThreshold : searchArrivalDistance;
        return Vector3.Distance(transform.position, targetPosition) <= threshold;
    }

    // This function marks the enemy dead, stops its movement, and unregisters it from the squad.
    public void MarkDead()
    {
        IsDead = true;
        ChangeActionState(EnemyActionState.Dead);
        StopMoving();

        if (squadManager != null)
        {
            squadManager.UnregisterEnemy(this);
        }
    }

    // This function unregisters the enemy from the squad if the GameObject is destroyed unexpectedly.
    private void OnDestroy()
    {
        if (squadManager != null)
        {
            squadManager.UnregisterEnemy(this);
        }
    }

    // This function runs the local LOS check and reports sight-gained or sight-lost events to the squad manager.
    private void UpdatePerception()
    {
        bool canSeePlayer = CanSeePlayer(out Vector3 visiblePlayerPosition);

        if (canSeePlayer)
        {
            SetLineOfSight(true, visiblePlayerPosition);
            squadManager?.ReportLineOfSightGained(this, visiblePlayerPosition);
        }
        else if (HasLineOfSight)
        {
            SetLineOfSight(false, LastKnownPlayerPosition);
            squadManager?.ReportLineOfSightLost(this);
        }
    }

    // This function copies the current squad-level awareness state into the local controller's working memory.
    private void SyncWithSquadBlackboard()
    {
        if (squadBlackboard == null)
        {
            return;
        }

        switch (squadBlackboard.SquadState)
        {
            case SquadState.Alerted:
                SetAwarenessState(EnemyAwarenessState.Alerted);
                break;
            case SquadState.Searching:
                SetAwarenessState(EnemyAwarenessState.Searching);
                if (!HasLineOfSight && squadBlackboard.HasLastKnownPlayerPosition)
                {
                    LastKnownPlayerPosition = squadBlackboard.SearchCenter;
                    HasLastKnownPlayerPosition = true;
                }
                break;
            case SquadState.Resetting:
                SetAwarenessState(EnemyAwarenessState.Resetting);
                if (!HasLineOfSight)
                {
                    ClearLastKnownPlayerPosition();
                }
                break;
            default:
                SetAwarenessState(EnemyAwarenessState.Idle);
                if (!HasLineOfSight)
                {
                    ClearLastKnownPlayerPosition();
                }
                break;
        }
    }

    // This function performs the actual field-of-view and raycast visibility test against the player.
    private bool CanSeePlayer(out Vector3 visiblePlayerPosition)
    {
        visiblePlayerPosition = Vector3.zero;
        if (playerTarget == null)
        {
            return false;
        }

        Vector3 eyePosition = EyePoint.position;
        Vector3 toPlayer = playerTarget.position - eyePosition;
        float sqrDistanceToPlayer = toPlayer.sqrMagnitude;

        if (sqrDistanceToPlayer > sightRange * sightRange)
        {
            return false;
        }

        float angleToPlayer = Vector3.Angle(EyePoint.forward, toPlayer);
        if (angleToPlayer > sightAngle * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(eyePosition, toPlayer.normalized, out RaycastHit hit, sightRange, sightBlockers))
        {
            if (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))
            {
                visiblePlayerPosition = playerTarget.position;
                return true;
            }

            return false;
        }

        visiblePlayerPosition = playerTarget.position;
        return true;
    }

    // This function updates the local awareness state and refreshes the debug text only when the value changes.
    private void SetAwarenessState(EnemyAwarenessState newState)
    {
        if (AwarenessState == newState)
        {
            return;
        }

        AwarenessState = newState;
        RefreshStateText();
    }

    // This function writes the current awareness and action states to the world-space TextMeshPro label.
    private void RefreshStateText()
    {
        if (stateText == null)
        {
            return;
        }

        stateText.text = $"{AwarenessState}\n{ActionState}";
    }
}
