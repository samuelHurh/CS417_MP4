# Foundation/Audio — Editor Setup Guide

S1-006 | Owner: Jerry Chen | Depends on: Foundation/PlayerRig (S1-001)

**Scope:** 3 of 19 GDD-cataloged events wired end-to-end — `WeaponFire`, `HitConfirmation`, `WeaponDryFire`. The other 16 enum variants exist for forward compatibility; their owning stories add `EventEntry` rows when implemented.

---

## 1. Create the Audio Mixer Asset

1. **Assets > Create > Audio Mixer**, name it `FeedbackAudioMixer`.
2. Save to `CS417_MP4/Assets/Audio/Mixers/FeedbackAudioMixer.mixer` (create the folder if missing).
3. Open the Audio Mixer window (Window > Audio > Audio Mixer) and add three groups under `Master`:

| Group | Used by | Notes |
|-------|---------|-------|
| `WeaponFire` | `WeaponFire` event | Loud, foreground. No ducking in MVP. |
| `HitConfirmation` | `HitConfirmation` event | 2D ear-space cue. |
| `WeaponSecondary` | `WeaponDryFire`, `MagDrop`, `WeaponHolster`, `DamageReceived` events (S1-009 + S1-008) | Catch-all for non-fire weapon/feedback events. Renamed from `DryFire` once S1-008 + S1-009 added more events. |

The full GDD specifies 5 mixer groups (Master / Weapons / Combat / Player Feedback / Environment / UI). Ducking and the remaining 2 groups are deferred — for MVP, the 3 groups above are sufficient.

---

## 2. Create the FeedbackEventConfig Asset

1. **Assets > Create > CS417 > Foundation > Audio Feedback Config**.
2. Save to `CS417_MP4/Assets/Audio/FeedbackEventConfig.asset`.
3. Set the Inspector fields:

### Per-Event Entries

Add **3 entries** to the `Entries` list:

| EventType | SpatialBlend | MixerGroup | BasePitch | PitchVariance | Volume | HapticAmplitude | HapticDuration | Clips |
|-----------|--------------|------------|-----------|---------------|--------|-----------------|----------------|-------|
| `WeaponFire` | `1.0` (3D) | `WeaponFire` group | `1.0` | `0.04` (±4 cents per GDD) | `1.0` | `0.8` | `0.04` | 2–4 gunshot variants |
| `HitConfirmation` | `0.0` (2D) | `HitConfirmation` group | `1.0` (overridden by formula) | `0.0` (formula only) | `0.8` | `0.0` | `0.0` | 1–3 hit ping variants |
| `WeaponDryFire` | `0.0` (2D) | `WeaponSecondary` group | `1.0` | `0.08` (±8 cents per GDD) | `0.7` | `0.3` | `0.02` | 1–3 dry-click variants |
| `MagDrop` (S1-009) | `1.0` (3D) | `WeaponSecondary` group | `1.0` | `0.04` | `1.0` | `0.0` (no haptic) | `0.0` | optional |
| `WeaponHolster` (S1-009) | `0.0` (2D) | `WeaponSecondary` group | `1.0` | `0.02` | `0.8` | `0.0` (no haptic) | `0.0` | optional |
| `DamageReceived` (S1-008) | `1.0` (3D) | `WeaponSecondary` group | `1.0` | `0.0` | `1.0` | `0.9` | `0.2` | optional |

Clips can be left empty for now — the service logs a `[AudioFeedbackService] Event has no AudioClips` warning per missing entry on startup, and `PostFeedbackEvent` warns and returns silently. Game still runs.

### HitConfirmation Pitch Scaling

| Field | Default | Why |
|-------|---------|-----|
| `Hit Confirm Damage Cap` | `330` | Matches `RarityMultiplierTable.PlayerDamageCap`. **Must be the same value** in both assets. |
| `Hit Confirm Pitch Floor` | `0.9` | GDD §Formulas. Pitch at zero damage. |
| `Hit Confirm Pitch Ceiling` | `1.4` | GDD §Formulas. Pitch at damage ≥ cap. |

Formula: `pitch = lerp(0.9, 1.4, finalDamage / 330)`. `Mathf.Lerp` clamps `t`, so damage above cap caps at `1.4`.

### Pool Sizing

| Field | Default | Why |
|-------|---------|-----|
| `Pool Size` | `8` | GDD S1-006 default. Raise to 12 if you hear audio cutoff on rapid fire. |

---

## 3. Scene Wiring

In `Jerry_Scene.unity`, on the `_Systems` GameObject (the same GO that already hosts `ProjectileSystem` + `DamageResolver` from S1-005):

1. **Add Component > Audio Feedback Service**.
2. Drag the `FeedbackEventConfig.asset` from step 2 into the `_config` Inspector slot.

That's it for inspector wiring. The service auto-resolves `IRigControllerProvider` from the scene's `PlayerRig` in `Awake` — no controller reference to drag.

`WeaponInstance` and `ProjectileSystem` both auto-resolve `AudioFeedbackService` via `FindAnyObjectByType` in their `Awake`. No inspector wiring needed on weapon prefabs or other components.

---

## 4. Architectural Note — Haptics

