# Hybrid ECS map

How this package uses DOTS. Not a Unity ECS tutorial. Read this if you already know MonoBehaviours and ordinary C#. Spec and API: [ARCHITECTURE.md](ARCHITECTURE.md). Decision: [DECISIONS.md](DECISIONS.md) ADR-005.

## Map

| Piece | Familiar analogue | In this package |
| --- | --- | --- |
| **DOTS** | A stack of libraries, not one feature | Entities + Burst + Collections + Mathematics. We use a slice. |
| **`World`** | Unit of work / a private database, **not** the scene | Created and disposed by `ProjectileWorld`. Isolated from Unity’s default ECS world. Not in the Player Loop. |
| **Entity** | A row id | One live (or pooled) instance. Not a GameObject. |
| **`IComponentData`** | Columns on that row; structs in contiguous chunks | `Projectile` (including remaining pierce/bounce, `ElapsedSeconds`, `IntervalSeconds`, multiply generations left, the homing lock id, combat stats copied from the Recipe: `Damage`, `CriticalChance`, `ManaCost`, `EffectRadius`, `Spread` copied from the Recipe's `Multiply` modifiers, `Gravity` copied from `Recipe.Gravity`, and `Delivery`). Beam pulse clock is phase 32. `ProjectilePosition`, `ProjectileVelocity`, `ProjectileCandidate` |
| **`Disabled`** | Soft-deleted / pooled row | Default queries skip it. Rent = enable; return = disable. No `Destroy` on despawn. |
| **System** | A pipeline stage over a query, not `Update` on each object | `LifetimeSystem` (Burst) then `ProjectileSimulationSystem` (managed) |
| **Burst** | AOT compiler for jobs over those structs (no GC, no managed refs) | Lifetime decrement + kinematic integrate (gravity then `pos + vel * dt`) |
| **`float3`** | `Vector3` for jobs (`Unity.Mathematics`) | Internal. Public API stays `Vector3`. |
| **Recipe / `IModifier`** | Ordinary classes (definition layer) | Cannot live on `IComponentData`. Entity holds `RecipeIndex`; `RecipeStore` looks up the Recipe. |

`IProjectileWorld` is still the façade. Sandbox, tests, and games call `Spawn` / `Tick` and listen to `Hit` / `Expired` / `StatusApplied` / `Spawned` / `HomingLocked`. They do not create entities.

## What we did not take

| Left out | Why |
| --- | --- |
| Unity Physics | Dummy, wall, and Play Mode hits stay PhysX colliders + `IDamageable`. Published hit volumes (ADR-030) are an extra Target source, not Unity Physics. |
| Entities Graphics | Visible spheres stay `ProjectileView` GameObjects. Tests can run with views off. |
| Baking / SubScenes | `RecipeAsset` is a ScriptableObject that builds a `Recipe`. Not ECS baker / SubScene authoring. |
| Default ECS world / Player Loop | Tests need an isolated `World`. Sandbox already ticks from one `Update`. |

Those can become later adapters without rewriting Recipes, stage lists, or `IProjectileWorld`.

## One tick

```text
SampleLab.Update
  → IProjectileWorld.Tick(dt)
       → write SimDeltaTime singleton
       → SimulationSystemGroup.Update
            1. LifetimeSystem              Burst: remaining lifetime -= dt
            2. ProjectileSimulationSystem  managed:
                 snapshot entities whose lifetime <= 0; expire them (OnExpire, return to pool)
                 snapshot remaining live entities **once**
                 OnTick modifiers (same list); Homing acquires if unassigned (skips last Target hit while it stays ahead), then steers at n degrees/second. Acquisition and steering read published hit-target poses as well as homing candidates (same id is one candidate; the hit-target position wins). AreaEffect on OnTick overlaps after that list (occupancy enter/exit, or a pulse every Tick)
                 OnInterval: if the instance's interval elapsed this tick, run the whole list in order (more than once if `dt` skips a beat). `Multiply` is skipped when this instance's multiply generations are spent; `Speed` / `Damage` / `Critical` / `Mana` / `Size` / `Spread` / `EffectRadius` / `Gravity` still run. `AreaEffect` overlaps at the instance position after each crossing (before integrate)
                 Burst integrate: **Arc only** — if Gravity > 0, velocity += (0,-1,0) * gravity * dt; then candidate = pos + vel * dt (Straight / Gravity <= 0 leave velocity unchanged). Hitscan does not reach this step (resolved in Spawn). Beam skips integrate (origin stays; lifetime still decrements)
                 move/hit on the same list; skip if a hit already disabled the entity
                 IHitQuery.TryCast (ScriptedHitQuery in Edit Mode, PhysicsHitQuery in Play Mode), nearest of PhysX and published hit spheres (ADR-030)
                 sample the instance combat stats (damage, crit chance, effect radius) onto that HitInfo; Source = Direct
                 on hit: OnHit, then Direct Hit unless AreaEffect suppressed it, then Direct StatusApplied unless suppressed
                 then IHitQuery.Overlap for each queued AreaEffect; Area Hit / StatusApplied per Target (World colliders ignored)
                 despawn unless Pierce/Bounce CancelDespawn
                 CancelDespawn: keep remaining counts, place at hit point along current velocity
                 killing hit: OnHit then Direct Hit/status then Area flush then OnExpire (`HasHit` still true so Multiply uses the hit point/normal)
                 Multiply children spawn slightly off the surface along that normal and inherit the parent's ignored collider so they do not immediately overlap-hit the same dummy
                 else: Position = Candidate, sync ProjectileView
       → flush RequestSpawn queue (child Recipes)
       → tick live ground fields (ADR-022): decrement lifetime; OverlapDisk on plant and each interval, or occupancy enter/exit each tick when that flag is set (ADR-031); Area / Occupancy Hit and StatusApplied on enter; disable view when lifetime ends. Each field has a `GroundFieldId`; `GroundFieldPlanted` / `GroundFieldPulsed` (after that pulse's Area Hits, even when empty) / `GroundFieldEnded` are raised from here (ADR-040). `AreaBurst` is raised from the area flush before its Area Hits (occupancy: only when first made)
```

Spawn (`OnSpawn` / `OnTravel`) runs in `ProjectileWorld` when an entity is rented, not inside the Burst job. **IgnoreCollider** (ADR-037): at rent, before `OnSpawn`, write the collider into `PhysicsHitQuery` so the first Direct overlap/sweep skips it; multicast extras `CopyIgnore` that skip. **Arc** (ADR-035): if `HomingAcquirePointSet`, `BallisticAim.TrySolve` runs at rent **before** `OnSpawn` so Multicast extras fan around the loft; unreachable returns default `ProjectileId` without renting. **Hitscan:** after `OnSpawn` / `OnTravel`, one polyline of `Radius` SphereCasts (PhysX + published volumes) runs inside `Spawn`; Homing `n` is degrees of kink; World ends unless Bounce; Pierce n+1; then OnExpire / pool before `Spawn` returns — no view. `AreaEffect` on `OnTravel` flushes its overlap there, before the first move. `Multicast` runs there too: it re-aims the rented entity through the modifier context and queues its extras, which `Spawn` flushes before it returns, so all `n` instances exist before the next `Tick`. Those queued requests carry a multicast budget: `Recipe.MulticastGenerations` (0 by default) counted down one per generation, so the extra's `OnSpawn` list normally runs with `Multicast` skipped. Remaining pierce/bounce are copied from `OnHit` `Pierce` / `Bounce` modifiers onto the `Projectile` row at rent. Combat stats (`Damage`, `CriticalChance`, `ManaCost`, `EffectRadius`) copy from the Recipe at rent; `Spread` copies from the last `Multiply` on `OnInterval` / `OnHit` / `OnExpire`; `Gravity` copies from `Recipe.Gravity` (default 0) but Burst applies it only for Arc. `OnTravel` `Damage` / `Critical` / `Mana` / `Size` / `Spread` / `EffectRadius` / `Gravity` (and `Speed`) scale that copy before the first move. `Size` writes `Radius`; SphereCast and the GameObject view follow it. `ElapsedSeconds` starts at 0 and is incremented in the Burst lifetime job; `OnInterval` uses that clock, not a second remaining-lifetime field. The period it is measured against is `Projectile.IntervalSeconds`, written at spawn by `Interval` on `OnSpawn` and 0 (never fires) without one. **Beam** (ADR-036): lifetime still ticks; integrate and kinematic move are skipped; OnTick Homing is not steering. A homing pulse steps the polyline and turns at degrees per second (`dt = distance / speed`). The first step is straight along the muzzle. The lock is kept across redraws while that target stays in front of the muzzle; a Target hit drops it for the rest of that polyline, including a silent redraw. A lock set through the four-argument `AimBeam` (`BeamSetLockSet` / `BeamSetLockId`, ADR-044) is restored at the start of every redraw and pulse whether or not it is ahead; `default` clears it and the spawn-assigned flag, so the Beam self-acquires again. Each Beam pulse and Hitscan query is capped at `Recipe.MaxRange` (`<= 0` is 10 km; ADR-043): Pierce, Bounce, and curve steps share the one budget. Segment cylinders have no colliders. Hitscan kink stays `dt = 1`. The first pulse runs inside `Spawn` after OnSpawn / OnTravel. Later pulses run when `BeamPulseElapsed` (lifetime job) reaches `Recipe.BeamPulseSeconds`, or every Tick when that period is `<= 0`. Each pulse resets Pierce/Bounce from the Recipe, rebuilds the polyline from the anchored spawn pose, raises synthetic Direct OnHit, and restores the pose. Without Pierce the pulse stops on the first Target; the next pulse can hit that Target again. Pierce n+1 ends the pulse only. The cylinder mesh stays up between pulses. `Despawn` is OnExpire + pool from the façade and does not require Beam.

Do not iterate `SystemAPI.Query` while despawning (`Disabled` is a structural change). After expire, one list is reused for `OnTick`, `OnInterval`, and move/hit so a pierce/bounce kill mid-move cannot skip later entities.

## Files

| File | Role |
| --- | --- |
| `Runtime/ProjectileWorld.cs` | Façade: owns the ECS `World`, `Tick`, `Spawn`, `Despawn`, `Dispose`. Arc Spawn calls `BallisticAim.TrySolve` before OnSpawn. Hitscan resolves inside `Spawn`. Beam anchors pose, pulses inside `Spawn`, and `Despawn` expires any live id. `AimBeam` redraws; the four-argument overload also sets the Beam's homing lock (ADR-044) |
| `Runtime/Ecs/ProjectileComponents.cs` | Component structs + delta-time singleton. `Delivery`, `BeamOrigin` / `BeamDirection`, `BeamPulseElapsed`, and the set lock (`BeamSetLockSet` / `BeamSetLockId`) live on the `Projectile` row |
| `Runtime/Ecs/RecipeStore.cs` | RecipeIndex ↔ Recipe. Keyed by `Recipe.Id`; a later `Spawn` with the same id **replaces** the slot (live instances read the new lists). Phase 27 compositions must pass a new id |
| `Runtime/ModifierKit.cs` | ScriptableObject layers (`RecipeStage` + modifiers) appended onto a cloned base Recipe (ADR-032). Not a second façade |
| `Runtime/Ecs/LifetimeSystem.cs` | Burst lifetime job |
| `Runtime/Ecs/ProjectileSimulationSystem.cs` | Stages, hits, pool return, views; one live snapshot after expire |
| `Runtime/Pierce.cs` | `OnHit` Target: consume remaining pierce, cancel despawn |
| `Runtime/Bounce.cs` | `OnHit` World or Target: consume remaining bounce, reflect velocity, cancel despawn; `Hit` still fires |
| `Runtime/Multiply.cs` | Explicit child Recipe; OnHit / OnExpire / OnInterval. Positions from `IMultiplyPattern` (default `SquarePattern`). Procedural patterns keep travel or hit-normal direction; `Spread` is meters between pips (0 stacks). Authored grid also aims via `GetDirection` (`Spread` is cone degrees; placement is Spacing). Children multiply again only if `Recipe.MultiplyGenerations` pays for it |
| `Runtime/ChainBudget.cs` | The counted-down "how many more generations" value both chaining modifiers ride on |
| `Runtime/Multicast.cs` | `OnSpawn`: total instances is `n`; extras use an explicit sibling Recipe and only multicast again if `Recipe.MulticastGenerations` pays for it |
| `Runtime/IMulticastPattern.cs` | Where multicast directions come from. `RandomConePattern` (v1 default) owns the cone math |
| `Runtime/IMultiplyPattern.cs` | Where Multiply child offsets (and grid aim) come from. Plane is perpendicular to travel or the hit normal; local +X right, +Y up. `GetOffset` / `GetDirection` / `ResolveCount` (ADR-038) |
| `Runtime/SquarePattern.cs` | Dice pips through 9, then odd-sized grids. Default Multiply pattern. Place-only; Spread is meters |
| `Runtime/CirclePattern.cs` | Triangle, then center + ring, then inner hexagon and outer rings |
| `Runtime/RandomCirclePattern.cs` | Uniform disk of radius Spread; uses the world seed; rejects samples closer than the child diameter |
| `Runtime/GridMultiplyPattern.cs` | Serializable copy of a `MultiplyPatternAsset`: Spacing + numbered cells. Places from geometric center; aims a cone with `Multiply.Spread` as degrees |
| `Runtime/MultiplyPatternAsset.cs` | ScriptableObject grid (`Create > Projectile > Multiply Pattern`). Not on the live Recipe; pick copies onto `GridMultiplyPattern` |
| `Runtime/Homing.cs` | Straight / Arc `OnTick`: steer at n degrees/second toward a lock. Hitscan: n is max degrees of kink per polyline segment (ADR-035). Beam: the same degrees/second, stepped by distance / speed (ADR-036); a game-set lock (four-argument `AimBeam`, ADR-044) is restored each pulse and still steers under the same cap. First acquire may use `SpawnRequest.HomingAcquirePoint` (`ClosestToPointAcquisition`, ADR-033). Later empty locks use `IHomingAcquisition` (default travel). A Target hit drops the lock (`Homing.ReleaseLockOnTargetHit` after `OnHit`). Never acquires `ModifierContext.ShooterTarget` (ADR-046) |
| `Runtime/Speed.cs` | Scale current speed by n, keep travel direction. `OnTravel` start scale, `OnInterval` progressive, `OnHit` after bounce/pierce. Scale <= 0 is a no-op |
| `Runtime/Gravity.cs` | Scale instance gravity by n. Same stages as `Speed`. Does not add `g * dt`; Burst still integrates, and only when `Delivery` is Arc |
| `Runtime/BallisticAim.cs` | Helper. `TrySolve` returns the launch direction speed + gravity need to pass through a point (world down, flatter/shorter-time root: extra speed → nearly straight, level min speed → ~45°). Arc `Spawn` calls it when the aim point is set (ADR-035). Not a modifier, not a flight path, not a Range stat |
| `Runtime/Damage.cs` | Scale instance damage by n. Same stages as `Speed`. Does not apply HP. OnHit scales do not rewrite the sampled Direct `HitInfo` |
| `Runtime/Critical.cs` | Scale instance critical chance by n, then clamp to [0, 1]. Same stages as `Speed`. Does not roll a crit |
| `Runtime/Mana.cs` | Scale the instance mana-cost copy by n. Same stages as `Speed`. Does not change `Recipe.ManaCost` or spend mana |
| `Runtime/Size.cs` | Scale instance `Radius` by n. Same stages as `Speed`. SphereCast and the view follow the new radius |
| `Runtime/Spread.cs` | Scale the instance copy of `Multiply.Spread` by n (meters on Square / Circle / RandomCircle, cone degrees on a grid). Same stages as `Speed`. Does not jitter the original shot or scale grid Spacing |
| `Runtime/EffectRadius.cs` | Scale instance effect radius by n. Same stages as `Speed`. Does not run an overlap query |
| `Runtime/Interval.cs` | `OnSpawn`: seconds between `OnInterval` runs, onto `Projectile.IntervalSeconds`. One period per instance drives the whole list |
| `Runtime/ApplyStatus.cs` | `OnHit` Target: request an `IStatusEffect`. Each request on the list is flushed as Direct `StatusApplied` after the list, in order, unless suppressed. World hits do nothing |
| `Runtime/AreaEffect.cs` | Overlap Targets in instance `EffectRadius`. Own damage/status. OnHit at the contact point, OnTravel at spawn, OnInterval and OnTick auras on the flying instance. `occupancy` tracks enter/exit instead of pulses. Queues on `ModifierContext`; does not call Physics |
| `Runtime/GroundField.cs` | Plants a lingering horizontal disk on OnHit / OnExpire / OnTravel. Own radius, lifetime, damage, optional status, pulse period, occupancy. Owned by `ProjectileWorld`, ticked from the same `Tick`. Not a Recipe |
| `Runtime/IGroundFieldViewFactory.cs` | `Create(radius, poolParent)` plus default `PrimitiveGroundFieldViewFactory` (thin cylinder scaled to `2 * radius` on XZ) |
| `Runtime/PrefabGroundFieldViewFactory.cs` | Map `GroundField.PresentationId` → prefab; does not rescale; unknown ids fall back to the primitive |
| `Runtime/IStatusEffect.cs` | Effect contract plus v1 `DummyStatusEffect` (`Id` `"dummy"`). Package does not tick DoT |
| `Runtime/IStatusReceiver.cs` | Game/sandbox apply contract. Package never calls it; sandbox maps `StatusApplied` → `Apply` |
| `Runtime/IHomingAcquisition.cs` | How an unassigned instance picks a lock. `HomingCatalog` holds `SetHomingCandidate` poses (id→index map, ADR-034). Acquisition also reads hit-target positions; the same id is one candidate and the hit-target position wins. `ClosestInTravelDirectionAcquisition` (v1 default) takes the nearest in-front candidate, skipping `HomingIgnoredTarget`. `ClosestToPointAcquisition` (ADR-033) takes the nearest to a fire-time world point, no in-front filter. The projectile row stores the point and that the aim-point pick already ran (`HomingAimPointUsed`) |
| `Runtime/ProjectilePool.cs` | Entity recycle + optional GameObject views from `IProjectileViewFactory` |
| `Runtime/HitTargetCatalog.cs` | Published spheres and upright capsules from `SetHitTarget` / `RemoveHitTarget`, including `ProtectionLevel`. Each is an axis segment (zero length for a sphere), a radius, and a homing point (ADR-045). Direct sweep, area sphere, and ground disk test the segment; Homing reads the homing point. A spawn `IgnoreTarget` (ADR-046) is its own per-instance slot: skipped by the Direct sweep until the body clears it, re-armed by `ClearIgnore` (each Beam pulse), copied to children, and read by `ModifierContext.ShooterTarget`. List order is `TryGet` order; a `Dictionary<TargetId, int>` makes Set/Contains/TryGetPosition by id average O(1) (ADR-034). Remove leaves a dead slot; the next list read compacts once, keeping order (not swap) |
| `Runtime/PhysicsHitQuery.cs` | PhysX SphereCast / overlap plus published hit volumes (ADR-030). Ignore last collider / TargetId so pierce/bounce can leave the surface. `SpawnRequest.IgnoreCollider` (ADR-037) seeds that collider skip at rent, before the first cast. `IgnoreColliders` (ADR-041) is a pooled copy of a set, skipped for the whole shot life. `Overlap` / `OverlapDisk` return `IDamageable` colliders and published Targets |
| `Runtime/IProjectileViewFactory.cs` | `Create(recipe, poolParent)` plus default `PrimitiveViewFactory` (sphere scaled to `2 * Radius`) |
| `Runtime/PrefabViewFactory.cs` | Map `PresentationId` → prefab; does not rescale; unknown ids fall back to the primitive factory |
| `Runtime/ProjectileView.cs` | Public presentation adapter (`Bind` / `SetPose` / `Release`). Prefab views keep authored scale; primitive views follow `Radius` |

`Dispose()` exists because an Entities `World` allocates native containers. Treat it like disposing a host that owns unmanaged memory: sandbox `OnDestroy`, tests `using` / `finally`.
