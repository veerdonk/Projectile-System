# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
