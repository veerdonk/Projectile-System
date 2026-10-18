# About com.veerdonk.projectile-system

Use this package to spawn, simulate, and pool kinematic projectiles. Games own HP, camera, input, and the player. The runtime owns movement, hits, pooling, and Recipe composition.

Call `IProjectileWorld.Spawn` with a `SpawnRequest` (Recipe, position, direction). Call `Tick` once per frame. Listen to `Hit`, `Expired`, and `StatusApplied`. For presentation, `Spawned` reports every live instance (game or package-made, with `ParentId` and `SpawnCause`), `HomingLocked` reports each new lock, `ExpireInfo` carries the last pose and an `ExpireReason`, and `HitInfo.Outcome` says whether a Direct hit ended, pierced, bounced, or continued. For effects, listen to `GroundFieldPlanted`, `GroundFieldPulsed` (raised after that pulse's Area hits, even over an empty disk), `GroundFieldEnded`, and `AreaBurst` (before its Area hits; occupancy only when first made). Do not tick instances yourself.

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

**Façade:** `IProjectileWorld` (`Spawn`, `Tick`, `Despawn`, `SetHitTarget`, `RemoveHitTarget`, `SetHomingCandidate`, `RemoveHomingCandidate`), `ProjectileWorld`, `Recipe`, `Delivery`, `SpawnRequest`, `HitInfo`, `HitKind`, `HitSource`, `ProtectionLevel`, `StatusInfo`, `ExpireInfo`, `ExpireReason`, `HitOutcome`, `SpawnInfo`, `SpawnCause`, `HomingLockInfo`, `HomingLockKind`, `ProjectileId`, `TargetId`, `HomingTarget`

**Hits and protection:** `SpawnRequest.IgnoreCollider` skips that collider on Direct overlap and sweep until the shot body no longer overlaps it (not a hit; a later contact is normal). `SpawnRequest.IgnoreColliders` is a copied list skipped for the whole shot life on Direct, `AreaEffect`, and `GroundField` queries (not a hit). `IDamageable` on colliders. `SetHitTarget(id, position, radius, protection)` publishes a sphere with no collider (`HitKind.Target`). `SetHitTarget(id, bottom, radius, height, homingHeight, protection)` publishes an upright capsule (height includes caps); `HitInfo.Point` is the contact on it, and Homing reads the point `homingHeight` above `bottom`. `protection` defaults to `None`; `Basic` and `Strong` match `IProjectileProtection`. `radius <= 0` or `RemoveHitTarget` drops it. Direct casts, `AreaEffect` (including `OnTick`), `GroundField`, and Homing all read that catalog. `occupancy` on `AreaEffect` / `GroundField` raises `HitSource.OccupancyEnter` and `OccupancyExit` instead of pulses. `IProjectileProtection`. Hit queries stay inside the package.

**Modifiers:** `Pierce`, `Bounce`, `Multiply`, `Multicast`, `Homing`, `Interval`, `Speed`, `Damage`, `Critical`, `Mana`, `Size`, `Spread`, `EffectRadius`, `Gravity`, `ApplyStatus`, `AreaEffect`, `GroundField`

**Patterns:** `IMultiplyPattern` (`SquarePattern`, `CirclePattern`, `RandomCirclePattern`, `GridMultiplyPattern`), `MultiplyPatternAsset` (`Create > Projectile > Multiply Pattern`), `IMulticastPattern` (`RandomConePattern`). Square / Circle / RandomCircle use `Multiply.Spread` as meters and keep parent direction (hit normal on a hit; travel direction past the body when Pierce still has charges). A grid copies onto the Recipe: `Spacing` places the numbered cells, and `Spread` is the cone half-angle in degrees.

**Status:** `IStatusEffect`, `DummyStatusEffect`, `IStatusReceiver`

**Delivery:** `Recipe.Delivery`. `Straight` (default) is a kinematic SphereCast and ignores `Gravity`. `Arc` applies gravity; `Spawn` calls `BallisticAim.TrySolve` when `HomingAcquirePointSet` (unreachable returns default `ProjectileId`, no `Expired`). `Hitscan` is one `Radius` polyline inside `Spawn` (`Recipe.MaxRange`, or 10 km when `<= 0`), then the instance is pooled. No view. World ends the ray unless Bounce. Pierce is n+1. Homing `n` is degrees of kink per segment. `Beam` stays live at the spawn pose and repeats a polyline. The first pulse is inside `Spawn`; `Recipe.BeamPulseSeconds` (default 0.1, `<= 0` every `Tick`) times the rest. Homing `n` on a Beam is degrees per second along that pulse (`distance / speed`). Without Pierce a pulse stops on the first Target and the next pulse can hit it again. Pierce n+1 is per pulse and does not despawn the Beam. `Despawn` or `LifetimeSeconds` does. `AimBeam` redraws a live Beam without raising `Hit` and leaves the lock alone. `AimBeam(id, position, direction, HomingTarget target)` also sets the Beam's homing lock: kept across redraws and pulses whether or not it is in front of the muzzle, dropped by a Target hit for the rest of that polyline only, `default` clears it and the Beam self-acquires again. `HomingLocked` is raised only when the set target differs from the current lock. `Recipe.MaxRange` caps a Beam pulse too (reach is `MaxRange + Radius`; the view ends at the cap). The view is a cylinder per segment and stays up between pulses. The root material is copied onto those cylinders, and the texture's length coordinate runs from the first point to the last. Direct queries honor `IgnoreCollider`; `IgnoreColliders` also covers area and ground queries.

**Aim:** `BallisticAim.TrySolve` — flatter (needed) arc only. Arc `Spawn` calls it when the aim point is set. Games may still call it for a barrel preview. Unreachable returns false.

**Homing acquire:** `IHomingAcquisition`, `ClosestInTravelDirectionAcquisition` (default, and every lock after the first), `ClosestToPointAcquisition` (first lock when `SpawnRequest.HomingAcquirePointSet` / `HomingAcquirePoint`). Publish poses with `SetHomingCandidate`. The package does not read the mouse.

**Views:** `IProjectileViewFactory`, `PrimitiveViewFactory`, `PrefabViewFactory`, `IGroundFieldViewFactory`, `PrimitiveGroundFieldViewFactory`, `PrefabGroundFieldViewFactory`, `ProjectileView`

**Authoring:** `RecipeAsset`, `ModifierKit`, `ModifierKitLayer`, `MultiplyPatternAsset`, `GridMultiplyPattern`, `Recipe.Clone`, `Recipe.Append`, `Recipe.Compose`, `RecipeStage`, `ModifierStageAttribute`

Internal ECS types under `Runtime/Ecs/` are not a consumer API.

## Shot lifecycle events (presentation)

Since 0.1.12 the world reports each moment of a shot's life, so a game can play a visual effect or sound at each one. Everything is additive: `Hit`, `Expired`, and `StatusApplied` behave as before, and the new fields default to zero.

| Moment | Where to listen | What you get |
| --- | --- | --- |
| Fired or split | `Spawned` | `SpawnInfo`: `InstanceId`, `ParentId`, `Cause`, `Position`, `Direction`, `Recipe` |
| Homing locks on | `HomingLocked` | `HomingLockInfo`: `InstanceId`, `TargetId`, `Position`, `Kind` |
| Hits | `Hit` | `HitInfo.Outcome`: what happened to the shot |
| Ends | `Expired` | `ExpireInfo`: `Position`, `Direction`, `Reason` |
| Flies | your own `ProjectileView` | Start it on `Spawned`, stop it on `Expired` |

**`Spawned`** is raised once for every instance that becomes live: your own `Spawn` calls and every spawn the package makes (Multicast extras, Multiply children including those past a pierced target, other queued spawns). `Cause` is `Game`, `Multicast`, `Multiply`, or `Other`. `ParentId` is the instance that produced it, or `default` for a game spawn. Use it to start flight effects on a child, play a split effect at the parent, or link a child to its parent. `Direction` is the normalized launch direction after the Arc solve and before OnSpawn modifiers run. `Recipe.Id` and `Recipe.PresentationId` pick the effect. A Hitscan raises it before its `Hit` and `Expired`. An unreachable Arc returns `default` and raises nothing.

**`Expired`** now says why and where. `Reason`:

| `ExpireReason` | Meaning |
| --- | --- |
| `Lifetime` | `LifetimeSeconds` ran out |
| `Hit` | a Direct hit ended it (including a Hitscan ray that ended on a hit) |
| `Absorbed` | `ProtectionLevel.Strong` ate it. No `Hit` is raised |
| `Despawned` | the game called `Despawn(id)` |
| `HitscanEnd` | a Hitscan ray finished without a killing hit |
| `Other` | reserved |

`Position` and `Direction` are the last simulated pose, read before the view is released.

**`HitInfo.Outcome`** is set on Direct hits before `Hit` is raised:

| `HitOutcome` | Meaning |
| --- | --- |
| `Ended` | the shot ends (for a Beam pulse, the pulse ends and the Beam lives) |
| `Pierced` | Pierce spent a charge and the shot flies on |
| `Bounced` | Bounce spent a charge and reflected the shot |
| `Continued` | another modifier cancelled the despawn and the shot continues |
| `None` | not a Direct hit (Area, occupancy, ground field) |

**`HomingLocked`** is raised when a shot gets a new lock: assigned at spawn through `SpawnRequest.HomingTarget` (`Kind = Assigned`, right after that instance's `Spawned`), and on every acquire or re-acquire (`Kind = Acquired`). It is not raised on ticks where the shot keeps the same lock. A shot that is assigned a lock at fire time never acquires, so it raises exactly one `Assigned` event.

**Ordering you can rely on**

- `Spawned` comes before that instance's first `HomingLocked`, `Hit`, or `Expired`.
- A child's `Spawned` comes after the parent's `Hit` that made it.
- `Hit` comes before the same instance's `Expired`.

**Example**

```csharp
world.Spawned += s =>
{
    Vfx.PlayFlight(s.InstanceId, s.Recipe.PresentationId, s.Position, s.Direction);
    if (s.Cause == SpawnCause.Multiply)
        Vfx.PlaySplit(s.Position, s.Direction);
};

world.HomingLocked += l => Vfx.PlayLockOn(l.InstanceId, l.TargetId, l.Position);

world.Hit += h =>
{
    if (h.Source != HitSource.Direct) return;
    switch (h.Outcome)
    {
        case HitOutcome.Pierced: Vfx.PlayPassThrough(h.Point, h.Normal); break;
        case HitOutcome.Bounced: Vfx.PlayRicochet(h.Point, h.Normal); break;
        default: Vfx.PlayImpact(h.Point, h.Normal); break;
    }
};

world.Expired += e =>
{
    Vfx.StopFlight(e.InstanceId);
    if (e.Reason == ExpireReason.Lifetime)
        Vfx.PlayFizzle(e.Position, e.Direction);
};
```

Notes: the events are structs, so there is no per-event allocation. `Absorbed` expires have no `Hit`, so play the absorb effect from `Expired`. A Beam that is re-aimed with `AimBeam` can raise `HomingLocked` again. Ground-field and area-effect events are not part of this set.

## Installing

Requires Unity 6 LTS (`6000.6`) and `com.unity.entities`. MIT license.

**From OpenUPM** (recommended; Package Manager shows updates): add a scoped registry in **Project Settings → Package Manager** with URL `https://package.openupm.com` and scope `com.veerdonk`, then install **Projectile System** from **My Registries**. Or run `openupm add com.veerdonk.projectile-system`.

**Add package from git URL** (Package Manager → +). Locked to one commit; no update checks:

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
