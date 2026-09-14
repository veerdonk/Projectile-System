# Architecture

Simulation state is **plain C# data**. MonoBehaviours are thin adapters (pool identity, colliders, presentation). Adapters can be replaced with Entities later without rewriting Recipes, modifiers, or hit rules.

Do not add a second composition model. Stage lists are the only one ([DECISIONS.md](DECISIONS.md)).

## Layers

| Layer | Lives in | Responsibility |
| --- | --- | --- |
| Definition | ScriptableObjects / serializable data | Recipe: base stats + six ordered modifier lists |
| Simulation | Plain structs/classes, **no** `UnityEngine.Object` fields | Position, velocity, remaining lifetime, pierce/bounce remaining, interval clocks |
| Movement | Swappable solver | Kinematic first; optional Rigidbody later |
| Hits | Query + callback pipeline | SphereCast / overlap; `HitInfo` events, not `OnCollisionEnter` |
| Presentation | Views bound to instance id | Mesh, trail, VFX; may be disabled in tests |
| Pooling | Package runtime | Rent / return by Recipe id |

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

**Multicast:** `Multicast(n)` on `OnSpawn` requests `n-1` extra instances. Extra instances must **not** run `Multicast` again (strip or skip it) or spawn explodes. Prefer spawning extras from the same Recipe with multicast ignored, or from an explicit sibling Recipe without multicast.

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
    HomingTarget HomingTarget; // optional; ignored until phase 5
}

struct ProjectileId { /* opaque handle */ }

interface IProjectileWorld
{
    ProjectileId Spawn(in SpawnRequest request);
    void Tick(float dt);
    event Action<HitInfo> Hit;
    event Action<ExpireInfo> Expired;
}
```

Sandbox fire button calls `Spawn`. A single sandbox driver calls `Tick` (for example from one `Update`). Instances do not have their own `Update`.

## Invariants

- After return to the pool, an instance must not tick, hit, or raise events
- Spawn never uses `Instantiate` outside the pool
- Presentation / views must not write simulation state
- Modifiers in a list run in list order
- Simulation types have no `MonoBehaviour`, `GameObject`, or other `UnityEngine.Object` fields
- No `FindObjectOfType` (or equivalent global search) in the runtime package
- Deterministic given the same Recipe, spawn pose, `dt` sequence, and seed (interval spread may use a seed on the world)

## Folder map

```text
Packages/com.davidvdveerdonk.com.veerdonk.projectile-system/
  Runtime/     simulation, recipes, modifiers, pooling, world  (asmdef)
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

## DOTS-ready (what it means now)

- Sim state is blittable-friendly structs/classes without Unity object refs
- Systems are `Tick` methods over arrays/lists of instances, not component `Update`
- Do **not** add `com.unity.entities` until the “later” phase

## Tests

- Edit Mode tests for simulation, modifiers, and despawn rules (no scene)
- Play Mode tests only for pooling against Unity objects and physics queries
- “Looks right in the Editor” is not acceptance
