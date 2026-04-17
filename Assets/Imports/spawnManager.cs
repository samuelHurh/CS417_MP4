using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawnManager : MonoBehaviour
{
    // Start is called before the first frame update

    //An array of the available room gameobjects to pull from
    //when generating the dungeon. Start is always [0] and
    // the boss room is always 1.
    public GameObject[] roomPrefabArr;

    public GameObject hallway;

    public GameObject hallwayCorner;

    public List<GameObject> hallways;

    public int hallwayNumber = 0;
    //A list of rooms being used in the dungeon
    //listed in order of creation and with dungeonIDs corresponding to idx.
    public List<RoomData> rooms;
    public Transform origin;
    //Defines minimum number of rooms on shortest path from start to boss
    public int minRoomsBeforeBoss;
    //Defines the number of assignable rooms in the roomPrefabArr;
    //This number excludes the start and boss room as well as future
    //special rooms I may define
    public int numNormalRooms;
    //defines the starting index in roomPrefabArr that contains
    //normal spawnable rooms. All special rooms will be placed
    //at earlier indices.
    public int start_idx_nonspecial = 2;

    public GameObject fromEntranceMarkerRef;
    public GameObject toEntranceMarkerRef;

    //Tree datastructure specific variables;
    private RoomData head;
    private Queue<RoomData> roomQueue;
    private List<int> visitedRoomIds;
    
    public Transform playerSpawn;
    public GameObject XROriginRef;
    public GameObject pistolRef;
    public GameObject magRef;

    public LayerMask roomLayerMask;

    private int currDungeon;

    public GameObject elevatorEntry;

    public GoalManager goalManagerRef;
    void Start()
    {
        numNormalRooms = roomPrefabArr.Length - start_idx_nonspecial;
        roomQueue = new Queue<RoomData>();
        rooms = new List<RoomData>();
        visitedRoomIds = new List<int>();
        currDungeon = 1;
        GenerateBaseTree();
        
        //Debug.Log("Check sequence here");
        SpawnPlayer();
        
    }

    public void SpawnPlayer() {
        XROriginRef.transform.position = playerSpawn.position + new Vector3(0,1,0);
        pistolRef.transform.position = XROriginRef.transform.position + new Vector3 (1, 0, 1);
        Instantiate(pistolRef, XROriginRef.transform.position + new Vector3 (1, 0, 1),XROriginRef.transform.rotation);
        Instantiate(magRef, XROriginRef.transform.position + new Vector3 (-1, 0, 1),XROriginRef.transform.rotation);
        //magRef.transform.position = XROriginRef.transform.position + new Vector3 (-1, 0, 1);
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    GameObject InstantiateRoom(int roomPrefabIdx, Transform baseTransform) {
        GameObject roomInstantiated = Instantiate(roomPrefabArr[roomPrefabIdx], baseTransform.position, baseTransform.rotation);
        //roomsInUse.Add(roomInstantiated);
        return roomInstantiated;
    }

    public void ResetDungeon() {
        Debug.Log("Dungeon reset called");
        Debug.Log("///////////////////////////////////////////////");
        for (int i = 0; i < rooms.Count; i++) {
            rooms[i].Delete();
        }
        Debug.Log(hallways.Count);
        for (int j = 0; j < hallways.Count; j++) {
            Destroy(hallways[j]);
        }
        hallways.Clear();
        rooms.Clear();
        head = null;
        roomQueue.Clear();
        visitedRoomIds.Clear();
        GenerateBaseTree();
    }

    public void GenerateBaseTree() {
        //The dungeon can be represented as a tree data structure. At least initially before rewriting takes place
        //The head of the tree is a reference to the starting room
        //Each room has a class called Room attached to it which stores its neighbors
        //I'm having each Room store its neighbors but may switch this to parent/child relationship.
        //The level in the tree that a room occupies is irrelevant for now but I may add more floors so we'll have to see
        //The boss room is always a leaf node
        
        //Every graph has to have the head and the boss room

        //Dungeon Design philosophy:
        //Controlling variables:
        //- Number of intermediate rooms between start and boss
        //- Number of mandatory rooms to be included in the dungeon (for each type)
        //- Number of "branches" These are pathways that need to be traversed down upon, then traversed back up either directly or looping
        //Debug.Log(currDungeon);
        if (currDungeon == 1) {
            GenerateDungeonOne();
            
        }
        spawnDungeon();

        Debug.Log("Finished");
        goalManagerRef.InstantiateTargets(rooms);
    }

    public void GenerateDungeonOne() {
        //First Level: The first level is all about building your first weapon. It will take the form of finding three components
        //Which will need to be combined into a weapon in order to unlock the boss. This entails: main room, 3 component rooms(key rooms), boss room.
        //The room spawns are all based around a main path connecting the starting room to the boss room
        int numIntermediateRooms = 5;
        int curIntermediateRooms= 0;
        int numKeyRooms = 3;
        int curKeyRooms= 0;
        //Key room positions in tree:
        //- One key room spawns off the direct path to the boss room 
        //  # This room will be connected to the first or second new room the player enters
        //  # this room has no path leading to it as it is a direct neighbor of the main path room
        //- One key room spawns at a dead end
        //  # The composite for this room will be spawned branching from the last, or second last regular room before the boss
        //- One key room spawns in a detour loop which loops back to the main path
        // # This composite is spawned last, with a longer room path to the key room occurring earlier in the main path and a shorter room path spawning from a later room in the main path
        // # This room can also connect to a non-key room in the dead end route.

        RoomData startRoomData = RoomData.CreateInstance<RoomData>();
        startRoomData.Constructor(0,0, 4);
        head = startRoomData;
        int linkingEntrance = Random.Range(0,4);
        RoomData lastRoom = startRoomData;
        RoomData curRoom;
        int curID = 1;
        rooms.Add(startRoomData);
        while (curIntermediateRooms < numIntermediateRooms) {
            //Initiate the roomData node 
            int roomType = Random.Range(2,4);
            curRoom = RoomData.CreateInstance<RoomData>();
            //Thanks to the rooms not being instantiated, you need to put
            //the data on numEntrances and the like in some kind of global dictionary.
            curRoom.Constructor(roomType, curID, 4);
            lastRoom.addNeighbor(curRoom, linkingEntrance, true);
            //Increment curRoom to pertain to newly created node
            linkingEntrance = curRoom.availableEntrances[Random.Range(0, curRoom.availableEntrances.Count)];
            lastRoom = curRoom;
            curID++;
            
            curIntermediateRooms++;
            rooms.Add(curRoom);
        }
        //Spawn bossEntrance:
        curRoom = RoomData.CreateInstance<RoomData>();
        curRoom.Constructor(6, curID, 4);
        lastRoom.addNeighbor(curRoom, linkingEntrance, true);
        curID++;
        rooms.Add(curRoom);

        //Spawn branching component room:
        int spawningRoomID = Random.Range(1,3);
        RoomData spawningRoom = rooms[spawningRoomID];
        linkingEntrance = spawningRoom.availableEntrances[Random.Range(0, spawningRoom.availableEntrances.Count)];
        RoomData spawnedRoom = RoomData.CreateInstance<RoomData>();
        spawnedRoom.Constructor(5, curID, 4);
        spawningRoom.addNeighbor(spawnedRoom, linkingEntrance, true);
        rooms.Add(spawnedRoom);
        curID++;

        //Spawn dead end component room path:
        spawningRoomID = Random.Range(4, 6);
        int numRoomsOnPath = Random.Range(2, 4); //2 or 3 intermediate rooms
        spawningRoom = rooms[spawningRoomID];
        linkingEntrance = spawningRoom.availableEntrances[Random.Range(0, spawningRoom.availableEntrances.Count)];
        for (int j = 0; j < numRoomsOnPath; j++) {
            spawnedRoom = RoomData.CreateInstance<RoomData>();
            int roomType = Random.Range(2,4);
            spawnedRoom.Constructor(roomType, curID, 4);
            spawningRoom.addNeighbor(spawnedRoom, linkingEntrance, true);
            
            curID++;
            rooms.Add(spawnedRoom);
            spawningRoom = spawnedRoom;
            linkingEntrance = spawningRoom.availableEntrances[Random.Range(0, spawningRoom.availableEntrances.Count)];
        }
        RoomData keyRoom = RoomData.CreateInstance<RoomData>();
        keyRoom.Constructor(5, curID, 4);
        spawningRoom.addNeighbor(keyRoom, linkingEntrance, true);
        rooms.Add(keyRoom);
        curID++;


    }

    public void spawnDungeon() {
        //Strategy: 
        //Traverse the tree using a BFS which will make sure rooms closer to spawn will
        //have sufficient space to spawn (DFS may loop back around to the head room and block another head-room-adjacent room from spawning)
        Transform currRoomCenter = origin;
        roomQueue.Enqueue(head);

        int bossRoomDirection = 0;
        Transform bossEntranceCenter = origin;
        RoomData bossEnt = ScriptableObject.CreateInstance<RoomData>();
        while(roomQueue.Count != 0) {
            RoomData currRoom = roomQueue.Dequeue();
            Debug.Log("Curr room ID: " + currRoom.dungeonID);
            RoomPrefab currRoomRef;
            //Instantiate room if not instantiated (should only spawn first room)
            if (!currRoom.isSpawned) {
                currRoom.prefab = InstantiateRoom(currRoom.roomType, currRoomCenter);
                currRoom.prefab.AddComponent<RoomPrefab>();
                currRoomRef = currRoom.prefab.GetComponent<RoomPrefab>();
                currRoomRef.setDimensions();  
                currRoom.isSpawned = true;
                currRoom.SetEntrances();
                playerSpawn = currRoom.prefab.transform;
                currRoom.entrancesInitiated = true;
            } else {
                currRoomRef = currRoom.prefab.GetComponent<RoomPrefab>();
            }
            currRoomCenter = currRoom.prefab.transform;
            //This loop goes through the currRoom neighbors 1 at a time
            for (int i = 0; i < currRoom.neighborRooms.Count; i++) {
                RoomData neighborRoom = currRoom.neighborRooms[i];
                RoomPrefab neighborRoomRef;
                //Create the room
                if (!neighborRoom.isSpawned) {
                    //Debug.Log("spawning neighbor");
                    neighborRoom.prefab = InstantiateRoom(neighborRoom.roomType, currRoomCenter);
                    neighborRoom.prefab.name = "Room " + neighborRoom.dungeonID;
                    neighborRoomRef = neighborRoom.prefab.GetComponent<RoomPrefab>();
                    neighborRoomRef.physicalRoom = neighborRoom.prefab;
                    neighborRoomRef.setDimensions();
                    neighborRoom.isSpawned = true;
                    //Assign id and increment currID for next assignment
                    
                    //Get linking entrances
                    GameObject currRoomEnt = currRoomRef.potentialEntrances[currRoom.entranceIDs[i]]; //Can use i here as we are iterating through neighbors of currRoom
                    int neighborIdxOfCurrRoom = neighborRoom.neighborRooms.FindIndex(a => a == currRoom);//Find currRoom index in neighborRoom's neighbor list
                    
                    GameObject neighborRoomEnt = neighborRoomRef.potentialEntrances[neighborRoom.entranceIDs[neighborIdxOfCurrRoom]]; //Use that index to find the index of the entrance stored in entranceIDs
                    
                    bool killProcess = false;
                    GameObject roomTransform = CalculateRoomOffset(currRoom, neighborRoom, currRoomEnt, neighborRoomEnt, ref killProcess, 20f);
                    if (killProcess) {
                        Debug.Log("Reset called current spawnDungeon process killed");
                        Destroy(roomTransform);
                        return;
                    }
                    //Debug.Log("Moving object to " + roomTransform.transform.position);
                    //Vector3 currRoomCenterSinY = new(currRoomCenter.position.x, 0, currRoomCenter.position.z);
                    neighborRoom.prefab.transform.SetPositionAndRotation(roomTransform.transform.position, roomTransform.transform.rotation);
                    neighborRoom.SetEntrances();
                    Destroy(roomTransform);  
                } else {
                    //Debug.Log("Neighbor already spawned");
                }
                //This section of code adds unvisited neighbors to the queue
                if (!visitedRoomIds.Contains(currRoom.neighborRooms[i].dungeonID)) {
                    //Debug.Log("Enqueue neighbor. Current visited: " + visitedRoomIds.Count);
                    roomQueue.Enqueue(neighborRoom);
                }
                
            }
            //Moving to the next room:
            visitedRoomIds.Add(currRoom.dungeonID);
            if (currRoom.roomType == 6) {
                //This is the boss EntranceRoom
                Debug.Log("Spawned boss entrance which has ID: " + currRoom.dungeonID);
                bossRoomDirection = (currRoom.entranceIDs[0] + 2) % 4; //This room should only have one entrance
                bossEntranceCenter.SetPositionAndRotation(currRoomCenter.position, currRoomCenter.rotation);
                Destroy(bossEnt);
                bossEnt = currRoom;
            }
        }
        SpawnUpperLoop();
        SpawnBossRoom(bossEnt,bossEntranceCenter, bossRoomDirection);
        
    }

    public void SpawnUpperLoop() {
        //This is where I'll spawn the upper loop of the dungeon.
        RoomData earlyVertSend = RoomData.CreateInstance<RoomData>();
        earlyVertSend.Constructor(7, rooms.Count, 4);
        rooms.Add(earlyVertSend);
        int earlyEntToUse = head.availableEntrances[Random.Range(0, head.availableEntrances.Count)];
        head.addNeighbor(earlyVertSend, earlyEntToUse, true);
        AuxiliaryRoomSpawn(ref earlyVertSend, ref head);

        RoomData laterVertSend = RoomData.CreateInstance<RoomData>();
        laterVertSend.Constructor(7, rooms.Count, 4);
        rooms.Add(laterVertSend);
        RoomData chosenRoom;
        int laterEntToUse;
        //Have it so that the room connected to the dead-end key branch is not used twice
        if (rooms[4].neighborRooms.Contains(rooms[8])) {
            Debug.Log("Room 4 has the dead end branch");
            laterEntToUse = rooms[5].availableEntrances[Random.Range(0, rooms[5].availableEntrances.Count)];
            rooms[5].addNeighbor(laterVertSend, laterEntToUse, true);
            chosenRoom = rooms[5];
        } else {
            Debug.Log("Room 5 has the dead end branch");
            laterEntToUse = rooms[4].availableEntrances[Random.Range(0, rooms[4].availableEntrances.Count)];
            rooms[4].addNeighbor(laterVertSend, laterEntToUse, true);
            chosenRoom = rooms[4];
        }
        AuxiliaryRoomSpawn(ref laterVertSend, ref chosenRoom);

        //Instantiate the entryway for the "elevators"
        Transform earlyEntryPos = earlyVertSend.prefab.GetComponent<RoomPrefab>().specialEntrance;
        Transform laterEntryPos = laterVertSend.prefab.GetComponent<RoomPrefab>().specialEntrance;
        GameObject earlyEntry = Instantiate(elevatorEntry, earlyEntryPos.transform.position, earlyEntryPos.transform.rotation);
        GameObject laterEntry = Instantiate(elevatorEntry, laterEntryPos.transform.position, laterEntryPos.transform.rotation);
        
        int earlyEntAfterSpawning = earlyVertSend.entranceIDs[earlyVertSend.neighborRooms.FindIndex(a => a == head)];
        int laterEntAfterSpawning = laterVertSend.entranceIDs[laterVertSend.neighborRooms.FindIndex(a => a == chosenRoom)];

        Transform earlyCorrectedTransform = CorrectEntryOrientation(earlyEntry.transform, earlyEntAfterSpawning);
        earlyEntry.transform.SetPositionAndRotation(earlyCorrectedTransform.position, earlyCorrectedTransform.rotation);
        Transform laterCorrectedTransform = CorrectEntryOrientation(laterEntry.transform, laterEntAfterSpawning);
        laterEntry.transform.SetPositionAndRotation(laterCorrectedTransform.position, laterCorrectedTransform.rotation);

        //Spawn the Upper level receiving rooms and the hallways connecting them
        
        RoomData earlyVertReceive = RoomData.CreateInstance<RoomData>();
        earlyVertReceive.Constructor(8, 70, 4);
        earlyVertReceive.prefab = InstantiateRoom(8, earlyVertSend.prefab.transform);
        earlyVertReceive.prefab.transform.position += new Vector3(0, 10f, 0);
        RoomData laterVertReceive = RoomData.CreateInstance<RoomData>();
        laterVertReceive.Constructor(8, 71, 4);
        laterVertReceive.prefab = InstantiateRoom(8, laterVertSend.prefab.transform);
        laterVertReceive.prefab.transform.position += new Vector3(0, 10f, 0);

        float ascendHallwayLength = earlyVertReceive.prefab.transform.position.y 
            - earlyVertSend.prefab.transform.position.y - 3.5f;

        GameObject earlyHallwayCenter = new();
        earlyHallwayCenter.transform.position = earlyVertReceive.prefab.transform.GetComponent<RoomPrefab>().specialEntrance.transform.position;
        earlyHallwayCenter.transform.position = new Vector3(earlyHallwayCenter.transform.position.x, earlyHallwayCenter.transform.position.y - (ascendHallwayLength / 2), earlyHallwayCenter.transform.position.z);
        GameObject laterHallwayCenter = new();
        laterHallwayCenter.transform.position = laterVertReceive.prefab.transform.GetComponent<RoomPrefab>().specialEntrance.transform.position;
        laterHallwayCenter.transform.position = new Vector3(laterHallwayCenter.transform.position.x, laterHallwayCenter.transform.position.y - (ascendHallwayLength / 2), laterHallwayCenter.transform.position.z);
        GameObject earlyHallway = Instantiate(hallway, earlyHallwayCenter.transform.position, earlyHallwayCenter.transform.rotation);
        earlyHallway.transform.localScale += new Vector3(0, 0, ascendHallwayLength);
        earlyHallway.transform.eulerAngles += new Vector3(90, 0, 0);
        GameObject laterHallway = Instantiate(hallway, laterHallwayCenter.transform.position, laterHallwayCenter.transform.rotation);
        laterHallway.transform.localScale += new Vector3(0, 0, ascendHallwayLength);
        laterHallway.transform.eulerAngles += new Vector3(90, 0, 0);
        hallways.Add(earlyHallway);
        hallways.Add(laterHallway);
        Destroy(earlyHallwayCenter);
        Destroy(laterHallwayCenter);
        //Don't forget to add a upward force to propel the player upwards in this hallway

        //The next step is to determine the distance between the 2 verticalReceives.
        //I can use this to describe how to layout the upper rooms to connect
        //I will let hallway length/buffer length be flexible in order to accomodate.
        float upperXDiff = (laterVertReceive.prefab.transform.position.x - earlyVertReceive.prefab.transform.position.x);
        float upperZDiff = (laterVertReceive.prefab.transform.position.z - earlyVertReceive.prefab.transform.position.z);
        Debug.Log("x: " + upperXDiff + " z: " + upperZDiff);

        RoomData bigRoom = RoomData.CreateInstance<RoomData>();
        bigRoom.Constructor(9, 80, 8);
        bigRoom.prefab = InstantiateRoom(9, earlyVertReceive.prefab.transform);
        
        //The big room is a 30x30 room. If one dimension is large enough to house it, it goes between the receives
        //Otherwise it must be offset.
        //WARNING: ARBITRARY NUMBERS USED BELOW BASED ON GEOMETRY AT TIME. DIFFERENT NUMBERS MAY BE NEEDED IN THE FUTURE
        Vector3 toAdd;
        if (Mathf.Abs(upperXDiff) > 54 || Mathf.Abs(upperZDiff) > 54) { 
            toAdd = new Vector3 (upperXDiff / 2, 4, upperZDiff / 2);
        } else {
            //Pick a random side to place it on. The direction should be based upon the non-dominant diff
            if (Mathf.Abs(upperXDiff) >= Mathf.Abs(upperZDiff)) {
                if (upperZDiff <= 0) {
                    //The later upper receive is left of the earlier one
                    //Go right
                    toAdd = new Vector3(upperXDiff / 2, 4, 40);
                } else {
                    //Go left
                    toAdd = new Vector3(upperXDiff / 2, 4, -40);
                }
            } else {
                if (upperXDiff <= 0) {
                    toAdd = new Vector3(40, 4, upperZDiff / 2);
                } else {
                    toAdd = new Vector3(-40, 4, upperZDiff / 2);
                }
            }
            
        }
        bigRoom.prefab.transform.position += toAdd;

        //Locate the closest entrances given that there is enough space for a corner hallway piece to be inserted to change direction
        int closestBigRoomEntrance = -1;
        int closestReceiveEntrance = -1;
        float closestDistance = 100000f; //Big number so it gets overwritten immediately
        float earlyChosenXDist = 0;
        float earlyChosenZDist = 0;
        GameObject earlyChosenEntrance = new();
        GameObject earlyChosenBREntrance = new();
        float entranceTestDist = 10000f;
        int earlyVertEnt = -1;
        //Predetermine closest entrance from vertical receive
        for (int j = 0; j < earlyVertReceive.availableEntrances.Count; j++) {
            float entDist = Vector3.Distance(earlyVertReceive.prefab.GetComponent<RoomPrefab>().potentialEntrances[j].transform.position, bigRoom.prefab.transform.position);
            if (entDist < entranceTestDist) {
                entranceTestDist = entDist;
                closestReceiveEntrance = j;
                earlyVertEnt = j;
                earlyChosenEntrance = earlyVertReceive.prefab.GetComponent<RoomPrefab>().potentialEntrances[j];
            }
        }
        
        Vector3 entPos = earlyVertReceive.prefab.GetComponent<RoomPrefab>().potentialEntrances[closestReceiveEntrance].transform.position;
        for (int i = 0; i < bigRoom.availableEntrances.Count; i++) {
            float dist = Vector3.Distance(entPos, bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i].transform.position);
            float xdist = entPos.x - bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i].transform.position.x;
            float zdist = entPos.z - bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i].transform.position.z;
            float recessiveDist = (Mathf.Abs(xdist) < Mathf.Abs(zdist)) ? Mathf.Abs(xdist) : Mathf.Abs(zdist);
            if (dist < closestDistance && (recessiveDist > 3f || recessiveDist == 0f)) {
                closestDistance = dist;
                closestBigRoomEntrance = i;
                earlyChosenXDist = xdist;
                earlyChosenZDist = zdist;
                earlyChosenBREntrance = bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i];
                
            }
        }  
        
         
        //addNeighbor doesn't work right for non-4 entrance rooms so I'll do it manually by setting isSending to false;
        earlyVertReceive.addNeighbor(bigRoom, closestReceiveEntrance, false);
        bigRoom.addNeighbor(earlyVertReceive, closestBigRoomEntrance, false);
        Debug.Log("earlyVertReceive ent: " + closestReceiveEntrance + " to bigRoom ent: " + closestBigRoomEntrance);
        
        //Repeat for the late entrance:
        closestBigRoomEntrance = -1;
        closestReceiveEntrance = -1;
        closestDistance = 100000f; //Big number so it gets overwritten immediately
        float laterChosenXDist = 0;
        float laterChosenZDist = 0;
        GameObject laterChosenEntrance = new();
        GameObject laterChosenBREntrance = new();
        entranceTestDist = 10000f;
        int laterVertEnt = -1;
        //Predetermine closest entrance from vertical receive
        for (int j = 0; j < laterVertReceive.availableEntrances.Count; j++) {
            float entDist = Vector3.Distance(laterVertReceive.prefab.GetComponent<RoomPrefab>().potentialEntrances[j].transform.position, bigRoom.prefab.transform.position);
            if (entDist < entranceTestDist) {
                entranceTestDist = entDist;
                closestReceiveEntrance = j;
                laterVertEnt = j;
                laterChosenEntrance = laterVertReceive.prefab.GetComponent<RoomPrefab>().potentialEntrances[j];
            }
        }
        
        entPos = laterVertReceive.prefab.GetComponent<RoomPrefab>().potentialEntrances[closestReceiveEntrance].transform.position;
        for (int i = 0; i < bigRoom.availableEntrances.Count; i++) {
            float dist = Vector3.Distance(entPos, bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i].transform.position);
            //Accounting for the case where there is not enough room to spawn a corner hallway to reach the room.
            float xdist = entPos.x - bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i].transform.position.x;
            float zdist = entPos.z - bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i].transform.position.z;
            float recessiveDist = (Mathf.Abs(xdist) < Mathf.Abs(zdist)) ? Mathf.Abs(xdist) : Mathf.Abs(zdist);
            if (dist < closestDistance && (recessiveDist > 3f || recessiveDist == 0f)) {
                closestDistance = dist;
                closestBigRoomEntrance = i;
                laterChosenXDist = xdist;
                laterChosenZDist = zdist;
                laterChosenBREntrance = bigRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[i];
            }
        }  
        
        laterVertReceive.addNeighbor(bigRoom, closestReceiveEntrance, false);
        bigRoom.addNeighbor(earlyVertReceive, closestBigRoomEntrance, false);
        Debug.Log("laterVertReceive ent: " + closestReceiveEntrance + " to bigRoom ent: " + closestBigRoomEntrance);

        RoomData upperKeyRoom = RoomData.CreateInstance<RoomData>();
        upperKeyRoom.Constructor(5, 99, 4);
        upperKeyRoom.prefab = InstantiateRoom(upperKeyRoom.roomType, bigRoom.prefab.transform);
        upperKeyRoom.prefab.transform.position += new Vector3(0,1,0);
        rooms.Add(upperKeyRoom);

        
        earlyVertReceive.SetEntrances();
        laterVertReceive.SetEntrances();
        bigRoom.SetEntrances();
        
        //To snake a hallway to the opening under the big room. Send a hallway immediately out from the receive until tacking on a corner hallway will place it
        //at an equivalent x or z level to the opening, then stem a hallway towards the opening from the corner hallway until there is only space for another corner
        //hallway pointing upwards, then add a hallway from there connecting to the bottom of the big room.
        SnakeHallwayToBigRoom(earlyChosenXDist, earlyChosenZDist, earlyChosenEntrance, earlyChosenBREntrance, earlyVertEnt);
        SnakeHallwayToBigRoom(laterChosenXDist, laterChosenZDist, laterChosenEntrance, laterChosenBREntrance, laterVertEnt);
        
        //NEW STRATEGY:
        //Spawn one big room with 8 individual possible entry points stemming from the floor
        //Calculate which entry points are closest to the vertical receives and connect via cornering hallways and an ascending hallway
        //The key room is placed upon a raised pedestal in the middle and obstacles/cover are spawned throughout the room


    }

    public void SnakeHallwayToBigRoom(float chosenXDist, float chosenZDist, GameObject chosenEntrance, GameObject chosenBREntrance, int entNum) {
        GameObject earlyFirstHallway = new();
        GameObject earlyFirstCorner = new();
        Debug.Log("XDIST: " + chosenXDist + " ZDIST: " + chosenZDist);
        earlyFirstHallway.transform.position = chosenEntrance.transform.position;
        earlyFirstCorner.transform.position = chosenEntrance.transform.position;
        int direction = entNum;
        bool dirWasX = false;
        bool shouldSpawnSecondCorner = true;
        Vector3 rotationToApply = new Vector3();
        if (direction % 2 == 0) {
            dirWasX = false;
            if (direction == 0) {
                earlyFirstHallway.transform.position += new Vector3(0,-1.5f, (Mathf.Abs(chosenZDist) / 2) - 1.25f);
                earlyFirstCorner.transform.position += new Vector3(0,-1.5f, Mathf.Abs(chosenZDist));
                
                rotationToApply += new Vector3(0, 180, 0);
                Debug.Log("Rotated 180");
                if (chosenXDist == 0) {
                    //Do nothing
                } else if (chosenXDist < 0 ) {
                    rotationToApply += new Vector3(0, 0, 90);
                    earlyFirstCorner.transform.position += new Vector3(-1.5f,1.5f, 0);
                    Debug.Log("Case 0A");
                } else {
                    rotationToApply += new Vector3(0, 0, -90);
                    earlyFirstCorner.transform.position += new Vector3(-1.5f,1.5f, 0);
                    Debug.Log("Case 1A");
                }
                
            } else {
                earlyFirstHallway.transform.position -= new Vector3(0,1.5f, (Mathf.Abs(chosenZDist) / 2) - 1.25f);
                earlyFirstCorner.transform.position -= new Vector3(0,1.5f, Mathf.Abs(chosenZDist));
                //earlyFirstCorner.transform.eulerAngles += new Vector3(0, 0, 0);
                Debug.Log("Didn't rotate");
                if (chosenXDist == 0) {
                    //Do nothing
                    shouldSpawnSecondCorner = false;
                } else if (chosenXDist < 0 ) {
                    rotationToApply += new Vector3(0, 0, -90);
                    earlyFirstCorner.transform.position += new Vector3(-1.5f,1.5f, 0);
                    Debug.Log("Case 0B");
                } else {
                    rotationToApply += new Vector3(0, 0, 90);
                    earlyFirstCorner.transform.position += new Vector3(1.5f,1.5f, 0);
                    Debug.Log("Case 1B");
                }
            }
            

            earlyFirstCorner.transform.eulerAngles = rotationToApply;
            
            earlyFirstHallway = Instantiate(hallway, earlyFirstHallway.transform.position, earlyFirstHallway.transform.rotation);
            earlyFirstHallway.transform.localScale += new Vector3(0, 0, Mathf.Abs(Mathf.Abs(chosenZDist) - 2.5f));
            earlyFirstCorner = Instantiate(hallwayCorner, earlyFirstCorner.transform.position, earlyFirstCorner.transform.rotation);
        } else {
            dirWasX = true;
            if (direction == 1) {
                 earlyFirstHallway.transform.position -= new Vector3((Mathf.Abs(chosenXDist) / 2) - 1.25f, 1.5f,0);
                earlyFirstCorner.transform.position -= new Vector3(Mathf.Abs(chosenXDist),1.5f, 0);
                rotationToApply += new Vector3(0, 90, 0);
                Debug.Log("rotated 90");
                if (chosenZDist == 0) {
                    //Do nothing
                    shouldSpawnSecondCorner = false;
                } else if (chosenZDist < 0 ) {
                    rotationToApply += new Vector3(0, 0, 90);
                    earlyFirstCorner.transform.position += new Vector3(0,1.5f, -1.5f);
                    Debug.Log("Case 2A");
                } else {
                    rotationToApply += new Vector3(0, 0, -90);
                    earlyFirstCorner.transform.position += new Vector3(0,1.5f, 1.5f);
                    Debug.Log("Case 3A");
                }
            } else {
                earlyFirstHallway.transform.position += new Vector3((Mathf.Abs(chosenXDist) / 2) - 1.25f, -1.5f,0);
                earlyFirstCorner.transform.position += new Vector3(Mathf.Abs(chosenXDist),-1.5f, 0);
                rotationToApply += new Vector3(0, -90, 0);
                Debug.Log("rotated -90");
                if (chosenZDist == 0) {
                    //Do nothing
                } else if (chosenZDist < 0 ) {
                    rotationToApply += new Vector3(0, 0, -90);
                    earlyFirstCorner.transform.position += new Vector3(0,1.5f, -1.5f);
                    Debug.Log("Case 2B");
                } else {
                    rotationToApply += new Vector3(0, 0, 90);
                    earlyFirstCorner.transform.position += new Vector3(0,1.5f, 1.5f);
                    Debug.Log("Case 3B");
                }
                
            }
            

            earlyFirstCorner.transform.eulerAngles += rotationToApply;
            earlyFirstHallway = Instantiate(hallway, earlyFirstHallway.transform.position, earlyFirstHallway.transform.rotation);
            earlyFirstHallway.transform.eulerAngles += new Vector3 (0, 90,0);
            earlyFirstHallway.transform.localScale += new Vector3(0, 0, Mathf.Abs(Mathf.Abs(chosenXDist) - 2.5f));
            earlyFirstCorner = Instantiate(hallwayCorner, earlyFirstCorner.transform.position, earlyFirstCorner.transform.rotation);
        }

        //Next instantiate the hallway that will lead to the final xz coordinate. Check to see if a hallway is needed at first
        if (shouldSpawnSecondCorner) {
            GameObject earlySecondCorner = Instantiate(hallwayCorner, chosenBREntrance.transform.position, chosenBREntrance.transform.rotation);
            earlySecondCorner.transform.position += new Vector3(0,-4,0);
            GameObject earlySecondHallway = Instantiate(hallway, earlySecondCorner.transform.position, earlySecondCorner.transform.rotation);

            if (dirWasX) {
                //z
                if (chosenZDist < 0) {
                    earlySecondCorner.transform.eulerAngles += new Vector3(0,180,0);
                    earlySecondHallway.transform.position -= new Vector3(0, 0, Mathf.Abs(chosenZDist /2) );
                } else {
                    //Don't change rotation of earlySecond corner
                    earlySecondHallway.transform.position += new Vector3(0, 0, Mathf.Abs(chosenZDist /2));
                }
                earlySecondHallway.transform.localScale += new Vector3(0, 0, Mathf.Abs(Mathf.Abs(chosenZDist) - 5f));

            } else {
                //x
                earlySecondHallway.transform.eulerAngles += new Vector3(0, 90, 0);
                if (chosenXDist < 0) {
                    earlySecondCorner.transform.eulerAngles += new Vector3(0, -90, 0);
                    earlySecondHallway.transform.position -= new Vector3(Mathf.Abs(chosenXDist / 2), 0, 0);
                } else {
                    earlySecondCorner.transform.eulerAngles += new Vector3(0, 90, 0);
                    earlySecondHallway.transform.position += new Vector3(Mathf.Abs(chosenXDist / 2), 0, 0);
                }
                earlySecondHallway.transform.localScale += new Vector3(0, 0, Mathf.Abs(Mathf.Abs(chosenXDist) - 5f));
            }
        }
    }

    public void SpawnUpperRoom(RoomData toSpawn, RoomData toSpawnFrom, ref float upperXDiff, ref float upperZDiff, ref bool killProcess, bool spawnFromEarly) {
        GameObject earlyVertEnt = new();
        GameObject toSpawnEnt = new();
        float temporaryHallwayLength = 16f; //Shouldn't need this when the algorithm is finished
        if (Mathf.Abs(upperXDiff) > Mathf.Abs(upperZDiff)) {
            if ((upperXDiff < 0 && spawnFromEarly) || (upperXDiff > 0 && !spawnFromEarly)) {
                //Use ent1
                Destroy(earlyVertEnt);
                Destroy(toSpawnEnt);
                toSpawnFrom.addNeighbor(toSpawn, 1, true);
                earlyVertEnt = toSpawnFrom.prefab.GetComponent<RoomPrefab>().potentialEntrances[1];
                toSpawnEnt = toSpawn.prefab.GetComponent<RoomPrefab>().potentialEntrances[3];
            } else {
                //Use ent3
                Destroy(earlyVertEnt);
                Destroy(toSpawnEnt);
                toSpawnFrom.addNeighbor(toSpawn, 3, true);
                earlyVertEnt = toSpawnFrom.prefab.GetComponent<RoomPrefab>().potentialEntrances[3];
                toSpawnEnt = toSpawn.prefab.GetComponent<RoomPrefab>().potentialEntrances[1];
                
            }
            if (upperXDiff < 0) {
                upperXDiff += temporaryHallwayLength + toSpawn.prefab.GetComponent<RoomPrefab>().Xdim / 2;
            } else {
                upperXDiff -= temporaryHallwayLength + toSpawn.prefab.GetComponent<RoomPrefab>().Xdim / 2;
            }
        } else if (Mathf.Abs(upperXDiff) < Mathf.Abs(upperZDiff)) {
            if ((upperZDiff < 0 && spawnFromEarly) ||(upperZDiff > 0 && ! spawnFromEarly)) {
                //Use ent2
                Destroy(earlyVertEnt);
                Destroy(toSpawnEnt);
                toSpawnFrom.addNeighbor(toSpawn, 2, true);
                earlyVertEnt = toSpawnFrom.prefab.GetComponent<RoomPrefab>().potentialEntrances[2];
                toSpawnEnt = toSpawn.prefab.GetComponent<RoomPrefab>().potentialEntrances[0];
                upperZDiff += temporaryHallwayLength + toSpawn.prefab.GetComponent<RoomPrefab>().Zdim / 2;
            } else {
                //Use ent4
                Destroy(earlyVertEnt);
                Destroy(toSpawnEnt);
                toSpawnFrom.addNeighbor(toSpawn, 0, true);
                earlyVertEnt = toSpawnFrom.prefab.GetComponent<RoomPrefab>().potentialEntrances[0];
                toSpawnEnt = toSpawn.prefab.GetComponent<RoomPrefab>().potentialEntrances[2];
            }
            if (upperZDiff < 0) {
                upperZDiff += temporaryHallwayLength + toSpawn.prefab.GetComponent<RoomPrefab>().Zdim / 2;
            } else {
                upperZDiff -= temporaryHallwayLength + toSpawn.prefab.GetComponent<RoomPrefab>().Zdim / 2;
            }
        } else {
            Debug.Log("Somehow, someway, the x and z diffs were equal");
        }
        GameObject newRoomTransform = CalculateRoomOffset(toSpawnFrom, toSpawn, earlyVertEnt, toSpawnEnt, ref killProcess, temporaryHallwayLength);
        toSpawn.prefab.transform.SetPositionAndRotation(newRoomTransform.transform.position, newRoomTransform.transform.rotation);
        toSpawn.SetEntrances();
        Destroy(newRoomTransform);
        Debug.Log("x: " + upperXDiff + " z: " + upperZDiff);
    }

    public Transform CorrectEntryOrientation(Transform newTransform, int entryDir) {
        if(entryDir == 0) {
            newTransform.eulerAngles += new Vector3(0, 0, 0);
            //newTransform.position += new Vector3(0, 0.5f, 1.5f);
        } else if (entryDir == 1) {
            newTransform.eulerAngles -= new Vector3(0,90,0);
        } else if (entryDir == 2) {
            newTransform.eulerAngles -= new Vector3(0,180,0);
        } else if (entryDir == 3) {
            newTransform.eulerAngles -= new Vector3(0, 270,0);
            //newTransform.position += new Vector3(1.5f, 2.5f, 0);

        }
        return newTransform;
    }

    public void AuxiliaryRoomSpawn(ref RoomData toSpawn, ref RoomData toSpawnFrom) {
        //This function is only called to spawn offshoots of the main graph
        //once it is initialized. It cannot be used if no rooms already exist.
        toSpawn.prefab = InstantiateRoom(toSpawn.roomType, toSpawnFrom.prefab.transform);
        RoomPrefab toSpawnRef = toSpawn.prefab.GetComponent<RoomPrefab>();
        toSpawnRef.setDimensions();
        
        RoomPrefab toSpawnFromRef = toSpawnFrom.prefab.GetComponent<RoomPrefab>();

        int toSpawnID = toSpawn.dungeonID;
        int toSpawnFromID = toSpawnFrom.dungeonID;
        int TSIdx = toSpawnFrom.neighborRooms.FindIndex(a => a.dungeonID == toSpawnID);
        int TSFIdx = toSpawn.neighborRooms.FindIndex(a => a.dungeonID == toSpawnFromID);
        GameObject toSpawnEnt = toSpawnRef.potentialEntrances[toSpawn.entranceIDs[TSFIdx]];
        GameObject toSpawnFromEnt = toSpawnFromRef.potentialEntrances[toSpawnFrom.entranceIDs[TSIdx]];
        
        bool killProcess = false;
        GameObject toSpawnNewTransform = CalculateRoomOffset(toSpawnFrom, toSpawn, toSpawnFromEnt, toSpawnEnt, ref killProcess, 20.0f);
        toSpawn.prefab.transform.SetPositionAndRotation(toSpawnNewTransform.transform.position, toSpawnNewTransform.transform.rotation);
        Destroy(toSpawnNewTransform);
        toSpawn.SetEntrances();
    }

    public void SpawnBossRoom(RoomData bossEntrance, Transform bossEntranceCenter, int bossRoomDirection) {
        //The bossRoom spawns underneath the bossRoomEntrance.
        //The player will fall through a hallway to reach it.
        //Part of the reason why it is the way it is is because
        //The bossroom's meshcollider wasn't set up by the time one of the key
        //room branching paths was spawned causing the collision checking to not work.

        //Step 1: Take stock of the entrance that the boss entrance room connects to the dungeon.
        //  Spawn the boss room such that the player lands facing the majority of the room:
        Vector3 offsetToAdd = new(0,0,0);
        if (bossRoomDirection == 0) {
            offsetToAdd.z = 10;
        } else if (bossRoomDirection == 1) {
            offsetToAdd.x = -10;
        } else if (bossRoomDirection == 2) {
            offsetToAdd.z = -10;
        } else {
            offsetToAdd.x = 10;
        }
        GameObject tempLocation = new();
        tempLocation.transform.position = bossEntranceCenter.position - new Vector3(0,20,0) + offsetToAdd;
        RoomData bossRoom = ScriptableObject.CreateInstance<RoomData>();
        bossRoom.Constructor(1, 69, 4);
        bossRoom.prefab = InstantiateRoom(1, tempLocation.transform);
        bossRoom.SetEntrances();

        //Next instantiate the vertical hallway dropping the player into the boss room.:
        //I've specially added a 5th entrance for the bossEnt to use;
        float dropHallwayLength = 12f;
        tempLocation.transform.position = bossEntrance.prefab.GetComponent<RoomPrefab>().specialEntrance.position;
        tempLocation.transform.position = new Vector3(tempLocation.transform.position.x, tempLocation.transform.position.y - (dropHallwayLength / 2), tempLocation.transform.position.z);
        GameObject dropHallway = Instantiate(hallway, tempLocation.transform.position, tempLocation.transform.rotation);
        dropHallway.transform.localScale += new Vector3(0,0, dropHallwayLength);
        dropHallway.transform.eulerAngles += new Vector3(90,0, 0);
        hallways.Add(dropHallway);
        
    }

    public GameObject CalculateRoomOffset(RoomData currRoom, RoomData branchingRoom, GameObject currRoomEnt, GameObject branchingRoomEnt, ref bool killProcess, float buf) {
        GameObject toReturn = new();
        //Calculate Position;
        //So a room's orientation is indicated from 0-4 inclusive
        //A room's entrances are indicated from 0-4 inclusive
        //To find the true orientation of the room, add the two together mod(4) to determine the direction the entrance should face
        

        float branchingCenterToEntrance;
        float currCenterToEntrance;
        bool spawnAlongZAxis;
        if (branchingRoomEnt.GetComponent<Entrance>().dir == 1 || branchingRoomEnt.GetComponent<Entrance>().dir == 3) {
            branchingCenterToEntrance = Mathf.Abs(branchingRoomEnt.transform.position.x - branchingRoom.prefab.transform.position.x);
        } else {
            branchingCenterToEntrance = Mathf.Abs(branchingRoomEnt.transform.position.z - branchingRoom.prefab.transform.position.z);
        }
        if (currRoomEnt.GetComponent<Entrance>().dir == 1 || currRoomEnt.GetComponent<Entrance>().dir == 3) {
            currCenterToEntrance = Mathf.Abs(currRoomEnt.transform.position.x - currRoom.prefab.transform.position.x);
            spawnAlongZAxis = false;
        } else {
            currCenterToEntrance = Mathf.Abs(currRoomEnt.transform.position.z - currRoom.prefab.transform.position.z);
            spawnAlongZAxis = true;
        }

        //Subtracting currRoomDir from branchingRoomDir:
        //If both x and z are nonzero, the branching room needs to rotate 90 degrees
        //If all are 0, the room rotates 180 degrees
        //If one component is 2, the room doesn't need to rotate
        Entrance currEnt = currRoomEnt.GetComponent<Entrance>();
        Entrance branEnt = branchingRoomEnt.GetComponent<Entrance>();
        //currEnt.EnableEntrance();
        //Determine direction to rotate branching room in:
        //1. Get the orientation of the current room
        //2. From that orientation get the entrance direction
        //3. Apply the branching room's offset from the entrance direction
        //4. Orient the room in such a way that the receiving entrance points in the opposite direction of the current entrance

        //int currOrientation = currRoom.orientation;
        int currEntNum = currEnt.dir;
        //int sumOrientation = (currOrientation + currEntNum) % 4; //Modulo by 4 as the 90 degree orientations are the only entrance orientations so far
        
        float distOffset = branchingCenterToEntrance + buf;

        if (currEntNum == 0) {
            toReturn.transform.position = new Vector3(currRoom.prefab.transform.position.x, currRoom.prefab.transform.position.y , currRoom.prefab.transform.position.z + distOffset);
        } else if (currEntNum == 1) {
            toReturn.transform.position = new Vector3(currRoom.prefab.transform.position.x - distOffset, currRoom.prefab.transform.position.y , currRoom.prefab.transform.position.z);
        } else if (currEntNum == 2) {
            toReturn.transform.position = new Vector3(currRoom.prefab.transform.position.x, currRoom.prefab.transform.position.y , currRoom.prefab.transform.position.z - distOffset);
        } else {
            toReturn.transform.position = new Vector3(currRoom.prefab.transform.position.x + distOffset, currRoom.prefab.transform.position.y , currRoom.prefab.transform.position.z);
        }
        bool compareZAxisSpawn = spawnAlongZAxis;
        int newEntID = 0;
        toReturn.transform.position = CheckOverlap(toReturn.transform.position, currRoom, branchingRoom, distOffset, ref spawnAlongZAxis, ref currRoomEnt, ref newEntID, ref killProcess);
        if (compareZAxisSpawn != spawnAlongZAxis) {
            //Checks if new room spawns along a different axis after collision check
            Debug.Log("Change in axis");
            
            currRoom.EntranceChange(branchingRoom, newEntID, true);
            branchingRoomEnt = branchingRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[newEntID];
            if (spawnAlongZAxis) {
                currCenterToEntrance = Mathf.Abs(currRoomEnt.transform.position.z - currRoom.prefab.transform.position.z);
                branchingCenterToEntrance = Mathf.Abs(branchingRoomEnt.transform.position.z - branchingRoom.prefab.transform.position.z);
            } else {
                currCenterToEntrance = Mathf.Abs(currRoomEnt.transform.position.x - currRoom.prefab.transform.position.x);
                branchingCenterToEntrance = Mathf.Abs(branchingRoomEnt.transform.position.x - branchingRoom.prefab.transform.position.x);
            }
            Debug.Log("branchingCenterToEntrance: " + branchingCenterToEntrance);
        }
        currRoom.ResetEntrances();

        float emptyDist;
        GameObject sendingHallway = new GameObject();
        if (spawnAlongZAxis) {
            emptyDist = Mathf.Abs(currRoom.prefab.transform.position.z - toReturn.transform.position.z) - branchingCenterToEntrance - currCenterToEntrance;
            if (currRoom.prefab.transform.position.z > toReturn.transform.position.z) {
                //Spawning in negative z direction
                sendingHallway.transform.position = new Vector3(currRoom.prefab.transform.position.x, currRoom.prefab.transform.position.y, currRoom.prefab.transform.position.z - emptyDist/2 - currCenterToEntrance);
            } else {
                sendingHallway.transform.position = new Vector3(currRoom.prefab.transform.position.x, currRoom.prefab.transform.position.y, currRoom.prefab.transform.position.z + emptyDist/2 + currCenterToEntrance);
            }
        } else {
            emptyDist = Mathf.Abs(currRoom.prefab.transform.position.x - toReturn.transform.position.x) - branchingCenterToEntrance - currCenterToEntrance;
            sendingHallway.transform.eulerAngles += new Vector3(0,90,0);
            if (currRoom.prefab.transform.position.x > toReturn.transform.position.x) {
                //Spawning in negative x direction
                sendingHallway.transform.position = new Vector3(currRoom.prefab.transform.position.x - emptyDist/2 - currCenterToEntrance, currRoom.prefab.transform.position.y, currRoom.prefab.transform.position.z);
            } else {
                sendingHallway.transform.position = new Vector3(currRoom.prefab.transform.position.x  + emptyDist/2 + currCenterToEntrance, currRoom.prefab.transform.position.y, currRoom.prefab.transform.position.z);
            }
        }
        if (!killProcess) {
            GameObject sendingHallwayObject = Instantiate(hallway, sendingHallway.transform.position, sendingHallway.transform.rotation);
            sendingHallwayObject.name = "sendingHallway " + hallwayNumber;
            hallwayNumber++;
            sendingHallwayObject.transform.localScale += new Vector3(0,0,emptyDist);
            hallways.Add(sendingHallwayObject);
        }
        Destroy(sendingHallway);
        return toReturn;
    }

    public Vector3 CheckOverlap(Vector3 pos, RoomData currRoom, RoomData branchingRoom, float dist, ref bool spawnAlongZAxis, ref GameObject newCurrEntrance, ref int newEntID, ref bool killProcess) {
        RoomPrefab roomRef = branchingRoom.prefab.GetComponent<RoomPrefab>();
        Vector3 toUse = new Vector3();
        toUse.x = Mathf.Abs(roomRef.Xdim / 2);
        toUse.y = Mathf.Abs(roomRef.Ydim / 2);
        toUse.z = Mathf.Abs(roomRef.Zdim / 2);
        Debug.Log(toUse);
        Collider[] collidingObjects = Physics.OverlapBox(pos, toUse, branchingRoom.prefab.transform.rotation, roomLayerMask);
        Debug.Log("Number of colliding objects: " + collidingObjects.Length);
        if (collidingObjects.Length != 0) {
            Debug.Log("*************************************");
            Vector3 newSpawnLocation;
            Vector3 currRoomCenter = currRoom.prefab.transform.position;
            Debug.Log("This room has " + currRoom.availableEntrances.Count + " entrances");
            for (int i = 0; i < currRoom.availableEntrances.Count; i++) {
                int newEnt = currRoom.availableEntrances[i];
                bool isZAxis;
                if (newEnt == 0) {
                    newSpawnLocation = new Vector3(currRoomCenter.x, currRoomCenter.y , currRoomCenter.z + dist);
                    isZAxis = true;
                } else if (newEnt == 1) {
                    newSpawnLocation = new Vector3(currRoomCenter.x - dist, currRoomCenter.y , currRoomCenter.z);
                    isZAxis = false;
                } else if (newEnt == 2) {
                    newSpawnLocation = new Vector3(currRoomCenter.x, currRoomCenter.y , currRoomCenter.z - dist);
                    isZAxis = true;
                } else {
                    newSpawnLocation = new Vector3(currRoomCenter.x + dist, currRoomCenter.y , currRoomCenter.z);
                    isZAxis = false;
                }
                //Instantiate(fromEntranceMarkerRef, newSpawnLocation, branchingRoom.prefab.transform.rotation);
                
                collidingObjects = Physics.OverlapBox(newSpawnLocation, toUse, branchingRoom.prefab.transform.rotation, roomLayerMask);
                if (collidingObjects.Length == 0) {
                    Debug.Log("Found new spawn location");
                    int branchingRoomIdx = currRoom.neighborRooms.FindIndex(a => a == branchingRoom);
                    int currRoomIdx = branchingRoom.neighborRooms.FindIndex(b => b == currRoom);
                    Debug.Log("Old entrance was: " + currRoom.entranceIDs[branchingRoomIdx] + " new entrance is " + newEnt);
                    currRoom.entranceIDs[branchingRoomIdx] = newEnt;
                    branchingRoom.entranceIDs[currRoomIdx] = (newEnt + 2) % 4;
                    spawnAlongZAxis = isZAxis;
                    newCurrEntrance = currRoom.prefab.GetComponent<RoomPrefab>().potentialEntrances[newEnt];
                    newEntID = newEnt;
                    return newSpawnLocation;
                }
                Debug.Log("This spawn location" + newEnt + " also has collisions");
            }
            Debug.Log("Couldn't find a new entrance to spawn the room from");
            //ResetDungeon();
            //killProcess = true;
        }
        
        return pos;
    }
    
}


