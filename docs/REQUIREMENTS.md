# Requirements

A reusable 3D Unity projectile package. Each projectile is defined by a **Recipe**: base stats plus **six ordered modifier lists**, one list per lifetime stage.

Agents implement the first version **one phase at a time** ([PHASES.md](PHASES.md)). Human review and refinement happen after each phase. Process rules live in [AGENTS.md](../AGENTS.md), not here.

## Glossary

Use these words only. Do not introduce synonyms in code or docs without an ADR.

- **Projectile** — the concept of a spell, bullet, missile, or shot (not a live object)
- **Recipe** — authored definition: base stats (delivery, speed, radius, lifetime, gravity, presentation id, combat/cast numbers) plus six modifier lists. Unity authors a `RecipeAsset`; `Spawn` still takes `Recipe` ([DECISIONS.md](DECISIONS.md) ADR-023). `Delivery` is Straight (default), Arc, Hitscan, or Beam (ADR-035, ADR-036)
- **Recipe asset** — ScriptableObject that creates and edits a Recipe in the Inspector. Six stage lists; Add menus are filtered by `[ModifierStage]`. `ToRecipe()` builds the runtime `Recipe`
- **Modifier kit** — ScriptableObject of extra modifiers plus the stage list each belongs to. Applied onto a cloned base Recipe at fire time (`Compose` / `ApplyTo`). No Speed / Lifetime / Presentation of its own. Each composition needs a unique `Recipe.Id`. An OnInterval extra does nothing unless `Interval` is already on the base or on an OnSpawn layer; kits do not insert one. See [DECISIONS.md](DECISIONS.md) ADR-032
- **Instance** — one live simulated projectile
- **Modifier** — a named behavior with a magnitude, written `Name(magnitude)` (see below)
- **Stage list** — one of the six ordered modifier lists on a Recipe (`OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`). See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Child recipe** — a full Recipe passed into a spawn modifier. Children do **not** inherit the parent’s lists unless that modifier says so
- **Hit** — a collision result from the hit pipeline, not Unity’s `OnCollision*` messages
- **Hit kind** — `Target` (`IDamageable` collider or a published hit volume) or `World` (wall / unmarked collider). Other projectiles are ignored until an ADR says otherwise
- **Protection** — collider marker (`IProjectileProtection`) or `ProtectionLevel` on `SetHitTarget` (ADR-031) that changes how a hit is processed: Basic (OnHit/area still run, no status, game skips HP) or Strong (eat the projectile). Not a health bar. See [DECISIONS.md](DECISIONS.md) ADR-020
- **Target** — the object described by a `Target` hit
- **Homing target** — the object an instance is steering toward (may differ from what it eventually hits)
- **Effect** — presentation or gameplay reaction associated with a modifier or event (VFX, audio). Damage is a sandbox effect, not package logic
- **Status effect** — a named gameplay condition requested on a Target hit (burn, poison, shock, …). The package delivers the request; the game simulates it. v1 ships `DummyStatusEffect` only
- **Pool** — inactive instances reused on spawn; despawn returns an instance to the pool
- **Damage** — authored hit amount copied onto the instance and `HitInfo`. The package does not apply HP
- **Critical chance** — probability 0–1 on the instance and `HitInfo`. The package does not roll a crit
- **Mana cost** — authored fire-time cost on the Recipe. Phase 10 copies it onto the instance so `Mana(n)` can scale that copy; the package does not spend mana
- **Spread** — meters between adjacent child pips when `Multiply` creates more than one instance. Authored on `Multiply`, not on the Recipe. Consumed by `IMultiplyPattern`. 0 stacks children on the same point
- **Multiply pattern** — layout of those child spawn points in the plane perpendicular to travel or the hit normal (`SquarePattern`, `CirclePattern`, `RandomCirclePattern` in v1)
- **Effect radius** — authored radius of an `AreaEffect` overlap sphere, copied onto the instance. Distinct from projectile `Radius`. `EffectRadius(n)` scales the instance copy; phase 13 queries it
- **Area effect** — overlap sphere around a spawn, hit point, or flying instance that applies area damage and optional status to Targets inside. Not a lingering ground field
- **Ground field** — flat lingering disk planted by `GroundField`. Has its own size, lifetime, damage, optional status, and pulse period. Stays after the projectile despawns. Not a Recipe and not an `AreaEffect` aura
- **Presentation** — GameObject view for an instance. Selected by `Recipe.PresentationId` through `IProjectileViewFactory`. Not the hit shape
- **Size** — projectile collision size. This **is** `Recipe.Radius`; there is no second size field
- **Gravity** — downward acceleration in m/s² on the Recipe (default 0) and the instance. Burst adds it to velocity each tick **only when `Delivery` is Arc** (ADR-035). Straight / Hitscan / Beam ignore it for motion. Arc with gravity 0 stays level. Ballistic reach is derived from Speed + Gravity (ADR-028); there is no Range stat
- **Delivery** — how the shot is delivered (`Straight`, `Arc`, `Hitscan`, `Beam`). Not a modifier and not a seventh stage. Arc: `Spawn` solves loft from `HomingAcquirePoint`. Hitscan: one polyline query then expire. Beam: live instance, pulse polyline until lifetime or `Despawn`
- **Ignore collider** — `SpawnRequest.IgnoreCollider`. Direct overlap and sweep skip that collider until the shot body no longer overlaps it. Not a hit. After clear, a later contact is a normal hit. Same one-slot skip pierce/bounce already use after a hit ([DECISIONS.md](DECISIONS.md) ADR-037). Not `IProjectileProtection`
- **Magnitude** — the number in `ModifierName(n)`:
  - **Count** for pierce, bounce, multicast, multiply (how many)
  - **Strength** for homing: degrees per second on Straight / Arc (`OnTick` steer); **degrees of kink** per segment on Hitscan / Beam (ADR-035)
  - **Scale** for speed, damage, critical chance, mana cost, multiply spread, effect radius, size, and gravity (multiplier: `Speed(2)` doubles, `Speed(0.5)` halves; same for `Damage`, `Critical`, `Mana`, `Spread`, `EffectRadius`, `Size`, `Gravity`)
  - **Seconds** for interval (`Interval(0.5)` runs `OnInterval` every half second), for `GroundField` lifetime / pulse period, and for `Recipe.BeamPulseSeconds` (phase 32)
  - **Distance** for multiply spread (`Multiply` `Spread` is meters between adjacent pips on the multiply pattern)

