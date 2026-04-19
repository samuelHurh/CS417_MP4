using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Magazine : MonoBehaviour
{
    private int maxCap = 17;
    private int currCap;


    // public Magazine() {
    //     maxCap = 17;
    //     currCap = maxCap;
    // }

    void Start() {
        
        currCap = 17;
        Debug.Log("starting capacity: " + currCap);
    }
    public void refill() {
        // Debug.Log(maxCap);
        // currCap = maxCap;
        // Debug.Log("Refilled magazine");
        Debug.Log("CurrCap is " + currCap);
        Vector3 force = new Vector3 (500, 0, 0);
    }

    public void decrement() {
        if (currCap > 0 ) {
            currCap--;
            Debug.Log(currCap);
        } else {
            Debug.Log("empty mag");
        }
    }

    public int getCurrCap() {
        return currCap;
    }

    public void setCurrCap(int cap) {
        currCap = cap;
    }
}
