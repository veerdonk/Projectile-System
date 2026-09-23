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

- Public `BallisticAim.TrySolve(origin, target, speed, gravity, out direction, highArc = false)` → `false` if unreachable. Uses world down. Low arc is the default of the two classic solutions. **Phase 22.b** drops `highArc` (ADR-028)
- Callers put `direction` on `SpawnRequest.Direction`. Do not add a second façade or a `Spawn` overload that aims for you
- Edit Mode tests: reachable dummy at sandbox range returns a direction that, when simulated with that speed/gravity, the instance’s path meets the target (within a small tolerance); too-far or `gravity = 0` with a high target returns false (gravity 0 only works if the target lies on the ray)
- Sandbox: cannonball fire uses the helper toward DummyTarget — it hits; toggle high arc if both solutions exist (removed in **phase 22.b**)

**Out of scope:** replacing tick integrate with closed-form `x(t)` during flight; Rigidbody; editor tooling; Unity Physics; Entities Graphics

**Proof:** fire the aimed cannonball — it hits DummyTarget. Fire with speed too low — helper returns false and the lab does not spawn.

**Stop.** Sample-scene barrel pitch and a ballistic preset are **phase 22** (do not build them in the same session). Dropping the high-arc pick is **phase 22.b**. Hold-to-fire, WASD, and orbit camera stay Later unless that follow-up says otherwise.

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
- **OnHit:** plant at `Hit.Point`. **OnExpire:** plant at the instance position. **OnTravel:** plant at spawn, once, before the first move. v1 snaps plant Y to the world ground plane (`0`) so the disk sits on the floor. The modifier queues the plant on `ModifierContext`; it does not call Physics or spawn a Recipe
- The field is owned by `ProjectileWorld` and ticked from the same `Tick`. It is **not** a projectile instance: no SphereCast, no `Recipe.Speed` / `Radius` / `LifetimeSeconds`, no stage lists. `radius <= 0` or `lifetimeSeconds <= 0` is a no-op (no plant)
- **Flat overlap:** a Target is inside when its XZ distance to the field center ≤ `radius`. Y does not count (a dummy above the disk but outside the circle is a miss). `IHitQuery.OverlapDisk(center, radius)` — Edit Mode `ScriptedHitQuery`, Play Mode `PhysicsHitQuery` (world-up cylinder of default height 2 so standing colliders are found; still filter to the XZ disk). Only `IDamageable` Targets; ignore World and other projectiles. Skip Strong; Basic may appear with `Protection = Basic`
- **Pulse:** overlap immediately on plant, then every `intervalSeconds` while the field lives. `intervalSeconds <= 0` never pulses (the disk can still exist until lifetime ends). Each Target in the disk raises `Hit` with `HitSource.Area` and the **modifier** damage, then `StatusApplied` if `status` is set. Package still does not apply HP or tick DoT. Hits use the planting instance’s `ProjectileId` (the parent may already be pooled). Do not mint a second id space
- Two `GroundField` modifiers plant two disks. `OnHit` then `OnExpire` both plant when both lists carry one
- Field lifetime is the despawn clock for that disk (independent of the projectile). When it elapses, stop pulsing and return the field view to the pool. Do **not** raise `Expired` for fields (that event is projectile pool accounting)
- View: `IGroundFieldViewFactory` (ADR-026). Default `PrimitiveGroundFieldViewFactory` is a thin cylinder scaled to `2 * radius` on XZ. `GroundField.PresentationId` selects the look. `PrefabGroundFieldViewFactory` maps id → prefab and does not overwrite scale. Unknown ids fall back to the primitive. `createViews: false` creates none
- Edit Mode tests: OnHit plant pulses a scripted Target in the disk immediately and again at the interval; a Target outside XZ (including one that a sphere of that radius would cover) is missed; after `lifetimeSeconds` there are no further Hits; status raises `StatusApplied` per pulse; OnExpire plants at expire position; OnTravel plants at spawn; Strong skipped; `radius = 0` plants nothing
- Sandbox: OnHit GroundField at DummyTarget — a disk stays on the lane, DummyTarget and the nearby splash dummy lose field damage each interval, a dummy outside the disk does not; after lifetime the disk is gone. OnExpire Recipe leaves a disk where the shot died. Optional status Recipe shows `dummy` on anyone standing in the disk

