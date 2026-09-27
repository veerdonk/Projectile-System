# Architecture

Simulation state is **ECS components** in a package-owned Entities `World`. MonoBehaviours are thin adapters (colliders, presentation). Hits stay PhysX; views stay GameObjects. Recipes and modifiers stay managed classes. Unity Physics and Entities Graphics can replace those adapters later without rewriting Recipes, modifiers, or `IProjectileWorld`.

How to read the ECS pieces: [ECS.md](ECS.md). Decision: [DECISIONS.md](DECISIONS.md) ADR-005.

Do not add a second composition model. Stage lists are the only one ([DECISIONS.md](DECISIONS.md)).

## Layers

| Layer | Lives in | Responsibility |
| --- | --- | --- |
| Definition | ScriptableObjects / serializable data | `RecipeAsset` authors a `Recipe`: base stats + six ordered modifier lists ([DECISIONS.md](DECISIONS.md) ADR-023) |
| Simulation | Internal `IComponentData` in an isolated Entities `World` | Position, velocity, remaining lifetime, pierce/bounce remaining, interval clocks, combat stats that can change in flight. No `UnityEngine.Object` fields |
| Movement | Swappable solver | Kinematic first (Burst integrate); optional Rigidbody later |
| Hits | Query + callback pipeline | PhysX SphereCast / overlap plus published hit volumes (ADR-030); `HitInfo` events, not `OnCollisionEnter` |
| Presentation | Views bound to instance id | Mesh, trail, VFX GameObjects; may be disabled in tests |
| Pooling | Package runtime | Rent / return by Recipe id: enable/disable entities; recycle views |

## Six stage lists

Every Recipe has these lists, each ordered; modifiers in a list run in index order.

| Stage | When it runs | Typical modifiers |
| --- | --- | --- |
| `OnSpawn` | Once when the instance is created | `Multicast(n)`, `Interval(n)` |
| `OnTravel` | Once after spawn, before the first move | `Speed(n)`, `Damage(n)`, `Critical(n)`, `Mana(n)`, `Spread(n)`, `EffectRadius(n)`, `Size(n)`, `Gravity(n)`, `AreaEffect`, `GroundField` |
| `OnTick` | Every `ProjectileWorld.Tick` | `Homing(n)`, `AreaEffect` (phase 26) |
| `OnInterval` | Each time the instance’s interval elapses during flight | `Multiply(n)`, scale modifiers, `AreaEffect` (aura) |
| `OnHit` | On each hit from the pipeline | `Pierce`, `Bounce`, `Multiply` spawn, scale modifiers, `ApplyStatus`, `AreaEffect`, `GroundField` |
| `OnExpire` | When the instance is ending | `Multiply` on death, `GroundField` |

**Hit then expire:** a hit that despawns the instance runs `OnHit` first, then `OnExpire`.

**Pierce / bounce** are `OnHit` modifiers. They may **cancel despawn** (see Hit pipeline). Pierce inspects `HitInfo.Kind` (`Target` only). Bounce reflects any hit.

`OnTravel` is one-shot setup. Continuous steering is `OnTick`. Periodic spawn or stat scale is `OnInterval`, not `OnTravel`. `Speed(n)` (and the other scale modifiers) on `OnTravel` is the spawn-time start scale; do not put them on `OnSpawn`.

**Interval:** the seconds between `OnInterval` runs belong to the instance, not to the modifiers on the list. `Interval(n)` on `OnSpawn` sets that one period and every modifier on `OnInterval` fires on those crossings, in list order ([DECISIONS.md](DECISIONS.md) ADR-012). A Recipe with no `Interval(n)` never runs `OnInterval`. `Interval` is not a despawn clock; `LifetimeSeconds` still is.

## Recipe

```text
Recipe
  Id
  Delivery                 // Straight (default), Arc, Hitscan; Beam in phase 31
  Speed, Radius, LifetimeSeconds, Gravity
  BeamPulseSeconds         // phase 31; Beam only
  Damage, CriticalChance, ManaCost, EffectRadius
  PresentationId
  OnSpawn[], OnTravel[], OnTick[], OnInterval[], OnHit[], OnExpire[]
```

`LifetimeSeconds` is the only despawn clock. When remaining lifetime reaches 0, run `OnExpire` and return the instance to the pool. `Despawn` (phase 31) is an explicit expire, not a second clock.

