# Architecture

Simulation state is **ECS components** in a package-owned Entities `World`. MonoBehaviours are thin adapters (colliders, presentation). Hits stay PhysX; views stay GameObjects. Recipes and modifiers stay managed classes. Unity Physics and Entities Graphics can replace those adapters later without rewriting Recipes, modifiers, or `IProjectileWorld`.

How to read the ECS pieces: [ECS.md](ECS.md). Decision: [DECISIONS.md](DECISIONS.md) ADR-005.

Do not add a second composition model. Stage lists are the only one ([DECISIONS.md](DECISIONS.md)).

## Layers

| Layer | Lives in | Responsibility |
| --- | --- | --- |
| Definition | ScriptableObjects / serializable data | Recipe: base stats + six ordered modifier lists |
| Simulation | Internal `IComponentData` in an isolated Entities `World` | Position, velocity, remaining lifetime, pierce/bounce remaining, interval clocks. No `UnityEngine.Object` fields |
| Movement | Swappable solver | Kinematic first (Burst integrate); optional Rigidbody later |
| Hits | Query + callback pipeline | PhysX SphereCast / overlap; `HitInfo` events, not `OnCollisionEnter` |
| Presentation | Views bound to instance id | Mesh, trail, VFX GameObjects; may be disabled in tests |
| Pooling | Package runtime | Rent / return by Recipe id: enable/disable entities; recycle views |

## Six stage lists

Every Recipe has these lists, each ordered; modifiers in a list run in index order.

| Stage | When it runs | Typical modifiers |
| --- | --- | --- |
| `OnSpawn` | Once when the instance is created | `Multicast(n)` |
| `OnTravel` | Once after spawn, before the first move | Initial speed, size, direction |
| `OnTick` | Every `ProjectileWorld.Tick` | `Homing(n)` |
| `OnInterval` | When that modifier’s elapsed interval elapses during flight | `Multiply(n)` every 0.5s |
| `OnHit` | On each hit from the pipeline | Damage event, `Pierce`, `Bounce`, `Multiply` spawn |
| `OnExpire` | When the instance is ending | `Multiply` on death |

**Hit then expire:** a hit that despawns the instance runs `OnHit` first, then `OnExpire`.

**Pierce / bounce** are `OnHit` modifiers. They inspect `HitInfo.Kind` and may **cancel despawn** (see Hit pipeline).

`OnTravel` is one-shot setup. Continuous steering is `OnTick`. Periodic spawn is `OnInterval`, not `OnTravel`.

## Recipe

```text
Recipe
  Id
  Speed, Radius, LifetimeSeconds
  PresentationId
  OnSpawn[], OnTravel[], OnTick[], OnInterval[], OnHit[], OnExpire[]
```

`LifetimeSeconds` is the only despawn clock. When remaining lifetime reaches 0, run `OnExpire` and return the instance to the pool.

## Modifier contract

A modifier may:

- Read instance simulation state
- Mutate motion fields (velocity, speed) and remaining pierce/bounce/interval
- Cancel despawn on the current hit
- Request spawns of **explicit child Recipes** (never implied inherit)

A modifier may not:

- Touch Unity objects, presentation, or the pool directly
- Call `Instantiate` / `Destroy`
- Subscribe to Unity messages

Magnitude meaning is per modifier (count vs strength) as in [REQUIREMENTS.md](REQUIREMENTS.md).

**Child Recipes:** each spawn modifier carries the Recipe to spawn. Phase 1 `OnHit` spawn takes that Recipe in the modifier. Later modifiers decide their own copy rules; default remains “explicit Recipe, no inherit.”

**Multicast:** `Multicast(n)` on `OnSpawn` requests `n-1` extra instances from an explicit sibling Recipe. By default extra instances do **not** run `Multicast` again: the spawn path skips it for them, so a sibling Recipe that carries one cannot explode. `Recipe.MulticastGenerations` buys further generations, counted down per extra so a chain always terminates ([DECISIONS.md](DECISIONS.md) ADR-007). `Recipe.MultiplyGenerations` is the same budget for `Multiply` children, which likewise do not inherit multiply by default.

**Prewarm:** `Prewarm(recipe, count)` also prewarms the Recipes those modifiers chain into, using the same generations ([DECISIONS.md](DECISIONS.md) ADR-008). Do not hand-prewarm the children as well. Directions for all `n` slots, the original included, come from an `IMulticastPattern` ([DECISIONS.md](DECISIONS.md) ADR-006); v1 ships `RandomConePattern`.