### Named modifiers (v1)

- **Pierce(n)** — `OnHit` + `Target`: pass through `n` targets; despawn on hit `n+1`
- **Bounce(n)** — `OnHit`: reflect off `n` `World` or `Target` hits; despawn on hit `n+1`. `Hit` still fires on `Target`, so dummy HP still drops
- **Homing(n)** — Straight / Arc `OnTick`: steer toward a homing target; higher `n` turns faster (°/s). Hitscan / Beam: max degrees each polyline segment may kink toward a lock (ADR-035). A `Target` hit drops the lock and the instance may acquire another candidate once that dummy is no longer in front of travel. Published hit volumes count as candidates (ADR-031); the same id in `SetHomingCandidate` is one candidate and the hit-target position wins. `SetHomingCandidate` remains for a pose that is not hittable. If `SpawnRequest.HomingAcquirePointSet`, the **first** lock is the candidate closest to that world point (ADR-033); retarget after a Target hit still uses travel direction. That same point is the Arc loft target and the Hitscan / Beam aim point (no second field)
- **Multicast(n)** — `OnSpawn`: total instances created at spawn is `n` (the original plus `n-1` extras)
- **Multiply(n)** — extra instances spawned from `OnInterval`, `OnHit`, or `OnExpire` (not from `OnSpawn`). Optional `Spread` is meters between adjacent pips. Layout comes from an `IMultiplyPattern` (default `SquarePattern`)
- **Speed(n)** — scale current speed by `n` (`n > 1` faster, `0 < n < 1` slower). `OnTravel` (start), `OnInterval` (progressive), `OnHit` (after bounce or pierce). Not `OnSpawn`
- **Interval(n)** — `OnSpawn`: seconds between `OnInterval` runs for this instance. One period per Recipe drives every modifier on that list; without it `OnInterval` never runs (see [DECISIONS.md](DECISIONS.md) ADR-012)
- **Damage(n)** — scale instance damage by `n`. Same stages as `Speed(n)`. Package still does not apply HP
- **Critical(n)** — scale instance critical chance by `n`, then clamp to `[0, 1]`. Same stages as `Speed(n)`. Package still does not roll a crit
- **Mana(n)** — scale the instance mana-cost copy by `n`. Same stages as `Speed(n)`. Does not change `Recipe.ManaCost` or spend mana
- **Spread(n)** — scale the instance multiply-spread (pip spacing for `IMultiplyPattern`) by `n`. Same stages as `Speed(n)`. Does not jitter the original shot
- **EffectRadius(n)** — scale instance effect radius by `n`. Same stages as `Speed(n)`. Phase 13 uses the result as the `AreaEffect` overlap radius
- **Size(n)** — scale `Radius` by `n`. Same stages as `Speed(n)`. There is no separate size field
- **ApplyStatus** — `OnHit` + `Target`: request one or more `IStatusEffect`s on the hit Target. The package does not simulate the effects (see [DECISIONS.md](DECISIONS.md) ADR-011)
- **AreaEffect** — overlap Targets in instance `EffectRadius` and raise Area `Hit` / `StatusApplied`. Own damage on the modifier. `OnHit` (splash at the hit point), `OnTravel` (burst at spawn), `OnInterval` and `OnTick` (aura on the flying instance; OnTick overlaps every Tick). Flags choose whether the projectile’s own contact damage/status still apply. See [DECISIONS.md](DECISIONS.md) ADR-016. `occupancy` (default false) uses enter/exit instead of pulses (ADR-031)
- **GroundField** — plant a lingering horizontal disk (`OnHit` at the hit point, `OnExpire` at the instance, `OnTravel` at spawn). Own radius, lifetime, damage, optional status, and pulse period on the modifier. Same `Hit` / `StatusApplied` events as `AreaEffect`. See [DECISIONS.md](DECISIONS.md) ADR-022. `occupancy` (default false) uses enter/exit instead of pulses (ADR-031)
- **Gravity(n)** — scale instance gravity by `n`. Same stages as `Speed(n)`. Burst still applies gravity **on Arc only**; this modifier does not add `g * dt` itself
- **Lifetime** — `Recipe.LifetimeSeconds` is the despawn clock. There is no separate `Timer` modifier (see [DECISIONS.md](DECISIONS.md)). Beam still expires when it elapses, even if the fire button is held. `Despawn` is an explicit expire (phase 32)

## Goals

- 3D projectiles only
- Unity 6 LTS (project is `6000.6.0f1`)
- Ship as an embedded UPM package with a Play Mode sample at `Samples~/Yard/` (ADR-029)
- GameObject adapters around hybrid ECS simulation (Entities for instance state, PhysX hits, GameObject views; see [DECISIONS.md](DECISIONS.md) ADR-005)
- Composition via the six stage lists
- Object pooling required
- Sandbox / sample scene for development and Play Mode proof only (not Runtime)

