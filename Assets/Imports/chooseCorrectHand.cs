using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


public class chooseCorrectHand : MonoBehaviour
{
    public XRGrabInteractable grabInteractable;
    public Transform leftHandAttach;
    public Transform rightHandAttach;

    public GameObject leftHandInteractor;
    public GameObject rightHandInteractor;
    private bool isGrabbed = false;

    void Start() {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    public void SwapHands() {
        Transform interactorTransform = GetCurrentInteractorTransform();
        if (interactorTransform == null)
        {
            return;
        }

        if (MatchesInteractor(interactorTransform, leftHandInteractor)) {
            grabInteractable.attachTransform = leftHandAttach;
            //Debug.Log("LEFT");
        }
        if (MatchesInteractor(interactorTransform, rightHandInteractor)) {
            grabInteractable.attachTransform = rightHandAttach;
            //Debug.Log("RIGHT");
        }
    }

    private Transform GetCurrentInteractorTransform()
    {
        if (grabInteractable == null)
        {
            return null;
        }

        if (grabInteractable.firstInteractorSelecting != null)
        {
            return grabInteractable.firstInteractorSelecting.transform;
        }

        if (grabInteractable.interactorsHovering.Count > 0)
        {
            return grabInteractable.interactorsHovering[0].transform;
        }

        return null;
    }

    private bool MatchesInteractor(Transform interactorTransform, GameObject handInteractor)
    {
        if (interactorTransform == null || handInteractor == null)
        {
            return false;
        }

        return interactorTransform.gameObject == handInteractor ||
               interactorTransform.root.gameObject == handInteractor ||
               interactorTransform.name == handInteractor.name;
    }
}
