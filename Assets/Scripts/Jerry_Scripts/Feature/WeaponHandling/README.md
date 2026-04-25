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
| Rarity | `Common` | |
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
| Haptic Fire Amplitude | `0.8` | 0–1 |
| Haptic Fire Duration | `0.04` | seconds |
| Haptic Dry Fire Amplitude | `0.2` | 0–1 |
| Magazine Prefab | `MagazineProp.prefab` | assign when available; optional for prototype |
| Muzzle Flash Prefab | `MuzzleFlash.prefab` | assign when available; optional for prototype |

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
| `BeginReload()` — mag drop SFX | Audio/Feedback System (S1-009) | GDD §Visual/Audio |
| `BeginReload()` — offhand mag spawn coroutine | WeaponInstance internal (S2-001) | GDD Rule 10 |
| `CompleteReload()` — insertion click + haptic | Audio/Feedback System (S2-001) | GDD §Visual/Audio |
| `SnapToMount()` — holster click SFX | Audio/Feedback System (S1-009) | GDD §Visual/Audio |
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
