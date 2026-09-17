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

Disk in the same plane, radius `= spread` (count does not grow the disk). `n = 1` at the origin. `n > 1`: uniform in the disk (world seed via `NextFloat`), then reject samples closer than the child diameter (`2 * Radius`). `spread = 0` stacks.

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

**Out of scope:** lingering ground fields (phase 19); occupancy enter/exit; `OnTick` aura; `OnExpire` area; scaling area damage with `Damage(n)`; rolling crit; applying HP/DoT in the package; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire OnHit AreaEffect — DummyTarget and a nearby dummy both lose area damage. Fire area-only — contact dummy loses area damage only. Fire OnTravel AreaEffect — dummy at muzzle is hit immediately. Fire interval aura — dummy beside the lane loses HP each interval while the shot is in range, then stops when it has passed.

**Stop.**

## Phase 14 — Recipe gravity

`Recipe.Gravity` is downward acceleration in m/s². Default **0**, so every existing straight shot stays a straight shot. The Burst integrate applies it; the path is calculated each tick, not a predefined curve. ADR-017.

**In scope**

- `Recipe.Gravity` (float, default 0). Copy onto the live instance at spawn (`ModifierContext.Gravity` writable so phase 15 can scale it)
- Burst integrate: `velocity += worldDown * gravity * dt`, then `candidate = position + velocity * dt` (world down is `(0,-1,0)`, not `Physics.gravity`). `Gravity <= 0` uses the current `candidate = pos + vel * dt` path
- SphereCast still runs along that segment. Bounce still reflects the current velocity (which may already point down)
- Edit Mode tests: `Gravity = 0` stays level (y unchanged over several ticks); `Gravity > 0` decreases y while x/z follow launch direction; bounce still reflects
- Sandbox: a cannonball Recipe with `Gravity > 0`, same speed as the straight shot, aimed ~35° up — it arcs and falls; the straight Recipe is unchanged

**Out of scope:** `Gravity(n)` (phase 15); ballistic aim helper (phase 16); `OnTick` gravity modifier; Rigidbody; `Physics.gravity`; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire the straight Recipe — still hits DummyTarget on the lane. Fire the cannonball aimed up — visible drop, lands short unless you raise speed or flatten the aim.

**Stop.**

## Phase 15 — Gravity scale modifier

`Gravity(n)` scales instance gravity the way `Speed(n)` scales speed (`n > 1` more drop, `0 < n < 1` less, `n <= 0` no-op). ADR-018. The Burst job still applies gravity; the modifier must **not** add `g * dt` itself (that would double-apply, and OnTick would compound every frame).

**In scope**

- `Gravity(n)` on `OnTravel` (start), `OnInterval` (progressive), and `OnHit` (after bounce/pierce). Not `OnSpawn`. Not `OnTick`
- Edit Mode tests: OnTravel `Gravity(2)` with Recipe `Gravity = 10` drops as 20 m/s²; OnInterval `Gravity(0.5)` halves drop each period; `n <= 0` leaves gravity unchanged; `Recipe.Gravity` stays the authored value
- Sandbox: cannonball with Gravity(2) on travel falls faster than the phase 14 cannonball

**Out of scope:** ballistic aim (phase 16); changing the Burst integrate; Rigidbody; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire Gravity(2) on travel with the cannonball Recipe — it drops harder than the unscaled cannonball.

**Stop.**

## Phase 16 — Ballistic aim helper

A fire-time solver that returns a launch **direction** so a given speed and gravity pass through a point. Flight is still phase 14 integrate. Not a spline, not a modifier, not a seventh stage. ADR-019.

**In scope**

- Public `BallisticAim.TrySolve(origin, target, speed, gravity, out direction, highArc = false)` → `false` if unreachable. Uses world down. Low arc is the default of the two classic solutions
- Callers put `direction` on `SpawnRequest.Direction`. Do not add a second façade or a `Spawn` overload that aims for you
- Edit Mode tests: reachable dummy at sandbox range returns a direction that, when simulated with that speed/gravity, the instance’s path meets the target (within a small tolerance); too-far or `gravity = 0` with a high target returns false (gravity 0 only works if the target lies on the ray)
- Sandbox: cannonball fire uses the helper toward DummyTarget — it hits; toggle high arc if both solutions exist

