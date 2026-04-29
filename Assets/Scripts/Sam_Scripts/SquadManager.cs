using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SquadMember
{
    [SerializeField] private EnemyAIController enemyAI;

    public EnemyAIController EnemyAI => enemyAI;

    public SquadMember(EnemyAIController enemyAIController)
    {
        enemyAI = enemyAIController;
    }
}

public class SquadManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SquadBlackboard blackboard;
    [SerializeField] private Transform playerTarget;

    [Header("Encounter")]
    [SerializeField] private List<SquadMember> members = new List<SquadMember>();

    private readonly HashSet<EnemyAIController> registeredEnemies = new HashSet<EnemyAIController>();

    public SquadBlackboard Blackboard => blackboard;
    public Transform PlayerTarget => playerTarget;

    private void Awake()
    {
        members.Clear();

        if (blackboard == null)
        {
            blackboard = GetComponent<SquadBlackboard>();
        }

        foreach (EnemyAIController enemy in GetComponentsInChildren<EnemyAIController>(true))
        {
            RegisterEnemy(enemy);
        }
    }

    public void SetPlayerTarget(Transform player)
    {
        playerTarget = player;

        foreach (EnemyAIController enemy in registeredEnemies)
        {
            if (enemy != null)
            {
                enemy.SetPlayerTarget(player);
            }
        }
    }

    public void RegisterEnemy(EnemyAIController enemy)
    {
        if (enemy == null || !registeredEnemies.Add(enemy))
        {
            return;
        }

        members.Add(new SquadMember(enemy));
        blackboard?.RegisterLivingEnemy(enemy);
        enemy.Initialize(this, blackboard, playerTarget);
    }

    public void UnregisterEnemy(EnemyAIController enemy)
    {
        if (enemy == null || !registeredEnemies.Remove(enemy))
        {
            return;
        }

        blackboard?.UnregisterLivingEnemy(enemy);
        members.RemoveAll(member => member == null || member.EnemyAI == null || member.EnemyAI == enemy);
    }
}
