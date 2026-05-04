using BNG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class RoomEventManager : MonoBehaviour
{
    public int difficultyPool;
    public List<GameObject> entryColliders = new List<GameObject>();
    public List<Entrance> entrances = new List<Entrance>();

    public List<Transform> spawns = new List<Transform>();
    public List<GameObject> enemies = new List<GameObject>();

    [Header("Waves")]
    public List<Transform> secondWaveSpawns = new List<Transform>();
    public List<GameObject> secondWaveEnemies = new List<GameObject>();

    [Header("Third Wave")]
    public List<Transform> thirdWaveSpawns = new List<Transform>();
    public List<GameObject> thirdWaveEnemies = new List<GameObject>();

    [Header("Spawn Indicator")]
    public GameObject enemySpawnIndicatorPrefab;
    public float spawnIndicatorDelay = 1.5f;

    [Header("Squad")]
    public SquadManager squadManager;
    public Transform playerTarget;

    [Header("NavMesh Spawning")]
    public float navMeshSpawnSampleRadius = 2f;

    private readonly List<EnemyAIController> aliveEnemies = new List<EnemyAIController>();
    private readonly Dictionary<Damageable, UnityAction> damageableDeathHandlers = new Dictionary<Damageable, UnityAction>();
    private bool encounterStarted;
    private bool encounterCompleted;
    private bool spawningWave;
    private int currentWaveIndex;

    private void Start()
    {
        EnsureSquadManager();
    }

    public void StartEncounter()
    {
        if (encounterStarted || encounterCompleted)
        {
            return;
        }

        encounterStarted = true;
        EnsureSquadManager();
        DisableEntryTriggers();
        SealConnectedEntrances();
        currentWaveIndex = 0;
        StartCoroutine(SpawnWave(currentWaveIndex));
    }

    private void DisableEntryTriggers()
    {
        foreach (GameObject entryCollider in entryColliders)
        {
            if (entryCollider == null)
            {
                continue;
            }

            entryCollider.SetActive(false);
            Debug.Log("Disable trigger");
        }
    }

    private void SealConnectedEntrances()
    {
        foreach (Entrance entrance in entrances)
        {
            if (entrance == null || !entrance.isUsed)
            {
                continue;
            }

            Debug.Log("sealingDoorway");
            entrance.SealDoorway();
        }
    }

    private IEnumerator SpawnWave(int waveIndex)
    {
        spawningWave = true;
        List<Transform> waveSpawns = GetWaveSpawns(waveIndex);
        List<GameObject> waveEnemies = GetWaveEnemies(waveIndex);

        int spawnCount = Mathf.Min(waveSpawns.Count, waveEnemies.Count);
        if (waveSpawns.Count != waveEnemies.Count)
        {
            Debug.LogWarning("RoomEventManager wave " + (waveIndex + 1) + " spawn point count does not match enemy prefab count. Spawning the matching subset.", this);
        }

        List<GameObject> indicators = new List<GameObject>();
        for (int i = 0; i < spawnCount; i++)
        {
            if (waveSpawns[i] == null || waveEnemies[i] == null)
            {
                continue;
            }

            if (enemySpawnIndicatorPrefab != null)
            {
                indicators.Add(Instantiate(enemySpawnIndicatorPrefab, waveSpawns[i].position, waveSpawns[i].rotation));
            }
        }

        if (spawnIndicatorDelay > 0f)
        {
            yield return new WaitForSeconds(spawnIndicatorDelay);
        }

        foreach (GameObject indicator in indicators)
        {
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }

        for (int i = 0; i < spawnCount; i++)
        {
            if (waveSpawns[i] == null || waveEnemies[i] == null)
            {
                continue;
            }

            Vector3 spawnPosition = GetNavMeshSpawnPosition(waveSpawns[i].position);
            GameObject enemyObject = Instantiate(waveEnemies[i], spawnPosition, waveSpawns[i].rotation);
            EnemyAIController enemyAI = enemyObject.GetComponent<EnemyAIController>();
            if (enemyAI == null)
            {
                enemyAI = enemyObject.GetComponentInChildren<EnemyAIController>();
            }

            if (enemyAI == null)
            {
                Debug.LogWarning("Spawned enemy prefab is missing an EnemyAIController.", enemyObject);
                continue;
            }

            WarpEnemyToSpawn(enemyAI, spawnPosition);
            RegisterSpawnedEnemy(enemyAI);
        }

        spawningWave = false;

        if (aliveEnemies.Count == 0)
        {
            HandleWaveCleared();
        }
    }

    private Vector3 GetNavMeshSpawnPosition(Vector3 requestedPosition)
    {
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, navMeshSpawnSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning("Enemy spawn point is not near a built NavMesh. Spawning at the marker position.", this);
        return requestedPosition;
    }

    private static void WarpEnemyToSpawn(EnemyAIController enemyAI, Vector3 spawnPosition)
    {
        if (enemyAI.NavMeshAgent != null && enemyAI.NavMeshAgent.enabled && enemyAI.NavMeshAgent.isOnNavMesh)
        {
            enemyAI.NavMeshAgent.Warp(spawnPosition);
        }
    }

    private void RegisterSpawnedEnemy(EnemyAIController enemyAI)
    {
        if (enemyAI == null || aliveEnemies.Contains(enemyAI))
        {
            return;
        }

        aliveEnemies.Add(enemyAI);
        enemyAI.Died += HandleEnemyDeath;

        if (squadManager != null)
        {
            squadManager.RegisterEnemy(enemyAI);
        }

        Damageable damageable = FindDamageable(enemyAI);
        if (damageable != null && !damageableDeathHandlers.ContainsKey(damageable))
        {
            UnityAction deathHandler = () => HandleEnemyDeath(enemyAI);
            damageableDeathHandlers.Add(damageable, deathHandler);
            damageable.onDestroyed.AddListener(deathHandler);
        }
    }

    private void HandleEnemyDeath(EnemyAIController enemyAI)
    {
        if (enemyAI == null || !aliveEnemies.Remove(enemyAI))
        {
            return;
        }

        enemyAI.Died -= HandleEnemyDeath;

        if (!enemyAI.IsDead)
        {
            enemyAI.MarkDead();
        }

        if (squadManager != null)
        {
            squadManager.UnregisterEnemy(enemyAI);
        }

        UnsubscribeDamageableDeath(enemyAI);

        if (aliveEnemies.Count == 0)
        {
            HandleWaveCleared();
        }
    }

    private void HandleWaveCleared()
    {
        if (spawningWave || encounterCompleted)
        {
            return;
        }

        int nextWaveIndex = currentWaveIndex + 1;

        if (HasWave(nextWaveIndex))
        {
            currentWaveIndex = nextWaveIndex;
            StartCoroutine(SpawnWave(currentWaveIndex));
            return;
        }

        CompleteEncounter();
    }

    private bool HasWave(int waveIndex)
    {
        return GetWaveEnemies(waveIndex).Count > 0;
    }

    private List<Transform> GetWaveSpawns(int waveIndex)
    {
        if (waveIndex == 0)
        {
            return spawns;
        }

        if (waveIndex == 1)
        {
            return secondWaveSpawns.Count > 0 ? secondWaveSpawns : spawns;
        }

        if (waveIndex == 2)
        {
            return thirdWaveSpawns.Count > 0 ? thirdWaveSpawns : spawns;
        }

        return new List<Transform>();
    }

    private List<GameObject> GetWaveEnemies(int waveIndex)
    {
        if (waveIndex == 0)
        {
            return enemies;
        }

        if (waveIndex == 1)
        {
            return secondWaveEnemies;
        }

        if (waveIndex == 2)
        {
            return thirdWaveEnemies;
        }

        return new List<GameObject>();
    }

    private void CompleteEncounter()
    {
        if (encounterCompleted)
        {
            return;
        }

        encounterCompleted = true;

        foreach (Entrance entrance in entrances)
        {
            if (entrance != null && entrance.isUsed)
            {
                entrance.UnsealDoorway();
            }
        }

        Debug.Log("Room encounter completed.", this);
    }

    private void EnsureSquadManager()
    {
        if (squadManager == null)
        {
            squadManager = GetComponent<SquadManager>();
        }

        if (squadManager == null)
        {
            if (GetComponent<SquadBlackboard>() == null)
            {
                gameObject.AddComponent<SquadBlackboard>();
            }

            squadManager = gameObject.AddComponent<SquadManager>();
        }

        squadManager.EnsureBlackboard();

        if (playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTarget = playerObject.transform;
            }
        }

        if (playerTarget != null)
        {
            squadManager.SetPlayerTarget(playerTarget);
        }
    }

    private void UnsubscribeDamageableDeath(EnemyAIController enemyAI)
    {
        Damageable damageable = FindDamageable(enemyAI);
        if (damageable != null && damageableDeathHandlers.TryGetValue(damageable, out UnityAction handler))
        {
            damageable.onDestroyed.RemoveListener(handler);
            damageableDeathHandlers.Remove(damageable);
        }
    }

    private static Damageable FindDamageable(EnemyAIController enemyAI)
    {
        if (enemyAI == null)
        {
            return null;
        }

        Damageable damageable = enemyAI.GetComponent<Damageable>();
        if (damageable == null)
        {
            damageable = enemyAI.GetComponentInChildren<Damageable>();
        }

        if (damageable == null)
        {
            damageable = enemyAI.GetComponentInParent<Damageable>();
        }

        return damageable;
    }

    private void OnDestroy()
    {
        foreach (EnemyAIController enemyAI in aliveEnemies)
        {
            if (enemyAI != null)
            {
                enemyAI.Died -= HandleEnemyDeath;
            }
        }

        foreach (KeyValuePair<Damageable, UnityAction> handler in damageableDeathHandlers)
        {
            if (handler.Key != null)
            {
                handler.Key.onDestroyed.RemoveListener(handler.Value);
            }
        }

        damageableDeathHandlers.Clear();
        aliveEnemies.Clear();
    }
}
