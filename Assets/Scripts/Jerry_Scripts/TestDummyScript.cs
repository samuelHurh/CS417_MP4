using JerryScripts.Foundation.Damage;
using UnityEngine;

public class TestDummyHittable : MonoBehaviour, IHittable
{
    public void TakeDamage(in DamageEvent dmg)
    {
        Debug.Log($"[TestDummy] Took {dmg.FinalDamage} dmg from {dmg.SourceId}");
    }
}