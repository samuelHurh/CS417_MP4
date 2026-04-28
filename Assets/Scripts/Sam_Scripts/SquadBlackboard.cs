using System.Collections.Generic;
using UnityEngine;

public enum SupportDecision
{
    Attack,
    Heal,
    DD
}

public class SquadBlackboard : MonoBehaviour
{
    [SerializeField] private List<EnemyAIController> registeredEnemies = new List<EnemyAIController>();
    [SerializeField] private List<EnemyAIController> livingEnemies = new List<EnemyAIController>();

    public IReadOnlyList<EnemyAIController> RegisteredEnemies => registeredEnemies;
    public IReadOnlyList<EnemyAIController> LivingEnemies => livingEnemies;

    public void RegisterLivingEnemy(EnemyAIController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (!registeredEnemies.Contains(enemy))
        {
            registeredEnemies.Add(enemy);
        }

        if (!livingEnemies.Contains(enemy))
        {
            livingEnemies.Add(enemy);
        }
    }

    public void UnregisterLivingEnemy(EnemyAIController enemy)
    {
        livingEnemies.Remove(enemy);
        livingEnemies.RemoveAll(livingEnemy => livingEnemy == null);
    }

    [Header("Support Decision Table")]
    [SerializeField] private SupportDecision[] baseTable =
    {
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.Attack,
        SupportDecision.DD,
        SupportDecision.DD,
        SupportDecision.DD,
        SupportDecision.DD,
    };

    [SerializeField] private int healSlotsAtOrBelow75Percent = 2;
    [SerializeField] private int healSlotsAtOrBelow50Percent = 4;
    [SerializeField] private int healSlotsAtOrBelow25Percent = 6;

    private SupportDecision[] workingTable;

    public float GetSquadHealthPercent()
    {
        registeredEnemies.RemoveAll(enemy => enemy == null);
        livingEnemies.RemoveAll(enemy => enemy == null || enemy.IsDead);

        float totalCurrentHealth = 0f;
        float totalMaxHealth = 0f;

        foreach (EnemyAIController enemy in registeredEnemies)
        {
            EnemyRoleBrain roleBrain = enemy.RoleBrain;

            if (roleBrain == null)
            {
                continue;
            }

            totalMaxHealth += roleBrain.MaxHealth;
            totalCurrentHealth += enemy.IsDead ? 0f : roleBrain.CurrentHealth;
        }

        if (totalMaxHealth <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(totalCurrentHealth / totalMaxHealth);
    }


    public void AdjustTable()
    {
        RebuildTableFromSquadHealth();
    }

    public SupportDecision PollTable()
    {
        RebuildTableFromSquadHealth();
        return workingTable[Random.Range(0, workingTable.Length)];
    }

    public EnemyAIController GetLowestHealthLivingEnemy(EnemyAIController excludeEnemy = null)
    {
        livingEnemies.RemoveAll(enemy => enemy == null || enemy.IsDead);

        EnemyAIController lowestHealthEnemy = null;
        float lowestHealthPercent = float.PositiveInfinity;

        foreach (EnemyAIController enemy in livingEnemies)
        {
            if (enemy == excludeEnemy || enemy.RoleBrain == null || !enemy.RoleBrain.IsAlive)
            {
                continue;
            }

            if (enemy.RoleBrain.HealthPercent < lowestHealthPercent)
            {
                lowestHealthPercent = enemy.RoleBrain.HealthPercent;
                lowestHealthEnemy = enemy;
            }
        }

        return lowestHealthEnemy;
    }

    public EnemyAIController GetRandomLivingEnemy(EnemyAIController excludeEnemy = null)
    {
        livingEnemies.RemoveAll(enemy => enemy == null || enemy.IsDead);

        List<EnemyAIController> candidates = new List<EnemyAIController>();

        foreach (EnemyAIController enemy in livingEnemies)
        {
            if (enemy == excludeEnemy || enemy.RoleBrain == null || !enemy.RoleBrain.IsAlive)
            {
                continue;
            }

            candidates.Add(enemy);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void RebuildTableFromSquadHealth()
    {
        if (baseTable == null || baseTable.Length == 0)
        {
            workingTable = new[] { SupportDecision.Attack };
            return;
        }

        workingTable = new SupportDecision[baseTable.Length];
        baseTable.CopyTo(workingTable, 0);

        int healSlotCount = GetHealSlotCount(GetSquadHealthPercent());
        ConvertRandomSlotsToHeal(healSlotCount);
    }

    private int GetHealSlotCount(float squadHealthPercent)
    {
        if (squadHealthPercent <= 0.25f)
        {
            return healSlotsAtOrBelow25Percent;
        }

        if (squadHealthPercent <= 0.5f)
        {
            return healSlotsAtOrBelow50Percent;
        }

        if (squadHealthPercent <= 0.75f)
        {
            return healSlotsAtOrBelow75Percent;
        }

        return 0;
    }

    private void ConvertRandomSlotsToHeal(int requestedHealSlots)
    {
        int maxAttempts = workingTable.Length * 4;
        int numSuccessfulChanges = 0;
        int healSlotTarget = Mathf.Clamp(requestedHealSlots, 0, workingTable.Length);

        for (int i = 0; i < maxAttempts; i++)
        {
            if (numSuccessfulChanges >= healSlotTarget)
            {
                break;
            }

            int randIndex = Random.Range(0, workingTable.Length);
            if (workingTable[randIndex] != SupportDecision.Heal)
            {
                workingTable[randIndex] = SupportDecision.Heal;
                numSuccessfulChanges++;
            }
        }
    }
}
