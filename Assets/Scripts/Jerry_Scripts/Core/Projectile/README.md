# Core/Projectile — Editor Setup Guide

S1-005 | Owner: Jerry Chen | Depends on: Foundation/Damage (S1-004)

**Scope: hitscan only.** Enemy projectiles are Sam's Enemy System scope — not implemented in this folder.

---

## 1. Required Physics Layers

Add **one** layer in **Edit > Project Settings > Tags & Layers** (the `PlayerHitbox` layer was already added in S1-001):

| Slot | Layer Name | Purpose |
|------|------------|---------|
| 8 | `EnemyHitbox` | Trigger volumes on enemy characters. Hitscan rays target this layer only. |

The exact slot number is a recommendation — what matters is that the name matches exactly. `LayerMask.NameToLayer("EnemyHitbox")` resolves by name at runtime.

---

## 2. Physics Collision Matrix

No new collision-matrix entries needed for hitscan (`Physics.Raycast` is layer-mask filtered, not matrix-filtered).

Sam will configure `EnemyProjectile` collision rules when his enemy system lands.

---

## 3. Scene Wiring

Place one `_Systems` GameObject in the scene root. Add these two components:

| Component | Inspector fields to set |
|-----------|------------------------|
| `ProjectileSystem` | `Enemy Hitbox Mask` → check the `EnemyHitbox` layer bit. Leave `Damage Resolver` empty (auto-resolved in Awake from scene). |
| `DamageResolver` | Wire `Rarity Multiplier Table` SO (set up in S1-004). |

`ProjectileSystem.Awake()` auto-resolves `DamageResolver` via `FindAnyObjectByType`. Inspector wiring is optional but preferred for deterministic startup.

---

## 4. WeaponInstance Wiring

`WeaponInstance.Awake()` resolves `IProjectileService` via `FindAnyObjectByType<ProjectileSystem>()`. No inspector wiring needed on the weapon prefab. Ensure `ProjectileSystem` is present in the scene before a `WeaponInstance` enters play.

---

## 5. Validation Checklist (Run before first Play)

- [ ] `EnemyHitbox` layer exists in Project Settings
- [ ] `_Systems` GameObject in scene has `ProjectileSystem` + `DamageResolver`
- [ ] `ProjectileSystem._enemyHitboxMask` shows `EnemyHitbox` layer checked
- [ ] `DamageResolver._rarityTable` has the Rarity Multiplier Table SO assigned
- [ ] Console shows `[ProjectileSystem] Hitscan hit '<name>'` when shooting an object on the `EnemyHitbox` layer
- [ ] No warnings from `[ProjectileSystem]` at startup

---

## 6. Testing Without a Headset

Until Sam's enemies land, create a quick test dummy to verify hitscan:

1. Add a Cube to Jerry_Scene
2. Set its layer to `EnemyHitbox`
3. Create a C# script `TestDummyHittable.cs` that implements `IHittable` (any namespace works):
   ```csharp
   using JerryScripts.Foundation.Damage;
   using UnityEngine;

   public class TestDummyHittable : MonoBehaviour, IHittable
   {
       public void TakeDamage(in DamageEvent dmg)
       {
           Debug.Log($"[TestDummy] Took {dmg.FinalDamage} dmg from {dmg.SourceId}");
       }
   }
   ```
4. Add the script to the cube
5. In PIE, pull the pistol trigger aimed at the cube — console should log both `[ProjectileSystem] Hitscan hit` and `[TestDummy] Took X dmg`

This is throwaway scaffolding — delete once Sam's enemies exist.
