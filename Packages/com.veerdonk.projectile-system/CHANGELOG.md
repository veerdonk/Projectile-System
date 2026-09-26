# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
