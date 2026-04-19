using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class setParent : MonoBehaviour
{
    // Start is called before the first frame update

    public GameObject myPrefab;
    public bool wasTriggered = false;
    public bool wasGrabbed = false;
    public Transform oldParent;
    public Transform newParent;
    void Start()

    {
        
    }
    public XRDirectInteractor this_interactor;
    // Update is called once per frame
    void Update()
    {
        if (this_interactor.isSelectActive && wasGrabbed == false) {
            //Debug.Log("Grabbing something");
            if (this_interactor.isActiveAndEnabled && wasTriggered == false) {
                //Debug.Log("trigger pulled");
                this.transform.SetParent(newParent);
                wasTriggered = true;
            }
            if (!this_interactor.isActiveAndEnabled) {
                wasTriggered = false;
            }
            wasGrabbed = true;
        } else {
            this.transform.SetParent(oldParent);
            wasGrabbed = false;
        }
    }
}
