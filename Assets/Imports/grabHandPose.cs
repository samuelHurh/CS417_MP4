using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class grabHandPose : MonoBehaviour
{
    
    public handData rightHandPose;
    private Vector3 startingHandPosition;
    private Vector3 finalHandPosition;
    private Quaternion startingHandRotation;
    private Quaternion finalHandRotation;
    private Quaternion[] startingFingerRotations;
    private Quaternion[] finalFingerRotations;
    // Start is called before the first frame update
    void Start()
    {
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        // grabInteractable.selectEntered.AddListener(SetupPose);
        // grabInteractable.selectExited.AddListener(UnsetPose);
        rightHandPose.gameObject.SetActive(false);
    }

    public void SetupPose(BaseInteractionEventArgs arg) {
        if (arg.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor) {
            handData handData = arg.interactorObject.transform.GetComponentInChildren<handData>();
            handData.animator.enabled = false;
            
            

            SetHandDataValues(handData, rightHandPose);
            setHandData(handData, finalHandPosition, finalHandRotation, finalFingerRotations);
        }
    }
    public void UnsetPose(BaseInteractionEventArgs arg) {
        handData handData = arg.interactorObject.transform.GetComponentInChildren<handData>();
        Debug.Log("HERE1");
        handData.animator.enabled = true;
        Debug.Log("HERE2");
        //setHandData(handData, startingHandPosition, startingHandRotation, startingFingerRotations);
    }

    public void SetHandDataValues(handData h1, handData h2) {
        startingHandPosition = h1.root.localPosition;
        finalHandPosition = h1.root.localPosition;

        startingHandRotation = h1.root.localRotation;
        finalHandRotation = h1.root.localRotation;


        startingFingerRotations = new Quaternion[h1.fingerBones.Length];
        finalFingerRotations = new Quaternion[h1.fingerBones.Length];

        for (int i = 0; i < h1.fingerBones.Length; i++) {
            startingFingerRotations[i] = h1.fingerBones[i].localRotation;
            finalFingerRotations[i] = h2.fingerBones[i].localRotation;
        }
    }

    public void setHandData(handData h, Vector3 newPosition, Quaternion newRotation, Quaternion[] newBonesRotation) {
        h.root.localPosition = newPosition;
        h.root.localRotation = newRotation;

        for (int i = 0; i < newBonesRotation.Length; i++) {
            h.fingerBones[i].localRotation = newBonesRotation[i];
        }
    }
}
