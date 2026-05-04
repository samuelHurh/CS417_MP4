using UnityEngine;
using UnityEngine.AI;
using TMPro;
using System;

public enum EnemyActionState
{
    Idle,
    MoveToPlayer,
    Attack,
    Reposition,
    Support,
    Dead
}

public class EnemyAIController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private SquadManager squadManager;
    [SerializeField] private SquadBlackboard squadBlackboard;
    [SerializeField] private EnemyRoleBrain roleBrain;
    [SerializeField] private Transform playerTarget;

    [Header("Movement")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private float attackRange = 1.5f;

    public SquadManager SquadManager => squadManager;
    public SquadBlackboard SquadBlackboard => squadBlackboard;
    public EnemyRoleBrain RoleBrain => roleBrain;
    public Transform PlayerTarget => playerTarget;
    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public float AttackRange => attackRange;

    public EnemyActionState ActionState { get; private set; } = EnemyActionState.Idle;

    public bool IsDead { get; private set; }
    public event Action<EnemyAIController> Died;

    [SerializeField] private TextMeshPro stateText;

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

    private void Update()
    {
        if (IsDead || roleBrain == null)
        {
            return;
        }

        roleBrain.Tick();
    }

    private void Start()
    {
        if (squadManager != null)
        {
            squadManager.RegisterEnemy(this);
        }
    }

    public void Initialize(SquadManager manager, SquadBlackboard blackboard, Transform player)
    {
        squadManager = manager;
        squadBlackboard = blackboard;
        playerTarget = player;

        if (roleBrain != null)
        {
            roleBrain.Initialize(this);
        }

        RefreshStateText();
    }

    public void SetPlayerTarget(Transform target)
    {
        playerTarget = target;
    }

    public void SetRoleBrain(EnemyRoleBrain newRoleBrain)
    {
        roleBrain = newRoleBrain;

        if (roleBrain != null)
        {
            roleBrain.Initialize(this);
        }
    }

    public void ChangeActionState(EnemyActionState newState)
    {
        if (ActionState == newState)
        {
            return;
        }

        ActionState = newState;
        RefreshStateText();
    }

    public void MoveTo(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(destination);
    }

    public bool TryGetNearestNavMeshPosition(Vector3 candidatePosition, float sampleRadius, out Vector3 navMeshPosition)
    {
        if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            navMeshPosition = hit.position;
            return true;
        }

        navMeshPosition = Vector3.zero;
        return false;
    }

    public bool HasCompletePathTo(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        return navMeshAgent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    public bool TryMoveToNearestNavMeshPosition(Vector3 candidatePosition, float sampleRadius)
    {
        if (!TryGetNearestNavMeshPosition(candidatePosition, sampleRadius, out Vector3 navMeshPosition))
        {
            return false;
        }

        if (!HasCompletePathTo(navMeshPosition))
        {
            return false;
        }

        MoveTo(navMeshPosition);
        return true;
    }

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

    public bool IsWithinAttackRange()
    {
        if (playerTarget == null)
        {
            return false;
        }

        return Vector3.Distance(transform.position, playerTarget.position) <= attackRange;
    }

    public bool HasReachedPosition(Vector3 targetPosition, float distanceThreshold = -1f)
    {
        float threshold = distanceThreshold > 0f ? distanceThreshold : attackRange;
        return Vector3.Distance(transform.position, targetPosition) <= threshold;
    }

    public void MarkDead()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        ChangeActionState(EnemyActionState.Dead);
        StopMoving();

        if (squadManager != null)
        {
            squadManager.UnregisterEnemy(this);
        }

        Died?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (squadManager != null)
        {
            squadManager.UnregisterEnemy(this);
        }
    }

    private void RefreshStateText()
    {
        if (stateText == null)
        {
            return;
        }

        stateText.text = ActionState.ToString();
    }
}