## Non-goals (v1)

- Pixel or material physics
- Networking
- Unity Physics / Entities Graphics (hybrid ECS is in; full DOTS stack is later)
- Player controller, inventory, wand UI, mana / spell points (Recipe may still *store* `ManaCost` for the game to read; the package does not spend it)
- A second composition model (Noita wrap-the-next-spell, behaviour trees, visual graphs)
- HP / damage application inside the runtime package (sandbox listens to `Hit` and applies damage). `IDamageable` is a Target-classification marker only; see [DECISIONS.md](DECISIONS.md) ADR-004
- Burn / poison / shock simulation, duration, stacking, or cleansing inside the runtime package (phase 8 delivers the apply request only; see [DECISIONS.md](DECISIONS.md) ADR-011)
- New folders or patterns without an ADR

## Package vs game boundary

**Package (`Packages/com.veerdonk.projectile-system`)**

- Spawn, simulate, compose (stage lists), pool
- Hit pipeline and events
- Hybrid ECS simulation (`com.unity.entities`; see [ECS.md](ECS.md))
- Recipe inspector (phase 20)
- Play Mode **sample** under `Samples~/Yard/` (phase 24 / ADR-029) — imported into the consumer project, not compiled until then

**Game / sample (not Runtime)**

- Cameras, lighting, URP (host project)
- Input handling, turret, sample HUD (preset dropdown, recipe sheet with Add/Remove from phase 21.b, fire, reset)
- Dummy targets (HP, `IDamageable`, `IStatusReceiver`, optional `IProjectileProtection`). Games without colliders publish hit volumes (phase 25 / ADR-030)
- Hold-to-fire for Beam (phase 32): press `Spawn`, release `Despawn`. Runtime does not read the mouse
- Player or enemy objects if you add them later

If a type needs `Player`, `Enemy`, or camera code, it does not belong in **Runtime**. The UPM sample may contain those as an example.

## Scenarios

Each scenario is owned by one phase. Do not implement a scenario before that phase.

### Phase 1 — Straight shot hits cube

- **Given:** a Recipe with no extra modifiers, lifetime long enough to reach the cube, has been fired at a dummy target
- **When:** the instance records a `Target` hit on the cube
- **Then:** the sandbox dummy loses HP, the instance despawns, and pool available count is unchanged from before the shot (rent + return)

### Phase 1 — Lifetime expire without a hit

- **Given:** a Recipe with `LifetimeSeconds = 3` and no obstacles
- **When:** the instance has been alive for 3 seconds
- **Then:** the instance despawns and returns to the pool; no child instances spawn

### Phase 1 — OnHit spawn along the hit normal

- **Given:** a parent Recipe whose `OnHit` list contains a spawn/multiply modifier with an **explicit child Recipe**
- **When:** the parent records a `Target` hit and despawns
- **Then:** one child instance spawns at the hit point, velocity along the hit normal, using only the child Recipe

### Phase 2 — Pierce(2)

- **Given:** `Pierce(2)` on `OnHit`; three dummy targets in a line
- **When:** the instance travels through all three
- **Then:** targets 1 and 2 receive a `Hit` and the instance continues; target 3 receives a `Hit` and the instance despawns and returns to the pool

### Phase 2 — Bounce(1) then hit a target

- **Given:** `Bounce(1)` on `OnHit`; a wall (`World`) then a dummy target
- **When:** the instance hits the wall, then the target
- **Then:** it reflects off the wall and does not despawn; it despawns on the `Target` hit because bounce remaining is already 0 (a `Target` hit with remaining bounce would reflect and still raise `Hit`)

### Phase 3 — Multiply(2) every 0.5s of travel

- **Given:** `Multiply(2)` on `OnInterval`, `Interval(0.5)` on `OnSpawn`, and an explicit child Recipe
- **When:** the parent has travelled for 0.5 seconds
- **Then:** two child instances spawn along the direction of travel at `IMultiplyPattern` positions (`Spread = 0` stacks)

### Phase 3 — Multiply(1) on expire

- **Given:** `Multiply(1)` on `OnExpire` with an explicit child Recipe
- **When:** the parent despawns because of lifetime or a killing hit (after `OnHit` runs)
- **Then:** one child instance spawns from the expire modifier using the child Recipe

### Phase 4 — Multicast(3)

- **Given:** `Multicast(3)` on `OnSpawn` with an explicit child/sibling Recipe (or the same Recipe without re-applying multicast — see [ARCHITECTURE.md](ARCHITECTURE.md))
- **When:** the Recipe is spawned once
- **Then:** three instances exist immediately (1 + 2 extras)

### Phase 5 — Strong homing

- **Given:** `Homing(100)` on `OnTick`; the instance acquires the closest dummy in its travel direction; initial velocity is 25° off that dummy
- **When:** the instance travels
- **Then:** it turns hard enough to hit the acquired dummy under sandbox distances

### Phase 5 — Weak homing

- **Given:** `Homing(10)` on `OnTick` and the same miss-setup as the strong case
- **When:** the instance travels
- **Then:** it turns toward the acquired dummy but misses at sandbox distances

### Phase 6 — Pierce(1) then home to a second dummy

- **Given:** `Pierce(1)` on `OnHit` and `Homing(300)` on `OnTick`; a first dummy on the initial trajectory; a second dummy off the remaining straight-line path
- **When:** the instance records a `Target` hit on the first dummy
- **Then:** Pierce cancels despawn; Homing drops that lock and does not re-acquire the pierced dummy while it stays in front of travel; later ticks acquire the next candidate and steer into it

### Phase 6 — Bounce(2) and Multiply(2) on each wall

