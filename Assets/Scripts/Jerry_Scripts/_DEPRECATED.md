# Deprecated Jerry Scripts (post-Sam-integration)

> **Status as of 2026-04-30 (`final-integration` branch).**
> This file lists scripts that are **superseded by Sam's BNG/VRIF integration** but
> remain on disk because they are still referenced by surviving code (`HUDSystem`,
> `PlayerStateManager`). They will be deleted in Phase 1 surgery once those callers
> are decoupled.
>
> **Do NOT add new code that depends on anything in this list.** New work should
> integrate with BNG (`Damageable`, `RaycastWeapon`, `Grabbable`) or with the
> surviving Jerry interfaces (`IHittable`, `DamageEvent`, `IPlayerStateReader`).

## Already deleted on this branch (2026-04-30)

| Path | Reason |
|---|---|
| `Tests/EditMode/MagDropPoolTests.cs` | BNG handles ammo pool |
| `Tests/EditMode/MagSpawnPoolTests.cs` | BNG handles ammo pool |
| `Tests/EditMode/MagWellSocketTests.cs` | BNG handles reload |
| `Tests/EditMode/WeaponInstanceReloadTests.cs` | BNG handles reload |
| `Tests/EditMode/MuzzleFlashPoolTests.cs` | BNG `RaycastWeapon` owns muzzle FX |
| `Tests/EditMode/ProjectileSystemTests.cs` | BNG `RaycastWeapon` owns hitscan |
| `Tests/EditMode/WeaponGeneratorTests.cs` | Replaced by Sam's `GeneratedWeaponManager` |
| `Tests/EditMode/PlayerHitboxTests.cs` | `PlayerHitbox` being replaced by `PlayerHitboxReceiver` |
| `Jerry_Scripts/TestDummyScript.cs` | Sam's enemies use BNG `Damageable`; TestDummy obsolete |

## Pending deletion (blocked by callers)

| Path | Reason | Blocked by |
|---|---|---|
| `Feature/WeaponHandling/WeaponInstance.cs` | Replaced by BNG `RaycastWeapon` + `Grabbable` | `HUDSystem` ammo subscription (Phase 4) |
| `Feature/WeaponHandling/WeaponData.cs` | Replaced by Sam's `GeneratedWeaponManager` | `HUDSystem.RefreshWeaponPanel`, `WeaponRarity` enum used by `HUDSystem.GetRarityColor` and `DamageResolver` API (Phase 1 — extract enum) |
| `Feature/WeaponHandling/WeaponInstanceState.cs` | Replaced by BNG state | `HUDSystem.SyncToCurrentState` (Phase 4) |
| `Feature/WeaponHandling/MagDropPool.cs` | BNG handles ammo pool | `WeaponInstance` |
| `Feature/WeaponHandling/MagSpawnPool.cs` | BNG handles ammo pool | `WeaponInstance`, `MagWellSocket` |
| `Feature/WeaponHandling/MagWellSocket.cs` | BNG handles reload | `WeaponInstance` |
| `Feature/WeaponHandling/MuzzleFlashPool.cs` | BNG `RaycastWeapon` owns muzzle FX | `WeaponInstance` |
| `Feature/WeaponGeneration/WeaponGenerator.cs` | Replaced by `GeneratedWeaponManager` | `WeaponSpawner` |
| `Feature/WeaponGeneration/WeaponGenerationConfig.cs` | Replaced by `GeneratedWeaponManager` | `WeaponGenerator` |
| `Feature/WeaponGeneration/WeaponSpawner.cs` | Replaced by `GeneratedWeaponManager` start-of-scene init | nothing (safe to delete next pass) |
| `Feature/WeaponGeneration/BarrelGuardData.cs` | Replaced by `GeneratedWeaponManager.Frames`/`Slides` lists | `WeaponGenerator`, `WeaponGenerationConfig` |
| `Foundation/PlayerRig/PlayerRig.cs` | Replaced by BNG XR Rig Advanced | `PlayerStateManager._playerRig`, `HUDSystem` (Phase 1) |
| `Foundation/PlayerRig/PlayerHitbox.cs` | Replaced by new `PlayerHitboxReceiver` (Phase 1) | `PlayerRig.SetupDamageCollider` |
| `Foundation/PlayerRig/PlayerRigConfig.cs` | Replaced by BNG settings | `PlayerRig` |
| `Foundation/Interfaces/IRigStateProvider.cs` | Replaced by direct PSM event flow | `PlayerStateManager`, `WeaponInstance` (Phase 1) |
| `Foundation/Interfaces/IRigControllerProvider.cs` | Replaced by `HUDSystem._leftControllerOverride` Inspector slot (Phase 4) | `HUDSystem`, `AudioFeedbackService` haptic dispatch |
| `Foundation/Interfaces/IMountPointProvider.cs` | Replaced by BNG holster points | `WeaponSpawner`, `WeaponInstance` |
| `Foundation/Interfaces/IMagInsertReceiver.cs` | Replaced by BNG reload | `WeaponInstance`, `MagWellSocket` |
| `Core/Projectile/ProjectileSystem.cs` | Replaced by BNG `RaycastWeapon` hitscan | `WeaponInstance` |
| `Core/Projectile/IProjectileService.cs` | Replaced by BNG | `ProjectileSystem`, `WeaponInstance` |