Per GDD Rule 2 (`audio-feedback-system.md`), **only `AudioFeedbackService` calls `SendHapticImpulse`**. `WeaponInstance.TriggerHaptic()` was removed in S1-006.

Haptic amplitude and duration are configured per-event via `EventEntry.HapticAmplitude` and `EventEntry.HapticDuration` (both `[SerializeField] float` fields, exposed in the Inspector). `AudioFeedbackService.PostFeedbackEvent` reads these fields and dispatches haptic impulses to the correct controller after playing audio. If either field is `0`, the haptic call is skipped entirely (guard: `amplitude > 0 && duration > 0`).

To add haptics to a new event:
1. Set `HapticAmplitude` and `HapticDuration` on the event's `EventEntry` row in the `FeedbackEventConfig` asset.
2. No code changes needed — `PostFeedbackEvent` already handles the dispatch.

Never call `SendHapticImpulse` from `WeaponInstance`, `ProjectileSystem`, or any other system.

---

## 5. Validation Checklist (Run before first PIE)

- [ ] `FeedbackAudioMixer.mixer` exists with `Master > WeaponFire / HitConfirmation / DryFire` groups
- [ ] `FeedbackEventConfig.asset` exists with 3 `EventEntry` rows (clips can be empty)
- [ ] `_Systems` GO in `Jerry_Scene.unity` has `AudioFeedbackService` component
- [ ] `AudioFeedbackService._config` slot points at the config asset
- [ ] `RarityMultiplierTable.PlayerDamageCap` and `FeedbackEventConfig.HitConfirmDamageCap` agree (both `330`)
- [ ] PIE starts with up to **16 `LogWarning` lines** — one for each deferred `FeedbackEvent` enum value that has no `EventEntry` row yet (`MagDrop`, `MagSpawn`, `MagInsertion`, `SlideRack`, `WeaponGrab`, `WeaponHolster`, `DamageReceived`, `PlayerDeath`, `RoomTransitionStart`, `RoomTransitionEnd`, `PauseActivated`, `SnapTurn`, `TrackingLost`, `EnemyNearMiss`, `CurrencyPickup`, `TrackingRestored`). Plus up to 3 more if your configured entries have empty `Clips` arrays. **By design — they're a dev aid, not bugs. Zero errors.**
- [ ] Test Runner > EditMode > Run All shows **76/76 green** (12 DamageResolver + 2 Projectile contract + 12 Audio + 4 MuzzleFlashPool + 5 PlayerHitbox + 5 MagDropPool + 5 MagSpawnPool + 5 MagWellSocket + 5 WeaponInstanceReload + 21 PlayerStateManager)

---

## 6. Testing Without Audio Clips

The service is null-safe end-to-end:

- Missing `EventEntry` → `LogWarning` once on startup, post-and-skip thereafter
- `EventEntry` with empty `Clips[]` → `LogWarning` once, no exception
- Missing `IRigControllerProvider` → haptic dispatch skipped, audio still plays
- `_config` not assigned → `LogError` once at `Awake`, all posts no-op

To verify the wire-up before importing real clips:

1. Pull the trigger on the pistol while aimed at the test dummy from S1-005.
2. Console should show `[AudioFeedbackService] No clips for WeaponFire — skipping play` (or similar — exact wording depends on the implementation log strings).
3. **No exceptions, no errors.** That confirms the post is reaching the service.
4. Drag any short `.wav` into the `WeaponFire` entry's `Clips` array → fire again → you should hear it 3D-positioned at the muzzle.

---

## 7. Asset Authoring Pointers

Per GDD §Visual/Audio Requirements:

| Asset | Format | Settings |
|-------|--------|----------|
| `WeaponFire` clips | ADPCM, Decompress On Load | 2–4 variants per weapon type. ~150–300ms each. |
| `HitConfirmation` clip | ADPCM, Decompress On Load | Single short ping ~100–200ms. Pitch headroom needed (will be played at 0.9–1.4×). |
| `WeaponDryFire` clip | ADPCM, Decompress On Load | Single hollow click ~50–100ms. |

Keep clips < 500ms each — hot-path audio. Anything longer should be a music/ambient asset on a different mixer group (deferred).

---

## 8. What's NOT Implemented (deferred to later stories)

| Feature | Owning Story | GDD Reference |
|---------|--------------|---------------|
| `OnVFXRequested` event channel | S1-007 (or later when VFX subsystem exists) | §VFX Routing |
| Ducking / priority / pause -12dB rules | Post-MVP | §Audio Mixer Hierarchy |
| Voice priority cap for enemies | Sam's Enemy System | §Priority and Interruption |
| `EnemyNearMiss` event | Sam's Enemy System | Catalog row 17 |
| `DamageReceived` haptic | S1-008 | Catalog row 9 |
| Mag drop / insert / slide rack / holster events | S1-009 / S2-001 | Catalog rows 3–8 |
| Tracking-lost loop, room transitions, death lowpass | Polish phase | §States and Transitions |

Adding any of these means: (a) wiring an `EventEntry` row in the config, (b) handling the event branch in `AudioFeedbackService.PostFeedbackEvent`, (c) updating the contract guard test count if you change the enum (don't), (d) updating this README.
