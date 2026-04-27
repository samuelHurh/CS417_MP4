# WeaponHandling — Pistol Prefab Wiring Guide

This document is the step-by-step inspector wiring reference for the `Pistol.prefab`.
All fields are serialised; nothing is looked up at runtime with `Find()`.

---

## 1. Prefab Hierarchy

```
Pistol (root)
├── [Components] WeaponInstance, XRGrabInteractable, Rigidbody
├── GrabOffset          — empty Transform; set as XRGrabInteractable Attach Transform
├── Muzzle              — empty Transform at muzzle tip, forward = barrel direction
├── MagWell             — empty Transform at magazine well socket
└── PistolMesh          — MeshRenderer / SkinnedMeshRenderer
    └── (optional sub-meshes for slide, magazine, etc.)
```

---

## 2. XRGrabInteractable Settings

| Field | Value |
|---|---|
| Attach Transform | `GrabOffset` child Transform |
| Movement Type | `Instantaneous` (zero-lag, 1:1 hand tracking — GDD §Input Responsiveness) |
| Select Mode | `Single` (one hand only, MVP) |
| Throw On Detach | `false` (WeaponInstance routes drop via EvaluateDropDestination) |
| Retain Transform Parent | `false` |
| Interaction Layer Mask | `Weapon` layer (create if absent; set XR Rig ray interactors to same layer) |

> Do NOT assign `selectEntered` / `selectExited` listeners in the Inspector.
> `WeaponInstance.OnEnable()` registers them in code to avoid duplicate-listener bugs.

---

## 3. Rigidbody Settings

| Field | Value |
|---|---|
| Use Gravity | `true` |
| Is Kinematic | `true` (default — WeaponInstance drives kinematic toggles per state) |
| Interpolate | `Interpolate` (smoother visual at 72fps VR) |
| Collision Detection | `Continuous Dynamic` |

---

## 4. WeaponInstance Component

### Data
| Inspector Field | Assign |
|---|---|
| Data | `PistolData` ScriptableObject asset (create via Assets > Create > JerryScripts > Weapon Data) |

### Rig References
These three fields accept the `PlayerRig` MonoBehaviour on the XR Origin root.
`PlayerRig` implements all three interfaces (`IRigStateProvider`, `IRigControllerProvider`,
`IMountPointProvider`) so you drag the same object into all three slots.

| Inspector Field | Assign |
|---|---|
| Rig State Provider Source | `PlayerRig` MonoBehaviour |
| Rig Controller Provider Source | `PlayerRig` MonoBehaviour |
| Mount Point Provider Source | `PlayerRig` MonoBehaviour |

### Weapon Transforms
| Inspector Field | Assign |
|---|---|
| Muzzle Transform | `Muzzle` child Transform |
| Mag Well Transform | `MagWell` child Transform |

### Input Actions
Wire these to your `.inputactions` asset. The canonical action names for the XRI
Starter Assets / OpenXR are shown; adjust to match your binding set.

| Inspector Field | Action Path |
|---|---|
| Trigger Action | `XRI Right Hand Interaction/Activate` (or Left, depending on dominant hand setup) |
| Primary Button Action | `XRI Right Hand Interaction/Primary Button` |
| Secondary Button Action | `XRI Right Hand Interaction/Secondary Button` |

---

## 5. PistolData ScriptableObject — Starter Values

Create: `Assets > Create > JerryScripts > Weapon Data` → name it `PistolData`.

| Field | Default | Notes |
|---|---|---|
| Weapon Name | `Pistol` | |
| Weapon Type | `Pistol` | |
| Rarity | `Basic` | 4-tier enum: Basic/Rare/Epic/Legendary (`Common` was removed in S1-004 GDD reconciliation — do not use) |
| Base Damage | `20` | |
| Rounds Per Minute | `180` | → 0.333s fire interval |
| Max Range | `50` | metres |
| Mag Capacity | `12` | |
| Magazine Persist Seconds | `8` | |
| Mag Spawn Delay | `0.2` | |
| Grab Radius | `0.15` | metres |
| Holster Snap Radius | `0.15` | metres |
| Mag Insertion Radius | `0.05` | metres |
| Recoil Pitch Base | `4` | degrees |
| Recoil Yaw Spread | `1.5` | degrees half-angle |
| Recoil Recovery Time | `0.18` | seconds |
| Haptic Fire Amplitude | `0.8` | **Forward-compat only.** Post-S1-006 cleanup, haptics are dispatched by `AudioFeedbackService` reading `FeedbackEventConfig.EventEntry.HapticAmplitude`. This field is NOT read in MVP. |
| Haptic Fire Duration | `0.04` | Same — forward-compat. Use `FeedbackEventConfig` instead. |
| Haptic Dry Fire Amplitude | `0.2` | Same — forward-compat. Use `FeedbackEventConfig` instead. |
| Magazine Prefab | leave empty | **Not read in MVP.** `MagDropPool._magPrefab` is the global mag prefab. Per-weapon mag variation is post-MVP. |
| Muzzle Flash Prefab | leave empty | **Not read in MVP.** `MuzzleFlashPool._flashPrefab` is the global flash prefab. Per-weapon flash variation is post-MVP. |

