using UnityEngine;

public class DoubleDamageAOEZone : AOEZoneBase
{
    [Header("Double Damage")]
    [SerializeField] private float damageMultiplier = 2f;
    [SerializeField] private LayerMask enemyLayerMask;

    protected override void OnAOEActivated()
    {
        Debug.Log("Double damage AOE activated");
    }

    protected override void OnAOEExpired()
    {
        Debug.Log("Double damage AOE expired");
    }
}