**Authoring:** `RecipeAsset` is the Unity asset ([DECISIONS.md](DECISIONS.md) ADR-023). Tests and `Spawn` keep using `Recipe`. `ToRecipe()` copies stats and builds modifier instances. Multiply/Multicast children on the asset are other `RecipeAsset` references, resolved on build. Do not put `RecipeAsset` on `SpawnRequest`.

**Modifier kits:** a game may treat one Recipe as a **base** and append extras at fire time ([DECISIONS.md](DECISIONS.md) ADR-032). `ModifierKit` is a ScriptableObject of layers (`RecipeStage` + modifiers), not a second Recipe and not stats. `Recipe.Clone()`, `Append`, `ModifierKit.ApplyTo`, and `Recipe.Compose(basis, id, kits)` clone then append. Each composition needs its own `Recipe.Id` (`RecipeStore` replaces the slot when the same id is spawned again). Kits do not insert `Interval(n)` for you.

**Delivery:** `Recipe.Delivery` is how the shot is delivered ([DECISIONS.md](DECISIONS.md) ADR-035, ADR-036). **Straight** is today’s kinematic SphereCast (no gravity). **Arc** is kinematic with gravity; `Spawn` calls `BallisticAim.TrySolve` when `HomingAcquirePointSet` (unreachable → no spawn). **Hitscan** is one `Radius` polyline query inside `Spawn`, then expire — no view, World ends the ray unless Bounce, Pierce n+1, Homing `n` is max degrees of kink. **Beam** (phase 31) is a live instance that pulses that polyline until lifetime or `Despawn`; Homing kink and Pierce n+1 are per pulse; World clips a pulse.

**Gravity:** `Recipe.Gravity` is downward acceleration in m/s², default 0 ([DECISIONS.md](DECISIONS.md) ADR-017). Burst integrates it each tick **only when Delivery is Arc** (ADR-035); `Gravity(n)` scales the instance value (ADR-018). Aim-to-point is `BallisticAim.TrySolve` (ADR-019, ADR-028): Arc `Spawn` calls it; games may still call it for barrel preview. Fast → nearly straight, barely enough on the level → ~45°. Not a baked curve and not an authored Range.

## Modifier contract

A modifier may:

- Read instance simulation state
- Mutate motion fields (velocity, speed, radius, gravity) and remaining pierce/bounce/interval
- Mutate combat stats on the instance (damage, critical chance, effect radius) and multiply spread scale
- Cancel despawn on the current hit
- Request spawns of **explicit child Recipes** (never implied inherit)
- Request status effects on the current `Target` hit (flushed as `StatusApplied` after `OnHit`, one event per request, in list order; see ADR-011)
- Request an area overlap (flushed after the stage; see ADR-016) and suppress the direct contact `Hit` / `ApplyStatus`
- Request a lingering ground field (flushed after the stage; see ADR-022)

A modifier may not:

- Touch Unity objects, presentation, or the pool directly
- Call `Instantiate` / `Destroy`
- Subscribe to Unity messages

Magnitude meaning is per modifier (count vs strength vs scale) as in [REQUIREMENTS.md](REQUIREMENTS.md).

**Child Recipes:** each spawn modifier carries the Recipe to spawn. Phase 1 `OnHit` spawn takes that Recipe in the modifier. Later modifiers decide their own copy rules; default remains “explicit Recipe, no inherit.”

**Multicast:** `Multicast(n)` on `OnSpawn` requests `n-1` extra instances from an explicit sibling Recipe. By default extra instances do **not** run `Multicast` again: the spawn path skips it for them, so a sibling Recipe that carries one cannot explode. `Recipe.MulticastGenerations` buys further generations, counted down per extra so a chain always terminates ([DECISIONS.md](DECISIONS.md) ADR-007). `Recipe.MultiplyGenerations` is the same budget for `Multiply` children, which likewise do not inherit multiply by default.

**Prewarm:** `Prewarm(recipe, count)` also prewarms the Recipes those modifiers chain into, using the same generations ([DECISIONS.md](DECISIONS.md) ADR-008). Do not hand-prewarm the children as well. Directions for all `n` slots, the original included, come from an `IMulticastPattern` ([DECISIONS.md](DECISIONS.md) ADR-006); v1 ships `RandomConePattern`.

**Multiply spread:** `Multiply` authors `Spread` as meters between adjacent pips on an `IMultiplyPattern` ([DECISIONS.md](DECISIONS.md) ADR-015). Default `SquarePattern`. `Spread = 0` stacks. Children keep travel / hit-normal direction. `Spread(n)` scales the instance copy of that spacing (phase 12). The original shot is not jittered.

