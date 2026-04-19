using UnityEngine;

public class WeaponMagazine : MonoBehaviour
{
    [SerializeField] private int capacity = 17;
    [SerializeField] private int startingRounds = 17;
    [SerializeField] private bool fillOnAwake = true;

    public int Capacity => Mathf.Max(0, capacity);
    public int CurrentRounds { get; private set; }
    public bool HasRounds => CurrentRounds > 0;

    private void Awake()
    {
        CurrentRounds = fillOnAwake ? Capacity : Mathf.Clamp(startingRounds, 0, Capacity);
    }

    public void SetRounds(int rounds)
    {
        CurrentRounds = Mathf.Clamp(rounds, 0, Capacity);
    }

    public void Refill()
    {
        CurrentRounds = Capacity;
    }

    public bool TryConsumeRound()
    {
        if (CurrentRounds <= 0)
        {
            return false;
        }

        CurrentRounds--;
        return true;
    }
}