---

## 6. Stubs to Implement Next

`WeaponInstance` contains `// TODO:` comments for calls that depend on systems
not yet written. Each stub is clearly marked with its target system:

| TODO location | Target system | GDD reference |
|---|---|---|
| ~~`ExecuteFire()` — FireHitscan call~~ | ✅ Wired in S1-005 | GDD Rule 6 |
| ~~`ExecuteFire()` — Fire SFX~~ | ✅ Wired in S1-006 (`AudioFeedbackService` `WeaponFire`) | GDD Rule 7 |
| ~~`ExecuteFire()` — Muzzle flash VFX~~ | ✅ Wired in S1-007 (`MuzzleFlashPool.Spawn`) | GDD §Visual/Audio |
| ~~`OnTriggerPerformed()` — dry-fire click~~ | ✅ Wired in S1-006 (`AudioFeedbackService` `WeaponDryFire`) | GDD Rule 8 |
| ~~`BeginReload()` — mag drop SFX~~ | ✅ Wired in S1-009 (`AudioFeedbackService` `MagDrop`) | GDD §Visual/Audio |
| `BeginReload()` — offhand mag spawn coroutine | WeaponInstance internal (S2-001) | GDD Rule 10 |
| `CompleteReload()` — insertion click + haptic | Audio/Feedback System (S2-001) | GDD §Visual/Audio |
| ~~`SnapToMount()` — holster click SFX~~ | ✅ Wired in S1-009 (`AudioFeedbackService` `WeaponHolster`) | GDD §Visual/Audio |
| `OnSecondaryButtonPerformed()` — slide SFX + haptic | Audio/Feedback System (S2-001) | GDD Rule 12 |

The mag-well proximity check (triggering `CompleteReload()`) requires a separate
`MagWellSocket` component (or an `XRSocketInteractor`) on the `MagWell` Transform.
Wire it to call `WeaponInstance.CompleteReload()` via an UnityEvent or a direct
`GetComponentInParent<WeaponInstance>()` call from the socket script.

---

## 7. Layer Setup (Project Settings)

Create the following layers in **Edit > Project Settings > Tags and Layers**:

| Layer Name | Usage |
|---|---|
| `Weapon` | XRI interaction layer for weapon grab interactables |
| `PlayerHitbox` | Player damage collider (already required by PlayerRig) |

Set the XR Direct / Ray Interactors on both hand controllers to include the
`Weapon` interaction layer.

---

## 8. MuzzleFlashPool (S1-007)

Place a `MuzzleFlashPool` component in the scene so `WeaponInstance` can resolve it on `Awake`.

### Scene wiring

In `Jerry_Scene.unity`, on the `_Systems` GameObject (alongside `AudioFeedbackService` / `ProjectileSystem` / `DamageResolver`):

1. **Add Component > Muzzle Flash Pool**.
2. **Inspector → Flash Prefab**: assign a ParticleSystem prefab (see "Placeholder prefab" below).
3. **Inspector → Pool Size**: leave default `6` (covers semi-auto rate-of-fire with headroom; raise to 12+ if rapid fire ever exhausts it).

`WeaponInstance.Awake()` auto-resolves the pool via `FindAnyObjectByType<MuzzleFlashPool>()`. No inspector wiring needed on weapon prefabs.

### Placeholder prefab

For MVP, any small ParticleSystem works. Quickest path:

1. **GameObject > Effects > Particle System** to create a default particle in the scene.
2. Tweak it: small `Start Lifetime` (~0.05s), small `Start Size` (~0.05), high `Start Speed` (~3), Emission Rate over Time = 0, Emission Burst = 1 burst of 8–15 particles.
3. **Important:** the `MuzzleFlashPool` automatically sets `MainModule.stopAction = ParticleSystemStopAction.Disable` on each pooled instance, so the pooled GO disables itself when emission completes — no coroutine, no Update tick. Don't fight this in the prefab.
4. Drag the GameObject into the Project to make a prefab. Delete the scene instance.
5. Assign the prefab to `MuzzleFlashPool._flashPrefab`.

Final-art muzzle flash with proper textures and lighting is post-MVP.

### Per-weapon flash variation (NOT implemented for MVP)

`WeaponData.MuzzleFlashPrefab` exists as a forward-compatibility field but is currently **not read** by `WeaponInstance`. The pool uses one global prefab for all weapons. Per-weapon variation (e.g., bigger flash for rifles) is post-MVP — when implemented, the pool will need to be keyed by prefab and swap based on `WeaponData`.

### Validation

- [ ] `_Systems` GO has `MuzzleFlashPool` component
- [ ] `_flashPrefab` slot points at a ParticleSystem prefab
- [ ] On fire: console silent (no errors), particle effect spawns and disables itself within ~100ms
- [ ] `MuzzleFlashPoolTests` 4/4 green in EditMode

---

## 9. MagDropPool (S1-009)

Place a `MagDropPool` component in the scene so `WeaponInstance.BeginReload` can drop magazines without `Instantiate`/`Destroy`.

### Scene wiring

In `Jerry_Scene.unity`, on the `_Systems` GameObject (alongside `MuzzleFlashPool` / `AudioFeedbackService` / `ProjectileSystem` / `DamageResolver`):

1. **Add Component > Mag Drop Pool**
2. **Inspector → Mag Prefab**: assign a Rigidbody+Collider mag prefab (see "Placeholder prefab" below)
3. **Inspector → Pool Size**: leave default `4` (covers semi-auto reload cadence; one held + 3 dropped before recycle)
4. **Inspector → Persist Seconds**: leave default `8` (matches GDD `magazine_persist_seconds`)

`WeaponInstance.Awake()` auto-resolves the pool via `FindAnyObjectByType<MagDropPool>()`. No inspector wiring needed on the weapon prefab.

### Placeholder prefab

For MVP, a simple cube with Rigidbody works:

1. `GameObject > 3D Object > Cube`
2. Scale to roughly mag dimensions: `(0.04, 0.10, 0.06)`
3. Add `Rigidbody` component:
   - `Mass` = `0.2`
   - `Use Gravity` = `true`
   - `Is Kinematic` = `true` (the pool toggles this on `Eject`)
   - `Interpolate` = `Interpolate`
   - `Collision Detection` = `Continuous Dynamic`
4. Keep the auto-added `BoxCollider`
5. Optional: add a dark material so it's visually distinct from the floor
6. Drag the GO from Hierarchy into `CS417_MP4/Assets/Prefabs/`, name it `Magazine.prefab`
7. Delete the scene instance
8. Assign the prefab to `MagDropPool._magPrefab`

### How the pool works

- 4 mag instances are pre-warmed at scene `Awake` as children of the pool, all deactivated and kinematic
- `Eject(Vector3 position, Quaternion rotation, float persistSeconds)`: round-robin pick the next slot, un-parent it (so physics isn't influenced by `_Systems` transform), reposition + reorient, activate, set `isKinematic = false`, clear velocities, schedule recycle via `Invoke` after `persistSeconds`
- After persist time: `Recycle` reactivates kinematic, re-parents to pool root, deactivates GO. Mag is back in the rotation
- If the player drops 5 mags in 8 seconds, the 5th drop triggers a recycle of the 1st one (visible as the original mag teleporting back to the gun before being re-ejected — acceptable for MVP)

### Per-weapon mag variation (NOT implemented for MVP)

`WeaponData.MagazinePrefab` is forward-compat only. Don't expect a rifle to drop a different mag than a pistol in MVP. Post-MVP enhancement: pool keyed by prefab.

### Validation

- [ ] `_Systems` GO has `MagDropPool` component
- [ ] `_magPrefab` slot points at a Rigidbody+Collider mag prefab
- [ ] On primary button press while pistol held: mag visibly drops with physics, `MagDrop` audio plays at mag well position
- [ ] After 8 seconds: dropped mag disappears (recycled, NOT destroyed — Hierarchy still shows 4 children of `MagDropPool`)
- [ ] `MagDropPoolTests` 5/5 green in EditMode
