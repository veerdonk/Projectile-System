# com.veerdonk.projectile-system

Kinematic projectile simulation for Unity 6 LTS. Games spawn through `IProjectileWorld`, tick the world once per frame, and listen to `Hit` / `Expired` / `StatusApplied`. The package does not apply HP, spend mana, or own the player, camera, or input.

## Use

1. Construct a `ProjectileWorld` (optional view factories for meshes).
2. Author a `Recipe` in code or a `RecipeAsset` (`Create > Projectile > Recipe`) and call `ToRecipe()`.
3. `Spawn(new SpawnRequest { Recipe, Position, Direction })`.
4. `Tick(dt)` each frame.

Stage lists on the Recipe are the composition model: `OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`. See [Documentation/index.md](Documentation/index.md).

## Namespace

`Veerdonk.ProjectileSystem`

## Sample

Package Manager → **com.veerdonk.projectile-system** → Samples → **Projectile Yard** → Import. Open `Scenes/Yard.unity` and enter Play Mode. The turret calls `Spawn` / `Tick`. Dummy HP listens to `Hit`. That code is not in the Runtime assembly; it compiles only after Import.

In this repository, **Projectile Lab > Rebuild** writes the same scene into `Samples~/Yard` and the gitignored import under `Assets/Samples/`. The sample assembly references Input System and uGUI, which are host-project packages, not dependencies of this package.