**Views:** the pool asks `IProjectileViewFactory` for a GameObject view from `Recipe.PresentationId` ([DECISIONS.md](DECISIONS.md) ADR-021). Default is a sphere primitive. Hits stay SphereCast of `Radius`; the mesh is not the collider. Hitscan creates **no** view (ADR-035). Beam uses a primitive **cylinder per polyline segment** (ADR-036); sample red via `PresentationId`. Ground-field disks use a separate `IGroundFieldViewFactory` (ADR-026); default is a thin cylinder. A game prefab is not rescaled.

**Area effect:** `AreaEffect` on `OnHit` / `OnTravel` / `OnInterval` / `OnTick` overlaps Targets in instance `EffectRadius` ([DECISIONS.md](DECISIONS.md) ADR-016, ADR-031). Area damage is on the modifier. Interval and OnTick are auras on the live instance (OnTick overlaps every Tick). Direct contact can be suppressed. Overlap includes `IDamageable` colliders **and** published hit volumes (ADR-030). `occupancy` (default false) replaces pulses with `HitSource.OccupancyEnter` / `OccupancyExit` (ADR-031).

**Ground field:** `GroundField` on `OnHit` / `OnExpire` / `OnTravel` plants a lingering horizontal disk ([DECISIONS.md](DECISIONS.md) ADR-022). Size, lifetime, damage, optional status, and pulse period are on the modifier. The disk is not a Recipe and has no stage lists. `IHitQuery.OverlapDisk` tests XZ distance (colliders and published volumes, ADR-030); pulses reuse Area `Hit` / `StatusApplied`. `occupancy` (default false) replaces those pulses with enter/exit (ADR-031). Views come from `IGroundFieldViewFactory` (ADR-026).

## Hit pipeline

Kinematic movement casts along the step (PhysX SphereCast / overlap **and** published hit volumes, ADR-030). Each contact produces:

```csharp
enum HitKind { Target, World }

enum HitSource { Direct, Area, OccupancyEnter, OccupancyExit } // ADR-031

enum ProtectionLevel { None, Basic, Strong }

struct HitInfo
{
    ProjectileId InstanceId;
    HitKind Kind;
    HitSource Source;       // Direct = contact; Area = pulse; OccupancyEnter/Exit = ADR-031
    ProtectionLevel Protection; // None, Basic, or Strong (ADR-020)
    Vector3 Point;
    Vector3 Normal;
    // TargetId is a sandbox/game id; package stores it opaquely
    float Damage;           // Direct: instance value sampled at contact. Area: AreaEffect.Damage or GroundField.Damage
    float CriticalChance;   // 0–1; package does not roll
    float EffectRadius;     // instance value; AreaEffect overlaps this radius (GroundField uses its own radius)
}
```

- `Target` — `IDamageable` collider **or** a published hit volume (ADR-030). Default: raise Direct `Hit` after `OnHit` unless suppressed, despawn unless `Pierce` or `Bounce` cancels despawn. Bounce still raises Direct `Hit` so dummy HP drops. `ApplyStatus` may also request a status; that is raised as `StatusApplied` **after** the `OnHit` list, not on `Hit`. `AreaEffect` may add Area Hits after that. `GroundField` may plant a disk at `Hit.Point` (Y snapped to the ground plane)
- `World` — environment. Default: despawn unless `Bounce` cancels despawn and reflects velocity around `Normal`. `ApplyStatus` ignores World. `AreaEffect` on a World hit still overlaps nearby Targets. `GroundField` on a World hit still plants at `Hit.Point` (Y snapped to the ground plane)
- `Protection = Basic` — `OnHit` still runs (pierce and bounce consume; pierce also applies when Kind is World). `AreaEffect` still runs. `GroundField` still plants. No `ApplyStatus` for this contact. Direct `Hit` is raised with `Protection = Basic` (game/sandbox skip HP)
- `Protection = Strong` — despawn with no Direct `Hit`, no `OnHit`, no `OnExpire`. `Expired` still fires. No field is planted
- Projectile-vs-projectile: ignore until an ADR.

The package does **not** apply HP, roll crits, spend mana, or tick DoT. Direct contact `HitInfo` is sampled before `OnHit` and raised after that list (ADR-016) so `AreaEffect` can suppress it; sampled damage still ignores OnHit `Damage(n)` on this contact. Sandbox listens to `Hit` for dummy health (`Hit.Damage`) when `Protection == None`, and to `StatusApplied` to call `IStatusReceiver.Apply`. Size is `Radius`. `Recipe.ManaCost` is the authored fire-time cost; `Mana(n)` scales the instance copy. `EffectRadius` is the `AreaEffect` overlap radius. A `GroundField` disk uses the modifier’s own radius and lifetime.

