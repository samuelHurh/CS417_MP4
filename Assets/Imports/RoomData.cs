using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomData : ScriptableObject
{

    
    //This class is the data representation of the room as it will
    //be inserted and interfaced with in the tree data structure
    //Note that this class is purely code and information as it doesn't
    //inherit from monobehaviour.
    
    //A definable variable detailing what roomPrefab to use;
    public GameObject prefab;
    
    public List<RoomData> neighborRooms;
    //Stores the idx of potentialEntrances that should be used to connect
    //to the corresponding idx of a neighbor in neighborRooms.
    public List<int> entranceIDs;
    //A list that stores the entrances not currently in use.
    public List<int> availableEntrances;
     //tells the manager if the room was instantiated already.
    public bool isSpawned;
    //This variable is used with the bfs traversal to determine if the room was visited in the visited list
    //A dungeonID of 0 will indicate that the room hasn't been spawned in yet.
    public int dungeonID;

    //Let typing be defined by int:
    // 0: Start Room
    // 1: Boss Room
    // 2+: normal rooms
    //This number corresponds with the room's position in the prefab array
    public int roomType;

    //How much the room has been rotated from the original orientation (base room's orientation)
    //0: same orientation
    //1: +90
    //2: +180
    //3: +270

    //A boolean that determines whether the  entrances have been filled in for this particular room
    public bool entrancesInitiated = false;

    public int numEntrances;

    public RoomData(int room_type, int dungeon_ID) {
        roomType = room_type;
        dungeonID = dungeon_ID;
        neighborRooms = new List<RoomData>();
        entranceIDs = new List<int>();
    }

    public void addNeighbor(RoomData neighbor, int entID, bool isSender) {
        //Assert that the number of entrances and neighbors are equal
        // if (neighborRooms.Count != entranceIDs.Count) {
        //     Debug.Log("Size mismatch between #neighbors: " + neighborRooms.Count + " and #entranceIDs: " + entranceIDs.Count);
        //     return;
        // }
        // //Assert that the provided entID does not correspond to another neighboring room already
        // if (entranceIDs.Contains(entID)) {
        //     Debug.Log("Tried to assign a new room to an entrance that already has an assigned room");
        //     return;
        // }
        neighborRooms.Add(neighbor);
        entranceIDs.Add(entID);
        availableEntrances.Remove(entID);
        if (isSender) {
            int neighborEntID = (entID + 2) % 4;
            neighbor.addNeighbor(this, neighborEntID, false);
        }
        
        
    }

    public void EntranceChange(RoomData neighbor, int newEntID, bool isSender) {
        availableEntrances.Remove(newEntID);
        if (isSender) {
            int neighborEntID = (newEntID + 2) % 4;
            neighbor.addNeighbor(this, neighborEntID, false);
        }
    }


    public void Constructor(int room_type, int dungeon_ID, int num_ent) {
        //Adding this function as an additional setup step
        //As CreateInstance<>() does not account for it.
        roomType = room_type;
        dungeonID = dungeon_ID;
        neighborRooms = new List<RoomData>();
        entranceIDs = new List<int>();
        availableEntrances = new List<int>();
        isSpawned = false;


        for (int i = 0; i < num_ent; i++) {
            availableEntrances.Add(i);
        }

    }
    public void SetEntrances() {
        RoomPrefab roomPrefab = GetRoomPrefab();
        if (roomPrefab == null || roomPrefab.potentialEntrances == null) {
            return;
        }

        for (int i = 0; i < roomPrefab.potentialEntrances.Length; i++ ) {
            if (!entranceIDs.Contains(i)) {
                GameObject entranceObject = roomPrefab.potentialEntrances[i];
                Entrance entrance = GetEntrance(entranceObject);
                if (entrance != null) {
                    entrance.InitiateEntrance(entranceObject.transform.position);
                }
            }
        }
    }

    public void ResetEntrances() {
        RoomPrefab roomPrefab = GetRoomPrefab();
        if (roomPrefab == null || roomPrefab.potentialEntrances == null) {
            return;
        }

        //Destroy existing
        for (int i = 0; i < roomPrefab.potentialEntrances.Length; i++ ) {
            Entrance toDelete = GetEntrance(roomPrefab.potentialEntrances[i]);
            if (toDelete != null) {
                toDelete.DeinitiateEntrance();
            }
        }
        //Debug.Log("EntranceIDs: " + entranceIDs);
        for (int j = 0; j < roomPrefab.potentialEntrances.Length; j++ ) {
            if (!entranceIDs.Contains(j)) {
                GameObject entranceObject = roomPrefab.potentialEntrances[j];
                Entrance entrance = GetEntrance(entranceObject);
                if (entrance != null) {
                    entrance.InitiateEntrance(entranceObject.transform.position);
                }
            }
        }


    }

    public void Delete() {
        if (prefab != null) {
            RoomPrefab roomPrefab = GetRoomPrefab();
            if (roomPrefab != null && roomPrefab.potentialEntrances != null) {
                for (int i = 0; i < roomPrefab.potentialEntrances.Length; i++ ) {
                    Entrance toDelete = GetEntrance(roomPrefab.potentialEntrances[i]);
                    if (toDelete != null) {
                        toDelete.DeinitiateEntrance();
                    }
                }
            }
        }
        Destroy(prefab);
        neighborRooms.Clear();
        entranceIDs.Clear();
        availableEntrances.Clear();
        isSpawned = false;
        dungeonID = -1;
        roomType = -1;
    }

    private RoomPrefab GetRoomPrefab() {
        if (prefab == null) {
            return null;
        }

        RoomPrefab roomPrefab = prefab.GetComponent<RoomPrefab>();
        if (roomPrefab == null) {
            roomPrefab = prefab.GetComponentInChildren<RoomPrefab>();
        }

        return roomPrefab;
    }

    private Entrance GetEntrance(GameObject entranceObject) {
        if (entranceObject == null) {
            return null;
        }

        Entrance entrance = entranceObject.GetComponent<Entrance>();
        if (entrance == null) {
            entrance = entranceObject.GetComponentInChildren<Entrance>();
        }

        return entrance;
    }
}