**Out of scope:** occupancy enter/exit; `OnTick` auras; chaining fields from fields; scaling field radius/damage with `EffectRadius(n)` / `Damage(n)`; ground raycast / terrain snap; Addressables; rolling crit; applying HP/DoT in the package; editor tooling; Unity Physics; Entities Graphics

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
- **Recipe sheet:** base stats (speed, radius, lifetime, damage, crit, mana, effect radius, presentation id) as labeled fields; then the six lists as rows (`Pierce(2)`, `Speed(0.5)`, Multiply spread/pattern, …). This is the visual of “what is on this Recipe.” Magnitudes and those properties are editable. **Do not** add/remove modifiers here (phase 20 is the asset inspector; **phase 21.b** adds Add/Remove on this same sheet). Child/sibling Recipes show as a name, not a nested editor
- **Working copy:** dropdown/`ToRecipe()` fills a runtime `Recipe`. Edits mutate that copy. The `RecipeAsset` on disk does not change. Switching preset discards tweaks and copies again
- **Reset:** button restores dummy HP and status, turret yaw, and the working copy from the current preset; `Dispose` the `ProjectileWorld` and create a new one (clears live shots and pool); re-prewarm; re-subscribe `Hit` / `StatusApplied`. Do not exit Play Mode
- Colored dummy cubes and status flash stay. Tiny HUD: active count, pool available, selected dummy HP/status. No wall of last-spawn dumps
- Edit Mode tests where they fit: ground-aim direction is horizontal toward the point; `ToRecipe()` then changing Pierce count on the copy leaves the asset’s count unchanged. Play Mode proof is the scene
- Rewrite [SANDBOX.md](SANDBOX.md) proofs to preset names. Replace the AGENTS.md “24 fire slots” note

**Out of scope:** implementing phases 13–19 (add those presets when the types exist); barrel pitch / `BallisticAim` (phase 22); hold-to-auto-fire; WASD turret move; orbit camera; saving Play Mode tweaks back to `RecipeAsset`; add/remove modifiers in Play Mode (**phase 21.b**); world-space 3D modifier icons; UPM `Samples/` folder split; putting turret/input/HP in the runtime package; Unity Physics; Entities Graphics

**Proof:** Play Mode, pick Pierce, aim at the dummy line, fire — first two live through. Tweak Pierce to 1 on the sheet, fire — dies on the second; the asset is still 2. Reset — dummy HP back, no leftover shots, Pierce on the sheet is 2 again. Aim does not fire when clicking the dropdown.

**Stop.** Play Mode Add/Remove is **phase 21.b** — do not build it in the same session.

## Phase 21.b — Play Mode modifier compose

Let the user add and remove modifiers on the sample sheet while playing, so one shot can carry several lists (Pierce+Homing, Bounce+Multiply, AreaEffect on Straight, …). Still the working `Recipe` copy. Still no write-back. ADR-027. Depends on phase 21’s turret/sheet.

**In scope**

- Each of the six lists on `SampleRecipeSheet` gets **Add** and per-row **Remove**. Add appends. List order is still apply order
- Add menu per list: `IModifier` types whose `[ModifierStage]` includes that stage — same filter as `RecipeAssetEditor.ModifiersFor`. OnHit offers Pierce; OnSpawn does not. OnTick offers Homing; OnHit does not
- Catalog lives in **sandbox** (runtime reflection over loaded assemblies). Do not call `RecipeAssetEditor` from Play Mode. Do not add a package `ModifierCatalog` unless a later ADR says so. Game assemblies with the attribute still show up
- New instances use a serializer/parameterless ctor plus small defaults: Pierce(1), Bounce(1), scale modifiers (1), Interval 0.5s, Homing 100°/s, Multiply(2) Square Spread 2 m, Multicast(2), AreaEffect and GroundField with fireable radius/damage/lifetime/interval. `ApplyStatus` uses `DummyStatusEffect`
- Multiply child / Multicast extra: pick from the sample’s `RecipeAsset` presets (`ToRecipe()`), not a nested editor. Default child/extra is the **Straight** preset (or the first preset if Straight is missing)
- AreaEffect / GroundField / Interval / Homing magnitudes are editable on the sheet once present (not labels-only)
- Working copy and asset rules from phase 21 stand: edits mutate the copy; switching preset or **Reset** discards adds; after Play Mode the `RecipeAsset` is unchanged
- Clicks on Add/Remove/menus do not fire. Do not revive the keyboard-slot HUD
- Edit Mode tests in sandbox/Editor if they fit: OnHit catalog contains Pierce and not Homing; appending Pierce to an empty OnHit list then firing is a Play Mode proof. Package tests stay on `Recipe` / `ToRecipe`, not the HUD
- Rewrite [SANDBOX.md](SANDBOX.md) proofs for compose. Start from `phase-21-sample-scene` once that sample is the Play Mode scene — do not reimplement the turret