## Public API sketch

Names are locked until an ADR. Implement these, do not invent `ProjectileManager` / `ProjectileSystem` as a second façade.

```csharp
struct SpawnRequest
{
    Recipe Recipe;
    Vector3 Position;
    Vector3 Direction;
    HomingTarget HomingTarget; // optional initial lock; skips acquire (ADR-009, ADR-010)
    bool HomingAcquirePointSet; // ADR-033 / ADR-035; game copies a world aim point (not mouse).
    Vector3 HomingAcquirePoint; // Homing first lock; Arc Spawn loft target; Hitscan / Beam aim. No second AimPoint field.
}

struct ProjectileId { /* opaque handle */ }

enum Delivery { Straight, Arc, Hitscan, Beam } // Beam is phase 31 (ADR-036)

interface IProjectileWorld
{
    ProjectileId Spawn(in SpawnRequest request);
    // Arc + HomingAcquirePointSet: TrySolve; unreachable → default id, no Expired (ADR-035).
    // Hitscan: polyline query inside Spawn, then pool (Hit events already raised).
    void Tick(float dt);
    void Despawn(ProjectileId id); // phase 31. OnExpire + pool. Unknown / pooled = no-op.
    void SetHomingCandidate(TargetId id, Vector3 position);
    void RemoveHomingCandidate(TargetId id);
    void SetHitTarget(TargetId id, Vector3 position, float radius, ProtectionLevel protection = None);
    // ADR-030 / ADR-031. radius<=0 removes. Homing reads this pose. Same id as SetHomingCandidate is one candidate.
    void RemoveHitTarget(TargetId id);
    event Action<HitInfo> Hit;
    event Action<ExpireInfo> Expired;
    event Action<StatusInfo> StatusApplied; // after OnHit / area / ground-field flush (ADR-011, ADR-016, ADR-022)
}

struct StatusInfo
{
    ProjectileId InstanceId;
    TargetId TargetId;
    IStatusEffect Effect;
}

interface IDamageable { TargetId TargetId { get; } } // HitKind.Target on colliders; package does not apply HP
// Published volumes (ADR-030) are also Target, with no collider.

interface IProjectileProtection { ProtectionLevel Level { get; } } // Basic / Strong; ADR-020

interface IStatusEffect { string Id { get; } }

interface IStatusReceiver { void Apply(IStatusEffect effect); }

interface IMultiplyPattern
{
    // World offset from parent/hit for child index of count.
    // Plane perpendicular to forward. spread=0 → zero.
    Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count);
}

static class BallisticAim
{
    // Direction so speed+gravity pass through target. Always the flatter (shorter-time) root.
    // Fast → nearly LOS; level min speed → ~45°. false = unreachable. Does not simulate.
    // Arc Spawn calls this when HomingAcquirePointSet (ADR-035). Games may still call it for barrel preview.
    bool TrySolve(Vector3 origin, Vector3 target, float speed, float gravity, out Vector3 direction);
}

interface IProjectileViewFactory
{
    ProjectileView Create(Recipe recipe, Transform poolParent);
}

interface IGroundFieldViewFactory
{
    ProjectileView Create(float radius, string presentationId, Transform poolParent);
}

[Flags]
enum RecipeStage { OnSpawn = 1, OnTravel = 2, OnTick = 4, OnInterval = 8, OnHit = 16, OnExpire = 32 }

[AttributeUsage(AttributeTargets.Class)]
sealed class ModifierStageAttribute : Attribute
{
    RecipeStage Stages { get; }
}

class RecipeAsset : ScriptableObject
{
    // Same stats as Recipe. Six SerializeReference IModifier lists.
    // Multiply/Multicast child slots are RecipeAsset refs.
    Recipe ToRecipe();
}

class Recipe
{
    // Stats + six lists as in the Recipe block above.
    Recipe Clone(); // deep copy; same Id until the caller changes it
    void Append(RecipeStage stage, IModifier modifier);
    static Recipe Compose(Recipe basis, string id, params ModifierKit[] kits);
}

struct ModifierKitLayer
{
    RecipeStage Stage; // exactly one of the six
    IModifier[] Modifiers;
}

class ModifierKit : ScriptableObject
{
    // Create > Projectile > Modifier Kit. No stats. ADR-032.
    ModifierKitLayer[] Layers;
    void ApplyTo(Recipe recipe); // clone-append; does not change Recipe.Id
}
```

