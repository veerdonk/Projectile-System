# Requirements

A reusable 3D Unity projectile package. Each projectile is defined by a **Recipe**: base stats plus **six ordered modifier lists**, one list per lifetime stage.

Agents implement the first version **one phase at a time** ([PHASES.md](PHASES.md)). Human review and refinement happen after each phase. Process rules live in [AGENTS.md](../AGENTS.md), not here.

## Glossary

Use these words only. Do not introduce synonyms in code or docs without an ADR.

- **Projectile** — the concept of a spell, bullet, missile, or shot (not a live object)
- **Recipe** — authored definition: base stats (speed, radius, lifetime, presentation id, combat/cast numbers) plus six modifier lists
- **Instance** — one live simulated projectile
- **Modifier** — a named behavior with a magnitude, written `Name(magnitude)` (see below)
- **Stage list** — one of the six ordered modifier lists on a Recipe (`OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`). See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Child recipe** — a full Recipe passed into a spawn modifier. Children do **not** inherit the parent’s lists unless that modifier says so
- **Hit** — a collision result from the hit pipeline, not Unity’s `OnCollision*` messages
- **Hit kind** — `Target` (damageable sandbox object) or `World` (wall / environment). Other projectiles are ignored until an ADR says otherwise
- **Target** — the object described by a `Target` hit
- **Homing target** — the object an instance is steering toward (may differ from what it eventually hits)
- **Effect** — presentation or gameplay reaction associated with a modifier or event (VFX, audio). Damage is a sandbox effect, not package logic
- **Status effect** — a named gameplay condition requested on a Target hit (burn, poison, shock, …). The package delivers the request; the game simulates it. v1 ships `DummyStatusEffect` only
- **Pool** — inactive instances reused on spawn; despawn returns an instance to the pool
- **Damage** — authored hit amount copied onto the instance and `HitInfo`. The package does not apply HP
- **Critical chance** — probability 0–1 on the instance and `HitInfo`. The package does not roll a crit
- **Mana cost** — authored fire-time cost on the Recipe. Phase 10 copies it onto the instance so `Mana(n)` can scale that copy; the package does not spend mana
- **Spread** — meters between child spawn points when `Multiply` creates more than one instance. Authored on `Multiply`, not on the Recipe. 0 stacks children on the same point
- **Effect radius** — authored radius for a hit/expire gameplay effect (explosion, etc.). Distinct from projectile `Radius`. The package does not run AoE queries from this field in v1
- **Size** — projectile collision size. This **is** `Recipe.Radius`; there is no second size field
- **Magnitude** — the number in `ModifierName(n)`:
  - **Count** for pierce, bounce, multicast, multiply (how many)
  - **Strength** for homing (how hard it turns)
  - **Scale** for speed, damage, critical chance, mana cost, multiply spread, effect radius, and size (multiplier: `Speed(2)` doubles, `Speed(0.5)` halves; same for `Damage`, `Critical`, `Mana`, `Spread`, `EffectRadius`, `Size`)
  - **Seconds** for interval (`Interval(0.5)` runs `OnInterval` every half second)
  - **Distance** for multiply spread (`Multiply` `Spread` is meters between child spawn points)

### Named modifiers (v1)