- **Given:** `Bounce(2)` and `Multiply(2)` on `OnHit` with an explicit child Recipe
- **When:** the instance records two `World` hits
- **Then:** each hit reflects the parent and does not despawn it; each of those hits also spawns two child instances from the child Recipe (four children total after two walls)

### Phase 7 — Speed(2) at spawn

- **Given:** `Speed(2)` on `OnTravel`; Recipe base speed is the same as the straight shot
- **When:** the instance is spawned
- **Then:** it travels at twice `Recipe.Speed` before the first move

### Phase 7 — Speed(0.5) every 0.5s

- **Given:** `Speed(0.5)` on `OnInterval` and `Interval(0.5)` on `OnSpawn`
- **When:** the instance has travelled 0.5s, then 1.0s
- **Then:** speed is half of spawn speed after the first interval, then a quarter after the second

### Phase 7 — Speed(2) after bounce

- **Given:** `Bounce(1)` then `Speed(2)` on `OnHit`
- **When:** the instance records a `World` hit
- **Then:** it reflects, does not despawn, and continues at twice the speed it had before the hit

### Phase 7 — Speed(0.5) after pierce

- **Given:** `Pierce(1)` then `Speed(0.5)` on `OnHit`
- **When:** the instance records a `Target` hit
- **Then:** Pierce cancels despawn and the instance continues at half the speed it had before the hit

### Phase 8 — Dummy status on Target hit

- **Given:** `ApplyStatus` with `DummyStatusEffect` on `OnHit`
- **When:** the instance records a `Target` hit
- **Then:** `StatusApplied` is raised with that effect and Target id; the sandbox dummy receives it; the instance still despawns unless Pierce/Bounce cancel that

### Phase 8 — Multiple status on one Target hit

- **Given:** two `ApplyStatus` modifiers on `OnHit`
- **When:** the instance records a `Target` hit
- **Then:** `StatusApplied` is raised once per modifier, in list order

### Phase 8 — Status does not apply on World

- **Given:** the same `ApplyStatus` Recipe
- **When:** the instance records a `World` hit
- **Then:** `StatusApplied` is not raised

### Phase 9 — Hit carries Recipe damage

- **Given:** a Recipe with `Damage = 5` and no extra modifiers
- **When:** the instance records a `Target` hit
- **Then:** `HitInfo.Damage` is 5 and the sandbox dummy loses 5 HP

### Phase 9 — Mana cost is Recipe data

- **Given:** a Recipe with `ManaCost = 12`
- **When:** the Recipe is spawned and ticked
- **Then:** `Recipe.ManaCost` is still 12; the package does not change or spend it

### Phase 10 — Damage(2) at spawn

- **Given:** a Recipe with `Damage = 5` and `Damage(2)` on `OnTravel`
- **When:** the instance records a `Target` hit
- **Then:** `HitInfo.Damage` is 10 and the sandbox dummy loses 10 HP

### Phase 10 — OnHit Damage(2) after pierce

- **Given:** `Pierce(1)` then `Damage(2)` on `OnHit`; Recipe `Damage = 5`
- **When:** the instance records two `Target` hits
- **Then:** the first `Hit.Damage` is 5 (OnHit has not run yet); the second `Hit.Damage` is 10

### Phase 10 — Mana(0.5) does not rewrite the Recipe

- **Given:** a Recipe with `ManaCost = 12` and `Mana(0.5)` on `OnTravel`
- **When:** the instance is spawned
- **Then:** instance mana cost is 6; `Recipe.ManaCost` is still 12

### Phase 11 — Square 5 is die 5

- **Given:** `Multiply(5)` with `SquarePattern`, `Spread = 2` on `OnHit`, and an explicit child Recipe
- **When:** the parent records a hit
- **Then:** five children: four corners of a 3×3 lattice (cell 2 m) plus the hit point (center)

### Phase 11 — Square 7 is an H

- **Given:** `Multiply(7)` with `SquarePattern` and `Spread = 2` on `OnHit`
- **When:** the parent records a hit
- **Then:** the seven children form an H on that lattice (two columns of three plus the center)

### Phase 11 — Square 9 is a 3×3; 10 adds the top middle

- **Given:** `Multiply(9)` then `Multiply(10)` with `SquarePattern` and `Spread = 2` on `OnHit`
- **When:** the parent records a hit
- **Then:** 9 fills the 3×3; 10 adds one pip at the top-middle of the 5×5 ring (2 m above the center, in the pattern plane)

### Phase 11 — Circle 3 is a triangle; 4 adds the center

- **Given:** `Multiply(3)` then `Multiply(4)` with `CirclePattern` and `Spread = 2` on `OnHit`
- **When:** the parent records a hit
- **Then:** 3 children form an equilateral triangle of side 2 m; 4 adds a pip at the hit point

### Phase 11 — Circle 6 is center plus five on a ring

- **Given:** `Multiply(6)` with `CirclePattern` and `Spread = 2` on `OnHit`
- **When:** the parent records a hit
- **Then:** one child at the hit point and five on a ring whose adjacent chord is 2 m

### Phase 11 — Random circle stays inside the disk

- **Given:** `Multiply(8)` with `RandomCirclePattern` and `Spread = 2` on `OnHit`, world seed fixed
- **When:** the parent records a hit
- **Then:** eight children lie in the plane disk of radius 2 m about the hit point; no two closer than the child diameter; the same seed repeats the poses

### Phase 11 — Spread 0 stacks

- **Given:** any of the three patterns, `Multiply(4)`, `Spread = 0`
- **When:** the parent records a hit
- **Then:** all four children spawn on the hit point

### Phase 12 — Size(2) at spawn