**Out of scope:** replacing tick integrate with closed-form `x(t)` during flight; Rigidbody; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire the aimed cannonball — it hits DummyTarget. Fire with speed too low — helper returns false and the lab does not spawn.

**Stop.** After phases 14–16 are implemented, **schedule a new phase** (do not build it in the same session) for the sample-scene items that waited on gravity: turret **barrel pitch** and a **ballistic preset** that calls `BallisticAim.TrySolve` (phase 21 left these under Later). Hold-to-fire, WASD, and orbit camera stay Later unless that follow-up says otherwise.

## Phase 17 — Projectile protection

Collider marker, same idea as `IDamageable`: the package classifies the hit; it still does not subtract HP. Two levels. ADR-020. This **does** belong in the package — it changes bounce, pierce, despawn, and which events fire, not how a health bar works.

**In scope**

- `IProjectileProtection { ProtectionLevel Level { get; } }` with `ProtectionLevel { None, Basic, Strong }`. `PhysicsHitQuery` uses `GetComponentInParent` like `IDamageable`. If both markers exist, `Kind` still comes from `IDamageable`; `HitInfo.Protection` comes from the protection marker
- **Basic:** run `OnHit` (Bounce and Pierce still apply — Pierce also consumes on Basic even when `Kind` is World). `AreaEffect` still runs. Do **not** flush `ApplyStatus` for this contact. Raise Direct `Hit` with `Protection = Basic` so the game can skip HP (and still play a block spark if it wants). Sandbox ignores HP when `Protection != None`
- **Strong:** return the instance to the pool immediately. No Direct `Hit`, no `StatusApplied`, no `OnHit` (no bounce, pierce, area, multiply-on-hit), no `OnExpire`. Still raise `Expired` so pool counts stay honest
- Area overlap (phase 13): skip Strong colliders; Basic colliders may appear, and those Area Hits carry `Protection = Basic`
- Edit Mode tests via `ScriptedHitQuery`: Basic + Bounce(1) reflects and no `StatusApplied`; Basic + Pierce(1) continues; Strong eats a bouncing shot with no `Hit` and no child from `OnHit`/`OnExpire` Multiply; unprotected Target is unchanged
- Sandbox: Basic dummy (invulnerable) next to DummyTarget; Strong shield cube. Straight shot at Basic — bounce/despawn as World-like motion, dummy HP unchanged. Straight shot at Strong — disappears, no splash Recipe if you fire AreaEffect at it

**Out of scope:** factions, i-frames, shield HP, projectile-vs-projectile; applying HP in the package; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire at the Basic dummy — it does not lose HP; a Bounce Recipe reflects. Fire at the Strong shield — the shot is gone, no `Hit`, no area splash.

**Stop.**

## Phase 18 — View factory

The pool stops hard-coding `CreatePrimitive(Sphere)`. It asks an `IProjectileViewFactory` for a view from `Recipe.PresentationId`. Hits stay the kinematic SphereCast of `Radius` — the mesh is display only. ADR-021.

**In scope**

- Public `IProjectileViewFactory { ProjectileView Create(Recipe recipe, Transform poolParent); }`. Pool still owns rent/return stacks per Recipe. `createViews: false` still creates no views (Edit Mode tests unchanged)
- `ProjectileView` becomes public (`Bind` / `SetPose` / `Release`). Prefabs may already have it; the factory adds it if missing. Disable/strip colliders on created views (same as today)
- Default when the game passes none: `PrimitiveViewFactory` — current sphere primitive, scaled to `2 * Radius`
- `PrefabViewFactory`: map `PresentationId` → prefab. Unknown id falls back to the primitive factory. **Do not** overwrite the prefab’s `localScale` (author at world scale)
- `ProjectileWorld` takes an optional factory; `new ProjectileWorld(poolParent)` keeps today’s sphere
- Two test/sandbox prefabs: **sphere** and **cube** (square silhouette). `PresentationId` `"sphere"` and `"cube"`. Live under sandbox or `Tests/Runtime` (not Addressables)
- Play Mode tests: Recipe `"cube"` rents a cube view; Recipe `"sphere"` rents a sphere view; both still hit DummyTarget using `Radius`; return-to-pool reuses the same view GameObject
- Sandbox: two Recipes, fire cube vs sphere — different look, same lane hit

