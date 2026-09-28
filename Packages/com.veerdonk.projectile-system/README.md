# com.veerdonk.projectile-system

Kinematic projectile simulation for Unity 6 LTS. Games spawn through `IProjectileWorld`, tick the world once per frame, and listen to `Hit` / `Expired` / `StatusApplied`. The package does not apply HP, spend mana, or own the player, camera, or input.

**License:** MIT. **Namespace:** `Veerdonk.ProjectileSystem`. Agents integrating this package: [AGENTS.md](AGENTS.md).

## Add the package

Requires **Unity 6 LTS `6000.6`** (this project is `6000.6.0f1`). Package Manager will pull **Entities 6.6**.

### From Git (another Unity project)

1. Unity → **Window → Package Manager**.
2. **+** → **Add package from git URL**.
3. Paste:

```text
https://github.com/veerdonk/Projectile-System.git?path=/Packages/com.veerdonk.projectile-system
```

To pin a release, append `#` and the tag (for example `#v0.1.1`). Merges to `main` that introduce a new `package.json` version create tag `v{version}` and a GitHub Release (notes from `CHANGELOG.md` plus merged PRs).

The GitHub repo must be public, or the other person needs access.

### Copy the folder

Copy `Packages/com.veerdonk.projectile-system/` into the other project’s `Packages/` folder.

### This repository

The package is already embedded. Open the project in Unity 6 LTS. Import sample **Projectile Yard** (or run **Projectile Lab > Rebuild**) and open `Scenes/Yard.unity`.

## Basic use

1. `new ProjectileWorld` (optional view factories for meshes).
2. Author a `Recipe` in code, or **Create > Projectile > Recipe** and call `ToRecipe()`. Extras at fire time are **Create > Projectile > Modifier Kit**, then `Recipe.Compose`.
3. `Spawn(new SpawnRequest { Recipe, Position, Direction })`. `Recipe.Delivery` is `Straight` (default, no gravity), `Arc`, `Hitscan`, or `Beam`. Arc with `HomingAcquirePointSet` lofts inside `Spawn` (`BallisticAim.TrySolve`); unreachable returns default `ProjectileId` and does not raise `Expired`. Hitscan is one radius polyline inside `Spawn`, then the instance is pooled. There is no view. Homing `n` on Hitscan is degrees of kink per segment. Homing `n` on Beam is degrees per second along the pulse (`distance / speed`). Beam stays at the spawn pose and pulses that polyline (`Recipe.BeamPulseSeconds`, first pulse inside `Spawn`) until `LifetimeSeconds` or `Despawn(id)`. Without Pierce a pulse stops on the first target. Pierce does not end a Beam. The view is a cylinder per segment and stays up between pulses.
4. `Tick(dt)` once per frame from one driver.
5. To spawn overlapping the shooter, set `SpawnRequest.IgnoreCollider` to that collider. Direct casts skip it until the shot has left; a later touch is a normal hit. Area and ground queries are unchanged.
6. Listen to `Hit` to apply HP in your game. Put `IDamageable` on colliders that should count as targets. A target with no collider is `SetHitTarget(id, position, radius, protection)` (`RemoveHitTarget` to drop it; `radius <= 0` removes). Homing also steers toward that pose. `protection` is `None`, `Basic`, or `Strong`. To lock the candidate closest to a fire-time aim point, set `SpawnRequest.HomingAcquirePointSet` and copy the world point into `HomingAcquirePoint`. Later locks stay travel-direction. The package does not read the mouse.
7. `Dispose()` when the scene unloads (the world owns an Entities `World`).

```csharp
using UnityEngine;
using Veerdonk.ProjectileSystem;

public sealed class ProjectileDriver : MonoBehaviour
{
    [SerializeField] RecipeAsset recipeAsset;
    [SerializeField] Transform muzzle;

    ProjectileWorld world;

    void Awake()
    {
        world = new ProjectileWorld(transform);
        world.Hit += OnHit;
    }

    void Update()
    {
        world.Tick(Time.deltaTime);
    }

    void OnDestroy()
    {
        world?.Dispose();
    }

    public void Fire()
    {
        world.Spawn(new SpawnRequest
        {
            Recipe = recipeAsset.ToRecipe(),
            Position = muzzle.position,
            Direction = muzzle.forward,
            IgnoreCollider = muzzle.GetComponentInParent<Collider>()
        });
    }

    public void FireWithKit(ModifierKit pierceKit, string recipeId)
    {
        Recipe recipe = Recipe.Compose(recipeAsset.ToRecipe(), recipeId, pierceKit);
        world.Spawn(new SpawnRequest
        {
            Recipe = recipe,
            Position = muzzle.position,
            Direction = muzzle.forward
        });
    }

    void OnHit(HitInfo hit)
    {
        // Subtract HP in your game. The package does not.
    }
}
```

Pass a new `recipeId` on every `Compose`. Spawning the same id again replaces the stored Recipe. An OnInterval kit layer does nothing unless `Interval` is already on the base, or on an OnSpawn layer. Recipe composition is six ordered stage lists: `OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`. API list: [Documentation/index.md](Documentation/index.md).

## Sample

Package Manager → **com.veerdonk.projectile-system** → Samples → **Projectile Yard** → Import. Open `Scenes/Yard.unity` and enter Play Mode. The turret calls `Spawn` / `Tick`. Dummy HP listens to `Hit`. That code is not in the Runtime assembly; it compiles only after Import.

In this repository, **Projectile Lab > Rebuild** writes the same scene into `Samples~/Yard` and the gitignored import under `Assets/Samples/`. The sample assembly references Input System and uGUI, which are host-project packages, not dependencies of this package.
