using UnityEngine;

public class SquadBlackboard : MonoBehaviour
{
    // This reference points back to the squad manager that owns this shared memory object.
    [SerializeField] private SquadManager squadManager;
    // This value stores the current squad-wide alert/search/reset mode.
    [SerializeField] private SquadState squadState = SquadState.Idle;
    // This reference stores the player transform so all squad members can reason about the same target.
    [SerializeField] private Transform playerTarget;
    // This position stores the last exact place where any squad member confirmed the player.
    [SerializeField] private Vector3 lastKnownPlayerPosition;
    // This position stores the current center point enemies should use when searching.
    [SerializeField] private Vector3 searchCenter;
    // This radius defines how far from the search center enemies should treat as valid search space.
    [SerializeField] private float searchRadius = 4f;
    // This duration defines how long the squad should stay in the searching state before resetting.
    [SerializeField] private float searchDuration = 5f;
    // This duration defines how long the squad should stay in reset mode before returning to idle.
    [SerializeField] private float resetDuration = 2f;

    // This flag tracks whether the squad currently has a valid shared last-known player position.
    private bool hasLastKnownPlayerPosition;
    // This timestamp records when the player was last confirmed by any enemy in the squad.
    private float lastSightedTime = float.NegativeInfinity;
    // This count tracks how many registered enemies currently have line of sight to the player.
    private int enemiesWithLineOfSight;

    public SquadManager SquadManager => squadManager;
    public SquadState SquadState => squadState;
    public Transform PlayerTarget => playerTarget;
    public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
    public Vector3 SearchCenter => searchCenter;
    public float SearchRadius => searchRadius;
    public float SearchDuration => searchDuration;
    public float ResetDuration => resetDuration;
    public bool HasLastKnownPlayerPosition => hasLastKnownPlayerPosition;
    public float LastSightedTime => lastSightedTime;
    public int EnemiesWithLineOfSight => enemiesWithLineOfSight;
    public bool IsPlayerVisibleToSquad => enemiesWithLineOfSight > 0;

    // This function auto-fills the owning squad manager if the reference was not assigned in the inspector.
    private void Awake()
    {
        if (squadManager == null)
        {
            squadManager = GetComponent<SquadManager>();
        }
    }

    // This function initializes the blackboard with the squad manager and shared player reference.
    public void Initialize(SquadManager manager, Transform player)
    {
        squadManager = manager;
        playerTarget = player;
    }

    // This function updates the shared player target when the encounter is given a new player reference.
    public void SetPlayerTarget(Transform player)
    {
        playerTarget = player;
    }

    // This function forces the squad into a specific high-level state.
    public void SetSquadState(SquadState newState)
    {
        squadState = newState;
    }

    // This function records a confirmed player sighting and upgrades the squad into alerted state.
    public void ReportPlayerSighted(Vector3 playerPosition, int visibleEnemyCount)
    {
        enemiesWithLineOfSight = Mathf.Max(visibleEnemyCount, 1);
        lastKnownPlayerPosition = playerPosition;
        searchCenter = playerPosition;
        hasLastKnownPlayerPosition = true;
        lastSightedTime = Time.time;
        squadState = SquadState.Alerted;
    }

    // This function updates the number of enemies with current line of sight without changing other search data.
    public void ReportVisibilityCount(int visibleEnemyCount)
    {
        enemiesWithLineOfSight = Mathf.Max(visibleEnemyCount, 0);
    }

    // This function transitions the squad into searching mode around the last confirmed player location.
    public void BeginSearch()
    {
        if (!hasLastKnownPlayerPosition)
        {
            squadState = SquadState.Resetting;
            return;
        }

        squadState = SquadState.Searching;
        searchCenter = lastKnownPlayerPosition;
    }

    // This function transitions the squad into reset mode after search behavior has finished.
    public void BeginReset()
    {
        squadState = SquadState.Resetting;
    }

    // This function clears all alert memory so the squad can return to an idle baseline.
    public void ClearAlert()
    {
        enemiesWithLineOfSight = 0;
        hasLastKnownPlayerPosition = false;
        squadState = SquadState.Idle;
    }
}
