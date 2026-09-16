# Phases

Agents build the **initial version** through these phases. After each phase: stop, human Play Mode check, code review, refine. Do not start the next phase in the same session unless the human says so.

Composition, API, and invariants: [ARCHITECTURE.md](ARCHITECTURE.md). Scenarios: [REQUIREMENTS.md](REQUIREMENTS.md). How to play the lab: [SANDBOX.md](SANDBOX.md).

## Rules

- Implement **exactly one phase** per session
- Do not add types, modifiers, or folders that the phase does not name
- Plan mode first if the phase needs new public types; match the API sketch
- Prefer Edit Mode tests; Play Mode only for pool + physics
- After the human refines, the next phase starts from that code — no parallel redesign

## Phase 0 — Lab shell

**In scope**

- Sandbox scene: ground, camera, spawn point, dummy target, fire control, pool-count readout (can be on-screen text)
- Confirm the embedded UPM package and asmdefs compile
- Remove or ignore Unity template `*Example*` scripts when they get in the way (do not replace them with a second framework)

**Out of scope:** movement, hits, pooling logic, modifiers

**Proof:** enter Play Mode, press fire (or equivalent). A spawn request is visible even if the projectile does not fly yet. Camera looks at the lane.

**Stop.**

## Phase 1 — Golden example

Fly, lifetime expire, `Target` hit, pool, `OnHit` spawn with an **explicit child Recipe**.

**In scope**

- `IProjectileWorld` (`Spawn` / `Tick` / `Hit` / `Expired`)
- Recipe base stats including `LifetimeSeconds`
- Kinematic move, SphereCast/overlap, `HitKind.Target` and `HitKind.World` (world hits despawn; no bounce yet)
- Pool rent/return
- `OnHit` spawn modifier with an explicit child Recipe (child velocity along hit normal)
- Empty stage lists other than the `OnHit` spawn used in the golden path
- Sandbox dummy that listens to `Hit` and reduces HP
- Edit Mode tests: move, expire, hit despawn, pool counts, child spawn pose
- Play Mode: fire, hit cube, pool count unchanged; fire OnHit-spawn Recipe; fire lifetime-only Recipe

**Out of scope:** pierce, bounce, multiply interval, multicast, homing, editor windows, Unity Physics, Entities Graphics, VFX beyond a primitive mesh

**Golden path**

1. Fire a kinematic sphere from the sandbox spawn point
2. It moves, expires on `LifetimeSeconds`, or hits a dummy
3. It returns to the pool
4. An `OnHit` spawn modifier fires a second instance from the explicit child Recipe

**Stop.** Human refines names and feel before phase 1b.

## Phase 1b — Hybrid ECS

Keep phase 1 behaviour. Move live instances onto Entities.

**In scope**

- ADR-005 and [ECS.md](ECS.md)
- Isolated Entities `World` owned by `ProjectileWorld` (`IDisposable`)
- Blittable instance components; entity recycle via `Disabled`
- Burst lifetime + kinematic candidate position
- PhysX `IHitQuery` and GameObject views unchanged
- Recipe / `IModifier` stay managed (`RecipeIndex` on the entity)
- Existing Edit Mode and Play Mode tests; dispose worlds
- Sandbox still fires the three Recipes

**Out of scope:** pierce, bounce, multiply interval, multicast, homing, Unity Physics, Entities Graphics, baking, Player Loop bootstrap

**Proof:** same as phase 1. Plus: Edit Mode tests pass against the ECS world; sandbox `OnDestroy` disposes.

**Stop.** Walk through the ECS pieces for the human ([ECS.md](ECS.md)). Do not start phase 2.

## Phase 2 — Pierce and bounce

**In scope**

- `Pierce(n)` on `OnHit` for `HitKind.Target`
- `Bounce(n)` on `OnHit` (reflect velocity around `HitInfo.Normal`; v1 started as World-only, [ADR-010](DECISIONS.md) also reflects `Target`)
- Remaining counts on the instance; despawn when the relevant count cannot absorb the hit
- Scenarios: Pierce(2), Bounce(1) then target
- Tests for remaining counts and reflection
- Snapshot live entities **once** after expire in `ProjectileSimulationSystem` and reuse that list for `OnTick` and move/hit (skip if a hit already disabled the entity). Do not query-copy-dispose three times; pierce/bounce will despawn mid-move. See [ECS.md](ECS.md) “One tick”

**Out of scope:** multiply, multicast, homing, interval

**Stop.**

## Phase 3 — Multiply on interval, hit, and expire

**In scope**

- `Multiply(n)` with an explicit child Recipe
- `OnInterval` (e.g. every 0.5s)
- `OnHit` and `OnExpire` multiply (killing hit: `OnHit` then `OnExpire`)
- Spread on interval children may be a small random cone; seed it

**Out of scope:** multicast, homing

**Stop.**

## Phase 4 — Multicast

**In scope**

- `Multicast(n)` on `OnSpawn`
- Extra instances must not recurse multicast (skip or strip `Multicast` on extras)
- Scenario: Multicast(3) → three instances

**Out of scope:** homing; a `Timer` modifier (lifetime is already `Recipe.LifetimeSeconds` from phase 1)

**Stop.**

## Phase 5 — Homing

**In scope**

- `Homing(n)` on `OnTick`
- Homing target on `SpawnRequest` when assigned; otherwise the instance acquires via `IHomingAcquisition` (v1: closest candidate in the travel direction)
- Strong `Homing(100)` hits; weak `Homing(10)` misses in the sandbox layout
- Document what `n` means (degrees per second) in code comments once chosen

