# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.4] - 2026-10-14

### Fixed

- A crowded scene could make the physics hit query miss the real target. The overlap, sweep, `Overlap`, `OverlapDisk`, and skin-check buffers held 16 colliders (64 for the skin check), and when they filled, Unity returned an unsorted subset. Ignored colliders (the `IgnoreCollider` slot and the `IgnoreColliders` set) take slots too. A query that fills its buffer now doubles it (cap 1024) and runs again, so a buffer allocates only when a scene first needs it. Buffers never shrink.

## [0.2.3] - 2026-10-14

### Added

- `SpawnRequest.IgnoreColliders` (`IReadOnlyList<Collider>`). Direct overlap and sweep (Straight, Arc, Hitscan, and Beam), `AreaEffect` overlaps, and `GroundField` disk pulses skip every collider in the set for the whole life of the shot. A skip is not a hit: no `Hit`, despawn, Pierce or Bounce spend, or status. The list is copied at `Spawn`, so the caller can reuse or clear it. Multicast extras, Multiply children, and `RequestSpawn` children inherit the set; a ground field keeps its own copy after the shot is gone. Null or empty keeps today's behaviour. `IgnoreCollider` is unchanged.

### Fixed

- A `Hit` handler that calls `Despawn` on the shot it was told about no longer runs that shot's OnHit area and ground flushes, OnExpire, and pool return a second time.

## [0.2.2] - 2026-10-14

### Fixed

- A Pierce shot no longer hits a concave `MeshCollider` Target a second time on its far side when the body is thicker than one tick step. The skip on that collider now holds until the shot has passed the pierce exit. It releases at once if the shot turns away from the exit, for example after a Bounce, so the shot can hit that collider again.
- On a concave collider, the overlap-pick raycast no longer reports a surface beyond the front of the shot's sphere. When that ray misses, the collider is ranked at the shot's radius, not at distance 0, so a closer Box Target overlapped in the same tick still wins.
- The pierce-exit raycast on a concave mesh reaches a skin past the hit point, so a thin double-sided body still finds its exit.
- The concave "still touching" check ignores triggers and other layers, so a crowded overlap cannot drop the skipped collider.

## [0.2.1] - 2026-10-14

### Fixed

- A shot that hits a concave `MeshCollider` or a `TerrainCollider` no longer logs the `Physics.ClosestPoint` warning, and its hit point and normal come from a raycast instead of the unchanged input point. The hit query never calls `ClosestPoint` on those colliders; Box, Sphere, Capsule, and convex Mesh colliders are unchanged.
- The pierce exit is computed only when Pierce reads it. On a concave mesh it is the far side of the whole body along the travel line; a `TerrainCollider` has no pierce exit, so Multiply falls back to the hit point.
- The `IgnoreCollider` / post-hit skip clears on a concave collider once the shot is clear, as for a box. Area and ground-field overlaps on a concave `IDamageable` use its bounds for the hit point.

## [0.2.0] - 2026-10-08

### Added

- `IProjectileWorld` events `GroundFieldPlanted`, `GroundFieldPulsed`, `GroundFieldEnded`, and `AreaBurst`, with `GroundFieldId`, `GroundFieldInfo`, `GroundFieldPulseInfo`, `GroundFieldEndReason`, and `AreaBurstInfo` (ADR-039). Pulses over an empty disk and bursts with no target inside now raise. Additive; existing events and behavior are unchanged.

## [0.1.12] - 2026-10-08

### Added

- `IProjectileWorld.Spawned` (`SpawnInfo`: `InstanceId`, `ParentId`, `SpawnCause` Game / Multicast / Multiply / Other, `Position`, `Direction`, `Recipe`). Raised once for every live instance, including package-made spawns and Hitscan (before its `Hit` and `Expired`). An unreachable Arc raises nothing.
- `IProjectileWorld.HomingLocked` (`HomingLockInfo`). Raised for a lock assigned at spawn (right after `Spawned`) and for every acquire or re-acquire. Not raised while a lock is kept.
- `ExpireInfo.Position`, `Direction`, and `Reason` (`ExpireReason`: Lifetime, Hit, Absorbed, Despawned, HitscanEnd, Other).
- `HitInfo.Outcome` (`HitOutcome`: None, Ended, Pierced, Bounced, Continued). Area, occupancy, and ground hits stay `None`.
- Event order a listener can rely on: `Spawned` before that instance's first `HomingLocked`, `Hit`, or `Expired`; a child's `Spawned` after the parent's `Hit`; `Hit` before the same instance's `Expired`.
- Usage guide with a worked example: "Shot lifecycle events (presentation)" in `Documentation/index.md`.

### Upgrading

Nothing to change. No existing event, field, or behavior changed, and the new struct fields default to zero (`Reason = Lifetime`, `Outcome = None`). Code that constructs `HitInfo` or `ExpireInfo` with object initializers still compiles. Custom `IProjectileWorld` implementations (the package ships only `ProjectileWorld`) must add the two new events.