**Out of scope:** saving Play Mode tweaks to `RecipeAsset`; nested child Recipe editor; drag-reorder; rebuilding `RecipeAssetEditor` as a Play Mode window; new package types or a second façade; barrel pitch / `BallisticAim` (phase 22); hold-to-fire; WASD; orbit camera; putting turret/input/HP in the runtime package; Unity Physics; Entities Graphics

**Proof:** Play Mode, **Straight**, Add Pierce on OnHit and Homing on OnTick, fire at the dummy line — first dummy is pierced and the shot can steer to another dummy. OnSpawn’s Add menu does not list Pierce. Exit Play Mode — Straight asset still has empty OnHit/OnTick. Reset while composed — sheet matches Straight again.

**Stop.**

## Phase 22 — Sample barrel pitch and ballistic preset

Phase 21 left lofted aim under Later until gravity and `BallisticAim` existed. Add barrel pitch and one ballistic preset to the sample scene. Do not grow the keyboard lab HUD. The helper computes the needed elevation (ADR-028) — do **not** add a high-arc toggle.

**In scope**

- Sample turret barrel pitches so `SpawnRequest.Direction` can leave the horizontal plane (still no PhysX collider on the gun)
- Mouse aim still hits the ground plane, then the barrel pitches toward that point (yaw + pitch). Horizontal-only presets can keep using flattened forward
- One ballistic `RecipeAsset` preset that calls `BallisticAim.TrySolve` toward the ground-aim point and copies the direction onto `SpawnRequest`. If 22.b is not yet in, call the current helper **without** `highArc` (the default is already the flatter root)
- Unreachable: helper returns false and the sample does not spawn (same as the phase 16 lab)
- Recipe sheet / HUD: show whether the last fire solved or was unreachable. Do **not** show or toggle high vs low arc
- Rewrite [SANDBOX.md](SANDBOX.md) proofs for the ballistic preset

**Out of scope:** high-arc pick; `Recipe.Range` / `Range(n)`; hold-to-fire; WASD turret move; orbit camera; saving Play Mode tweaks to `RecipeAsset`; add/remove modifiers in Play Mode (phase 21.b); putting turret/input/HP in the runtime package; replacing tick integrate; Unity Physics; Entities Graphics

**Proof:** Play Mode, pick the ballistic preset, aim at DummyTarget, fire — the shot hits and the barrel is lofted. Raise speed on the sheet — the next solve is flatter (closer to the line of sight). Lower speed until `TrySolve` fails — no spawn.

**Stop.** Dropping `highArc` from the package API is **phase 22.b**. Do not start it in the same session.

## Phase 22.b — Needed-arc ballistic solve

`BallisticAim` computes the loft that Recipe speed and gravity need to hit the point. It does not allow or disallow a chosen high vs low arc. A shot with spare speed is nearly straight; a shot that can only just reach a level target leaves at about 45°. ADR-028. No `Recipe.Range` / `Range(n)`. If both 22 and 22.b are still unbuilt, implement **this phase first** so the sample never compiles against `highArc`.

**In scope**

- Drop `highArc` from `TrySolve(origin, target, speed, gravity, out direction)`. Always the shorter-time (flatter) root. When a level target is barely reachable, that root is ~45° (max range ≈ `Speed² / Gravity` on flat ground)
- Callers still copy `direction` onto `SpawnRequest.Direction`. Flight is still phase 14 Burst integrate. Gravity 0 is unchanged (horizontal ray only)
- Edit Mode tests: same-height target at the **lowest** speed that still returns true → launch elevation within a few degrees of 45°; the same geometry at several times that speed → elevation much closer to the line of sight (near 0°) than to 45°, and the simulated path still meets the target; too slow / too far still false. Delete `TrySolve_HighArc_AlsoMeetsReachableSandboxDummy`
- Sample: do not add a high-arc control. If phase 22 already shipped a high/low HUD, remove it
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) sketch, [ECS.md](ECS.md) map, [SANDBOX.md](SANDBOX.md) proofs, ADR-019 high-arc clause superseded