- **Given:** a Recipe with `Radius = 0.25` and `Size(2)` on `OnTravel`
- **When:** the instance is spawned
- **Then:** instance radius is 0.5

### Phase 12 — Spread(2) widens Multiply spacing

- **Given:** `Multiply(2)` Square with `Spread = 2`, and `Spread(2)` on `OnTravel`
- **When:** the parent records a hit
- **Then:** the two children spawn 4 meters apart

### Phase 12 — EffectRadius(2) at spawn

- **Given:** a Recipe with `EffectRadius = 1` and `EffectRadius(2)` on `OnTravel`
- **When:** the instance records a hit
- **Then:** `HitInfo.EffectRadius` is 2

### Phase 13 — OnHit area hits nearby Targets

- **Given:** `AreaEffect` with damage 3 on `OnHit`; `EffectRadius` large enough to cover DummyTarget and a nearby dummy; `includeDirectHit = true`
- **When:** the instance records a `Target` hit on DummyTarget
- **Then:** DummyTarget receives a Direct `Hit` (Recipe damage) and an Area `Hit` (3); the nearby dummy receives an Area `Hit` (3)

### Phase 13 — Area-only suppresses the contact Hit

- **Given:** the same splash, `includeDirectHit = false`, Recipe `Damage = 5`, area damage 3
- **When:** the instance records a `Target` hit on DummyTarget
- **Then:** DummyTarget receives only the Area `Hit` (3); no Direct `Hit` is raised

### Phase 13 — OnTravel burst at spawn

- **Given:** `AreaEffect` with damage 3 on `OnTravel`; a dummy inside `EffectRadius` of the spawn point
- **When:** the Recipe is spawned
- **Then:** that dummy receives an Area `Hit` (3) before the instance moves

### Phase 13 — Interval aura follows the instance

- **Given:** `AreaEffect` with damage 3 on `OnInterval`, `Interval(0.5)` on `OnSpawn`; a dummy beside the flight path inside `EffectRadius`
- **When:** the instance has travelled 0.5s (still beside the dummy) then later has passed out of range
- **Then:** the dummy receives an Area `Hit` (3) on that interval; after the instance has left the radius, later intervals do not hit it

### Phase 13 — Area status and suppress direct status

- **Given:** `AreaEffect` with `DummyStatusEffect` and `includeDirectStatus = false` on `OnHit`; `ApplyStatus` also on that list
- **When:** the instance records a `Target` hit
- **Then:** each Target in the sphere receives `StatusApplied` with the area effect; `ApplyStatus` does not run for the contact Target

### Phase 14 — Gravity 0 stays straight

- **Given:** a Recipe with `Gravity = 0` and the same aim as the straight shot
- **When:** the instance is ticked several times with no obstacles
- **Then:** its height (y) is unchanged

### Phase 14 — Gravity drops the shot

- **Given:** a Recipe with `Gravity > 0` and the same horizontal aim and speed as the straight shot
- **When:** the instance is ticked several times with no obstacles
- **Then:** its y decreases each tick while it still moves along the launch heading

### Phase 15 — Gravity(2) at spawn

- **Given:** a Recipe with `Gravity = 10` and `Gravity(2)` on `OnTravel`
- **When:** the instance is spawned
- **Then:** instance gravity is 20; `Recipe.Gravity` is still 10

### Phase 16 — Ballistic aim hits a reachable target

- **Given:** spawn origin, DummyTarget position, cannonball `Speed` and `Gravity` that can reach it
- **When:** `BallisticAim.TrySolve` is called (phase 16 defaulted to the low/flatter arc)
- **Then:** it returns true and a direction such that simulating with that Recipe meets the dummy (within test tolerance)

### Phase 16 — Unreachable target fails

- **Given:** the same helper, a target farther than that speed and gravity can reach
- **When:** `TrySolve` is called
- **Then:** it returns false

### Phase 17 — Basic protection skips HP and status, not bounce

- **Given:** a collider with `IProjectileProtection` Basic and `Bounce(1)` on the Recipe
- **When:** the instance records a hit on that collider
- **Then:** Bounce reflects and does not despawn; Direct `Hit` has `Protection = Basic`; `StatusApplied` is not raised; sandbox HP on that dummy is unchanged

### Phase 17 — Basic protection still pierces

- **Given:** `Pierce(1)` and a Basic-protected dummy in front of an unprotected dummy
- **When:** the instance travels through both
- **Then:** the first contact does not despawn the instance; the unprotected dummy then receives a normal Target `Hit`

### Phase 17 — Strong protection eats the shot

- **Given:** a Strong-protected collider and a Recipe with `Bounce(1)`, `AreaEffect`, and `Multiply` on `OnHit` / `OnExpire`
- **When:** the instance records a hit on that collider
- **Then:** the instance returns to the pool; no Direct `Hit`; no Area `Hit`; no child instances; `Expired` fires

### Phase 18 — Cube presentation is not a cube hit

- **Given:** a Recipe with `PresentationId = "cube"`, the same `Radius` as the sphere Recipe, and `PrefabViewFactory` mapping `"cube"` and `"sphere"`
- **When:** the instance records a `Target` hit on DummyTarget
- **Then:** the view is the cube prefab; `Hit` still uses `Radius` (same contact as the sphere Recipe)

### Phase 18 — Unknown PresentationId falls back to a sphere

- **Given:** `PresentationId = "missing"` and the same factory
- **When:** the instance is spawned with views on
- **Then:** the view is the primitive sphere fallback

### Phase 19 — Ground field lingers after the shot

