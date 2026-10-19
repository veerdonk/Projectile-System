# com.veerdonk.projectile-system

Kinematic projectile simulation for Unity 6 LTS. Games spawn through `IProjectileWorld`, tick the world once per frame, and listen to `Hit` / `Expired` / `StatusApplied` (plus `Spawned` / `HomingLocked` for presentation). `GroundFieldPlanted` / `GroundFieldPulsed` / `GroundFieldEnded` and `AreaBurst` report fields and bursts for effects. The package does not apply HP, spend mana, or own the player, camera, or input.

**License:** MIT. **Namespace:** `Veerdonk.ProjectileSystem`. Agents integrating this package: [AGENTS.md](AGENTS.md).

## Add the package

Requires **Unity 6 LTS `6000.6`** (this project is `6000.6.0f1`). Package Manager will pull **Entities 6.6**.

### From OpenUPM (recommended)

Package Manager checks registry packages for updates, so this is the only route with an **Update** button.

1. Unity → **Edit → Project Settings → Package Manager**.
2. Under **Scoped Registries**, add:
   - **Name:** `OpenUPM`
   - **URL:** `https://package.openupm.com`
   - **Scope(s):** `com.veerdonk`
3. **Window → Package Manager** → **My Registries** → **Projectile System** → **Install**.

Or add the registry and dependency to `Packages/manifest.json` yourself:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.veerdonk"]
    }
  ],
  "dependencies": {
    "com.veerdonk.projectile-system": "0.2.7"
  }
}
```

With the [openupm-cli](https://openupm.com/docs/getting-started-cli.html): `openupm add com.veerdonk.projectile-system`.

Merges to `main` that introduce a new `package.json` version create tag `v{version}` and a GitHub Release (notes from `CHANGELOG.md` plus merged PRs). OpenUPM builds that tag into a registry version shortly after.

**Moving from a Git URL install:** remove the package in Package Manager (or delete its line in `manifest.json`), add the scoped registry, then install from **My Registries**. A Git install does not see registry updates.

### From Git (another Unity project)

1. Unity → **Window → Package Manager**.
2. **+** → **Add package from git URL**.
3. Paste:

```text
https://github.com/veerdonk/Projectile-System.git?path=/Packages/com.veerdonk.projectile-system
```

To pin a release, append `#` and the tag (for example `#v0.2.7`). Package Manager locks a Git install to one commit and does not check for updates; add the package again to update.

### Copy the folder

Copy `Packages/com.veerdonk.projectile-system/` into the other project’s `Packages/` folder.

### This repository

The package is already embedded. Open the project in Unity 6 LTS. Import sample **Projectile Yard** (or run **Projectile Lab > Rebuild**) and open `Scenes/Yard.unity`.

## Basic use

1. `new ProjectileWorld` (optional view factories for meshes).
2. Author a `Recipe` in code, or **Create > Projectile > Recipe** and call `ToRecipe()`. Extras at fire time are **Create > Projectile > Modifier Kit**, then `Recipe.Compose`. An authored Multiply grid is **Create > Projectile > Multiply Pattern**; the Pattern dropdown copies it onto the Recipe (`Spacing` places cells, `Multiply.Spread` is the cone half-angle in degrees). Square, Circle, and RandomCircle still use Spread as meters and do not aim.
3. `Spawn(new SpawnRequest { Recipe, Position, Direction })`. `Recipe.Delivery` is `Straight` (default, no gravity), `Arc`, `Hitscan`, or `Beam`. Arc with `HomingAcquirePointSet` lofts inside `Spawn` (`BallisticAim.TrySolve`); unreachable returns default `ProjectileId` and does not raise `Expired`. Hitscan is one radius polyline inside `Spawn`, then the instance is pooled. There is no view. Homing `n` on Hitscan is degrees of kink per segment. Homing `n` on Beam is degrees per second along the pulse (`distance / speed`). Beam stays at the spawn pose and pulses that polyline (`Recipe.BeamPulseSeconds`, first pulse inside `Spawn`) until `LifetimeSeconds` or `Despawn(id)`. Without Pierce a pulse stops on the first target. Pierce does not end a Beam. `AimBeam` redraws a live Beam from a new pose without raising `Hit`; the four-argument overload also sets the Beam's homing lock (`default` clears it). `Recipe.MaxRange` caps a Beam pulse or Hitscan query (`<= 0` is 10 km). The view is a cylinder per segment and stays up between pulses. Its material is copied from the presentation prefab, and the texture's length coordinate runs from the first point to the last.
4. `Tick(dt)` once per frame from one driver.
5. To spawn overlapping the shooter, set `SpawnRequest.IgnoreCollider` to that collider. Direct casts skip it until the shot has left; a later touch is a normal hit. Area and ground queries are unchanged. For a set of colliders (the shooter and its pet, for example), use `SpawnRequest.IgnoreColliders`. Every collider in the list is skipped for the whole life of the shot, by Direct, area, and ground queries, and the list is copied at `Spawn`. When the shooter is a published hit target (`SetHitTarget`) rather than a collider, set `SpawnRequest.IgnoreTargetSet` and `IgnoreTarget` to its id: Direct casts skip it until the shot has left, and Homing never locks it.
6. Listen to `Hit` to apply HP in your game. Put `IDamageable` on colliders that should count as targets. A target with no collider is `SetHitTarget(id, position, radius, protection)` (`RemoveHitTarget` to drop it; `radius <= 0` removes). Homing also steers toward that pose. For a tall upright body, `SetHitTarget(id, bottom, radius, height, homingHeight, protection)` publishes a capsule (feet point, total height including caps); hits test the capsule and Homing steers toward the point `homingHeight` above `bottom`. `protection` is `None`, `Basic`, or `Strong`. To lock the candidate closest to a fire-time aim point, set `SpawnRequest.HomingAcquirePointSet` and copy the world point into `HomingAcquirePoint`. Later locks stay travel-direction. The package does not read the mouse.
7. To draw an aim preview, call `PredictPath(request, new PathPrediction { StepSeconds = 1f / 60f }, points, out PathHit hit)` with the request you would spawn. It fills `points` along a Straight or Arc path and returns true with the first contact (point, normal, kind, target, time), or false when the lifetime or `MaxSteps` ends it (an unreachable Arc aim point returns false with an empty list). It spawns nothing and raises no events, so it can run every frame. Only `Speed`, `Gravity`, and `Size` on OnSpawn and OnTravel apply, and Homing does not steer the path; a `Multicast` that turns the main shot is not predicted either. Hitscan and Beam return false with an empty list.
8. `Dispose()` when the scene unloads (the world owns an Entities `World`).

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
