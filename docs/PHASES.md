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
- Child directions: a small random cone is allowed (seed it). Phase 11 replaces this with `IMultiplyPattern` spawn positions

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

## Phase 9 — Combat and cast stats

Store game numbers on the Recipe (and on the instance when they can change in flight). Do **not** add a modifier per field. Phase 10 adds combat scale modifiers; phase 11 reworks Multiply layout; phase 12 scales size / effect radius / multiply spread. ADR-013. **Spread is not a Recipe stat** — it belongs on `Multiply` (phase 11 / ADR-015).

**In scope**

- Recipe fields: `Damage`, `CriticalChance` (0–1), `ManaCost`, `EffectRadius`. **Size is `Radius`** — do not add a `Size` field
- Copy damage, crit chance, effect radius, and radius onto the live instance at spawn; expose them on `ModifierContext` so a later modifier can write them without new plumbing
- `ManaCost` stays Recipe-only (read at fire time). Package does not spend mana
- `HitInfo` carries `Damage`, `CriticalChance`, and `EffectRadius` from the instance at contact (today `Hit` fires before `OnHit`)
- Default `Damage = 1` so existing dummy HP (currently −1 per hit) keeps working when the sandbox uses `Hit.Damage`. Other new fields default to 0
- Edit Mode tests: `Hit.Damage` matches Recipe; `ManaCost` readable on Recipe, unchanged by `Tick`
- Sandbox HUD shows selected Recipe damage, crit, mana, radius, effect radius. Dummy HP subtracts `Hit.Damage`

**Out of scope:** rolling crit; AoE overlap queries (phase 13); spending mana; `Damage(n)` (phase 10); Multiply patterns / `Spread` (phase 11); `Size(n)` / `EffectRadius(n)` / `Spread(n)` (phase 12); original-shot direction jitter; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire a Recipe with Damage 5 — dummy loses 5 HP. HUD shows mana/radius.

**Stop.**

## Phase 10 — Damage, critical, and mana modifiers

`Damage(n)`, `Critical(n)`, and `Mana(n)` scale the instance stats from phase 9 the way `Speed(n)` scales speed (`n > 1` up, `0 < n < 1` down, `n <= 0` no-op). ADR-014. Do not apply HP, roll crit, or spend mana.

**In scope**

- `Damage(n)` / `Critical(n)` / `Mana(n)` on `OnTravel` (start), `OnInterval` (progressive), and `OnHit` (after bounce/pierce). Not `OnSpawn`. Interval period is still `Interval(n)` on `OnSpawn` (ADR-012)
- Copy `ManaCost` onto the instance at spawn so `Mana(n)` mutates that copy, not the shared Recipe. `Recipe.ManaCost` stays the authored fire-time cost
- Clamp critical chance to `[0, 1]` after a scale
- `OnTravel` scales run before the first move, so the first `Hit` sees them. `OnHit` scales run after the current `Hit` event; later pierce/bounce hits see the new values
- Edit Mode tests: OnTravel `Damage(2)` doubles `Hit.Damage`; OnHit `Damage(2)` plus Pierce(1) leaves the first hit unscaled and doubles the second; `Mana(0.5)` halves instance mana cost and leaves `Recipe.ManaCost` unchanged; `Critical(2)` clamps at 1
- Sandbox Recipes: Damage(2) on travel (dummy loses more HP); Mana(2) HUD shows instance cost after fire without changing the Recipe readout

**Out of scope:** Multiply patterns (phase 11); `Spread(n)` / `Size(n)` / `EffectRadius(n)` (phase 12); rolling crit; spending mana; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire Damage(2) on travel — dummy loses twice the Recipe damage. Fire Pierce + OnHit Damage(2) — first dummy takes base damage, second takes double.

**Stop.**

## Phase 11 — Multiply patterns and spread

Rework `Multiply` so children spawn on an `IMultiplyPattern` instead of a 12° random cone. `Multiply.Spread` is meters between pips (0 stacks). Children still travel along the parent direction or hit normal — the pattern places **positions**, it does not aim. ADR-015. Match `IMulticastPattern`: one method, index + count, no unused editor types.

**In scope**

- `IMultiplyPattern.GetOffset(context, forward, spread, index, count)` → world-space offset from the parent/hit point. Plane basis: `right = normalize(cross(forward, world up))` (fallback `world right`), `up = cross(right, forward)`. Local +X is right, +Y is up
- `Multiply(count, childRecipe, spread = 0, pattern = null)`. Null pattern is `SquarePattern`. `Spread = 0` returns zero offsets for every pattern
- Three implementations: `SquarePattern`, `CirclePattern`, `RandomCirclePattern`. Drop `RandomConePattern` from Multiply
- Layouts below (units of `spread`, origin at parent/hit). Edit Mode tests for each named count, plus `Spread = 0` stacks, plus OnHit vs OnInterval origin/forward
- Sandbox: cycle Square / Circle / RandomCircle on Multiply Recipes; **Spread** meters row (not the later `Spread(n)` scale)

