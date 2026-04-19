using UnityEngine;

public class WeaponCore : MonoBehaviour
{
    [Header("Ammo")]
    [SerializeField] private bool useDetachableMagazine = true;
    [SerializeField] private WeaponMagazine startingMagazine;
    [SerializeField] private int internalReserveRounds;
    [SerializeField] private bool startWithRoundChambered = true;

    [Header("Action")]
    [SerializeField] private bool lockOpenOnEmptyMagazine = true;

    public WeaponMagazine InsertedMagazine { get; private set; }
    public int InternalReserveRounds => internalReserveRounds;
    public bool HasChamberedRound { get; private set; }
    public bool IsActionLockedOpen { get; private set; }
    public bool CanFire => HasChamberedRound && !IsActionLockedOpen;
    public int AvailableFeedRounds => useDetachableMagazine
        ? (InsertedMagazine != null ? InsertedMagazine.CurrentRounds : 0)
        : internalReserveRounds;

    private void Awake()
    {
        InsertedMagazine = startingMagazine;
        HasChamberedRound = startWithRoundChambered;

        if (ShouldLockOpenOnRelease())
        {
            IsActionLockedOpen = true;
        }
    }

    public void InsertMagazine(WeaponMagazine magazine)
    {
        InsertedMagazine = magazine;
    }

    public WeaponMagazine EjectMagazine()
    {
        WeaponMagazine previousMagazine = InsertedMagazine;
        InsertedMagazine = null;
        return previousMagazine;
    }

    public bool TryConsumeChamberedRound()
    {
        if (!CanFire)
        {
            return false;
        }

        HasChamberedRound = false;
        return true;
    }

    public bool EjectChamberedRound()
    {
        if (!HasChamberedRound)
        {
            return false;
        }

        HasChamberedRound = false;
        return true;
    }

    public bool TryFeedRoundToChamber()
    {
        if (HasChamberedRound)
        {
            return true;
        }

        if (!TryConsumeFeedRound())
        {
            return false;
        }

        HasChamberedRound = true;
        return true;
    }

    public bool ShouldLockOpenOnRelease()
    {
        return lockOpenOnEmptyMagazine &&
               useDetachableMagazine &&
               InsertedMagazine != null &&
               InsertedMagazine.CurrentRounds <= 0 &&
               !HasChamberedRound;
    }

    public void SetActionLockedOpen(bool isLockedOpen)
    {
        IsActionLockedOpen = isLockedOpen;
    }

    public void SetInternalReserveRounds(int rounds)
    {
        internalReserveRounds = Mathf.Max(0, rounds);
    }

    public void AddInternalReserveRounds(int rounds)
    {
        internalReserveRounds = Mathf.Max(0, internalReserveRounds + rounds);
    }

    private bool TryConsumeFeedRound()
    {
        if (useDetachableMagazine)
        {
            return InsertedMagazine != null && InsertedMagazine.TryConsumeRound();
        }

        if (internalReserveRounds <= 0)
        {
            return false;
        }

        internalReserveRounds--;
        return true;
    }
}
