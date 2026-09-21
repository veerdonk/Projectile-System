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
