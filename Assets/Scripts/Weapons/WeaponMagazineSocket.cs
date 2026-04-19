using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class WeaponMagazineSocket : MonoBehaviour
{
    [SerializeField] private WeaponCore weaponCore;
    [SerializeField] private XRSocketInteractor socketInteractor;

    private void Reset()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
    }

    private void OnEnable()
    {
        if (socketInteractor == null)
        {
            socketInteractor = GetComponent<XRSocketInteractor>();
        }

        if (socketInteractor == null)
        {
            return;
        }

        socketInteractor.selectEntered.AddListener(OnMagazineInserted);
        socketInteractor.selectExited.AddListener(OnMagazineRemoved);
    }

    private void OnDisable()
    {
        if (socketInteractor == null)
        {
            return;
        }

        socketInteractor.selectEntered.RemoveListener(OnMagazineInserted);
        socketInteractor.selectExited.RemoveListener(OnMagazineRemoved);
    }

    private void OnMagazineInserted(SelectEnterEventArgs args)
    {
        WeaponMagazine magazine = GetMagazine(args.interactableObject.transform);
        if (weaponCore != null && magazine != null)
        {
            weaponCore.InsertMagazine(magazine);
        }
    }

    private void OnMagazineRemoved(SelectExitEventArgs args)
    {
        WeaponMagazine magazine = GetMagazine(args.interactableObject.transform);
        if (weaponCore != null && weaponCore.InsertedMagazine == magazine)
        {
            weaponCore.EjectMagazine();
        }
    }

    private WeaponMagazine GetMagazine(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        if (target.TryGetComponent(out WeaponMagazine directMagazine))
        {
            return directMagazine;
        }

        return target.GetComponentInParent<WeaponMagazine>();
    }
}
