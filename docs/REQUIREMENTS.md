# Requirements

A reusable 3D Unity projectile package. Each projectile is defined by a **Recipe**: base stats plus **six ordered modifier lists**, one list per lifetime stage.

Agents implement the first version **one phase at a time** ([PHASES.md](PHASES.md)). Human review and refinement happen after each phase. Process rules live in [AGENTS.md](../AGENTS.md), not here.

## Glossary

Use these words only. Do not introduce synonyms in code or docs without an ADR.

- **Projectile** — the concept of a spell, bullet, missile, or shot (not a live object)
- **Recipe** — authored definition: base stats (speed, radius, lifetime, presentation id) plus six modifier lists
- **Instance** — one live simulated projectile
- **Modifier** — a named behavior with a magnitude, written `Name(magnitude)` (see below)
- **Stage list** — one of the six ordered modifier lists on a Recipe (`OnSpawn`, `OnTravel`, `OnTick`, `OnInterval`, `OnHit`, `OnExpire`). See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Child recipe** — a full Recipe passed into a spawn modifier. Children do **not** inherit the parent’s lists unless that modifier says so
- **Hit** — a collision result from the hit pipeline, not Unity’s `OnCollision*` messages
- **Hit kind** — `Target` (damageable sandbox object) or `World` (wall / environment). Other projectiles are ignored until an ADR says otherwise
- **Target** — the object described by a `Target` hit
- **Homing target** — the object an instance is steering toward (may differ from what it eventually hits)
- **Effect** — presentation or gameplay reaction associated with a modifier or event (VFX, audio). Damage is a sandbox effect, not package logic
- **Pool** — inactive instances reused on spawn; despawn returns an instance to the pool
- **Magnitude** — the number in `ModifierName(n)`:
  - **Count** for pierce, bounce, multicast, multiply (how many)
  - **Strength** for homing (how hard it turns)

### Named modifiers (v1)

- **Pierce(n)** — `OnHit` + `Target`: pass through `n` targets; despawn on hit `n+1`
- **Bounce(n)** — `OnHit` + `World`: reflect off `n` world hits; despawn on world hit `n+1`
- **Homing(n)** — `OnTick`: steer toward a homing target; higher `n` turns faster
- **Multicast(n)** — `OnSpawn`: total instances created at spawn is `n` (the original plus `n-1` extras)
- **Multiply(n)** — extra instances spawned from `OnInterval`, `OnHit`, or `OnExpire` (not from `OnSpawn`)
- **Lifetime** — `Recipe.LifetimeSeconds` is the despawn clock. There is no separate `Timer` modifier (see [DECISIONS.md](DECISIONS.md))

## Goals

- 3D projectiles only
- Unity 6 LTS (project is `6000.6.0f1`)
- Ship as an embedded UPM package with a sandbox lab
- GameObject adapters around plain C# simulation data (DOTS-ready data, not a DOTS runtime yet)
- Composition via the six stage lists
- Object pooling required
- Sandbox scene for development and Play Mode proof only

## Non-goals (v1)

- Pixel or material physics
- Networking
- DOTS / Entities runtime
- Player controller, inventory, wand UI, mana / spell points
- A second composition model (Noita wrap-the-next-spell, behaviour trees, visual graphs)
- `IDamageable` or HP inside the runtime package (sandbox listens to `Hit` and applies damage)
- New folders or patterns without an ADR

## Package vs game boundary

**Package (`Packages/com.davidvdveerdonk.com.veerdonk.projectile-system`)**

- Spawn, simulate, compose (stage lists), pool
- Hit pipeline and events
- Later: developer tooling for Recipes (not before the “later” phase)

**Game / sandbox (`Assets/`)**

- Cameras, lighting, URP
- Input handling and debug compose / fire UI
- Dummy targets (HP, `IDamageable` if useful)
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
- **Then:** it reflects off the wall and does not despawn; it despawns on the `Target` hit (bounce remaining was already 0 for a second world hit, and pierce is 0)

### Phase 3 — Multiply(2) every 0.5s of travel

- **Given:** `Multiply(2)` on `OnInterval` with interval 0.5s and an explicit child Recipe
- **When:** the parent has travelled for 0.5 seconds
- **Then:** two child instances spawn roughly in the direction of travel (small random spread is allowed)

### Phase 3 — Multiply(1) on expire

- **Given:** `Multiply(1)` on `OnExpire` with an explicit child Recipe
- **When:** the parent despawns because of lifetime or a killing hit (after `OnHit` runs)
- **Then:** one child instance spawns from the expire modifier using the child Recipe

### Phase 4 — Multicast(3)

- **Given:** `Multicast(3)` on `OnSpawn` with an explicit child/sibling Recipe (or the same Recipe without re-applying multicast — see [ARCHITECTURE.md](ARCHITECTURE.md))
- **When:** the Recipe is spawned once
- **Then:** three instances exist immediately (1 + 2 extras)

### Phase 5 — Strong homing

- **Given:** `Homing(100)` on `OnTick` and a homing target assigned; initial velocity points away from the target
- **When:** the instance travels
- **Then:** it turns hard enough to hit the homing target under sandbox distances

### Phase 5 — Weak homing

- **Given:** `Homing(10)` on `OnTick` and the same miss-setup as the strong case
- **When:** the instance travels
- **Then:** it turns toward the target but misses at sandbox distances

## Product constraints

- Sandbox uses URP
- Collision queries use 3D PhysX
- Input System is sandbox-only
- Runtime package stays render-pipeline agnostic (no URP-only shaders or camera code in core)
