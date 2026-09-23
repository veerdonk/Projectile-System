# Agent guide — com.veerdonk.projectile-system

How to use this package in a **game**. Namespace `Veerdonk.ProjectileSystem`. Install and a copy-paste driver: [README.md](README.md). Public types: [Documentation/index.md](Documentation/index.md).

This file is not the pairing spec for the package repo.

## Call the façade

- One `ProjectileWorld` per play session. `Tick(dt)` once per frame from **one** driver. Instances must not have their own `Update`.
- `Spawn(new SpawnRequest { Recipe, Position, Direction })`. Do not add a second world type or a `Spawn` overload that aims for you.
- `Dispose()` when the scene unloads. The world owns an isolated Entities `World` (not the default injection world).
- Listen to `Hit`, `Expired`, `StatusApplied`. The package does not subtract HP, spend mana, roll crits, or tick status DoT.

## Recipes

- A `Recipe` is base stats plus **six ordered lists**: `OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`. List order is apply order.
- Author in code (`new Recipe`) or **Create > Projectile > Recipe** (`RecipeAsset`) then `ToRecipe()` before `Spawn`. Do not put `RecipeAsset` on `SpawnRequest`.
- Do not invent wrap-the-next-spell, behaviour trees, or a graph. Extend with `IModifier` on those lists (`[ModifierStage]`).
- Child shots need an **explicit** child `Recipe`. They do not inherit the parent’s lists.

## Hits and game objects

- Mark damageable colliders with `IDamageable`. Optional shields: `IProjectileProtection` (`Basic` / `Strong`).
- ECS enemies with no collider: `SetHitTarget(id, position, radius)` / `RemoveHitTarget(id)`. `radius <= 0` removes. Those spheres are `HitKind.Target` for the direct cast, `AreaEffect`, and `GroundField`. They are not homing locks.
- Apply HP in the game on `Hit`. Skip HP when `HitInfo.Protection != None` if you use protection.
- Homing: `SetHomingCandidate` / `RemoveHomingCandidate` with live poses. Do not use `SetHitTarget` for a lock that should not be hittable.
- Cannonballs: `BallisticAim.TrySolve` → copy `direction` onto `SpawnRequest.Direction`. Unreachable is `false`. No high-arc pick.
- Views: pass `IProjectileViewFactory` / `IGroundFieldViewFactory` if you need meshes other than the primitive sphere / disk. Hits stay a kinematic sphere / XZ disk.

## Do not

- Put camera, input, player, or HP logic in a fork of Runtime.
- `FindObjectOfType` (or equivalent) to locate the world.
- `Instantiate` / `Destroy` projectile instances outside the pool.
- Tick through Unity Physics, Entities Graphics, baking, or the Player Loop. Hybrid ECS is already behind `IProjectileWorld`.
- Treat `Runtime/Ecs/` as a consumer API.
- Compile the Play Mode sample as game code until the user imports **Projectile Yard** (`Samples~/Yard/`).

## Sample

Package Manager → import **Projectile Yard** → `Scenes/Yard.unity`. Turret, presets, and dummy HP live there, not in Runtime.