**Out of scope:** `Recipe.Range` / `Range(n)` / an OnExpire-at-distance fuse; barrel pitch / ballistic sample preset (phase 22); changing the Burst integrate; closed-form `x(t)` during flight; Rigidbody; editor tooling; Unity Physics; Entities Graphics

**Proof:** Edit Mode min-speed level shot is ~45°; fast shot is nearly straight and still hits. Play Mode ballistic preset (if 22 is in) still hits DummyTarget; there is no high-arc control.

**Stop.**

## Phase 23 — Cleanup

Housekeeping after **phases 1–22.b are in code**. Rename the UPM id, document the full public API (including 17–22.b), delete leftovers, fill test holes, Sonar quality pass against the local SonarQube container. ADR-025. No new modifiers, no second façade. Do not start this phase on a tree that still has 17–22.b unbuilt.

**In scope**

- **UPM id:** `package.json` `name` and folder `Packages/com.veerdonk.projectile-system/`. Namespace `Veerdonk.ProjectileSystem`; asmdefs `Veerdonk.ProjectileSystem` / `.Editor` / `.Tests` (and InternalsVisibleTo). Update sandbox usings, `packages-lock.json`, [AGENTS.md](../AGENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md) folder map, [SANDBOX.md](SANDBOX.md), [REQUIREMENTS.md](REQUIREMENTS.md). Display name stays `com.veerdonk.projectile-system`
- **User docs:** XML `<summary>` on every **public** package type and public member through phase 22.b: façade (`IProjectileWorld`, `Recipe`, `SpawnRequest`, `HitInfo`, `HitKind`, `HitSource`, `ProtectionLevel`), modifiers (including `GroundField`, `AreaEffect`, `Gravity`, scales, `ApplyStatus`), patterns, `BallisticAim`, `IProjectileProtection`, `IProjectileViewFactory` / `PrimitiveViewFactory` / `PrefabViewFactory` / `IGroundFieldViewFactory` / `PrimitiveGroundFieldViewFactory` / `PrefabGroundFieldViewFactory` / `ProjectileView`, `IHitQuery.OverlapDisk`, `RecipeAsset` / `RecipeStage` / `ModifierStageAttribute`. Enable documentation generation on the runtime asmdef. Replace template `Documentation/index.md`, package `README.md`, `package.json` description (drop example.com author/changelog URLs or omit them). `CHANGELOG.md` matches 0.1.0. Do **not** narrate internal ECS (`Runtime/Ecs/`) for consumers; [ECS.md](ECS.md) stays the agent map. Do not XML-doc sandbox `Assets/` (turret, sample UI, dummy HP). Inline comments on obvious code stay out ([AGENTS.md](../AGENTS.md))
- **Leftovers:** delete Unity template `Samples/Example` (`*Example*` / `MyPublicSampleExampleClass`), template `Documentation/images/example.png` if unused, unused `Assets/Scenes/SampleScene.unity`. After phase 21, delete the keyboard-slot lab driver if it is still in the tree (`SandboxLab` key grid). Strip empty template Third-Party boilerplate only if it is still placeholder. No dead `using`s in the package
- **Tests:** Unity Code Coverage on Edit Mode package tests. Inventory **all** shipped public types through 22.b vs `Tests/Editor` (and Play Mode pool/physics/views). Add Edit Mode tests for holes — especially spec no-ops (`n <= 0` scales, `EffectRadius = 0` area, `Gravity = 0` straight, `GroundField` `radius`/`lifetimeSeconds` `<= 0`), `Prewarm` chain counts (ADR-008), protection edges that 17’s list missed, unknown `PresentationId` falling back to the primitive factory, `RecipeAsset.ToRecipe` cycle sharing. Do not chase 100% on Burst/ECS generated code. Sample-scene behaviour stays proven in Play Mode / `Assets/` tests, not by inventing a second façade
- **Sonar:** analyze the **renamed** package Runtime + Editor on the **local SonarQube container** (Community, default Docker: `http://localhost:9000`). Project key `com.veerdonk.projectile-system`. Auth is `SONAR_TOKEN` (and optional `SONAR_HOST_URL`); **never commit the token**, a user/password, or `sonar.login` / `sonar.token` in the repo. A committed `sonar-project.properties` may set project key, `sonar.host.url=http://localhost:9000`, `sonar.sources` (Runtime + Editor after the folder rename), and `sonar.tests`; it must not set credentials. Exclude `Assets/`, `Library/`, `Temp/`, `com.coplaydev.unity-mcp`. Fix Blocker, Critical, and Major in Runtime/Editor product code. Do not mass-`#pragma` them away. Do **not** add Sonar as a UPM dependency. Record the dashboard URL and what was fixed. If the container is down, start it (default SonarQube image, port 9000) rather than falling back to an unreviewed local-only rule dump
- Existing Edit Mode + Play Mode tests still pass

