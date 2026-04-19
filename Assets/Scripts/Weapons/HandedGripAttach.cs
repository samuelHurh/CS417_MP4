using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandedGripAttach : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable primaryGrip;
    [SerializeField] private Transform defaultAttach;
    [SerializeField] private Transform leftAttach;
    [SerializeField] private Transform rightAttach;

    public InteractorHandedness CurrentHandedness { get; private set; } = InteractorHandedness.None;

    private void Reset()
    {
        primaryGrip = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (primaryGrip == null)
        {
            primaryGrip = GetComponent<XRGrabInteractable>();
        }

        if (primaryGrip == null)
        {
            return;
        }

        primaryGrip.hoverEntered.AddListener(OnHoverEntered);
        primaryGrip.selectEntered.AddListener(OnSelectEntered);
        primaryGrip.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (primaryGrip == null)
        {
            return;
        }

        primaryGrip.hoverEntered.RemoveListener(OnHoverEntered);
        primaryGrip.selectEntered.RemoveListener(OnSelectEntered);
        primaryGrip.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        ApplyAttachForInteractor(args.interactorObject);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        ApplyAttachForInteractor(args.interactorObject);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (primaryGrip != null && !primaryGrip.isSelected)
        {
            CurrentHandedness = InteractorHandedness.None;

            if (defaultAttach != null)
            {
                primaryGrip.attachTransform = defaultAttach;
            }
        }
    }

    private void ApplyAttachForInteractor(IXRInteractor interactor)
    {
        if (primaryGrip == null || interactor == null)
        {
            return;
        }

        CurrentHandedness = interactor.handedness;

        Transform desiredAttach = CurrentHandedness switch
        {
            InteractorHandedness.Left when leftAttach != null => leftAttach,
            InteractorHandedness.Right when rightAttach != null => rightAttach,
            _ => defaultAttach,
        };

        if (desiredAttach != null)
        {
            primaryGrip.attachTransform = desiredAttach;
        }
    }
}
