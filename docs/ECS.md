# Hybrid ECS map

How this package uses DOTS. Not a Unity ECS tutorial. Read this if you already know MonoBehaviours and ordinary C#. Spec and API: [ARCHITECTURE.md](ARCHITECTURE.md). Decision: [DECISIONS.md](DECISIONS.md) ADR-005.

## Map

| Piece | Familiar analogue | In this package |
| --- | --- | --- |
| **DOTS** | A stack of libraries, not one feature | Entities + Burst + Collections + Mathematics. We use a slice. |
| **`World`** | Unit of work / a private database, **not** the scene | Created and disposed by `ProjectileWorld`. Isolated from Unity’s default ECS world. Not in the Player Loop. |
| **Entity** | A row id | One live (or pooled) instance. Not a GameObject. |
| **`IComponentData`** | Columns on that row; structs in contiguous chunks | `Projectile` (including remaining pierce/bounce, `ElapsedSeconds`, `IntervalSeconds`, multiply generations left, the homing lock id, combat stats copied from the Recipe: `Damage`, `CriticalChance`, `ManaCost`, `EffectRadius`, and `Spread` copied from the Recipe's `Multiply` modifiers), `ProjectilePosition`, `ProjectileVelocity`, `ProjectileCandidate` |
| **`Disabled`** | Soft-deleted / pooled row | Default queries skip it. Rent = enable; return = disable. No `Destroy` on despawn. |
| **System** | A pipeline stage over a query, not `Update` on each object | `LifetimeSystem` (Burst) then `ProjectileSimulationSystem` (managed) |
| **Burst** | AOT compiler for jobs over those structs (no GC, no managed refs) | Lifetime decrement + candidate position only |
| **`float3`** | `Vector3` for jobs (`Unity.Mathematics`) | Internal. Public API stays `Vector3`. |
| **Recipe / `IModifier`** | Ordinary classes (definition layer) | Cannot live on `IComponentData`. Entity holds `RecipeIndex`; `RecipeStore` looks up the Recipe. |

`IProjectileWorld` is still the façade. Sandbox, tests, and games call `Spawn` / `Tick` and listen to `Hit` / `Expired` / `StatusApplied`. They do not create entities.

## What we did not take

| Left out | Why |
| --- | --- |
| Unity Physics | Dummy, wall, and Play Mode hits stay PhysX colliders + `IDamageable`. |
| Entities Graphics | Visible spheres stay `ProjectileView` GameObjects. Tests can run with views off. |
| Baking / SubScenes | `RecipeAsset` is a ScriptableObject that builds a `Recipe`. Not ECS baker / SubScene authoring. |
| Default ECS world / Player Loop | Tests need an isolated `World`. Sandbox already ticks from one `Update`. |

Those can become later adapters without rewriting Recipes, stage lists, or `IProjectileWorld`.

## One tick

```text
SandboxLab.Update
  → IProjectileWorld.Tick(dt)
       → write SimDeltaTime singleton
       → SimulationSystemGroup.Update
            1. LifetimeSystem              Burst: remaining lifetime -= dt
            2. ProjectileSimulationSystem  managed:
                 snapshot entities whose lifetime <= 0; expire them (OnExpire, return to pool)
                 snapshot remaining live entities **once**
                 OnTick modifiers (same list); Homing acquires if unassigned (skips last Target hit while it stays ahead), then steers at n degrees/second
                 OnInterval: if the instance's interval elapsed this tick, run the whole list in order (more than once if `dt` skips a beat). `Multiply` is skipped when this instance's multiply generations are spent; `Speed` / `Damage` / `Critical` / `Mana` / `Size` / `Spread` / `EffectRadius` still run
                 Burst integrate: candidate = pos + vel * dt
                 move/hit on the same list; skip if a hit already disabled the entity
                 IHitQuery.TryCast (ScriptedHitQuery in Edit Mode, PhysicsHitQuery in Play Mode)
                 sample the instance combat stats (damage, crit chance, effect radius) onto that HitInfo
                 on hit: Hit event, OnHit, StatusApplied after OnHit once per ApplyStatus request on a Target
                 despawn unless Pierce/Bounce CancelDespawn
                 CancelDespawn: keep remaining counts, place at hit point along current velocity
                 killing hit: OnHit then StatusApplied then OnExpire (`HasHit` still true so Multiply uses the hit point/normal)
                 Multiply children spawn slightly off the surface along that normal and inherit the parent's ignored collider so they do not immediately overlap-hit the same dummy
                 else: Position = Candidate, sync ProjectileView
       → flush RequestSpawn queue (child Recipes)
       → tick live ground fields (ADR-022): decrement lifetime; OverlapDisk on plant and each interval; Area Hit / StatusApplied; disable view when lifetime ends
```

Spawn (`OnSpawn` / `OnTravel`) runs in `ProjectileWorld` when an entity is rented, not inside the Burst job. `Multicast` runs there too: it re-aims the rented entity through the modifier context and queues its extras, which `Spawn` flushes before it returns, so all `n` instances exist before the next `Tick`. Those queued requests carry a multicast budget: `Recipe.MulticastGenerations` (0 by default) counted down one per generation, so the extra's `OnSpawn` list normally runs with `Multicast` skipped. Remaining pierce/bounce are copied from `OnHit` `Pierce` / `Bounce` modifiers onto the `Projectile` row at rent. Combat stats (`Damage`, `CriticalChance`, `ManaCost`, `EffectRadius`) copy from the Recipe at rent; `Spread` copies from the last `Multiply` on `OnInterval` / `OnHit` / `OnExpire`. `OnTravel` `Damage` / `Critical` / `Mana` / `Size` / `Spread` / `EffectRadius` (and `Speed`) scale that copy before the first move. `Size` writes `Radius`; SphereCast and the GameObject view follow it. `ElapsedSeconds` starts at 0 and is incremented in the Burst lifetime job; `OnInterval` uses that clock, not a second remaining-lifetime field. The period it is measured against is `Projectile.IntervalSeconds`, written at spawn by `Interval` on `OnSpawn` and 0 (never fires) without one.

Do not iterate `SystemAPI.Query` while despawning (`Disabled` is a structural change). After expire, one list is reused for `OnTick`, `OnInterval`, and move/hit so a pierce/bounce kill mid-move cannot skip later entities.

## Files

| File | Role |
| --- | --- |
| `Runtime/ProjectileWorld.cs` | Façade: owns the ECS `World`, `Tick`, `Spawn`, `Dispose` |
| `Runtime/Ecs/ProjectileComponents.cs` | Component structs + delta-time singleton |
| `Runtime/Ecs/RecipeStore.cs` | RecipeIndex ↔ Recipe |
| `Runtime/Ecs/LifetimeSystem.cs` | Burst lifetime job |
| `Runtime/Ecs/ProjectileSimulationSystem.cs` | Stages, hits, pool return, views; one live snapshot after expire |
| `Runtime/Pierce.cs` | `OnHit` Target: consume remaining pierce, cancel despawn |
| `Runtime/Bounce.cs` | `OnHit` World or Target: consume remaining bounce, reflect velocity, cancel despawn; `Hit` still fires |
| `Runtime/Multiply.cs` | Explicit child Recipe; OnHit / OnExpire / OnInterval. Positions from `IMultiplyPattern` (default `SquarePattern`); children keep travel or hit-normal direction. `Spread` is meters between pips (0 stacks). Children multiply again only if `Recipe.MultiplyGenerations` pays for it |
| `Runtime/ChainBudget.cs` | The counted-down "how many more generations" value both chaining modifiers ride on |
| `Runtime/Multicast.cs` | `OnSpawn`: total instances is `n`; extras use an explicit sibling Recipe and only multicast again if `Recipe.MulticastGenerations` pays for it |
| `Runtime/IMulticastPattern.cs` | Where multicast directions come from. `RandomConePattern` (v1 default) owns the cone math |
| `Runtime/IMultiplyPattern.cs` | Where Multiply child offsets come from. Plane is perpendicular to travel or the hit normal; local +X right, +Y up |
| `Runtime/SquarePattern.cs` | Dice pips through 9, then odd-sized grids. Default Multiply pattern |
| `Runtime/CirclePattern.cs` | Triangle, then center + ring, then inner hexagon and outer rings |
| `Runtime/RandomCirclePattern.cs` | Uniform disk of radius Spread; uses the world seed; rejects samples closer than the child diameter |
| `Runtime/Homing.cs` | `OnTick`: steer at n degrees/second toward a lock. Acquires via `IHomingAcquisition` when unassigned. A Target hit drops the lock (`Homing.ReleaseLockOnTargetHit` after `OnHit`) |
| `Runtime/Speed.cs` | Scale current speed by n, keep travel direction. `OnTravel` start scale, `OnInterval` progressive, `OnHit` after bounce/pierce. Scale <= 0 is a no-op |
| `Runtime/Damage.cs` | Scale instance damage by n. Same stages as `Speed`. Does not apply HP. OnHit scales do not rewrite the `Hit` that already fired |
| `Runtime/Critical.cs` | Scale instance critical chance by n, then clamp to [0, 1]. Same stages as `Speed`. Does not roll a crit |
| `Runtime/Mana.cs` | Scale the instance mana-cost copy by n. Same stages as `Speed`. Does not change `Recipe.ManaCost` or spend mana |
| `Runtime/Size.cs` | Scale instance `Radius` by n. Same stages as `Speed`. SphereCast and the view follow the new radius |
| `Runtime/Spread.cs` | Scale the instance copy of `Multiply.Spread` by n. Same stages as `Speed`. Does not jitter the original shot |
| `Runtime/EffectRadius.cs` | Scale instance effect radius by n. Same stages as `Speed`. Does not run an overlap query |
| `Runtime/Interval.cs` | `OnSpawn`: seconds between `OnInterval` runs, onto `Projectile.IntervalSeconds`. One period per instance drives the whole list |
| `Runtime/ApplyStatus.cs` | `OnHit` Target: request an `IStatusEffect`. Each request on the list is flushed as `StatusApplied` after the list, in order. World hits do nothing |
| `Runtime/IStatusEffect.cs` | Effect contract plus v1 `DummyStatusEffect` (`Id` `"dummy"`). Package does not tick DoT |
| `Runtime/IStatusReceiver.cs` | Game/sandbox apply contract. Package never calls it; sandbox maps `StatusApplied` → `Apply` |
| `Runtime/IHomingAcquisition.cs` | How an unassigned instance picks a lock. `HomingCatalog` holds published poses; `ClosestInTravelDirectionAcquisition` (v1 default) takes the nearest in-front candidate, skipping `HomingIgnoredTarget` |
| `Runtime/ProjectilePool.cs` | Entity recycle + optional GameObject views |
| `Runtime/PhysicsHitQuery.cs` | PhysX SphereCast / overlap; ignore last collider so pierce/bounce can leave the surface |
| `Runtime/ProjectileView.cs` | Presentation adapter (MonoBehaviour) |

`Dispose()` exists because an Entities `World` allocates native containers. Treat it like disposing a host that owns unmanaged memory: sandbox `OnDestroy`, tests `using` / `finally`.
