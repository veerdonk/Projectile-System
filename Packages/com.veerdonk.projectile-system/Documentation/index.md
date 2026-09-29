# About com.veerdonk.projectile-system

Use this package to spawn, simulate, and pool kinematic projectiles. Games own HP, camera, input, and the player. The runtime owns movement, hits, pooling, and Recipe composition.

Call `IProjectileWorld.Spawn` with a `SpawnRequest` (Recipe, position, direction). Call `Tick` once per frame. Listen to `Hit`, `Expired`, and `StatusApplied`. Do not tick instances yourself.

## Recipe stage lists

A `Recipe` is base stats plus six ordered `IModifier` lists. List order is apply order. There is no wrap-the-next-spell graph.

| Stage | Typical modifiers |
| --- | --- |
| `OnSpawn` | `Multicast`, `Interval` |
| `OnTravel` | start scales (`Speed`, `Damage`, `Size`, `Gravity`, …), `AreaEffect`, `GroundField` |
| `OnTick` | `Homing` |
| `OnInterval` | `Multiply`, scales, `AreaEffect` aura (needs `Interval` on `OnSpawn`) |
| `OnHit` | `Pierce`, `Bounce`, `Multiply`, scales, `ApplyStatus`, `AreaEffect`, `GroundField` |
| `OnExpire` | `Multiply`, `GroundField` |

Author in code (`new Recipe { … }`) or as a `RecipeAsset` (`Create > Projectile > Recipe`) and call `ToRecipe()` before `Spawn`. Extras at fire time are a `ModifierKit` (`Create > Projectile > Modifier Kit`): `Recipe.Compose(base.ToRecipe(), uniqueId, kits)`. Each composition needs its own `Recipe.Id`. An OnInterval kit layer does nothing unless `Interval` is already on the base or on an OnSpawn layer.

## Public types

**Façade:** `IProjectileWorld` (`Spawn`, `Tick`, `Despawn`, `SetHitTarget`, `RemoveHitTarget`, `SetHomingCandidate`, `RemoveHomingCandidate`), `ProjectileWorld`, `Recipe`, `Delivery`, `SpawnRequest`, `HitInfo`, `HitKind`, `HitSource`, `ProtectionLevel`, `StatusInfo`, `ExpireInfo`, `ProjectileId`, `TargetId`, `HomingTarget`

**Hits and protection:** `SpawnRequest.IgnoreCollider` skips that collider on Direct overlap and sweep until the shot body no longer overlaps it (not a hit; a later contact is normal). `IDamageable` on colliders. `SetHitTarget(id, position, radius, protection)` publishes a sphere with no collider (`HitKind.Target`). `protection` defaults to `None`; `Basic` and `Strong` match `IProjectileProtection`. `radius <= 0` or `RemoveHitTarget` drops it. Direct casts, `AreaEffect` (including `OnTick`), `GroundField`, and Homing all read that catalog. `occupancy` on `AreaEffect` / `GroundField` raises `HitSource.OccupancyEnter` and `OccupancyExit` instead of pulses. `IProjectileProtection`. Hit queries stay inside the package.

**Modifiers:** `Pierce`, `Bounce`, `Multiply`, `Multicast`, `Homing`, `Interval`, `Speed`, `Damage`, `Critical`, `Mana`, `Size`, `Spread`, `EffectRadius`, `Gravity`, `ApplyStatus`, `AreaEffect`, `GroundField`

**Patterns:** `IMultiplyPattern` (`SquarePattern`, `CirclePattern`, `RandomCirclePattern`, `GridMultiplyPattern`), `MultiplyPatternAsset` (`Create > Projectile > Multiply Pattern`), `IMulticastPattern` (`RandomConePattern`). Square / Circle / RandomCircle use `Multiply.Spread` as meters and keep parent direction (hit normal on a hit; travel direction past the body when Pierce still has charges). A grid copies onto the Recipe: `Spacing` places the numbered cells, and `Spread` is the cone half-angle in degrees.