- **Pierce(n)** — `OnHit` + `Target`: pass through `n` targets; despawn on hit `n+1`
- **Bounce(n)** — `OnHit`: reflect off `n` `World` or `Target` hits; despawn on hit `n+1`. `Hit` still fires on `Target`, so dummy HP still drops
- **Homing(n)** — `OnTick`: steer toward a homing target; higher `n` turns faster. A `Target` hit drops the lock and the instance may acquire another candidate once that dummy is no longer in front of travel
- **Multicast(n)** — `OnSpawn`: total instances created at spawn is `n` (the original plus `n-1` extras)
- **Multiply(n)** — extra instances spawned from `OnInterval`, `OnHit`, or `OnExpire` (not from `OnSpawn`). Optional `Spread` is meters between those child spawn points
- **Speed(n)** — scale current speed by `n` (`n > 1` faster, `0 < n < 1` slower). `OnTravel` (start), `OnInterval` (progressive), `OnHit` (after bounce or pierce). Not `OnSpawn`
- **Interval(n)** — `OnSpawn`: seconds between `OnInterval` runs for this instance. One period per Recipe drives every modifier on that list; without it `OnInterval` never runs (see [DECISIONS.md](DECISIONS.md) ADR-012)
- **Damage(n)** — scale instance damage by `n`. Same stages as `Speed(n)`. Package still does not apply HP
- **Critical(n)** — scale instance critical chance by `n`, then clamp to `[0, 1]`. Same stages as `Speed(n)`. Package still does not roll a crit
- **Mana(n)** — scale the instance mana-cost copy by `n`. Same stages as `Speed(n)`. Does not change `Recipe.ManaCost` or spend mana
- **Spread(n)** — scale the instance multiply-spread (distance between `Multiply` child spawn points) by `n`. Same stages as `Speed(n)`. Does not jitter the original shot
- **EffectRadius(n)** — scale instance effect radius by `n`. Same stages as `Speed(n)`. Package still does not run AoE queries
- **Size(n)** — scale `Radius` by `n`. Same stages as `Speed(n)`. There is no separate size field
- **ApplyStatus** — `OnHit` + `Target`: request an `IStatusEffect` on the hit Target. The package does not simulate the effect (see [DECISIONS.md](DECISIONS.md) ADR-011)
- **Lifetime** — `Recipe.LifetimeSeconds` is the despawn clock. There is no separate `Timer` modifier (see [DECISIONS.md](DECISIONS.md))

## Goals

- 3D projectiles only
- Unity 6 LTS (project is `6000.6.0f1`)
- Ship as an embedded UPM package with a sandbox lab
- GameObject adapters around hybrid ECS simulation (Entities for instance state, PhysX hits, GameObject views; see [DECISIONS.md](DECISIONS.md) ADR-005)
- Composition via the six stage lists
- Object pooling required
- Sandbox scene for development and Play Mode proof only

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

**Package (`Packages/com.davidvdveerdonk.com.veerdonk.projectile-system`)**

- Spawn, simulate, compose (stage lists), pool
- Hit pipeline and events
- Hybrid ECS simulation (`com.unity.entities`; see [ECS.md](ECS.md))
- Later: developer tooling for Recipes (not before the “later” phase)

**Game / sandbox (`Assets/`)**

- Cameras, lighting, URP
- Input handling and debug compose / fire UI
- Dummy targets (HP, `IDamageable`, `IStatusReceiver`)
- Player or enemy objects if you add them later

If a type needs `Player`, `Enemy`, or camera code, it does not belong in the package.

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
- **Then:** two child instances spawn roughly in the direction of travel (a small random cone is allowed until phase 11 replaces it with `Multiply.Spread` meters)

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

### Phase 11 — Size(2) at spawn

- **Given:** a Recipe with `Radius = 0.25` and `Size(2)` on `OnTravel`
- **When:** the instance is spawned
- **Then:** instance radius is 0.5

### Phase 11 — Multiply spread is spawn-point distance

- **Given:** `Multiply(2)` with `Spread = 2` on `OnHit` and an explicit child Recipe
- **When:** the parent records a hit
- **Then:** the two children spawn 2 meters apart on a line perpendicular to the hit normal, centered on the hit point

### Phase 11 — Spread(2) widens Multiply spacing

- **Given:** the same `Multiply(2)` with `Spread = 2`, and `Spread(2)` on `OnTravel`
- **When:** the parent records a hit
- **Then:** the two children spawn 4 meters apart

### Phase 11 — EffectRadius(2) at spawn

- **Given:** a Recipe with `EffectRadius = 1` and `EffectRadius(2)` on `OnTravel`
- **When:** the instance records a hit
- **Then:** `HitInfo.EffectRadius` is 2

## Product constraints

- Sandbox uses URP
- Collision queries use 3D PhysX
- Input System is sandbox-only
- Runtime package stays render-pipeline agnostic (no URP-only shaders or camera code in core)