## [0.1.10] - 2026-09-28

### Added

- `MultiplyPatternAsset` (`Create > Projectile > Multiply Pattern`). Square grid, default 3×3, minimum 2×2. `Spacing` is meters between cell centers (`<= 0` stacks). Left-click an empty cell for the next number; right-click removes it and compacts to `1..k`. + adds a bottom row and a right column. − drops those, not below 2×2, then compact-renumbers. The inspector draws a line between cells.
- `GridMultiplyPattern`, the serializable copy a Recipe or Modifier Kit stores. `Multiply(n)` uses cells `1..n`. No numbers spawns nothing. `Multiply.Spread` on this pattern is the cone half-angle in degrees of the farthest numbered cell (`<= 0` keeps every child parallel). Square, Circle, and RandomCircle still read Spread as meters and still fly on parent forward / hit normal.
- Recipe and Modifier Kit Pattern dropdowns list those three patterns plus every `MultiplyPatternAsset` in the project. Picking an asset copies Spacing and the cell numbers. `ToRecipe` and kit clone copy those fields. The live Recipe does not keep the asset. `Spread(n)` still scales `Multiply.Spread` only.
- Projectile Yard preset **Multiply on hit (Grid)** and pattern asset `GridFan` (center, right, above; Spacing 2; Spread 20°). The sample sheet pattern button uses the same catalog.

## [0.1.9] - 2026-09-28

### Fixed

- A Modifier Kit's Multiply Pattern field is a dropdown of the shipped `IMultiplyPattern` types, the same catalog as a Recipe asset. The labels are Circle Pattern, Random Circle Pattern, and Square Pattern. There is no empty choice. An unset pattern shows Square Pattern.

## [0.1.8] - 2026-09-28

### Added

- `Delivery.Beam` and `Recipe.BeamPulseSeconds` (default 0.1; `<= 0` pulses every `Tick`). A Beam is a live instance at the spawn pose. The first pulse runs inside `Spawn`; later pulses follow `BeamPulseSeconds`. `LifetimeSeconds` still expires it, including while a game keeps the shot held.
- `IProjectileWorld.Despawn(ProjectileId)`. Runs OnExpire, returns any live instance to the pool, and raises `Expired`. Unknown or already pooled ids do nothing. The yard Laser preset holds one Beam while Fire is down, calls `AimBeam` each frame from the current muzzle pose, and calls `Despawn` on release.
- Each pulse is a polyline of published volumes and World. World clips the pulse unless Bounce reflects. Direct queries honor `SpawnRequest.IgnoreCollider`. `Pierce(n)` is n+1 contacts per pulse and does not despawn the Beam. Without Pierce the pulse stops on the first Target, and the next pulse can hit that Target again. A Strong target eats the pulse with no `Hit`. A continuing pulse does not immediately strike the same surface again.
- Homing on a Beam curves at `n` degrees per second of travel (`distance / speed`). Hitscan stays a per-segment kink. The first step leaves along the muzzle. The lock is kept across redraws while that target stays in front of the muzzle. A Target hit drops the lock for the rest of that polyline.
- `IProjectileWorld.AimBeam(ProjectileId, position, direction)`. Redraws a live Beam from that pose without raising `Hit`. Damage stays on the pulse clock. A silent redraw drops the homing lock on a Target hit the same way the damage pulse does. Other deliveries and unknown ids do nothing.
- The view is one primitive cylinder per segment, diameter `2 * Radius`, and it stays up between pulses. Those cylinders have no colliders, so the beam is not a hit and Bounce cannot reflect off it. Root scale does not change the diameter. The root renderer's material is copied onto the segments, so color and texture come from the presentation prefab. Each segment keeps its own mesh copy: the texture's length coordinate runs from 0 at the first point to 1 at the last. The material's own tiling still repeats at the scale set on the material.

## [0.1.7] - 2026-09-27

### Changed

- Gravity applies only when `Recipe.Delivery` is `Arc`. The default is `Straight`, so a Recipe that already sets `Gravity` flies level until you set `Delivery.Arc` on the Recipe or `RecipeAsset`. `Gravity(n)` still scales the instance value; the integrate step uses it only for Arc.
- Arc shots no longer need `BallisticAim.TrySolve` copied onto `SpawnRequest.Direction`. Set `Delivery.Arc`, set `HomingAcquirePointSet`, and copy the world aim point into `HomingAcquirePoint`. `Spawn` lofts before OnSpawn. If that point cannot be reached, `Spawn` returns the default `ProjectileId` and does not raise `Expired`. `TrySolve` stays available for a barrel preview. If the aim point is unset, Arc uses `Direction` as an already-solved loft and still applies gravity in flight.

### Added

- `Delivery` on `Recipe` and `RecipeAsset`: `Straight` (default), `Arc`, and `Hitscan`. Hitscan is one `Radius` polyline of SphereCasts (PhysX and published volumes) inside `Spawn`, then the instance is pooled. There is no view. Unbounded length is 10 km. World ends the polyline unless Bounce reflects. `Pierce(n)` still dies on hit n+1. Homing `n` on Hitscan is the maximum kink in degrees per segment, not degrees per second. Direct queries still honor `SpawnRequest.IgnoreCollider`.