**SquarePattern** (dice pips, then fill a 3×3, then grow the grid)

3×3 lattice cell size is `spread` (`s`). Coords: `(-s,s) (0,s) (s,s)` / `(-s,0) (0,0) (s,0)` / `(-s,-s) (0,-s) (s,-s)`.

| n | Layout |
| --- | --- |
| 1 | Center `(0,0)` |
| 2 | Horizontal pair `(-s/2, 0)`, `(s/2, 0)` — distance `s` |
| 3 | Line `(-s, 0)`, `(0, 0)`, `(s, 0)` |
| 4 | Four corners of the 3×3 |
| 5 | Four corners + center (die 5) |
| 6 | Two columns of three (die 6): left and right lattice columns |
| 7 | 6 + center → **H** |
| 8 | 7 + top-center `(0, s)` |
| 9 | Full 3×3 (8 + bottom-center) |

`n > 9`: odd-sized grids 5×5, 7×7, … Cell size stays `s`. Fill the new Chebyshev ring `r = 2, 3, …` in this order: cardinal middles `(0,±r)`, `(±r,0)`; then toward the corners (for `k = 1 .. r-1`, the eight edge cells `(±k,±r)` and `(±r,±k)`); then the four corners `(±r,±r)`. First extra pip is top-middle of the 5×5.

**CirclePattern**

| n | Layout |
| --- | --- |
| 1 | Center (same as square) |
| 2 | Horizontal pair (same as square) |
| 3 | Equilateral triangle, side `s`, centroid at origin, point up |
| 4 | That triangle + center |
| 5–10 | Center + `n-1` equally spaced on a ring, first pip up. Adjacent **chord** is `s`, so radius `s / (2 sin(π / ringCount))` |

`n > 10`: keep the n=10 pose (center + 9). Fill an **inner** ring of up to 6 pips at radius `s`, equally spaced, first pip up. Then start a new **outside** ring: `m` pips equally spaced, chord `s` (radius `s / (2 sin(π / m))` for `m ≥ 2`; `m = 1` sits up at previous outer radius `+ s`). That outside ring is full at 12; then ring 3, and so on (capacity `6k` on ring `k ≥ 2`). Never shrink an outer ring inside a previous one — if the chord formula would, use previous outer radius `+ s`.

**RandomCirclePattern**

Disk in the same plane, radius `= spread` (count does not grow the disk). `n = 1` at the origin. `n > 1`: uniform in the disk (world seed via `NextFloat`). `spread = 0` stacks.

**Out of scope:** `Spread(n)` / `Size(n)` / `EffectRadius(n)` (phase 12); line / arc / cross / triangle patterns; a pattern editor or unused `OffsetPattern` class; original-shot jitter; changing `IMulticastPattern`; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire Square 5 — die 5 on the dummy. Square 7 — H. Square 9 — 3×3. Circle 3 — triangle. Circle 6 — center + pentagon. RandomCircle — scatter inside a disk of radius Spread. Spread 0 — stacked.

**Stop.** A visual pattern editor is **more** work than these three types (Editor window, assets, preview). Do not pivot this phase into one. `IMultiplyPattern` is the same extension shape as `IMulticastPattern`; an editor later authors a new implementation (local offset list × spread). Unbounded `n > 9` / `n > 10` still needs the overflow rules above, which a static offset list cannot replace.

## Phase 12 — Effect radius, size, and spread scale modifiers

`EffectRadius(n)` and `Size(n)` scale the remaining phase 9 stats. `Size(n)` scales `Radius` — still no second size field. `Spread(n)` scales the instance copy of `Multiply.Spread`. ADR-014. Do not jitter the original shot.

**In scope**