Sandbox fire button / sample turret calls `Spawn`. A single sandbox driver calls `Tick` (for example from one `Update`). Instances do not have their own `Update`. `ProjectileWorld` is `IDisposable` (dispose the Entities `World`). Play Mode demo: turret + preset dropdown + recipe sheet in `Samples~/Yard/` ([DECISIONS.md](DECISIONS.md) ADR-024, ADR-029). Phase 21.b can Add/Remove modifiers on that working copy (ADR-027); it still does not write the `RecipeAsset`. Runtime has no camera, input, or HP.

## Invariants

- After return to the pool, an instance must not tick, hit, or raise events
- Spawn never uses `Instantiate` outside the pool
- Presentation / views must not write simulation state
- Modifiers in a list run in list order. `CancelDespawn` does not skip the rest of that list (so `Bounce` and `Multiply` on the same `OnHit` list both run)
- An instance that cancelled despawn stays live and still runs `OnTick` on later ticks (so `Pierce` then `Homing` compose: the next tick can drop the pierced dummy and acquire another)
- Simulation components have no `MonoBehaviour`, `GameObject`, or other `UnityEngine.Object` fields
- The simulation `World` is package-owned, not `DefaultGameObjectInjectionWorld`, and is not ticked from the Player Loop
- No `FindObjectOfType` (or equivalent global search) in the runtime package
- Deterministic given the same Recipe, spawn pose, `dt` sequence, seed, homing candidate catalog, and published hit catalog (interval spread may use a seed on the world)

## Folder map

```text
Packages/com.veerdonk.projectile-system/
  Runtime/     façade, recipes, modifiers, pooling, PhysX query  (asmdef `Veerdonk.ProjectileSystem`)
  Runtime/Ecs/ internal components, RecipeStore, systems
  Editor/      RecipeAsset inspector (ADR-023); ModifierKit inspector (ADR-032)
  Tests/Runtime
  Tests/Editor
  Samples~/Yard/   UPM sample (ADR-029): turret yard, presets, dummy HP. Hidden until Import
Assets/        host project only after 24 (imported sample copy is gitignored)
docs/          this spec
AGENTS.md      pairing protocol (repo root)
```

Package id `com.veerdonk.projectile-system`, namespace `Veerdonk.ProjectileSystem` (ADR-025). Public API through 0.1.5 includes Protection, the view factories, GroundField, `RecipeAsset`, `ModifierKit`, `Recipe.Clone` / `Append` / `Compose` (ADR-032), the needed-arc `BallisticAim` signature, `SetHitTarget` / `RemoveHitTarget` (ADR-030, ADR-031), `SpawnRequest.HomingAcquirePoint` / `ClosestToPointAcquisition` (ADR-033), and indexed catalogs (ADR-034). Phase 30 adds `Delivery` / Arc Spawn / Hitscan (0.1.6). Phase 31 adds `Delivery.Beam`, `BeamPulseSeconds`, and `Despawn` (0.1.7).

New Runtime folders (for example `Runtime/Modifiers/`) need an ADR if they are a new pattern; adding files under an existing agreed folder does not.

## Unity implementation constraints

- Central `IProjectileWorld.Tick`; no per-instance `Update`
- 3D PhysX queries for hits
- Pooling required from phase 1
- Input System and URP stay in the host project / sample, not Runtime
- Runtime package: no `UnityEditor` namespace, no camera / input / UI

## Hybrid ECS (ADR-005)

- Live instances are entities with blittable components; `Tick` updates a package-owned `World`
- Burst: lifetime and kinematic candidate position only
- Hits: 3D PhysX via `IHitQuery` **and** published hit volumes (ADR-030; not Unity Physics)
- Views: GameObject `ProjectileView` (not Entities Graphics)
- Recipe / `IModifier` stay managed; entity holds `RecipeIndex`
- Do **not** add Unity Physics, Entities Graphics, baking, or Player Loop bootstrap without an ADR
- Map for readers new to ECS: [ECS.md](ECS.md)

## Tests

- Edit Mode tests for simulation, modifiers, despawn rules, `RecipeAsset.ToRecipe`, and stage catalogs (no scene)
- Play Mode tests only for pooling against Unity objects and physics queries
- “Looks right in the Editor” is not acceptance for simulation. Inspector Add-menu filtering is proven by catalog tests plus a human check (ADR-023)