## [0.1.6] - 2026-09-27

### Added

- `SpawnRequest.IgnoreCollider`. Direct overlap and sweep skip that collider until the shot body no longer overlaps it, without a hit, despawn, Pierce/Bounce spend, or status. After it clears, a later contact is a normal hit. Multicast extras and multiply children copy the same one-slot skip. Null leaves the previous behaviour. Area and ground queries are unchanged.

## [0.1.5] - 2026-09-27

### Changed

- `SetHitTarget`, `RemoveHitTarget`, and `SetHomingCandidate` / `RemoveHomingCandidate` look ids up in a dictionary instead of scanning the published list. Republishing every living enemy each frame no longer costs N²/2 comparisons. Removals are deferred and compacted in one pass before the next hit or homing query, so a mass despawn no longer costs one list shift per id. Hits, homing picks, and catalog order are unchanged. No public API change.

## [0.1.4] - 2026-09-26

### Added

- `SpawnRequest.HomingAcquirePointSet` and `HomingAcquirePoint`. When set, and `HomingTarget` is unassigned, Homing’s first lock is the catalog candidate closest to that world point (`ClosestToPointAcquisition`, no travel-direction filter). After that lock, or when spawn already assigned a target, later empty locks stay closest-in-travel-direction. The package does not read the mouse. Multicast extras inherit the point.

## [0.1.3] - 2026-09-26

### Added

- `ModifierKit` (`Create > Projectile > Modifier Kit`) and `ModifierKitLayer`. A kit is extra modifiers plus the stage list each belongs to. It has no speed, lifetime, or presentation.
- `Recipe.Clone`, `Recipe.Append`, and `Recipe.Compose(basis, id, kits)`. Compose clones the base, sets a new `Recipe.Id`, and appends kits in order. Reusing an id on `Spawn` still replaces the stored Recipe. OnInterval extras do nothing unless `Interval` is already on the base or on an OnSpawn layer; kits do not insert one.

### Fixed

- The modifier kit inspector picks `ApplyStatus`, `AreaEffect`, and `GroundField` status from the shipped `IStatusEffect` list.

## [0.1.2] - 2026-09-26

### Added

- `SetHitTarget` takes an optional `ProtectionLevel`. `Basic` matches a Basic collider (OnHit runs, no `ApplyStatus`). `Strong` eats the shot and is skipped by area, ground fields, and occupancy.
- Homing acquires and steers toward published hit-target positions. The same id in `SetHomingCandidate` is one candidate; the hit-target position wins. `SetHomingCandidate` remains for a pose that is not hittable.
- `AreaEffect` may run on `OnTick` (an overlap every Tick). `occupancy` on `AreaEffect` and `GroundField` raises `HitSource.OccupancyEnter` once (and status, if authored) and `HitSource.OccupancyExit` (damage 0, no status) on leave.

## [0.1.1] - 2026-09-23

### Added

- `IProjectileWorld.SetHitTarget` / `RemoveHitTarget`. Games can publish a sphere (`TargetId`, position, radius) and get `HitKind.Target` with no PhysX collider and no `IDamageable`. Direct casts, `AreaEffect`, and `GroundField` read the catalog. Pierce and Bounce treat those hits like collider Targets. `radius <= 0` does not publish.

## [0.1.0] - 2026-09-20

First release of **com.veerdonk.projectile-system**.

### Added

- `IProjectileWorld` façade: `Spawn`, `Tick`, pool, `Hit` / `Expired` / `StatusApplied`
- `Recipe` with six ordered modifier lists and combat/cast stats (damage, crit chance, mana cost, radius, effect radius, gravity)
- Named modifiers: Pierce, Bounce, Multiply (Square / Circle / RandomCircle patterns), Multicast, Homing, Interval, Speed / Damage / Critical / Mana / Size / Spread / EffectRadius / Gravity scales, ApplyStatus, AreaEffect, GroundField
- `BallisticAim.TrySolve` needed-arc launch direction (no high-arc pick)
- `IProjectileProtection` (Basic / Strong), `IProjectileViewFactory` / `IGroundFieldViewFactory` (primitive and prefab)
- `RecipeAsset` ScriptableObject authoring with a stage-filtered inspector
- Play Mode sample **Projectile Yard** under `Samples~/Yard` (import from Package Manager; not part of Runtime)
- Hybrid ECS simulation behind the façade (Entities world owned by `ProjectileWorld`)

### Changed

- Package id and C# namespace are `com.veerdonk.projectile-system` / `Veerdonk.ProjectileSystem` (ADR-025). The doubled template id is not published.

### Fixed

- SonarQube Blocker/Critical/Major findings on package Runtime/Editor (assembly version, deserialization defaults, Bind arity, pattern loops, area-overlap complexity).
