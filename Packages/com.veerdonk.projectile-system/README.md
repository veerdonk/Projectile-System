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

To pin a release later, append `#` and the tag name (for example `#v0.1.0`) after you create that tag.

The GitHub repo must be public, or the other person needs access.

### Copy the folder

Copy `Packages/com.veerdonk.projectile-system/` into the other project’s `Packages/` folder.

### This repository

The package is already embedded. Open the project in Unity 6 LTS. Import sample **Projectile Yard** (or run **Projectile Lab > Rebuild**) and open `Scenes/Yard.unity`.

## Basic use

1. `new ProjectileWorld` (optional view factories for meshes).
2. Author a `Recipe` in code, or **Create > Projectile > Recipe** and call `ToRecipe()`.
3. `Spawn(new SpawnRequest { Recipe, Position, Direction })`.
4. `Tick(dt)` once per frame from one driver.
5. Listen to `Hit` to apply HP in your game. Put `IDamageable` on colliders that should count as targets. A target with no collider is `SetHitTarget(id, position, radius)` (`RemoveHitTarget` to drop it; `radius <= 0` removes). That catalog is separate from Homing.
6. `Dispose()` when the scene unloads (the world owns an Entities `World`).

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
            Direction = muzzle.forward
        });
    }

    void OnHit(HitInfo hit)
    {
        // Subtract HP in your game. The package does not.
    }
}
```

Recipe composition is six ordered stage lists: `OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`. API list: [Documentation/index.md](Documentation/index.md).

## Sample

Package Manager → **com.veerdonk.projectile-system** → Samples → **Projectile Yard** → Import. Open `Scenes/Yard.unity` and enter Play Mode. The turret calls `Spawn` / `Tick`. Dummy HP listens to `Hit`. That code is not in the Runtime assembly; it compiles only after Import.

In this repository, **Projectile Lab > Rebuild** writes the same scene into `Samples~/Yard` and the gitignored import under `Assets/Samples/`. The sample assembly references Input System and uGUI, which are host-project packages, not dependencies of this package.