**Out of scope:** editor tooling, Unity Physics, Entities Graphics, extra modifiers

**Stop.**

## Phase 6 — Modifier combinations

Prove that named modifiers from phases 2–5 still do their jobs when they share a Recipe. Combinations may already work; **tests are still required**. Fix bugs those tests find. Do not add modifiers, public types, or a second composition model.

**In scope**

- One Recipe with `Pierce(1)` on `OnHit` and `Homing(300)` on `OnTick`: pierce the first `Target`, stay live, drop that homing lock, skip the pierced dummy while it stays in front of travel, then acquire the next dummy and steer. No public retarget API ([ADR-010](DECISIONS.md)). Sandbox uses 300°/s so the instance can leave DummyTarget instead of looping back at 20 speed.
- One Recipe with `Bounce(2)` and `Multiply(2)` on `OnHit` (explicit child Recipe): each of two `World` hits reflects the parent and spawns two children. Children do not inherit Bounce/Multiply ([ADR-002](DECISIONS.md)). Bounce also reflects `Target` hits while still raising `Hit`.
- Edit Mode tests for both scenarios (scripted hits). Assert remaining counts, that the parent stays live after the absorbed hits, child spawn count (2 per bounce), and that homing acquires a new target after pierce.
- Sandbox Recipes and Play Mode proofs for both scenarios. Add a second World wall only if the current wall cannot produce two bounces.
- Confirm existing list rules: the full `OnHit` list runs even when Pierce/Bounce sets `CancelDespawn`; a surviving instance still runs `OnTick` on later ticks. See [ARCHITECTURE.md](ARCHITECTURE.md) invariants.

**Out of scope:** new modifiers or `SpawnRequest` fields; cartesian product of every modifier pair; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire Pierce(1)+Homing(300): first dummy is pierced, instance curves into DummyBounce after it leaves the first dummy ahead. Fire Bounce(2)+Multiply(2): two wall hits, two extra instances per bounce, parent still alive. Bounce off a dummy still drops dummy HP.

**Stop.**

## Phase 7 — Speed up / down

`Speed(n)` scales the instance’s current speed (`n > 1` faster, `0 < n < 1` slower). Same modifier on three stages; not a seventh stage and not `OnSpawn` (that list stays multicast). Preserve travel direction when speed changes. `n <= 0` is a no-op.

**In scope**

- `Speed(n)` on `OnTravel`: spawn-time / one-shot setup so the instance **starts** slower or faster than `Recipe.Speed`
- `Speed(n)` on `OnInterval` (same elapsed-time crossings as `Multiply`, e.g. every 0.5s): speed scales again each time the interval fires, so the instance can get progressively slower or faster
- `Speed(n)` on `OnHit` for both `HitKind.Target` and `HitKind.World`, so pierce and bounce can change speed after a hit that keeps the parent alive. List order still applies (`Bounce` then `Speed` scales the reflected velocity)
- Generalize `OnInterval` so it is not `Multiply`-only. Do not skip `Speed` when multiply generations are 0. Do not add an interval interface
- Refined after review: the period is per Recipe, not per modifier — `Interval(n)` on `OnSpawn` sets it and `Multiply` / `Speed` dropped their `intervalSeconds` ([ADR-012](DECISIONS.md))
- Edit Mode tests for start scale, interval re-scale, and OnHit scale after Bounce(1) and Pierce(1)
- Sandbox Recipes: start faster, interval slowdown, bounce-then-faster

**Out of scope:** `ApplyStatus`; `Speed` on `OnTick` or `OnSpawn`; additive speed; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire `Speed(2)` on `OnTravel` — leaves the muzzle faster than the straight Recipe. Fire interval `Speed(0.5)` — visibly slows each 0.5s. Fire Bounce(1)+`Speed(2)` — faster after the wall.

**Stop.**

## Phase 8 — Status on hit

Apply a status effect on `HitKind.Target`. Do not simulate burn, poison, or shock. ADR-011, an apply interface, and one dummy effect are enough.

**In scope**

- ADR-011 and the public types it names: `IStatusEffect`, `DummyStatusEffect`, `IStatusReceiver`, `ApplyStatus`, `StatusInfo`
- `ApplyStatus` on `OnHit` for `HitKind.Target` only (World hits do nothing)
- `IProjectileWorld.StatusApplied` after the `OnHit` list (the existing `Hit` event stays HP-only and still fires before `OnHit`)
- Package does not call `IStatusReceiver`, tick DoT, or change HP. Sandbox dummy implements `IStatusReceiver`; the lab maps `StatusApplied` → `Apply`. HUD shows the effect id
- Edit Mode tests: Target hit raises `StatusApplied` with `DummyStatusEffect`; World hit does not; Pierce(1)+`ApplyStatus` raises once per Target hit
- Sandbox Recipe that applies the dummy effect on the first dummy

**Out of scope:** burn / poison / shock classes, duration, stacking, cleansing, `Speed`, editor tooling, Unity Physics, Entities Graphics

**Proof:** fire ApplyStatus; dummy HUD shows `dummy`. Fire at the wall; no status. Pierce through two dummies; both show the effect.

**Stop.**

## Later (not scheduled)

- Unity Physics and/or Entities Graphics adapters
- Editor Recipe authoring
- More modifiers, optional Rigidbody solver, projectile-vs-projectile hits
- Package samples vs sandbox split cleanup
- Rename the UPM id if the double `com.davidvdveerdonk.com.veerdonk` prefix should go
