using System.Collections.Generic;
using UnityEngine;

public enum SquadState
{
    Idle,
    Alerted,
    Searching,
    Resetting
}

[System.Serializable]
public class SquadMember
{
    // This reference stores the enemy controller that belongs to this squad member entry.
    [SerializeField] private EnemyAIController enemyAI;

    public EnemyAIController EnemyAI => enemyAI;

    // This constructor creates a lightweight registry entry for a spawned enemy controller.
    public SquadMember(EnemyAIController enemyAIController)
    {
        enemyAI = enemyAIController;
    }
}

public class SquadManager : MonoBehaviour
{
    [Header("References")]
    // This reference points to the blackboard that stores squad-wide awareness and search data.
    [SerializeField] private SquadBlackboard blackboard;
    // This reference stores the player transform that should be distributed to registered enemies.
    [SerializeField] private Transform playerTarget;

    [Header("Encounter")]
    // This list keeps a serialized view of the squad members currently registered to this encounter.
    [SerializeField] private List<SquadMember> members = new List<SquadMember>();

    // This set stores runtime-registered enemies so the squad can avoid duplicate registrations.
    private readonly HashSet<EnemyAIController> registeredEnemies = new HashSet<EnemyAIController>();
    // This set tracks which enemies currently have line of sight to the player.
    private readonly HashSet<EnemyAIController> visibleEnemies = new HashSet<EnemyAIController>();

    public SquadBlackboard Blackboard => blackboard;
    public Transform PlayerTarget => playerTarget;

    // This function bootstraps the blackboard and auto-registers any enemy controllers already parented under the squad.
    private void Awake()
    {
        members.Clear();

        if (blackboard == null)
        {
            blackboard = GetComponent<SquadBlackboard>();
        }

        if (blackboard != null)
        {
            blackboard.Initialize(this, playerTarget);
        }

        foreach (EnemyAIController enemy in GetComponentsInChildren<EnemyAIController>(true))
        {
            RegisterEnemy(enemy);
        }
    }

    // This function advances the squad-level state machine based on shared visibility and search timers.
    private void Update()
    {
        if (blackboard == null)
        {
            return;
        }

        if (blackboard.IsPlayerVisibleToSquad)
        {
            blackboard.SetSquadState(SquadState.Alerted);
            return;
        }

        if (blackboard.SquadState == SquadState.Alerted && blackboard.HasLastKnownPlayerPosition)
        {
            blackboard.BeginSearch();
            return;
        }

        if (blackboard.SquadState == SquadState.Searching &&
            Time.time - blackboard.LastSightedTime >= blackboard.SearchDuration)
        {
            blackboard.BeginReset();
            return;
        }

        if (blackboard.SquadState == SquadState.Resetting &&
            Time.time - blackboard.LastSightedTime >= blackboard.SearchDuration + blackboard.ResetDuration)
        {
            blackboard.ClearAlert();
        }
    }

    // This function pushes a new player target into the blackboard and all currently registered enemies.
    public void SetPlayerTarget(Transform player)
    {
        playerTarget = player;

        if (blackboard != null)
        {
            blackboard.SetPlayerTarget(player);
        }

        foreach (EnemyAIController enemy in registeredEnemies)
        {
            if (enemy != null)
            {
                enemy.SetPlayerTarget(player);
            }
        }
    }

    // This function registers a new enemy with the squad and injects squad dependencies into its controller.
    public void RegisterEnemy(EnemyAIController enemy)
    {
        if (enemy == null || !registeredEnemies.Add(enemy))
        {
            return;
        }

        members.Add(new SquadMember(enemy));
        enemy.Initialize(this, blackboard, playerTarget, enemy.AnchorPoint);
    }

    // This function removes an enemy from all squad tracking collections when it dies or is destroyed.
    public void UnregisterEnemy(EnemyAIController enemy)
    {
        if (enemy == null || !registeredEnemies.Remove(enemy))
        {
            return;
        }

        visibleEnemies.Remove(enemy);
        members.RemoveAll(member => member == null || member.EnemyAI == null || member.EnemyAI == enemy);
        if (blackboard != null)
        {
            blackboard.ReportVisibilityCount(visibleEnemies.Count);
        }
    }

    // This function updates squad awareness when any registered enemy regains direct sight of the player.
    public void ReportLineOfSightGained(EnemyAIController enemy, Vector3 playerPosition)
    {
        if (enemy == null || blackboard == null)
        {
            return;
        }

        visibleEnemies.Add(enemy);
        blackboard.ReportPlayerSighted(playerPosition, visibleEnemies.Count);
    }

    // This function updates squad awareness when an enemy loses direct sight of the player.
    public void ReportLineOfSightLost(EnemyAIController enemy)
    {
        if (enemy == null || blackboard == null)
        {
            return;
        }

        visibleEnemies.Remove(enemy);
        blackboard.ReportVisibilityCount(visibleEnemies.Count);

        if (!blackboard.IsPlayerVisibleToSquad)
        {
            blackboard.BeginSearch();
        }
    }
}
