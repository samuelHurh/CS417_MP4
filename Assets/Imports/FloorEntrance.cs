using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorEntrance : Entrance
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public override void InitiateEntrance(Vector3 pos) {
        isUsed = false;
        wall = Instantiate(entrancePrefab, pos, this.transform.rotation);
        wall.transform.eulerAngles += new Vector3(90,0,0);
    }
}
