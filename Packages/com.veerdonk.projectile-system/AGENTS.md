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
- Fire-time upgrades: **Create > Projectile > Modifier Kit**, then `Recipe.Compose(base.ToRecipe(), uniqueId, kits)`. `ApplyTo` appends onto a Recipe you already cloned and does not change `Recipe.Id`. Pass a new id for every composition; `Spawn` replaces the stored Recipe when the id repeats.
- Do not expect an OnInterval kit layer to pulse by itself. `Interval` has to be on the base, or on an OnSpawn layer. Kits do not insert it.
- Do not invent wrap-the-next-spell, behaviour trees, or a graph. Extend with `IModifier` on those lists (`[ModifierStage]`).
- Child shots need an **explicit** child `Recipe`. They do not inherit the parent’s lists.

## Hits and game objects

- Mark damageable colliders with `IDamageable`. Optional shields: `IProjectileProtection` (`Basic` / `Strong`).
- ECS enemies with no collider: `SetHitTarget(id, position, radius, protection)` / `RemoveHitTarget(id)`. `radius <= 0` removes. `protection` defaults to `None`. Those spheres are `HitKind.Target` for the direct cast, `AreaEffect`, `GroundField`, and Homing. `Basic` still raises `Hit` and skips `ApplyStatus`. `Strong` eats the shot (no `Hit`) and is skipped by area, ground, and occupancy.
- Apply HP in the game on `Hit`. Skip HP when `HitInfo.Protection != None` if you use protection. Occupancy exit is damage 0.
- Homing: `SetHomingCandidate` / `RemoveHomingCandidate` for a pose that is not hittable. A published hit target is also a homing candidate. The same id in both catalogs is one candidate, and the hit-target position wins. Copy a world aim point onto `SpawnRequest.HomingAcquirePoint` and set `HomingAcquirePointSet` when the first lock should be the candidate closest to that point. The package does not read the mouse. After that lock, or when `HomingTarget` was assigned at spawn, later empty locks use travel direction.
- `AreaEffect` may sit on `OnTick` (a Hit every Tick while a Target is inside). `occupancy: true` on `AreaEffect` or `GroundField` raises `HitSource.OccupancyEnter` once (plus status if authored) and `HitSource.OccupancyExit` on leave, with no pulses while they stay.
- `SpawnRequest.IgnoreCollider`: Direct overlap and sweep skip that collider until the shot body no longer overlaps it. Not a hit and not a shield. After it clears, a later contact is a normal hit. Multicast extras and multiply children copy the skip. Null leaves today’s behaviour. Area and ground queries still see the collider.
- `Recipe.Delivery`: `Straight` (default, no gravity), `Arc` (gravity; `Spawn` calls `BallisticAim.TrySolve` when `HomingAcquirePointSet`, unreachable returns default `ProjectileId` with no `Expired`), `Hitscan` (one `Radius` polyline inside `Spawn`, then pool, no view), `Beam` (live instance, polyline each `BeamPulseSeconds`, first pulse inside `Spawn`, Pierce n+1 per pulse does not despawn, cylinder per segment stays up between pulses; the length texture coordinate runs from the first point to the last). Homing `n` on Hitscan is degrees of kink per segment. Homing `n` on Beam is degrees per second along the pulse (`distance / speed`); the first step leaves along the muzzle, and the lock is kept across redraws while that target stays in front of the muzzle. Without Pierce a Beam pulse stops on the first Target. Beam cylinders are not colliders. World ends a Hitscan, and clips a Beam pulse, unless Bounce reflects. `Despawn(id)` is OnExpire plus pool for any live instance. `AimBeam(id, position, direction)` redraws a live Beam from that pose without raising `Hit`. Direct queries honor `IgnoreCollider`.
- Cannonballs: set `Delivery.Arc` and copy the aim point onto `SpawnRequest.HomingAcquirePoint` (`HomingAcquirePointSet`). `BallisticAim.TrySolve` stays public for barrel preview. Unreachable Arc does not spawn. No high-arc pick.
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