**Out of scope:** mesh-shaped hits; Addressables; Entities Graphics; scaling prefabs to `Radius`; hit/expire VFX beyond the flying view; editor tooling; Unity Physics

**Proof:** fire the sphere Recipe — looks like today. Fire the cube Recipe — a cube flies the lane and still hits the dummy.

**Stop.**

## Phase 19 — Lingering ground fields

`GroundField` plants a **flat disk** that stays in the world after the shot is gone. Size, lifetime, damage, and optional status live on the modifier. Pulses use the same `Hit` / `StatusApplied` path as `AreaEffect`. Not a child Recipe, not a seventh stage, not a second façade. ADR-022.

**In scope**

- `GroundField(radius, lifetimeSeconds, damage, status = null, intervalSeconds)` on `OnHit`, `OnExpire`, and `OnTravel`. Not `OnSpawn`, `OnTick`, or `OnInterval` (interval would drop a trail of fields)
- **OnHit:** plant at `Hit.Point`. **OnExpire:** plant at the instance position. **OnTravel:** plant at spawn, once, before the first move. The modifier queues the plant on `ModifierContext`; it does not call Physics or spawn a Recipe
- The field is owned by `ProjectileWorld` and ticked from the same `Tick`. It is **not** a projectile instance: no SphereCast, no `Recipe.Speed` / `Radius` / `LifetimeSeconds`, no stage lists. `radius <= 0` or `lifetimeSeconds <= 0` is a no-op (no plant)
- **Flat overlap:** a Target is inside when its XZ distance to the field center ≤ `radius`. Y does not count (a dummy above the disk but outside the circle is a miss). `IHitQuery.OverlapDisk(center, radius)` — Edit Mode `ScriptedHitQuery`, Play Mode `PhysicsHitQuery` (world-up cylinder of default height 2 so standing colliders are found; still filter to the XZ disk). Only `IDamageable` Targets; ignore World and other projectiles. Skip Strong; Basic may appear with `Protection = Basic`
- **Pulse:** overlap immediately on plant, then every `intervalSeconds` while the field lives. `intervalSeconds <= 0` never pulses (the disk can still exist until lifetime ends). Each Target in the disk raises `Hit` with `HitSource.Area` and the **modifier** damage, then `StatusApplied` if `status` is set. Package still does not apply HP or tick DoT. Hits use the planting instance’s `ProjectileId` (the parent may already be pooled). Do not mint a second id space
- Two `GroundField` modifiers plant two disks. `OnHit` then `OnExpire` both plant when both lists carry one
- Field lifetime is the despawn clock for that disk (independent of the projectile). When it elapses, stop pulsing and return the field view to the pool. Do **not** raise `Expired` for fields (that event is projectile pool accounting)
- View: a thin primitive cylinder/quad scaled to `2 * radius` on XZ. `createViews: false` creates none. Do not add a second view factory or `PresentationId` on the modifier
- Edit Mode tests: OnHit plant pulses a scripted Target in the disk immediately and again at the interval; a Target outside XZ (including one that a sphere of that radius would cover) is missed; after `lifetimeSeconds` there are no further Hits; status raises `StatusApplied` per pulse; OnExpire plants at expire position; OnTravel plants at spawn; Strong skipped; `radius = 0` plants nothing
- Sandbox: OnHit GroundField at DummyTarget — a disk stays on the lane, DummyTarget and the nearby splash dummy lose field damage each interval, a dummy outside the disk does not; after lifetime the disk is gone. OnExpire Recipe leaves a disk where the shot died. Optional status Recipe shows `dummy` on anyone standing in the disk