**Out of scope:** new modifiers or types beyond what 17–22.b already shipped; publishing to a registry; OpenUPM; renaming the GitHub repo; putting turret/input/HP in the runtime package; Unity Physics; Entities Graphics

**Proof:** Package Manager shows `com.veerdonk.projectile-system`. No `MyPublicSampleExampleClass`. `Documentation/index.md` describes Spawn / Tick / Recipe stage lists and names the 17–22.b public types. New coverage tests are green (including GroundField no-ops, view-factory fallback, RecipeAsset cycle). Local SonarQube at `http://localhost:9000` has no remaining Blocker/Critical on package Runtime/Editor.

**Stop.**

## Phase 24 — Package sample (Projectile Yard)

Move the Play Mode turret yard into the UPM package so a consumer who added `com.veerdonk.projectile-system` can **Import sample** and see Spawn / Tick / Hit without cloning this repo. ADR-029. Do not put HP, camera, or input on the runtime façade.

**In scope**

- `Packages/com.veerdonk.projectile-system/Samples~/Yard/`: scene, `RecipeAsset` presets, cube/sphere view prefabs, sample scripts (lab, yard, sheet, dummies, protection, ground aim, ballistic clearance), Rebuild + preset-asset Editor scripts. Tilde folder so the yard is hidden until Import
- `package.json` `samples` entry: display name **Projectile Yard**, path `Samples~/Yard`. Short description: import, open the scene, Play Mode
- Asmdefs `Veerdonk.ProjectileSystem.Samples` (runtime sample) and `Veerdonk.ProjectileSystem.Samples.Editor`. Reference `Veerdonk.ProjectileSystem` only. Do **not** fold sample types into the Runtime/Editor product assemblies
- Namespace `Veerdonk.ProjectileSystem.Samples` / `.Samples.Editor`. Drop host `Sandbox` / `Sandbox.Editor`
- **Projectile Lab > Rebuild** writes `Samples~/Yard/Scenes/Yard.unity` (not `Assets/Scenes/Sandbox.unity`) and copies/imports that tree into `Assets/Samples/com.veerdonk.projectile-system/<package.json version>/Projectile Yard/` (Package Manager uses the sample display name as the folder) so this development project can Play Mode without clicking Package Manager. gitignore `Assets/Samples/`
- Delete `Assets/Sandbox/` and `Assets/Scenes/Sandbox.unity` once the sample folder has them. Do not leave a second copy of the yard in `Assets/` except the gitignored Import copy
- Keep sample behaviour: turret, mouse aim, presets, sheet Add/Remove, Reset, Cannonball `BallisticAim`, dummy HP from `Hit`. Do not add Runtime types or a second façade
- Input System may stay on the **sample** asmdef. Do **not** add `com.unity.inputsystem` or URP to package `dependencies`. If a stock template cannot compile the imported sample, switch Fire/Aim to `UnityEngine.Input` in this phase rather than adding a Runtime dependency. No URP-only shaders in the sample
- Sample Editor tests (`GroundAim`, `BallisticClearance`, modifier catalog) move with the sample. Package `Tests/` stay product tests
- Docs: [SANDBOX.md](SANDBOX.md) Play Mode path, [ARCHITECTURE.md](ARCHITECTURE.md) folder map, [REQUIREMENTS.md](REQUIREMENTS.md) package vs game, package `README.md` / `Documentation/index.md` Import steps, [MCP.md](MCP.md) scene path, [AGENTS.md](../AGENTS.md) map. Sonar `sonar.sources` stays Runtime + Editor (exclude `Samples~`)

