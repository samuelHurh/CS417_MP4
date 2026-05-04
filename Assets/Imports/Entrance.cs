using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entrance : MonoBehaviour
{
    //Defines the x/z axis orientation of the entrance relative to the center of the room
    //0: +z
    //1: -x
    //2: -z
    //3: +x
    //This order is the counterclockwise order from the +z axis
    public int dir;

    public bool isUsed;
    public GameObject entrancePrefab;
    public GameObject doorPrefab;
    public GameObject wall;
    public GameObject door;

    //This function should be called upon room creation
    public virtual void InitiateEntrance(Vector3 pos) {
        if (entrancePrefab == null) {
            Debug.LogWarning("Entrance is missing an entrancePrefab wall/blocker reference.", this);
            return;
        }

        isUsed = false;
        Quaternion spawnRotation = this.transform.rotation;
        if (dir % 2 != 0) {
            spawnRotation.eulerAngles += new Vector3(0,90,0);
        }
        wall = Instantiate(entrancePrefab, pos, spawnRotation);
        //Debug.Log("Entrance instantiated at: " + this.transform.position);
    }

    public void SealDoorway()
    {
        if (doorPrefab == null) {
            Debug.LogWarning("Entrance is missing a doorPrefab seal reference.", this);
            return;
        }

        if (door != null) {
            return;
        }

        //Doorway being sealed
        Quaternion spawnRotation = this.transform.rotation;
        if (dir % 2 != 0) {
            spawnRotation.eulerAngles += new Vector3(0,90,0);
        }
        door = Instantiate(doorPrefab, this.transform.position, spawnRotation);
    }
    
    public void UnsealDoorway()
    {
        if (door != null) {
            Destroy(door);
            door = null;
        }
    }
    // public void EnableEntrance() {
    //     Debug.Log("EnableEntrance called");
    //     isUsed = true;
    //     if (wall != null) {
    //         Destroy(wall);
    //     } else {
    //         Debug.Log("An entrance wall was never here when EnableEntrance was called");
    //     }
    // }

    public void DeinitiateEntrance() {
        isUsed = true;
        if (wall != null) {
            Destroy(wall);
            wall = null;
        }
    }
}