- **Given:** `GroundField` on `OnHit` with radius large enough for DummyTarget and the nearby splash dummy, `lifetimeSeconds` longer than one interval, damage 3, `intervalSeconds = 0.5`
- **When:** the instance records a `Target` hit on DummyTarget and despawns
- **Then:** DummyTarget and the splash dummy each receive an Area `Hit` (3) immediately and again at 0.5s; after `lifetimeSeconds` no further field Hits are raised

### Phase 19 — Ground field is a disk, not a sphere

- **Given:** the same field radius, and a dummy whose XZ distance from the plant point is greater than radius but whose 3D distance is less than radius
- **When:** the field pulses
- **Then:** that dummy receives no Area `Hit`

### Phase 19 — OnExpire plants where the shot died

- **Given:** `GroundField` on `OnExpire` only, a lifetime-only Recipe that dies short of DummyTarget
- **When:** the instance expires
- **Then:** the disk is at the expire position; DummyTarget is not hit unless it stands in that disk

### Phase 19 — Ground field status matches AreaEffect

- **Given:** `GroundField` with `DummyStatusEffect` on `OnHit`
- **When:** a Target stands in the disk at a pulse
- **Then:** that Target receives `StatusApplied` with the field effect (same event as a projectile `ApplyStatus` / `AreaEffect`)

### Phase 20 — Inspector Add menu is per stage

- **Given:** a new `RecipeAsset` in the Editor
- **When:** the user opens the OnHit Add menu and the OnSpawn Add menu
- **Then:** OnHit offers Pierce (and other OnHit types that exist); OnSpawn does not offer Pierce; OnSpawn offers Interval and Multicast

### Phase 20 — ToRecipe matches the asset

- **Given:** a `RecipeAsset` with Speed 20, LifetimeSeconds 3, and `Pierce(2)` on OnHit
- **When:** `ToRecipe()` is called
- **Then:** the `Recipe` has those stats and one OnHit modifier whose type is `Pierce` and count is 2

### Phase 20 — Child RecipeAsset resolves

- **Given:** parent and child `RecipeAsset`s; parent OnHit Multiply references the child asset
- **When:** `parent.ToRecipe()` is called
- **Then:** `Multiply.ChildRecipe` is the child’s `ToRecipe()` result (same Id/stats)

### Phase 21 — Preset dropdown fires a named Recipe

- **Given:** Play Mode on the sample scene, Pierce preset selected
- **When:** the user aims at the dummy line and fires
- **Then:** the shot uses that preset’s working copy; the first two dummies are pierced as in phase 2

### Phase 21 — Sheet edits the copy, not the asset

- **Given:** a Pierce `RecipeAsset` with count 2, selected in the sample
- **When:** the user sets Pierce to 1 on the recipe sheet and fires
- **Then:** the shot has Pierce(1); after Play Mode the asset is still Pierce(2)

### Phase 21 — Reset restores the scene

- **Given:** dummy HP has changed and a shot is still in the air
- **When:** the user presses Reset
- **Then:** dummy HP and status are initial; no live instances remain; the working copy matches the selected preset again; turret yaw is the default

### Phase 21 — UI clicks do not fire

- **Given:** the pointer is over the preset dropdown or recipe sheet
- **When:** the user clicks
- **Then:** no `Spawn` runs

### Phase 21.b — Play Mode can compose modifiers

- **Given:** Play Mode on the sample, Straight preset selected
- **When:** the user Adds Pierce on OnHit and Homing on OnTick, then fires at the dummy line
- **Then:** the first dummy is pierced and the shot can home; after Play Mode the Straight asset still has empty OnHit/OnTick

### Phase 21.b — Add menu is stage-filtered

- **Given:** the sample sheet Add menu for OnSpawn and OnHit
- **When:** the user opens both
- **Then:** OnHit offers Pierce; OnSpawn does not; OnTick offers Homing

### Phase 21.b — Reset discards composed modifiers

- **Given:** Straight with Pierce added on OnHit in Play Mode
- **When:** the user presses Reset
- **Then:** the sheet matches Straight again (no Pierce); dummy HP is restored

### Phase 22 — Ballistic preset hits DummyTarget

- **Given:** Play Mode on the sample, ballistic preset selected
- **When:** the user aims at DummyTarget and fires
- **Then:** `TrySolve` supplies `SpawnRequest.Direction`; the shot hits; the barrel is pitched. There is no high-arc toggle

### Phase 22 — Extra speed flattens the solve

- **Given:** the ballistic preset aimed at DummyTarget
- **When:** the user raises Speed on the sheet and fires again
- **Then:** the next solve is flatter (closer to the line of sight) than the slower shot

### Phase 22.b — Barely-enough speed on the level is ~45°

- **Given:** origin and target at the same height, gravity > 0, speed just high enough that `TrySolve` still returns true
- **When:** `BallisticAim.TrySolve` is called
- **Then:** it returns true and the launch elevation is within a few degrees of 45°

### Phase 22.b — Spare speed is nearly straight

- **Given:** the same level geometry at several times that minimum speed
- **When:** `TrySolve` is called
- **Then:** it returns true; elevation is much closer to the line of sight than to 45°; simulating with that Recipe still meets the target

### Phase 22.b — No high-arc parameter

- **Given:** the public `TrySolve` signature after 22.b
- **When:** a caller aims
- **Then:** there is no `highArc` argument; unreachable shots still return false

### Phase 23 — Package id is not doubled

- **Given:** the embedded UPM package after the cleanup rename
- **When:** Package Manager lists project packages
- **Then:** the id is `com.veerdonk.projectile-system` (not `com.davidvdveerdonk.com.veerdonk.projectile-system`)

### Phase 23 — Public façade is documented

