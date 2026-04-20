using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CustomXRGrabInteractable : XRGrabInteractable
{
    private List<Transform> usedCollidersTracker = new List<Transform>(); //A list that keeps track of which colliders are grabbed (size 2 max)
    //Note that the 0th index of this list and the colliders list (inherited) will always be the primary grip of the weapon from which it can be fired
    public bool primaryGripHeld; //Flag for other objects stating that the user is holding the firing/primary grip
    void Start() {
        this.attachTransform = null;
        this.secondaryAttachTransform = null;
        // for (int i = 0; i < usedCollidersTracker.Count; i++) {
        //     usedCollidersTracker[i] = false;
        // }
    }
    //On grab, search for closest collider, make that the grab transform, disable collider
    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        //Get interactor via:
        //args.interactorObject
        float minDist = 9000;
        Transform closerColliderTransform = null;
        Collider closerCollider = null;
        GameObject closestObject = null;
        Debug.Log("Curr colliders size: " + usedCollidersTracker.Count);
        for (int i = 0; i < colliders.Count; i++) {
            //OG plan was to check if hand was colliding with one of the colliders
            //I'm going to switch to checking distance between hands and colliders and using the one closer to the hand at "SelectEntered" time
            if (usedCollidersTracker.Contains(colliders[i].transform)) {
                Debug.Log("Found an already held object");
                continue;
            }
            float currDist = (args.interactorObject.transform.position - colliders[i].transform.position).magnitude;
            if (currDist < minDist)  {
                minDist = currDist;
                closerColliderTransform = colliders[i].transform;
                closestObject = colliders[i].gameObject;
                closerCollider = colliders[i];
            }
        }
        Debug.Log("Calling custom on select entered");
        Debug.Log("Using this attach transform: " + closestObject.name + " with this collider: " + closerCollider);
        
        //Grab weapon cases:
        //1. Nothing is grabbed -> Primary grip is grabbed -> assign this.attachTransfomr to primary grip
        //2. Nothing is grabbed -> Non-primary grip is grabbed -> assign this.attachTransform to Non-primary grip
        //3. Non-primary grip is grabbed (occupies this.attachTransform) -> Non-primary grip is grabbed -> assign this.secondaryTransform to newly-grabbed non-primary grip
        //4. Non-primary grip is grabbed(occupies this.attachTransform) -> primary grip is grabbed 
        //  -> reshuffle: assign this.attachTransform to primaryGrip and this.secondaryTransform to previously grabbed non-primary grip
        //5. Primary grip is grabbed(occupies this.attachTransform) -> non-primary grip (only option) is grabbed -> assign this.secondaryTransform to newly-grabbed non-primary grip
        if (this.attachTransform == null) {
            //Cases 1 or 2
            this.attachTransform = closerColliderTransform;
            usedCollidersTracker.Add(closerColliderTransform);
            if (closerCollider == colliders[0]) {
                primaryGripHeld = true;
            }
        } else {
            if(closerCollider.name == "Grip") {
                //Case 4 the reassignment case
                this.attachTransform = closerColliderTransform;
                Debug.Assert(usedCollidersTracker.Count == 1);
                this.secondaryAttachTransform = usedCollidersTracker[0]; //Note that I put primary grip at the front of the list
                usedCollidersTracker.Insert(0, closerColliderTransform);
                primaryGripHeld = true;
                Debug.Log("HERE: " + this.attachTransform.name + " secondary: " + this.secondaryAttachTransform.name);
            } else {
                //Case 3 or 5
                this.secondaryAttachTransform = closerColliderTransform;
                usedCollidersTracker.Add(closerColliderTransform);
            }
        }
        
        closerCollider.enabled = false;
        base.OnSelectEntering(args);
    }
    //On let go, search for closest collider, make that the grab transform, enable collider
    protected override void OnSelectExiting(SelectExitEventArgs args) {
        float minDist = 9000;
        Transform closerColliderTransform = null;
        Collider closerCollider = null;
        GameObject closestObject = null;
        for (int i = 0; i < colliders.Count; i++) {
            //OG plan was to check if hand was colliding with one of the colliders
            //I'm going to switch to checking distance between hands and colliders and using the one closer to the hand at "SelectEntered" time
            float currDist = (args.interactorObject.transform.position - colliders[i].transform.position).magnitude;
            if (currDist < minDist) {
                minDist = currDist;
                closerColliderTransform = colliders[i].transform;
                closestObject = colliders[i].gameObject;
                closerCollider = colliders[i];
            }
        }
        Debug.Log("Calling custom on select exited");
        Debug.Log("Using this attach transform: " + closestObject.name + " with collider: " + closerCollider );
        //Grip release cases:
        //1. Primary grip was held only -> Set this.attachTransform = null;
        //2. non-primary grip was held only -> set this.attachTransform = null;
        //3. Primary+non-primary held -> release primary -> set this.attachTransform = this.secondaryAttachTransform then this.seconaryTransform = null
        //4. Primary+non-primary held -> release non-primary -> set this.secondaryTransform = null
        //5. 2x non-primary held -> release any -> set this.attachTransform to the remaining grip and this.secondaryAttachTransform = null
        if (usedCollidersTracker.Count == 2) {
            //2-1 hand case 3,4,5
            if (closerColliderTransform == usedCollidersTracker[0]) {
                //case 3
                usedCollidersTracker.RemoveAt(0);
                this.attachTransform = usedCollidersTracker[0];
                this.secondaryAttachTransform = null;
                primaryGripHeld = false;
            } else {
                //cases 4 and 5
                usedCollidersTracker.RemoveAt(1);
                if (closerColliderTransform == this.attachTransform) {
                    //If we are getting rid of the current attachTransform, reassign the remaining hold to it
                    this.attachTransform = this.secondaryAttachTransform;
                }
                this.secondaryAttachTransform = null;
            }
        } else {
            //Cases 1 and 2
            primaryGripHeld = false;
            usedCollidersTracker.Clear();
            Debug.Assert(this.secondaryAttachTransform == null);
            this.attachTransform = null;
        }
        closerCollider.enabled = true;
        base.OnSelectExiting(args);
    }

    // // Start is called before the first frame update
    // void Start()
    // {

    // }

    // // Update is called once per frame
    // void Update()
    // {

    // }
}