## Surviving code (DO use these)

| Path | Notes |
|---|---|
| `Foundation/Damage/DamageEvent.cs` | Used by Sam's `PlayerDamageHelpers.TryDamagePlayer` |
| `Foundation/Damage/IHittable.cs` | Used by Sam's enemy damage flow + Phase-1 `PlayerHitboxReceiver` |
| `Foundation/Damage/DamageResolver.cs` | Damage clamping + rarity multipliers |
| `Foundation/Damage/RarityMultiplierTable.cs` | SO with damage caps + rarity scalars |
| `Foundation/Audio/*` | Audio + haptic dispatch (still useful — Phase 4 keeps it) |
| `Core/PlayerState/*` | Player HP / currency / state (Phase 1 adds `ApplyDamage`) |
| `Presentation/HUD/HUDSystem.cs` | Will be refactored in Phase 4 to use BNG controller + `GeneratedWeaponManager` |
| `Presentation/HUD/HUDConfig.cs` | Same — keep with new fields added |

## Phase plan

See `production/sprints/sprint-final.md` for the four implementation phases:
1. Player Health Wiring ✅ **CODE COMPLETE 2026-04-30** — `Foundation/Player/PlayerHitboxReceiver.cs`, PSM `ApplyDamage`, PSM decoupled from `PlayerRig`
2. Enemy Death Hook (gem drop) ✅ **CODE COMPLETE 2026-04-30** — `Feature/Collectables/EnemyDeathRewards.cs`
3. Currency Collectable System ✅ **CODE COMPLETE 2026-04-30** — `Feature/Collectables/CurrencyGem.cs`
4. HUD Adaptation (BNG rig + GeneratedWeaponManager) ✅ **CODE COMPLETE 2026-04-30** — `Presentation/HUD/BNGWeaponBridge.cs`, `HUDSystem` decoupled from `PlayerRig` + `WeaponInstance`, `Foundation/Damage/WeaponRarity.cs` extracted

After Phase 4, the "Pending deletion" entries above are technically deletable, but per Jerry's 2026-04-30 instruction we are **keeping them on disk** as deprecated reference. They compile fine because the surviving consumers (`AudioFeedbackService` still references `IRigControllerProvider` via `FindAnyObjectByType<PlayerRig>()`; will silently fail on the BNG rig and skip haptics) — no compile errors but no runtime effect either.

## 2026-04-30 cleanup additions

| Path | Status | Notes |
|---|---|---|
| `Tests/EditMode/HUDSystemTests.cs` | **STALE — 3 stub tests** | Heavy refactor needed for the new `BNGWeaponBridge` + `GeneratedWeaponManager` API. Currently kept as a 3-test stub on `HUDConfig` defaults so the EditMode runner finds something. Full rewrite is post-alpha. |
| `Tests/EditMode/PlayerStateManagerTests.cs` | **PASSING** | Updated `SimulateDamage` helper to call new public `ApplyDamage` API. All 21 tests still relevant. |