- **Given:** the runtime assembly after phases 17–22.b
- **When:** a consumer inspects `IProjectileWorld`, `Recipe`, `IProjectileProtection`, `IProjectileViewFactory`, `GroundField`, `RecipeAsset`, and `BallisticAim`
- **Then:** every public type and public member has XML documentation; `Documentation/index.md` is not the Unity template

### Phase 23 — Sonar has no Blocker or Critical on the package

- **Given:** the local SonarQube container at `http://localhost:9000` and a scan of the renamed Runtime + Editor
- **When:** the quality pass finishes
- **Then:** the project `com.veerdonk.projectile-system` has no remaining Blocker or Critical issues on package product code

### Phase 24 — Consumers can import the yard

- **Given:** Package Manager listing `com.veerdonk.projectile-system`
- **When:** the user imports sample **Projectile Yard** and enters Play Mode on `Yard.unity`
- **Then:** the turret, presets, and dummy line are present; firing Pierce pierces the first two dummies as in phase 21

### Phase 24 — Sample is not Runtime

- **Given:** a project that added the package but has not imported the sample
- **When:** the consumer inspects the Runtime assembly
- **Then:** there is no `SampleLab`, dummy HP, or turret type; `SampleLab` compiles only after Import

### Phase 25 — Published volume is Target without a collider

- **Given:** `SetHitTarget` for id 7 at a point with radius 0.5; a straight Recipe aimed through that point; no PhysX collider there
- **When:** the instance is ticked until it reaches the volume
- **Then:** Direct `Hit` has `Kind = Target` and `TargetId` 7; the package does not change HP

### Phase 25 — Unmarked collider is still World

- **Given:** a PhysX wall with no `IDamageable` and no published volume
- **When:** a straight shot hits the wall
- **Then:** `Kind = World`

### Phase 25 — Pierce consumes published Targets

- **Given:** `Pierce(2)` and two published volumes in a line, then a third (`Pierce(n)` absorbs n Target hits)
- **When:** the instance travels through all three
- **Then:** the first two raise Target `Hit` and the instance continues; the third raises `Hit` and despawns

### Phase 25 — Bounce reflects a published Target

- **Given:** `Bounce(1)` and one published volume, then empty space
- **When:** the instance contacts that volume
- **Then:** Direct `Hit` fires (`Kind = Target`); velocity reflects around the contact normal; the instance stays live

### Phase 25 — AreaEffect and GroundField include published volumes

- **Given:** a published volume inside `EffectRadius` / inside a GroundField XZ disk, and one outside
- **When:** OnHit `AreaEffect` runs, then a `GroundField` pulse
- **Then:** the inside volume receives Area `Hit` with that `TargetId`; the outside volume does not

### Phase 26 — Homing acquires a published hit volume

- **Given:** `SetHitTarget` only (no `SetHomingCandidate`); `Homing` on `OnTick`; a volume in front of travel
- **When:** the instance ticks
- **Then:** it locks that `TargetId` and steers toward the published position

### Phase 26 — ApplyStatus on a published Target

- **Given:** `ApplyStatus` with `DummyStatusEffect` on `OnHit`; a published volume, no collider
- **When:** Direct contact happens
- **Then:** `StatusApplied` fires for that `TargetId`. A World collider still does not. Pierce through two published volumes raises twice

### Phase 26 — Published Basic and Strong protection

- **Given:** `SetHitTarget` with `ProtectionLevel.Basic` or `Strong`
- **When:** a straight shot (and an ApplyStatus shot) contacts the volume
- **Then:** Basic: OnHit runs, Direct `Hit` has `Protection = Basic`, no `StatusApplied`. Strong: no Direct `Hit`, no OnHit, instance despawns (`Expired` still fires)

### Phase 26 — OnTick AreaEffect pulses every Tick

- **Given:** `AreaEffect` on `OnTick`, occupancy false; a published volume inside `EffectRadius`
- **When:** two `Tick`s while it stays inside
- **Then:** two Area `Hit`s for that `TargetId`

### Phase 26 — Occupancy enter once and exit

- **Given:** `AreaEffect` or `GroundField` with `occupancy` true; a published volume that starts outside, enters, stays, then leaves
- **When:** ticked through that motion
- **Then:** one `HitSource.OccupancyEnter` (and status if authored) on enter; no further Hits from that modifier while inside; `HitSource.OccupancyExit` (Damage 0, no status) on leave. Strong published volumes are skipped

### Phase 27 — Base Recipe plus a Pierce kit pierces

- **Given:** a Straight `RecipeAsset` (empty OnHit) and a `ModifierKit` with Pierce(2) on OnHit
- **When:** `Recipe.Compose(base.ToRecipe(), "straight+pierce", pierceKit)` is spawned down a three-dummy line
- **Then:** the first two Targets live through and the third despawns the instance. The Straight asset’s lists are unchanged

### Phase 27 — Two kits append in order

- **Given:** a Pierce kit and an ApplyStatus kit, both OnHit
- **When:** `Compose` applies Pierce then ApplyStatus
- **Then:** OnHit runs Pierce then ApplyStatus. Swapping kit order swaps list order

### Phase 27 — Illegal stage throws

- **Given:** `Append(RecipeStage.OnSpawn, new Pierce(1))` or a kit layer with combined stage flags
- **When:** compose / append runs
- **Then:** it throws. The base Recipe is unchanged

### Phase 27 — Unique Id per composition

- **Given:** two `Compose` results that would otherwise share the base Recipe’s `Id`
- **When:** both are spawned
- **Then:** the caller assigned distinct ids. Reusing one id still replaces the store slot (existing `RecipeStore` rule)

### Phase 28 — First lock is closest to the fire-time aim point

