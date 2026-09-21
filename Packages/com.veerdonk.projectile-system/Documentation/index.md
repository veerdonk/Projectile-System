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

Author in code (`new Recipe { … }`) or as a `RecipeAsset` (`Create > Projectile > Recipe`) and call `ToRecipe()` before `Spawn`.

## Public types (through 22.b)

**Façade:** `IProjectileWorld`, `ProjectileWorld`, `Recipe`, `SpawnRequest`, `HitInfo`, `HitKind`, `HitSource`, `ProtectionLevel`, `StatusInfo`, `ExpireInfo`, `ProjectileId`, `TargetId`, `HomingTarget`

**Hits and protection:** `IDamageable`, `IProjectileProtection`, `IHitQuery.Overlap` / `OverlapDisk` (PhysX implementation is internal; Edit Mode tests use a scripted query)

**Modifiers:** `Pierce`, `Bounce`, `Multiply`, `Multicast`, `Homing`, `Interval`, `Speed`, `Damage`, `Critical`, `Mana`, `Size`, `Spread`, `EffectRadius`, `Gravity`, `ApplyStatus`, `AreaEffect`, `GroundField`

**Patterns:** `IMultiplyPattern` (`SquarePattern`, `CirclePattern`, `RandomCirclePattern`), `IMulticastPattern` (`RandomConePattern`)

**Status:** `IStatusEffect`, `DummyStatusEffect`, `IStatusReceiver`

**Aim:** `BallisticAim.TrySolve` — flatter (needed) arc only. Copy the direction onto `SpawnRequest.Direction`. Unreachable returns false.

**Views:** `IProjectileViewFactory`, `PrimitiveViewFactory`, `PrefabViewFactory`, `IGroundFieldViewFactory`, `PrimitiveGroundFieldViewFactory`, `PrefabGroundFieldViewFactory`, `ProjectileView`

**Authoring:** `RecipeAsset`, `RecipeStage`, `ModifierStageAttribute`

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
| `Editor/` | `RecipeAsset` inspector |
| `Tests/` | Edit Mode and Play Mode tests |
| `Samples~/Yard/` | Play Mode sample (hidden until Import) |
| `Documentation/` | This file |
| `AGENTS.md` | How game-side agents should call the package |