- Copy authored `Multiply.Spread` onto the instance at spawn (default scale 1) so `Spread(n)` mutates that copy. Multiply patterns read the instance value, not only the modifier field
- `Spread(n)` / `EffectRadius(n)` / `Size(n)` on `OnTravel`, `OnInterval`, and `OnHit`. Same scale contract as `Speed(n)` / phase 10. Not `OnSpawn`
- Multicast extras still use ADR-006 patterns
- `Size(n)` must be able to write `Radius` on `ModifierContext` (phase 9 may have left it read-only). SphereCast and the view scale with the new radius
- `OnHit` `EffectRadius(n)` / `Size(n)` follow the same Hit-then-OnHit timing as phase 10
- Edit Mode tests: OnTravel `Size(2)` doubles radius; OnTravel `Spread(2)` with `Multiply(2)` Square `Spread = 2` places the pair 4 m apart; `Spread = 0` still stacks; OnTravel `EffectRadius(2)` doubles `HitInfo.EffectRadius`; `n <= 0` is a no-op
- Sandbox Recipes: Size(2) looks and hits larger; Spread(2) on travel doubles Square pip spacing

**Out of scope:** AoE overlap queries (phase 13); `Damage(n)` (phase 10); new multiply patterns (phase 11); original-shot direction jitter; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire Size(2) — sphere is larger and contacts sooner. Fire Spread(2) on travel with Square Multiply Spread 2 — pips 4 m apart.

**Stop.**

## Phase 13 — Area effects

`AreaEffect` on `OnHit`, `OnTravel`, and `OnInterval`. Same modifier, three stages. Size is instance `EffectRadius` (phase 9 field, phase 12 scale). Area **damage** is authored on the modifier, not `Recipe.Damage`. Optional status too. Whether the projectile still applies its own contact damage/status is a pair of flags. ADR-016. Package still does not apply HP or tick DoT.

**In scope**

- `AreaEffect(damage, status = null, includeDirectHit = true, includeDirectStatus = true)`
- Query a PhysX overlap sphere of instance `EffectRadius` (0 = no-op). `IHitQuery.Overlap` — Edit Mode `ScriptedHitQuery`, Play Mode `PhysicsHitQuery`. Only `IDamageable` Targets; ignore World and other projectiles. Modifier queues the request on `ModifierContext`; it does not call Physics
- Each Target in the sphere raises `Hit` with `HitSource.Area` and the **modifier** damage, then `StatusApplied` if `status` is set. Same events as today; sandbox dummy HP still listens to `Hit.Damage`
- **OnHit:** sphere at `Hit.Point`. **OnTravel:** sphere at spawn position, once, before the first move. **OnInterval:** aura — sphere at the instance’s current position on each interval crossing (`Interval(n)` on `OnSpawn` still required). The volume moves with the projectile; it is not a ground zone left behind. Not `OnTick` (that would apply every frame)
- Direct contact `HitInfo` is still **sampled** before `OnHit` (phase 10 OnHit `Damage(n)` does not rewrite this contact). Raise that event **after** the `OnHit` list unless `includeDirectHit` is false (`SuppressDirectHit`). Direct `ApplyStatus` still flushes after the list unless `includeDirectStatus` is false. Area Hits/status flush after that
- `includeDirectHit` / `includeDirectStatus` apply only on `OnHit`. OnTravel / OnInterval have no contact to suppress
- If both flags stay true, the contacted dummy can receive a Direct Hit **and** an Area Hit (projectile damage + area damage)
- Edit Mode tests: OnHit splash hits a nearby scripted Target; area-only suppresses the Direct Hit; OnTravel splash at spawn; OnInterval aura hits a Target beside the flight path on the first interval and again on the second; `EffectRadius = 0` hits nobody; World overlap is ignored; area status raises `StatusApplied` per Target
- Sandbox: OnHit splash with two dummies in radius; area-only (contact dummy takes area damage, not Recipe damage); OnTravel burst if a dummy is at the muzzle; aura flying the lane ticks anyone inside `EffectRadius`

**Out of scope:** lingering ground fields; occupancy enter/exit; `OnTick` aura; `OnExpire` area; scaling area damage with `Damage(n)`; rolling crit; applying HP/DoT in the package; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire OnHit AreaEffect — DummyTarget and a nearby dummy both lose area damage. Fire area-only — contact dummy loses area damage only. Fire OnTravel AreaEffect — dummy at muzzle is hit immediately. Fire interval aura — dummy beside the lane loses HP each interval while the shot is in range, then stops when it has passed.

**Stop.**

## Later (not scheduled)

- Unity Physics and/or Entities Graphics adapters
- Editor Recipe authoring
- Multiply patterns: line, arc, cross, triangle; a pattern editor that authors an `IMultiplyPattern`
- Lingering ground fields; occupancy enter/exit or `OnTick` auras
- More modifiers, optional Rigidbody solver, projectile-vs-projectile hits
- Package samples vs sandbox split cleanup
- Rename the UPM id if the double `com.davidvdveerdonk.com.veerdonk` prefix should go