- **Given:** two published candidates; A is nearer in front of travel; B is nearer `HomingAcquirePoint`
- **When:** Homing spawns with `HomingAcquirePointSet` and no `HomingTarget`
- **Then:** the first lock is B

### Phase 28 — No aim point keeps travel-direction acquire

- **Given:** the same two candidates and `HomingAcquirePointSet` false
- **When:** Homing acquires
- **Then:** the first lock is A (closest in front of travel)

### Phase 28 — Retarget after a Target hit is travel-direction

- **Given:** first lock B from an aim point; a Target hit on B
- **When:** Homing acquires again
- **Then:** it uses `ClosestInTravelDirectionAcquisition` (skip B while in front); it does not pick from the old aim point

### Phase 29 — Republishing a hit target does not reorder the catalog

- **Given:** `SetHitTarget` for ids 1, 2, 3 in that order
- **When:** `SetHitTarget` is called again for id 2 with a new position (same radius)
- **Then:** `TryGet` index 0, 1, 2 is still ids 1, 2, 3; id 2 has the new position. A following `RemoveHitTarget(2)` leaves ids 1 then 3

### Phase 29 — Many SetHitTarget updates do not grow the catalog

- **Given:** 32 published volumes
- **When:** `SetHitTarget` is called again for each of those 32 ids
- **Then:** the catalog still has 32 entries (in-place update, not a second copy of each id)

### Phase 30 — Overlapping the firing collider is not a hit

- **Given:** a Straight Recipe spawned overlapping a World collider, with that collider on `SpawnRequest.IgnoreCollider`
- **When:** the first `Tick` runs
- **Then:** no `Hit`, the instance stays live, Pierce/Bounce remaining are unchanged, no `StatusApplied`

### Phase 30 — Without IgnoreCollider the same overlap despawns

- **Given:** the same overlapping spawn and `IgnoreCollider` null
- **When:** the first `Tick` runs
- **Then:** World `Hit` and the instance returns to the pool

### Phase 30 — After clear, later contact is a normal hit

- **Given:** a shot that skipped the firing collider and has moved so it no longer overlaps
- **When:** that same collider overlaps the instance again
- **Then:** Direct `Hit` as World (or Target if `IDamageable`)

### Phase 30 — Multicast extras keep the skip

- **Given:** Multicast from a spawn that set `IgnoreCollider`, extras still overlapping that collider
- **When:** those extras tick
- **Then:** they also skip it until clear

### Phase 31 — Arc Spawn solves loft

- **Given:** a Recipe with `Delivery.Arc`, reachable speed/gravity, and `HomingAcquirePointSet` at DummyTarget
- **When:** `Spawn` runs
- **Then:** the instance is lofted so the simulated path meets DummyTarget. The caller did not have to copy `BallisticAim.TrySolve` onto `Direction`

### Phase 31 — Unreachable Arc does not spawn

- **Given:** the same Arc Recipe with speed too low for the aim point
- **When:** `Spawn` runs
- **Then:** it returns default `ProjectileId` and raises no `Expired`

### Phase 31 — Straight ignores gravity

- **Given:** a Straight Recipe with `Gravity > 0`
- **When:** it flies several ticks
- **Then:** y is unchanged (level shot). The same gravity on `Delivery.Arc` drops

### Phase 31 — Hitscan published volume

- **Given:** a Hitscan Recipe and a published sphere with no GameObject collider on the ray
- **When:** `Spawn` returns
- **Then:** Direct Target `Hit` with that `TargetId` has already been raised. There is no flying view

### Phase 31 — Hitscan Pierce n+1 on published volumes

- **Given:** Hitscan + `Pierce(2)` and three published volumes along the ray
- **When:** `Spawn` returns
- **Then:** three Target Hits; the instance is pooled after the third (`Pierce(n)` absorbs n, dies on n+1)

### Phase 31 — Hitscan World ends the ray

- **Given:** Hitscan + `Pierce(2)` and a World wall before a published Target
- **When:** `Spawn` returns
- **Then:** World `Hit` only (no Pierce through the wall). With `Bounce(1)` instead, the ray reflects and may hit the Target

### Phase 31 — Hitscan Homing kink after pierce

- **Given:** Hitscan + Pierce + Homing, first published volume on the barrel ray, second off-axis
- **When:** `Spawn` returns
- **Then:** after the first pierce, the next segment kinks toward the second volume by at most Homing `n` degrees

### Phase 32 — Beam pulse is Direct OnHit

- **Given:** a Beam Recipe and a published volume on the first segment
- **When:** a pulse runs
- **Then:** Direct `Hit` (and `ApplyStatus` if authored). The instance stays live

### Phase 32 — Beam Pierce n+1 is per pulse

- **Given:** Beam + `Pierce(2)` and three published volumes along the polyline
- **When:** two pulses run
- **Then:** each pulse Hits the first two and ends on the third; the instance is still live after both pulses. `Despawn` or `LifetimeSeconds` is what ends the Beam

### Phase 32 — Lifetime caps a held Beam

- **Given:** a Beam with a short `LifetimeSeconds`, never `Despawn`ed
- **When:** remaining lifetime reaches 0
- **Then:** `OnExpire` / `Expired` run and the instance returns to the pool even if the game is still holding fire

### Phase 33 — Hitscan debug line

- **Given:** Play Mode, Hitscan preset, debug overlay on
- **When:** fire at the dummy line
- **Then:** a debug polyline shows the query (Hitscan has no mesh)

## Product constraints

- Sandbox uses URP
- Collision queries use 3D PhysX plus published hit volumes (ADR-030)
- Input System is sample-only (not a Runtime / package.json dependency)
- Runtime package stays render-pipeline agnostic (no URP-only shaders or camera code in core)
