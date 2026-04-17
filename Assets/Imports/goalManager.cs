using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GoalManager : MonoBehaviour
{
    
    public GameObject targetPrefab;
    public List<GameObject> activeTargets;
    public GameObject bossEntranceLockPrefab;
    public GameObject bossEntranceLock;

    public GameObject[] keyArray;
    public int remainingTargets;

    public void InstantiateTargets(List<RoomData> rooms) {
        int currKeyIdx = 0;
        for (int i = 0; i < rooms.Count; i++) {

            if (rooms[i].roomType == 6) {
                //For now, the boss entrance only has one lateral neighbor
                int entranceToBlock = rooms[i].entranceIDs[0];
                GameObject entRef = rooms[i].prefab.GetComponent<RoomPrefab>().potentialEntrances[entranceToBlock];
                bossEntranceLock = Instantiate(bossEntranceLockPrefab, entRef.transform.position, entRef.transform.rotation);
                if (entranceToBlock % 2 != 0) {
                    bossEntranceLock.transform.eulerAngles += new Vector3(0, 90, 0);
                }

            }
            if (rooms[i].roomType == 5) {
                //GameObject newTarget = Instantiate(targetPrefab, rooms[i].prefab.transform.position, rooms[i].prefab.transform.rotation);
                Debug.Log("Here " + currKeyIdx);
                GameObject currKey = keyArray[currKeyIdx];
                currKey.SetActive(true);
                currKey.transform.position = rooms[i].prefab.transform.position;
                currKey.transform.position += new Vector3(0, 4, 0);
                currKey.transform.eulerAngles += new Vector3(90, 0, 0);
                remainingTargets++;
                currKeyIdx++;
            }
        }
    }
    public void DecrementTargets(int keyHit) {
        Debug.Log("Hit");
        remainingTargets--;
        keyArray[keyHit].SetActive(false);
        if (remainingTargets == 0) {
            Destroy(bossEntranceLock);
        }
    }
}