**Out of scope:** OpenUPM / registry publish; LICENSE / making the GitHub repo public; putting turret/input/HP on `IProjectileWorld`; hold-to-fire; WASD; orbit camera; save-to-asset; Addressables; Unity Physics; Entities Graphics

**Proof:** Package Manager on this project shows sample **Projectile Yard**. Import (or Rebuild) → open `Yard.unity` → Play Mode, pick Pierce, fire — first two dummies live through (same as phase 21). A git-URL install of the package does not compile `SampleLab` until the user imports the sample. Runtime assembly has no `SampleLab` / dummy HP types.

**Stop.**

## Phase 25 — Published hit targets

Games publish living enemies as hit volumes (`TargetId`, position, radius) so a shot can raise `HitKind.Target` with **no PhysX collider and no `IDamageable`**. Direct cast, `AreaEffect`, `GroundField`, `Pierce`, and `Bounce` all use that catalog. ADR-030. Package still does not apply HP. Do not expose `IHitQuery`.

**In scope**

- `IProjectileWorld.SetHitTarget(TargetId id, Vector3 position, float radius)` / `RemoveHitTarget(TargetId id)`. Set again to move or resize. `radius <= 0` does not publish (removes that id if present). Separate from `SetHomingCandidate` (no radius, homing-only)
- Direct: nearest contact among PhysX SphereCast/overlap **and** swept instance sphere vs each published sphere. Published hit → `Kind = Target`, that `TargetId`, `Protection = None`, radial `Normal`. Unmarked collider → World. `IDamageable` collider → Target as today
- `AreaEffect` overlap: existing `IDamageable` Targets **plus** published volumes whose spheres overlap `EffectRadius`. `GroundField` disk: existing XZ filter **plus** published volumes whose XZ circle overlaps the disk (Y ignored; radii add). Skip Strong colliders as today; published volumes are never Strong
- `Pierce(n)` consumes published Target hits like collider Targets. While the instance still overlaps that volume, do not re-hit the same `TargetId` (same idea as last-collider ignore)
- `Bounce(n)` reflects a published Target around `HitInfo.Normal` and still raises Direct `Hit` (ADR-010)
- `IHitQuery` stays internal. `ScriptedHitQuery` and `PhysicsHitQuery` both read the world catalog. No Unity Physics, no companion colliders, no second façade
- Edit Mode tests: straight Recipe vs a published sphere (no GameObject collider) raises Target `Hit` with that id; a PhysX wall without `IDamageable` is still World; Pierce(1) through two published volumes then dies on the third; Bounce(1) off a published volume reflects and `Hit` fires; AreaEffect OnHit includes a published volume outside the contact collider; GroundField pulse includes a published volume in the XZ disk and misses one outside; `radius <= 0` never hits; `RemoveHitTarget` then Tick does not hit. Existing collider tests stay green
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) sketch, [ECS.md](ECS.md) query map, package `AGENTS.md` / `README.md` / `Documentation/index.md`, [SANDBOX.md](SANDBOX.md). Bump `package.json` version to **0.1.1**. Do not create a git tag

**Out of scope:** merging hit and homing catalogs; published `IProjectileProtection`; exposing `IHitQuery`; converting the yard to ECS entities; applying HP; Unity Physics; Entities Graphics; OpenUPM

**Proof:** Edit Mode published-sphere straight shot raises Target `Hit`. Pierce, Bounce, AreaEffect, and GroundField tests above are green. Yard Pierce line still uses collider dummies.

**Stop.**

## Later (not scheduled)

- Unity Physics and/or Entities Graphics adapters
- Multiply patterns: line, arc, cross, triangle; a pattern editor that authors an `IMultiplyPattern`
- Occupancy enter/exit; `OnTick` auras
- GroundField plant pose: v1 snaps Y to the world ground plane (`0`). Revisit spawn process and positioning (terrain / ground raycast, sloped floors, a chosen ground height) without changing the disk overlap or pulse contract
- More modifiers, optional Rigidbody solver, projectile-vs-projectile hits
- Hold-to-fire; save Play Mode tweaks to `RecipeAsset`
- Authored `Range` / `Range(n)` / an OnExpire-at-distance fuse (ballistic reach stays Speed + Gravity, ADR-028)
- Convert remaining sandbox C# Recipes