**Out of scope:** occupancy enter/exit; `OnTick` auras; chaining fields from fields; scaling field radius/damage with `EffectRadius(n)` / `Damage(n)`; ground raycast / terrain snap; Addressables; field prefabs; rolling crit; applying HP/DoT in the package; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire OnHit GroundField — a flat disk appears at the dummy; HP ticks while the dummy stands in it; the disk vanishes when its lifetime ends. Fire a lifetime-only shot with GroundField on OnExpire — the disk is where the shot died, not at the dummy. A dummy beside the disk (inside a sphere of that radius, outside the circle) is not hit.

**Stop.**

## Phase 20 — Recipe editor

Create and edit Recipes in Unity as ScriptableObjects. The inspector is the six stage lists; each list’s **Add** menu only offers modifiers that belong on that stage. `Spawn` still takes a `Recipe`. ADR-023.

**In scope**

- `RecipeAsset` (`Create > Projectile > Recipe`) in the runtime assembly. Same stats as `Recipe` (whatever fields exist at implementation: id, speed, radius, lifetime, combat/cast numbers, presentation id, multicast/multiply generations). Six `[SerializeReference] IModifier[]` lists
- `Recipe ToRecipe()`. Tests and `IProjectileWorld.Spawn` keep using `Recipe`. Do **not** make `Recipe` a ScriptableObject and do not put `RecipeAsset` on `SpawnRequest`
- `[Flags] RecipeStage` and `[ModifierStage(RecipeStage)]` on each **existing** `IModifier` class, matching the six-list table in [ARCHITECTURE.md](ARCHITECTURE.md). Types with no attribute do not appear in any Add menu. `TypeCache.GetTypesDerivedFrom<IModifier>()` is the discovery (game assemblies with the attribute show up; no registry)
- Existing modifiers become `[Serializable]` with a serializer-friendly ctor. Public parameterized ctors stay. `Apply` behavior does not change
- Multiply / Multicast child and sibling slots on the asset are `RecipeAsset` references. `ToRecipe()` fills `ChildRecipe` / `ExtraRecipe` with a visited-asset map so a cycle shares one `Recipe`. Homing keeps default `ClosestInTravelDirectionAcquisition`. Multicast keeps default `RandomConePattern`. Multiply `Pattern` is a type choice among the three shipped `IMultiplyPattern`s (null = Square). ApplyStatus authors `DummyStatusEffect` (or a SerializeReference `IStatusEffect`)
- Custom inspector (`Editor/RecipeAssetEditor`): base stats, then six reorderable lists (order is list order). **Add** filtered by `[ModifierStage]`. Remove and reorder. Draw serialized magnitude / child-asset / pattern fields. Not a graph window, not a default managed-reference picker that lists every type
- Edit Mode tests: Pierce is in the OnHit catalog and not OnSpawn; `ToRecipe()` copies stats and a `Pierce(2)` on OnHit; Multiply child asset resolves to `ChildRecipe`; a self-child asset does not throw in `ToRecipe()`
- Sandbox: one sample `RecipeAsset` (straight Pierce or similar). Optional inspector slot on `SandboxLab` to fire `asset.ToRecipe()`. Do **not** delete the C# Recipes the lab already builds

**Out of scope:** EditorWindow / node graph; one ScriptableObject per modifier; converting every sandbox Recipe to an asset; visual `IMultiplyPattern` editor; inspector “fire preview”; Addressables; baking / SubScenes; unimplemented modifiers (AreaEffect, GroundField, Gravity appear when those types ship); Unity Physics; Entities Graphics

**Proof:** **Create > Projectile > Recipe**, set speed/lifetime, add Pierce on **OnHit** — OnSpawn’s Add menu does not list Pierce. `ToRecipe()` has that Pierce. Fire the sample asset from the sandbox if a slot is wired — same hit as a C# Pierce Recipe.

**Stop.**

## Phase 21 — Sample scene

Replace the keyboard lab with a Play Mode sample: turret, mouse aim, preset dropdown, on-screen recipe sheet, reset. It shows the system, it does not grow another 24-slot HUD. ADR-024. Depends on phase 20 `RecipeAsset`. Camera, input, HP, and turret stay in `Assets/`.

**In scope**