## Hit pipeline

Kinematic movement casts along the step (SphereCast / overlap). Each contact produces:

```csharp
enum HitKind { Target, World }

struct HitInfo
{
    ProjectileId InstanceId;
    HitKind Kind;
    Vector3 Point;
    Vector3 Normal;
    // TargetId is a sandbox/game id; package stores it opaquely
}
```

- `Target` — dummy (or later enemy). Default: raise `Hit`, despawn unless `Pierce` cancels despawn.
- `World` — environment. Default: despawn unless `Bounce` cancels despawn and reflects velocity around `Normal`.
- Projectile-vs-projectile: ignore until an ADR.

The package does **not** apply HP. Sandbox listens to `Hit` and updates dummy health.

## Public API sketch

Names are locked until an ADR. Implement these, do not invent `ProjectileManager` / `ProjectileSystem` as a second façade.

```csharp
struct SpawnRequest
{
    Recipe Recipe;
    Vector3 Position;
    Vector3 Direction;
    HomingTarget HomingTarget; // optional; Homing acquires if unassigned (ADR-009)
}

struct ProjectileId { /* opaque handle */ }

interface IProjectileWorld
{
    ProjectileId Spawn(in SpawnRequest request);
    void Tick(float dt);
    void SetHomingCandidate(TargetId id, Vector3 position);
    void RemoveHomingCandidate(TargetId id);
    event Action<HitInfo> Hit;
    event Action<ExpireInfo> Expired;
}
```

Sandbox fire button calls `Spawn`. A single sandbox driver calls `Tick` (for example from one `Update`). Instances do not have their own `Update`. `ProjectileWorld` is `IDisposable` (dispose the Entities `World`).

## Invariants

- After return to the pool, an instance must not tick, hit, or raise events
- Spawn never uses `Instantiate` outside the pool
- Presentation / views must not write simulation state
- Modifiers in a list run in list order. `CancelDespawn` does not skip the rest of that list (so `Bounce` and `Multiply` on the same `OnHit` list both run)
- An instance that cancelled despawn stays live and still runs `OnTick` on later ticks (so `Pierce` then `Homing` compose without a retarget API)
- Simulation components have no `MonoBehaviour`, `GameObject`, or other `UnityEngine.Object` fields
- The simulation `World` is package-owned, not `DefaultGameObjectInjectionWorld`, and is not ticked from the Player Loop
- No `FindObjectOfType` (or equivalent global search) in the runtime package
- Deterministic given the same Recipe, spawn pose, `dt` sequence, seed, and homing candidate catalog (interval spread may use a seed on the world)

## Folder map

```text
Packages/com.davidvdveerdonk.com.veerdonk.projectile-system/
  Runtime/     façade, recipes, modifiers, pooling, PhysX query  (asmdef)
  Runtime/Ecs/ internal components, RecipeStore, systems
  Editor/      authoring later
  Tests/Runtime
  Tests/Editor
  Samples/     optional package samples; prefer Assets sandbox for the lab
Assets/        sandbox only (scenes, dummy targets, fire UI)
docs/          this spec
AGENTS.md      pairing protocol (repo root)
```

New Runtime folders (for example `Runtime/Modifiers/`) need an ADR if they are a new pattern; adding files under an existing agreed folder does not.

## Unity implementation constraints

- Central `IProjectileWorld.Tick`; no per-instance `Update`
- 3D PhysX queries for hits
- Pooling required from phase 1
- Input System and URP stay in the sandbox
- Runtime package: no `UnityEditor` namespace, no camera / input / UI

## Hybrid ECS (ADR-005)

- Live instances are entities with blittable components; `Tick` updates a package-owned `World`
- Burst: lifetime and kinematic candidate position only
- Hits: 3D PhysX via `IHitQuery` (not Unity Physics)
- Views: GameObject `ProjectileView` (not Entities Graphics)
- Recipe / `IModifier` stay managed; entity holds `RecipeIndex`
- Do **not** add Unity Physics, Entities Graphics, baking, or Player Loop bootstrap without an ADR
- Map for readers new to ECS: [ECS.md](ECS.md)

## Tests

- Edit Mode tests for simulation, modifiers, and despawn rules (no scene)
- Play Mode tests only for pooling against Unity objects and physics queries
- “Looks right in the Editor” is not acceptance
