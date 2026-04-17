using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomPrefab : MonoBehaviour
{
    //This class pertains to the room as a physical prefab
    //In world space.
    
    //A definable variable detailing what roomPrefab to use;
    public GameObject physicalRoom;
    //Define bounding dimensions of room prefab to allocate sufficient
    //worldspace and prevent intersecting rooms in the procedural generation
    //This is the bounding dimension so it is the max dimension of the room in any axis direction
    public float Xdim;
    public float Ydim;
    public float Zdim;

    
    //If the room is a starting room, then this room will have a reference to the XR origin
    //in order to spawn the player in that room
    public GameObject XROriginRef;
    //A variable that holds the center of the room
    public Transform roomCenter;
    //Defines the max number of neighbors a particular room can have
    public int maxNeighbors;
    //Depending on the room, define the entrances/exits for the room
    public GameObject[] potentialEntrances;
    //Stores a list of connecting rooms
    public List<int> neighborRoomIDs;
    //Stores the idx of potentialEntrances that should be used to connect
    //to the corresponding idx of a neighbor in neighborRooms.
    public List<int> entranceIDs;
     //tells the manager if the room was instantiated already.
    //public bool isSpawned;
    //This variable is used with the bfs traversal to determine if the room was visited in the visited list
    //A dungeonID of 0 will indicate that the room hasn't been spawned in yet.
    public int dungeonID;

    public Transform specialEntrance;
    // Start is called before the first frame update
    void Start()
    {
        
    }


    public int getMaxNeighbors() {
        return maxNeighbors;
    }

    public void setDimensions() {
        //Since my code does not choose/instantiate the prefab upon creating the gameObject housing the Room object type,
        //I should call this function to set the dimensions of the room after instantiating the actual room prefab
        Xdim = physicalRoom.GetComponent<Renderer>().bounds.size.x;
        Ydim = physicalRoom.GetComponent<Renderer>().bounds.size.y;
        Zdim = physicalRoom.GetComponent<Renderer>().bounds.size.z;
    }

    

    
}