- New default scene (replace `Sandbox.unity` as the Play Mode scene, or `Assets/Scenes/Sample.unity` with that as the opened scene). **Projectile Lab > Rebuild** rebuilds this layout. Drop the number-key / letter-key fire grid and the per-modifier `-/+` HUD
- **Turret:** sphere body + cylinder barrel. Spawn at the barrel tip. No PhysX collider (shots must not World-hit the gun). Camera sits behind/above looking at the yard, not the old side-on lane
- **Mouse aim:** camera ray vs the ground plane; turret yaws so the barrel’s flattened forward points at the hit. Horizontal shots only (no pitch). Pointer over UI does not aim-fire
- **Fire:** click in the world or an on-screen Fire control (Space may stay). `SpawnRequest.Direction` is barrel forward. One driver `Tick`s
- **Presets:** dropdown of `RecipeAsset`s. Curated, named for what they show, **only modifiers that exist in code**. Suggested set: Straight, Pierce, Bounce, Multiply on hit (Square), Interval multiply, Multicast, Homing, Pierce+Homing, Bounce+Multiply, Speed on travel, ApplyStatus, Size. One layout still has a pierce line of dummies, a bounce wall, DummyBounce off-lane, and the existing splash dummy. Homing candidates stay published from dummy poses
- **Recipe sheet:** base stats (speed, radius, lifetime, damage, crit, mana, effect radius, presentation id) as labeled fields; then the six lists as rows (`Pierce(2)`, `Speed(0.5)`, Multiply spread/pattern, …). This is the visual of “what is on this Recipe.” Magnitudes and those properties are editable. **Do not** add/remove modifiers or stages here (that is phase 20). Child/sibling Recipes show as a name, not a nested editor
- **Working copy:** dropdown/`ToRecipe()` fills a runtime `Recipe`. Edits mutate that copy. The `RecipeAsset` on disk does not change. Switching preset discards tweaks and copies again
- **Reset:** button restores dummy HP and status, turret yaw, and the working copy from the current preset; `Dispose` the `ProjectileWorld` and create a new one (clears live shots and pool); re-prewarm; re-subscribe `Hit` / `StatusApplied`. Do not exit Play Mode
- Colored dummy cubes and status flash stay. Tiny HUD: active count, pool available, selected dummy HP/status. No wall of last-spawn dumps
- Edit Mode tests where they fit: ground-aim direction is horizontal toward the point; `ToRecipe()` then changing Pierce count on the copy leaves the asset’s count unchanged. Play Mode proof is the scene
- Rewrite [SANDBOX.md](SANDBOX.md) proofs to preset names. Replace the AGENTS.md “24 fire slots” note

**Out of scope:** implementing phases 13–19 (add those presets when the types exist); barrel pitch / `BallisticAim` (schedule after 14–16 — see phase 16 stop); hold-to-auto-fire; WASD turret move; orbit camera; saving Play Mode tweaks back to `RecipeAsset`; add/remove modifiers in Play Mode; world-space 3D modifier icons; UPM `Samples/` folder split; putting turret/input/HP in the runtime package; Unity Physics; Entities Graphics

**Proof:** Play Mode, pick Pierce, aim at the dummy line, fire — first two live through. Tweak Pierce to 1 on the sheet, fire — dies on the second; the asset is still 2. Reset — dummy HP back, no leftover shots, Pierce on the sheet is 2 again. Aim does not fire when clicking the dropdown.

**Stop.**

## Later (not scheduled)

- Unity Physics and/or Entities Graphics adapters
- Multiply patterns: line, arc, cross, triangle; a pattern editor that authors an `IMultiplyPattern`
- Occupancy enter/exit; `OnTick` auras
- More modifiers, optional Rigidbody solver, projectile-vs-projectile hits
- Barrel pitch / ballistic sample (schedule after phase 16 is implemented); hold-to-fire; save Play Mode tweaks to `RecipeAsset`
- Convert remaining sandbox C# Recipes; package `Samples/` vs `Assets/` split
- Rename the UPM id if the double `com.davidvdveerdonk.com.veerdonk` prefix should go
