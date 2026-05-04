using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class RefactoredDungeonGenerationManager : MonoBehaviour
{
    public enum RoomKind
    {
        Start,
        Normal,
        Key,
        Loot,
        End
    }

    [Serializable]
    public class RoomDefinition
    {
        public RoomKind kind;
        public GameObject prefab;
        [Min(1)] public int weight = 1;
    }

    private class RoomNode
    {
        public int id;
        public RoomKind kind;
        public RoomDefinition definition;
        public Vector2Int gridPosition;
        public GameObject instance;
        public readonly List<RoomEdge> edges = new();
    }

    private class RoomEdge
    {
        public RoomNode a;
        public RoomNode b;
        public int aEntrance;
        public int bEntrance;
        public bool lockedByKey;
        public bool physicalConnection;
        public bool hallwaySpawned;

        public bool Contains(RoomNode node)
        {
            return a == node || b == node;
        }

        public RoomNode Other(RoomNode node)
        {
            return a == node ? b : a;
        }

        public int EntranceFor(RoomNode node)
        {
            return a == node ? aEntrance : bEntrance;
        }
    }

    [Header("Room Catalog")]
    public List<RoomDefinition> roomDefinitions = new();

    [Header("Dungeon Shape")]
    [Min(3)] public int mainPathRoomCount = 8;
    [Min(0)] public int extraNormalBranchRooms = 5;
    [Min(0)] public int loopConnectionCount = 2;
    [Min(1)] public int minStartEndGridDistance = 6;
    [Min(1)] public int maxLayoutAttempts = 80;

    [Header("Instantiation")]
    public Transform origin;
    public GameObject hallway;
    public float hallwayBuffer = 20f;
    public LayerMask roomLayerMask;

    [Header("Progression")]
    public GameObject keyPickupPrefab;
    public GameObject lockedEndDoorPrefab;

    [Header("Enemies")]
    public GameObject[] enemyPrefabs;
    public Transform enemyTarget;
    public bool spawnEnemiesInSpecialRooms;

    [Header("Player Spawn")]
    public GameObject XROriginRef;
    public GameObject pistolRef;
    public GameObject magRef;

    [Header("Debug")]
    public bool drawGraphGizmos = true;

    private readonly List<RoomNode> nodes = new();
    private readonly List<RoomEdge> edges = new();
    private readonly List<GameObject> spawnedHallways = new();
    private readonly Dictionary<Vector2Int, RoomNode> occupiedGrid = new();

    private RoomNode startNode;
    private RoomNode keyNode;
    private RoomNode lootNode;
    private RoomNode endNode;
    private GameObject lockedEndDoorInstance;

    private static readonly Vector2Int[] GridDirections =
    {
        new(0, 1),
        new(-1, 0),
        new(0, -1),
        new(1, 0)
    };

    private void Start()
    {
        GenerateDungeon();
        SpawnPlayer();
    }

    public void GenerateDungeon()
    {
        ClearDungeon();
        if (!GenerateGraphWithRetries())
        {
            return;
        }

        InstantiateGraph();
        SpawnSpecialRoomContents();
    }

    public void ResetDungeon()
    {
        GenerateDungeon();
    }

    private bool GenerateGraphWithRetries()
    {
        for (int attempt = 0; attempt < maxLayoutAttempts; attempt++)
        {
            ClearGraph();

            if (TryGenerateMainPath() && TryPlaceKeyRoom() && TryPlaceLootRoom())
            {
                PlaceExtraNormalBranches();
                return true;
            }
        }

        Debug.LogError("Failed to generate a valid dungeon graph.");
        return false;
    }

    private bool TryGenerateMainPath()
    {
        RoomNode current = CreateNode(RoomKind.Start, Vector2Int.zero);
        startNode = current;

        for (int pathIndex = 1; pathIndex < mainPathRoomCount; pathIndex++)
        {
            RoomKind kind = pathIndex == mainPathRoomCount - 1 ? RoomKind.End : RoomKind.Normal;
            List<int> availableDirections = GetFreeDirections(current.gridPosition);

            if (availableDirections.Count == 0)
            {
                return false;
            }

            int direction = PickMainPathDirection(current.gridPosition, availableDirections);
            Vector2Int nextPosition = current.gridPosition + GridDirections[direction];
            RoomNode next = CreateNode(kind, nextPosition);
            AddEdge(current, next, direction, kind == RoomKind.End);
            current = next;
        }

        endNode = current;
        int requiredDistance = Mathf.Min(minStartEndGridDistance, mainPathRoomCount - 1);
        return GridDistance(startNode.gridPosition, endNode.gridPosition) >= requiredDistance;
    }

    private bool TryPlaceKeyRoom()
    {
        List<RoomNode> candidates = new();
        int earliestIndex = Mathf.Max(1, mainPathRoomCount / 2);

        for (int i = earliestIndex; i < nodes.Count - 1; i++)
        {
            if (nodes[i].kind == RoomKind.Normal && GetFreeDirections(nodes[i].gridPosition).Count > 0)
            {
                candidates.Add(nodes[i]);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        RoomNode anchor = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        int direction = PickFreeDirection(anchor.gridPosition);
        keyNode = CreateNode(RoomKind.Key, anchor.gridPosition + GridDirections[direction]);
        AddEdge(anchor, keyNode, direction, false);
        return true;
    }

    private bool TryPlaceLootRoom()
    {
        List<RoomNode> candidates = new();

        foreach (RoomNode node in nodes)
        {
            if (node.kind == RoomKind.End || node.kind == RoomKind.Start)
            {
                continue;
            }

            if (GraphDistance(node, endNode, false) < GraphDistance(node, startNode, false)
                && GetFreeDirections(node.gridPosition).Count > 0)
            {
                candidates.Add(node);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        RoomNode anchor = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        int direction = PickFreeDirection(anchor.gridPosition);
        lootNode = CreateNode(RoomKind.Loot, anchor.gridPosition + GridDirections[direction]);
        AddEdge(anchor, lootNode, direction, false);
        return true;
    }

    private void PlaceExtraNormalBranches()
    {
        for (int i = 0; i < extraNormalBranchRooms; i++)
        {
            List<RoomNode> candidates = new();

            foreach (RoomNode node in nodes)
            {
                if (node.kind != RoomKind.End && GetFreeDirections(node.gridPosition).Count > 0)
                {
                    candidates.Add(node);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            RoomNode anchor = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            int direction = PickBranchDirection(anchor);
            RoomNode branch = CreateNode(RoomKind.Normal, anchor.gridPosition + GridDirections[direction]);
            AddEdge(anchor, branch, direction, false);
        }
    }

    private void PlaceLoopConnections()
    {
        int loopsPlaced = 0;
        List<(RoomNode from, RoomNode to, int direction)> candidates = new();

        foreach (RoomNode node in nodes)
        {
            if (node.kind == RoomKind.End)
            {
                continue;
            }

            for (int direction = 0; direction < GridDirections.Length; direction++)
            {
                Vector2Int neighborPosition = node.gridPosition + GridDirections[direction];
                if (!occupiedGrid.TryGetValue(neighborPosition, out RoomNode neighbor)
                    || neighbor.kind == RoomKind.End
                    || node.id > neighbor.id
                    || HasEdge(node, neighbor)
                    || !CanConnectPlacedAdjacentRooms(node, neighbor, direction))
                {
                    continue;
                }

                int existingDistance = GraphDistance(node, neighbor, false);
                if (existingDistance >= 3 && existingDistance < int.MaxValue)
                {
                    candidates.Add((node, neighbor, direction));
                }
            }
        }

        Shuffle(candidates);

        foreach ((RoomNode from, RoomNode to, int direction) in candidates)
        {
            if (loopsPlaced >= loopConnectionCount)
            {
                break;
            }

            RoomEdge edge = AddEdge(from, to, direction, false);
            SpawnHallwayForExistingEdge(edge);
            edge.hallwaySpawned = true;
            loopsPlaced++;
        }

        if (loopConnectionCount > 0)
        {
            Debug.Log("Placed " + loopsPlaced + " adjacent loop connections.");
        }
    }

    private RoomNode CreateNode(RoomKind kind, Vector2Int gridPosition)
    {
        RoomNode node = new()
        {
            id = nodes.Count,
            kind = kind,
            definition = PickDefinition(kind),
            gridPosition = gridPosition
        };

        nodes.Add(node);
        occupiedGrid.Add(gridPosition, node);
        return node;
    }

    private RoomEdge AddEdge(RoomNode from, RoomNode to, int fromDirection, bool lockedByKey, bool physicalConnection = true)
    {
        RoomEdge edge = new()
        {
            a = from,
            b = to,
            aEntrance = fromDirection,
            bEntrance = OppositeDirection(fromDirection),
            lockedByKey = lockedByKey,
            physicalConnection = physicalConnection
        };

        from.edges.Add(edge);
        to.edges.Add(edge);
        edges.Add(edge);
        return edge;
    }

    private void InstantiateGraph()
    {
        Queue<RoomNode> queue = new();
        HashSet<int> visited = new();
        startNode.instance = InstantiateRoom(startNode, origin);
        queue.Enqueue(startNode);
        visited.Add(startNode.id);

        while (queue.Count > 0)
        {
            RoomNode current = queue.Dequeue();

            foreach (RoomEdge edge in current.edges)
            {
                if (!edge.physicalConnection)
                {
                    continue;
                }

                RoomNode neighbor = edge.Other(current);
                if (visited.Contains(neighbor.id))
                {
                    if (edge.physicalConnection && !edge.hallwaySpawned)
                    {
                        SpawnHallwayForExistingEdge(edge);
                        edge.hallwaySpawned = true;
                    }

                    continue;
                }

                neighbor.instance = InstantiateRoom(neighbor, current.instance.transform);
                PositionRoomFromEdge(current, neighbor, edge);
                queue.Enqueue(neighbor);
                visited.Add(neighbor.id);
            }
        }

        if (loopConnectionCount > 0)
        {
            PlaceLoopConnections();
        }

        RefreshAllDoorBlockers();
        BuildRoomNavMeshes();
        SpawnRoomEnemies();
        SpawnLockedEndDoor();
    }

    private GameObject InstantiateRoom(RoomNode node, Transform baseTransform)
    {
        if (node.definition == null || node.definition.prefab == null)
        {
            Debug.LogError("Missing room definition for " + node.kind);
            return new GameObject("Missing " + node.kind + " Room");
        }

        GameObject instance = Instantiate(node.definition.prefab, baseTransform.position, baseTransform.rotation);
        instance.name = node.kind + " Room " + node.id;

        RoomPrefab roomPrefab = EnsureRoomPrefab(instance);
        roomPrefab.physicalRoom = instance;
        roomPrefab.dungeonID = node.id;
        roomPrefab.setDimensions();

        return instance;
    }

    private void PositionRoomFromEdge(RoomNode current, RoomNode neighbor, RoomEdge edge)
    {
        RoomPrefab currentRoom = EnsureRoomPrefab(current.instance);
        RoomPrefab neighborRoom = EnsureRoomPrefab(neighbor.instance);

        int currentEntranceId = edge.EntranceFor(current);
        int neighborEntranceId = edge.EntranceFor(neighbor);

        GameObject currentEntrance = currentRoom.potentialEntrances[currentEntranceId];
        GameObject neighborEntrance = neighborRoom.potentialEntrances[neighborEntranceId];

        float currentCenterToEntrance = DistanceFromCenterToEntrance(current.instance.transform, currentEntrance.transform);
        float neighborCenterToEntrance = DistanceFromCenterToEntrance(neighbor.instance.transform, neighborEntrance.transform);
        float centerOffset = currentCenterToEntrance + hallwayBuffer + neighborCenterToEntrance;
        Vector3 direction = DirectionToVector(currentEntranceId);

        neighbor.instance.transform.SetPositionAndRotation(
            current.instance.transform.position + direction * centerOffset,
            current.instance.transform.rotation);

        SpawnStraightHallway(current.instance.transform.position, currentCenterToEntrance, direction, hallwayBuffer);
        edge.hallwaySpawned = true;
    }

    private void SpawnHallwayForExistingEdge(RoomEdge edge)
    {
        RoomPrefab aRoom = EnsureRoomPrefab(edge.a.instance);
        RoomPrefab bRoom = EnsureRoomPrefab(edge.b.instance);
        Transform aEntrance = aRoom.potentialEntrances[edge.aEntrance].transform;
        Transform bEntrance = bRoom.potentialEntrances[edge.bEntrance].transform;

        Vector3 delta = bEntrance.position - aEntrance.position;
        bool alongX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
        float length = alongX ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z);

        if (length <= 0.01f)
        {
            return;
        }

        Vector3 center = (aEntrance.position + bEntrance.position) * 0.5f;
        Quaternion rotation = Quaternion.identity;
        if (alongX)
        {
            rotation = Quaternion.Euler(0f, 90f, 0f);
        }

        GameObject spawnedHallway = Instantiate(hallway, center, rotation);
        spawnedHallway.name = "Loop Hallway " + spawnedHallways.Count;
        spawnedHallway.transform.localScale += new Vector3(0f, 0f, length);
        spawnedHallways.Add(spawnedHallway);
    }

    private void SpawnStraightHallway(Vector3 roomCenter, float centerToEntrance, Vector3 direction, float length)
    {
        if (hallway == null)
        {
            return;
        }

        Vector3 hallwayCenter = roomCenter + direction * (centerToEntrance + length * 0.5f);
        Quaternion hallwayRotation = Mathf.Abs(direction.x) > 0f
            ? Quaternion.Euler(0f, 90f, 0f)
            : Quaternion.identity;

        GameObject spawnedHallway = Instantiate(hallway, hallwayCenter, hallwayRotation);
        spawnedHallway.name = "Hallway " + spawnedHallways.Count;
        spawnedHallway.transform.localScale += new Vector3(0f, 0f, length);
        spawnedHallways.Add(spawnedHallway);
    }

    private void RefreshAllDoorBlockers()
    {
        foreach (RoomNode node in nodes)
        {
            RoomPrefab roomPrefab = EnsureRoomPrefab(node.instance);
            HashSet<int> usedEntrances = new();

            foreach (RoomEdge edge in node.edges)
            {
                if (!edge.physicalConnection)
                {
                    continue;
                }

                usedEntrances.Add(edge.EntranceFor(node));
            }

            for (int i = 0; i < roomPrefab.potentialEntrances.Length; i++)
            {
                Entrance entrance = roomPrefab.potentialEntrances[i].GetComponent<Entrance>();
                if (entrance == null)
                {
                    continue;
                }

                entrance.DeinitiateEntrance();

                if (!usedEntrances.Contains(i))
                {
                    entrance.InitiateEntrance(roomPrefab.potentialEntrances[i].transform.position);
                }
            }
        }
    }

    private void BuildRoomNavMeshes()
    {
        foreach (RoomNode node in nodes)
        {
            NavMeshSurface[] surfaces = node.instance.GetComponentsInChildren<NavMeshSurface>();
            foreach (NavMeshSurface surface in surfaces)
            {
                surface.BuildNavMesh();
            }
        }
    }

    private void SpawnRoomEnemies()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            return;
        }

        Transform target = enemyTarget != null ? enemyTarget : XROriginRef != null ? XROriginRef.transform : null;

        foreach (RoomNode node in nodes)
        {
            if (!ShouldSpawnEnemiesInRoom(node.kind))
            {
                continue;
            }

            RoomSpawnPoints spawnPoints = node.instance.GetComponentInChildren<RoomSpawnPoints>();
            if (spawnPoints == null || spawnPoints.enemySpawnPoints == null)
            {
                continue;
            }

            foreach (Transform spawnPoint in spawnPoints.enemySpawnPoints)
            {
                if (spawnPoint == null)
                {
                    continue;
                }

                GameObject enemyPrefab = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
                if (enemyPrefab == null)
                {
                    continue;
                }

                Vector3 spawnPosition = spawnPoint.position;
                if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    spawnPosition = hit.position;
                }

                GameObject enemy = Instantiate(enemyPrefab, spawnPosition, spawnPoint.rotation);
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled)
                {
                    agent.Warp(spawnPosition);
                }

                ZombieController zombie = enemy.GetComponent<ZombieController>();
                if (zombie != null && target != null)
                {
                    zombie.SetTarget(target);
                }

                EnemyAIController enemyAI = enemy.GetComponent<EnemyAIController>();
                if (enemyAI != null && target != null)
                {
                    enemyAI.SetPlayerTarget(target);
                }
            }
        }
    }

    private bool ShouldSpawnEnemiesInRoom(RoomKind kind)
    {
        if (kind == RoomKind.Normal)
        {
            return true;
        }

        return spawnEnemiesInSpecialRooms && kind != RoomKind.Start && kind != RoomKind.End;
    }

    private void SpawnSpecialRoomContents()
    {
        if (keyPickupPrefab != null && keyNode != null && keyNode.instance != null)
        {
            GameObject keyPickup = Instantiate(keyPickupPrefab, keyNode.instance.transform.position + Vector3.up * 4f, keyNode.instance.transform.rotation);
            GeneratedDungeonKeyPickup pickup = keyPickup.GetComponent<GeneratedDungeonKeyPickup>();
            if (pickup == null)
            {
                pickup = keyPickup.AddComponent<GeneratedDungeonKeyPickup>();
            }

            pickup.Initialize(this);
        }
    }

    private void SpawnLockedEndDoor()
    {
        if (lockedEndDoorPrefab == null || endNode == null || endNode.instance == null)
        {
            return;
        }

        foreach (RoomEdge edge in endNode.edges)
        {
            if (!edge.lockedByKey)
            {
                continue;
            }

            RoomPrefab endRoomPrefab = EnsureRoomPrefab(endNode.instance);
            int entranceId = edge.EntranceFor(endNode);
            Transform entrance = endRoomPrefab.potentialEntrances[entranceId].transform;
            lockedEndDoorInstance = Instantiate(lockedEndDoorPrefab, entrance.position, entrance.rotation);
            if (entranceId % 2 != 0)
            {
                lockedEndDoorInstance.transform.eulerAngles += new Vector3(0f, 90f, 0f);
            }

            return;
        }
    }

    public void UnlockEndRoom()
    {
        if (lockedEndDoorInstance != null)
        {
            Destroy(lockedEndDoorInstance);
            lockedEndDoorInstance = null;
        }
    }

    private void SpawnPlayer()
    {
        if (startNode == null || startNode.instance == null || XROriginRef == null)
        {
            return;
        }

        XROriginRef.transform.position = startNode.instance.transform.position + Vector3.up;

        if (pistolRef != null)
        {
            Instantiate(pistolRef, XROriginRef.transform.position + new Vector3(1f, 0f, 1f), XROriginRef.transform.rotation);
        }

        if (magRef != null)
        {
            Instantiate(magRef, XROriginRef.transform.position + new Vector3(-1f, 0f, 1f), XROriginRef.transform.rotation);
        }
    }

    private void ClearDungeon()
    {
        foreach (RoomNode node in nodes)
        {
            if (node.instance != null)
            {
                Destroy(node.instance);
            }
        }

        foreach (GameObject spawnedHallway in spawnedHallways)
        {
            if (spawnedHallway != null)
            {
                Destroy(spawnedHallway);
            }
        }

        spawnedHallways.Clear();
        ClearGraph();
    }

    private void ClearGraph()
    {
        nodes.Clear();
        edges.Clear();
        occupiedGrid.Clear();
        startNode = null;
        keyNode = null;
        lootNode = null;
        endNode = null;
        lockedEndDoorInstance = null;
    }

    private RoomDefinition PickDefinition(RoomKind kind)
    {
        List<RoomDefinition> candidates = new();
        int totalWeight = 0;

        foreach (RoomDefinition definition in roomDefinitions)
        {
            if (definition.kind == kind && definition.prefab != null)
            {
                candidates.Add(definition);
                totalWeight += Mathf.Max(1, definition.weight);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (RoomDefinition definition in candidates)
        {
            roll -= Mathf.Max(1, definition.weight);
            if (roll < 0)
            {
                return definition;
            }
        }

        return candidates[candidates.Count - 1];
    }

    private int GraphDistance(RoomNode from, RoomNode to, bool respectLockedEdges)
    {
        Queue<RoomNode> queue = new();
        Dictionary<int, int> distances = new();
        queue.Enqueue(from);
        distances[from.id] = 0;

        while (queue.Count > 0)
        {
            RoomNode current = queue.Dequeue();
            if (current == to)
            {
                return distances[current.id];
            }

            foreach (RoomEdge edge in current.edges)
            {
                if (respectLockedEdges && edge.lockedByKey)
                {
                    continue;
                }

                RoomNode neighbor = edge.Other(current);
                if (distances.ContainsKey(neighbor.id))
                {
                    continue;
                }

                distances[neighbor.id] = distances[current.id] + 1;
                queue.Enqueue(neighbor);
            }
        }

        return int.MaxValue;
    }

    private List<int> GetFreeDirections(Vector2Int gridPosition)
    {
        List<int> freeDirections = new();

        for (int i = 0; i < GridDirections.Length; i++)
        {
            if (!occupiedGrid.ContainsKey(gridPosition + GridDirections[i]))
            {
                freeDirections.Add(i);
            }
        }

        return freeDirections;
    }

    private int PickMainPathDirection(Vector2Int currentPosition, List<int> availableDirections)
    {
        int bestDirection = availableDirections[0];
        int bestScore = int.MinValue;

        foreach (int direction in availableDirections)
        {
            Vector2Int candidate = currentPosition + GridDirections[direction];
            int distanceFromStart = GridDistance(Vector2Int.zero, candidate);
            int freeNeighborCount = GetFreeDirections(candidate).Count;
            int adjacencyPenalty = CountOccupiedNeighbors(candidate) * 3;
            int score = distanceFromStart * 10 + freeNeighborCount - adjacencyPenalty + UnityEngine.Random.Range(0, 3);

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = direction;
            }
        }

        return bestDirection;
    }

    private int PickBranchDirection(RoomNode anchor)
    {
        List<int> freeDirections = GetFreeDirections(anchor.gridPosition);
        int bestDirection = freeDirections[0];
        int bestScore = int.MinValue;

        foreach (int direction in freeDirections)
        {
            Vector2Int candidate = anchor.gridPosition + GridDirections[direction];
            int loopableNeighbors = CountLoopableAdjacentRooms(candidate, anchor);
            int freeNeighborCount = GetFreeDirections(candidate).Count;
            int score = loopableNeighbors * 20 + freeNeighborCount + UnityEngine.Random.Range(0, 3);

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = direction;
            }
        }

        return bestDirection;
    }

    private int PickFreeDirection(Vector2Int gridPosition)
    {
        List<int> freeDirections = GetFreeDirections(gridPosition);
        return freeDirections[UnityEngine.Random.Range(0, freeDirections.Count)];
    }

    private bool HasEdge(RoomNode a, RoomNode b)
    {
        foreach (RoomEdge edge in a.edges)
        {
            if (edge.Contains(b))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanConnectPlacedAdjacentRooms(RoomNode from, RoomNode to, int direction)
    {
        if (from.instance == null || to.instance == null)
        {
            return false;
        }

        RoomPrefab fromRoom = EnsureRoomPrefab(from.instance);
        RoomPrefab toRoom = EnsureRoomPrefab(to.instance);
        int toEntrance = OppositeDirection(direction);

        if (direction >= fromRoom.potentialEntrances.Length || toEntrance >= toRoom.potentialEntrances.Length)
        {
            return false;
        }

        Transform fromEntrance = fromRoom.potentialEntrances[direction].transform;
        Transform toEntranceTransform = toRoom.potentialEntrances[toEntrance].transform;
        Vector3 delta = toEntranceTransform.position - fromEntrance.position;

        if (Mathf.Abs(delta.y) > 1.5f)
        {
            return false;
        }

        if (direction == 0 || direction == 2)
        {
            return Mathf.Abs(delta.x) <= 2f && Mathf.Abs(delta.z) > 0.1f;
        }

        return Mathf.Abs(delta.z) <= 2f && Mathf.Abs(delta.x) > 0.1f;
    }

    private int CountOccupiedNeighbors(Vector2Int gridPosition)
    {
        int count = 0;

        foreach (Vector2Int direction in GridDirections)
        {
            if (occupiedGrid.ContainsKey(gridPosition + direction))
            {
                count++;
            }
        }

        return count;
    }

    private int CountLoopableAdjacentRooms(Vector2Int gridPosition, RoomNode anchor)
    {
        int count = 0;

        foreach (Vector2Int direction in GridDirections)
        {
            Vector2Int neighborPosition = gridPosition + direction;
            if (!occupiedGrid.TryGetValue(neighborPosition, out RoomNode neighbor)
                || neighbor == anchor
                || neighbor.kind == RoomKind.End
                || HasEdge(anchor, neighbor))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static int GridDistance(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    private static int OppositeDirection(int direction)
    {
        return (direction + 2) % 4;
    }

    private static int DirectionFromTo(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x < 0 ? 1 : 3;
        }

        return delta.y < 0 ? 2 : 0;
    }

    private static Vector3 DirectionToVector(int entranceId)
    {
        switch (entranceId)
        {
            case 0:
                return Vector3.forward;
            case 1:
                return Vector3.left;
            case 2:
                return Vector3.back;
            default:
                return Vector3.right;
        }
    }

    private static float DistanceFromCenterToEntrance(Transform roomCenter, Transform entrance)
    {
        Vector3 offset = entrance.position - roomCenter.position;
        return Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z));
    }

    private static RoomPrefab EnsureRoomPrefab(GameObject instance)
    {
        RoomPrefab roomPrefab = instance.GetComponent<RoomPrefab>();
        if (roomPrefab == null)
        {
            roomPrefab = instance.AddComponent<RoomPrefab>();
        }

        if (roomPrefab.physicalRoom == null)
        {
            roomPrefab.physicalRoom = instance;
        }

        return roomPrefab;
    }

    private void OnDrawGizmos()
    {
        if (!drawGraphGizmos || edges == null)
        {
            return;
        }

        foreach (RoomEdge edge in edges)
        {
            if (edge.a?.instance == null || edge.b?.instance == null)
            {
                continue;
            }

            Gizmos.color = edge.physicalConnection ? Color.green : Color.cyan;
            Gizmos.DrawLine(edge.a.instance.transform.position + Vector3.up * 2f, edge.b.instance.transform.position + Vector3.up * 2f);
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