**Status:** `IStatusEffect`, `DummyStatusEffect`, `IStatusReceiver`

**Delivery:** `Recipe.Delivery`. `Straight` (default) is a kinematic SphereCast and ignores `Gravity`. `Arc` applies gravity; `Spawn` calls `BallisticAim.TrySolve` when `HomingAcquirePointSet` (unreachable returns default `ProjectileId`, no `Expired`). `Hitscan` is one `Radius` polyline inside `Spawn` (10 km max), then the instance is pooled. No view. World ends the ray unless Bounce. Pierce is n+1. Homing `n` is degrees of kink per segment. `Beam` stays live at the spawn pose and repeats a polyline. The first pulse is inside `Spawn`; `Recipe.BeamPulseSeconds` (default 0.1, `<= 0` every `Tick`) times the rest. Homing `n` on a Beam is degrees per second along that pulse (`distance / speed`). Without Pierce a pulse stops on the first Target and the next pulse can hit it again. Pierce n+1 is per pulse and does not despawn the Beam. `Despawn` or `LifetimeSeconds` does. `AimBeam` redraws a live Beam without raising `Hit`. The view is a cylinder per segment and stays up between pulses. The root material is copied onto those cylinders, and the texture's length coordinate runs from the first point to the last. Direct queries honor `IgnoreCollider`.

**Aim:** `BallisticAim.TrySolve` — flatter (needed) arc only. Arc `Spawn` calls it when the aim point is set. Games may still call it for a barrel preview. Unreachable returns false.

**Homing acquire:** `IHomingAcquisition`, `ClosestInTravelDirectionAcquisition` (default, and every lock after the first), `ClosestToPointAcquisition` (first lock when `SpawnRequest.HomingAcquirePointSet` / `HomingAcquirePoint`). Publish poses with `SetHomingCandidate`. The package does not read the mouse.

**Views:** `IProjectileViewFactory`, `PrimitiveViewFactory`, `PrefabViewFactory`, `IGroundFieldViewFactory`, `PrimitiveGroundFieldViewFactory`, `PrefabGroundFieldViewFactory`, `ProjectileView`

**Authoring:** `RecipeAsset`, `ModifierKit`, `ModifierKitLayer`, `MultiplyPatternAsset`, `GridMultiplyPattern`, `Recipe.Clone`, `Recipe.Append`, `Recipe.Compose`, `RecipeStage`, `ModifierStageAttribute`

Internal ECS types under `Runtime/Ecs/` are not a consumer API.

## Installing

Requires Unity 6 LTS (`6000.6`) and `com.unity.entities`. MIT license.

**Add package from git URL** (Package Manager → +):

```text
https://github.com/veerdonk/Projectile-System.git?path=/Packages/com.veerdonk.projectile-system
```

Or copy this folder into the other project’s `Packages/`. This repository already embeds the package. Full steps: [README.md](../README.md).

## Sample

Import **Projectile Yard** from Package Manager, then open `Scenes/Yard.unity` and enter Play Mode. The sample shows `Spawn`, `Tick`, and `Hit`. It is not part of the Runtime assembly and does not compile until you import it.

## Requirements

- Unity 6000.6.0f1 or later
- `com.unity.entities` 6.6.0 (hybrid ECS simulation)
- `com.unity.test-framework` (package tests)

## Package contents

| Location | Description |
| --- | --- |
| `Runtime/` | Façade, Recipes, modifiers, pooling, PhysX query |
| `Runtime/Ecs/` | Internal simulation components and systems |
| `Editor/` | `RecipeAsset`, Modifier Kit, and Multiply Pattern inspectors |
| `Tests/` | Edit Mode and Play Mode tests |
| `Samples~/Yard/` | Play Mode sample (hidden until Import) |
| `Documentation/` | This file |
| `AGENTS.md` | How game-side agents should call the package |
