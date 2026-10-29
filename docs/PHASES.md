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
- Clamp critical chance to `[0, 1]` after a scale (the upper clamp is removed in phase 44, ADR-049)
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

**Out of scope:** lingering ground fields (phase 19); occupancy enter/exit (**phase 26**); `OnTick` aura (**phase 26**); `OnExpire` area; scaling area damage with `Damage(n)`; rolling crit; applying HP/DoT in the package; editor tooling; Unity Physics; Entities Graphics

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

**Out of scope:** occupancy enter/exit (**phase 26**); `OnTick` auras (**phase 26**); chaining fields from fields; scaling field radius/damage with `EffectRadius(n)` / `Damage(n)`; ground raycast / terrain snap; Addressables; rolling crit; applying HP/DoT in the package; editor tooling; Unity Physics; Entities Graphics

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
- Edit Mode tests: straight Recipe vs a published sphere (no GameObject collider) raises Target `Hit` with that id; a PhysX wall without `IDamageable` is still World; Pierce(2) through two published volumes then dies on the third (`Pierce(n)` absorbs n Target hits, same as phase 2); Bounce(1) off a published volume reflects and `Hit` fires; AreaEffect OnHit includes a published volume outside the contact collider; GroundField pulse includes a published volume in the XZ disk and misses one outside; `radius <= 0` never hits; `RemoveHitTarget` then Tick does not hit. Existing collider tests stay green
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) sketch, [ECS.md](ECS.md) query map, package `AGENTS.md` / `README.md` / `Documentation/index.md`, [SANDBOX.md](SANDBOX.md). Bump `package.json` version to **0.1.1**. Do not create a git tag in the phase session; `.github/workflows/release-package.yml` tags `v{version}` and opens a GitHub Release after the bump reaches `main`

**Out of scope:** merging hit and homing catalogs into one method; published `IProjectileProtection` (**phase 26**); Homing reading the hit catalog (**phase 26**); ApplyStatus proofs on volumes (**phase 26**); OnTick auras and occupancy (**phase 26**); exposing `IHitQuery`; converting the yard to ECS entities; applying HP; Unity Physics; Entities Graphics; OpenUPM

**Proof:** Edit Mode published-sphere straight shot raises Target `Hit`. Pierce, Bounce, AreaEffect, and GroundField tests above are green. Yard Pierce line still uses collider dummies.

**Stop.** Homing, ApplyStatus, published protection, OnTick auras, and occupancy are **phase 26**. Do not start it in the same session.

## Phase 26 — Published targets: Homing, status, protection, auras, occupancy

Finish Target-gated behaviour on the phase-25 catalog so an ECS enemy published once can be homed, statused, shielded, and tracked in auras/fields. Greybox does not need this. ADR-031. Depends on phase 25.

**In scope**

- **Homing:** acquisition and lock steering read published hit-target positions as well as `SetHomingCandidate`. Same id in both catalogs is one candidate. `SetHomingCandidate` remains for a pose that is not hittable. A published Target hit still drops the lock and ignores that id while it stays in front (ADR-010)
- **ApplyStatus:** Direct published Target raises `StatusApplied`; World still does not; Pierce through two published volumes raises twice. AreaEffect / GroundField status include published volumes. Basic published protection suppresses ApplyStatus for that contact
- **Protection:** `SetHitTarget(id, position, radius, ProtectionLevel protection = None)`. Basic and Strong match collider ADR-020, including Area/Ground/occupancy skip Strong and Pierce consuming Basic
- **OnTick aura:** `AreaEffect` on `OnTick` overlaps every `Tick` (same sphere as OnInterval). Default occupancy false → a Hit each Tick while inside
- **Occupancy:** `occupancy` (default false) on `AreaEffect` and `GroundField`. True: `HitSource.OccupancyEnter` (Area Hit + status once) when a Target (`IDamageable` or published) becomes inside; `HitSource.OccupancyExit` (Damage 0, no status) when they leave; no pulse/tick Hits from that modifier while they stay. False: today’s interval / OnTick pulses
- Edit Mode tests for each bullet. `IHitQuery` stays internal. Yard collider protection/status presets stay as they are
- Docs: sketch, `HitSource`, package `AGENTS.md`. Bump `package.json` to **0.1.2**. Do not create a git tag in the phase session; the `main` release workflow tags `v0.1.2` after merge

**Out of scope:** deleting `SetHomingCandidate`; exposing `IHitQuery`; converting the yard to ECS; applying HP/DoT; Unity Physics; Entities Graphics; projectile-vs-projectile

**Proof:** Homing acquires a volume published only via `SetHitTarget`. ApplyStatus fires on that Direct hit. Strong published volume eats the shot with no `Hit`. Occupancy enter once, silent while standing, exit Hit; OnTick AreaEffect without occupancy Hits every Tick.

**Stop.** Modifier kits (base Recipe + Inspector upgrades) are **phase 27**. Do not start it in the same session.

## Phase 27 — Modifier kits (base Recipe + Inspector upgrades)

Games designate a Recipe as the projectile base and append one modifier or a combination at fire time. Inspector-authored **kits** hold the extras; runtime compose clones the base, assigns a new `Id`, and appends. ADR-032. Does not depend on phase 26; if both are still queued, implement **26 first**.

**In scope**

- `ModifierKit` ScriptableObject (`Create > Projectile > Modifier Kit`). Layers: `ModifierKitLayer { RecipeStage Stage; IModifier[] Modifiers }` (`SerializeReference`). No stats on the kit. One layer = one of the six stages (not a flags combination). A combination kit is several layers (e.g. Pierce OnHit + Homing OnTick)
- Inspector: per layer, stage dropdown + Add/Remove using the same `[ModifierStage]` filter as `RecipeAssetEditor` (OnHit offers Pierce; OnSpawn does not). Game `IModifier` types still show through `TypeCache`
- `Recipe.Clone()`: independent copy of stats and lists. Reuse `RecipeAsset`’s modifier clone so authored kit/asset instances are not shared with the spawned Recipe
- `Recipe.Append(RecipeStage stage, IModifier modifier)` (and array overload): append; throw if the modifier’s `[ModifierStage]` does not include `stage`, if `stage` is not exactly one flag, or if `modifier` is null
- `ModifierKit.ApplyTo(Recipe recipe)`: clone-append each layer in order. Null/empty kit or layer is a no-op. Does **not** change `Recipe.Id`
- `Recipe.Compose(Recipe basis, string id, params ModifierKit[] kits)`: `Clone`, set `Id`, apply kits in order, return. Throw if `basis` is null or `id` is null/empty. Same `Id` on `Spawn` still replaces the store slot — callers must pass a unique id per composition
- Do **not** auto-insert `Interval(n)` when a kit targets `OnInterval`. Document that OnInterval extras do nothing without `Interval` on the base (or on an OnSpawn layer)
- Edit Mode tests: Straight `ToRecipe` + Pierce kit → Pierce through two dummies then die on the third; two kits append in order (Pierce then ApplyStatus both on OnHit); Homing+Pierce combination kit; `Append` Pierce onto OnSpawn throws; Compose with a new id leaves the base Recipe lists unchanged; applying the kit does not alias the asset’s modifier instance; empty kit is identity. Existing collider tests stay green
- **Docs (required):** [ARCHITECTURE.md](ARCHITECTURE.md) sketch, [REQUIREMENTS.md](REQUIREMENTS.md) glossary + scenarios, [SANDBOX.md](SANDBOX.md), package `README.md` (base + kit fire snippet), package `AGENTS.md`, `Documentation/index.md`, `CHANGELOG.md` for 0.1.3. XML `<summary>` on the new public types and `Clone` / `Append` / `Compose`. Call out unique `Recipe.Id` and the Interval footgun
- One example kit asset in `Samples~/Yard` (Pierce on OnHit) so Import shows the Inspector. Do not add a second turret path or write back to `RecipeAsset`. Bump `package.json` to **0.1.3**. Do not create a git tag in the phase session

**Out of scope:** merging two `RecipeAsset`s’ stats; wrap / graphs; changing `RecipeStore` identity; auto-`Interval`; drag-reorder; save Play Mode sheet to a kit; `IProjectileWorld` changes; applying HP; Unity Physics; Entities Graphics

**Proof:** Edit Mode compose tests above are green. Create menu offers **Projectile > Modifier Kit**. Package README / AGENTS.md show `Compose` / `ApplyTo`. Yard Straight asset is unchanged.

**Stop.** Aim-point first Homing lock is **phase 28**. Do not start it in the same session.

## Phase 28 — First Homing lock is closest to the aim point

The first homing target should be the published candidate closest to the world point the player aimed at when the shot was fired (yard: mouse ray). Travel-direction lock is what feels off. ADR-033. Package still does not read the mouse.

**In scope**

- `SpawnRequest.HomingAcquirePointSet` / `HomingAcquirePoint`. When set and `HomingTarget` is unassigned, Homing’s **first** acquire is nearest catalog candidate to that point (hit targets + `SetHomingCandidate`, same id is one candidate, hit pose wins). 3D sqrDistance; **no** in-front / positive-dot filter. Ties keep the first considered. Assigned `HomingTarget` still skips acquire
- `ClosestToPointAcquisition` (`IHomingAcquisition`). Homing uses it until the first lock succeeds, then the modifier’s `IHomingAcquisition` (default `ClosestInTravelDirectionAcquisition`) for later empty locks (ADR-010 after a Target hit). Do not keep steering toward the fire-time point after that, and do not sample the cursor in flight
- Copy the point onto the instance at spawn (blittable). Multicast extras inherit it from that `SpawnRequest`. `false` / unset keeps today’s travel-direction first lock
- Runtime does **not** reference Input System, `Camera`, or mouse. Games copy a world point
- Yard: `SampleLab.Fire` sets the acquire point from `lastAimPoint` when `hasGroundPoint` (the same ray as turret aim). Homing and Pierce+Homing presets use it. Do not add a new preset
- Edit Mode tests: two candidates; travel would pick A (in front, nearer the muzzle) but the acquire point is nearer B → first lock is B. No acquire point → A. Assigned `HomingTarget` still wins. After a scripted Target hit on B, the next lock is travel-direction (A if in front), not B’s old point. Closest-to-point may lock a candidate beside/behind travel. Existing Homing tests that omit the point stay green
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) `SpawnRequest`, [REQUIREMENTS.md](REQUIREMENTS.md), [SANDBOX.md](SANDBOX.md) Homing proof, package `AGENTS.md` / `README.md` / `Documentation/index.md`, `CHANGELOG` **0.1.4**. XML on the new fields and `ClosestToPointAcquisition`. Bump `package.json` to **0.1.4**. Do not create a git tag in the phase session

**Out of scope:** cursor-follow / retarget to current mouse; exposing `HomingCatalog`; factions; changing turn rate; putting input on `IProjectileWorld`; applying HP; Unity Physics; Entities Graphics

**Proof:** Edit Mode aim-point vs travel tests above. Play Mode Homing: click DummyBounce (off-lane) and fire — the shot turns toward DummyBounce first, not DummyTarget. Pierce+Homing: click the first dummy in the line, pierce it, then travel-acquire DummyBounce as today.

**Stop.** Indexed hit-target publish (`SetHitTarget` must not scan the whole list per call) is **phase 29**. Do not start it in the same session.

## Phase 29 — Constant-time hit-target publish

`SetHitTarget` / `RemoveHitTarget` (and the same shape on `HomingCatalog`) must not walk the whole list to find an id. Games publish every living enemy once per frame; linear `Set` is ~N²/2 and was measured at ~9 ms for 1000 already-present targets. Hits, homing, and `TryGet(index)` order stay the same. ADR-034. No public API change.

**In scope**

- `HitTargetCatalog`: keep `List<Volume>` as `TryGet` / sweep / overlap order. Add `Dictionary<TargetId, int>` id → index. `Set` of an existing id overwrites that slot (O(1) average). First `Set` of an id still **appends**. `Contains` / `TryGetPosition` / pierce-overlap `TryFind` use the dictionary
- `Remove` (and `radius <= 0`): drop the id from the dictionary and leave a dead slot. The next list read compacts once (stable, live entries slide left, indices rewritten), so order matches today’s compact-left list and a mass despawn is one pass, not one pass per id. Do **not** swap-with-last
- Same index map on `HomingCatalog` (`Set` / `Remove` / `TryGetPosition` by id). `TryGet(index)` order unchanged
- Sweep, `AppendSphere`, `AppendDisk` still iterate the list (one pass per query). Do not add a spatial hash in this phase
- `HitTargetCatalog` stays internal. `IProjectileWorld.SetHitTarget` / `RemoveHitTarget` signatures unchanged. `TargetId` is already a dictionary key (`IEquatable` + `GetHashCode`)
- Edit Mode tests: Set A,B,C then update B’s pose — `TryGet` 0,1,2 is still A,B,C with B’s new position; Remove B — remaining order is A,C; `Contains` / `TryGetPosition` after Set; `radius <= 0` removes; 32 already-present `SetHitTarget` calls do not grow `Count`. Existing published-hit / Homing tests stay green
- Docs: [ECS.md](ECS.md) catalog row, [REQUIREMENTS.md](REQUIREMENTS.md) scenario, `CHANGELOG` **0.1.5**. Bump `package.json` to **0.1.5**. Do not create a git tag in the phase session

**Out of scope:** exposing `HitTargetCatalog` or `IHitQuery`; changing hit/homing results; spatial partitioning of sweeps; occupancy list scans; applying HP; Unity Physics; Entities Graphics

**Proof:** Edit Mode order / update / remove tests above. Re-Set of 1000 existing ids is no longer a linear scan per id (implementation uses the dictionary). Yard unchanged.

**Stop.** Seeding the Direct ignore collider at spawn is **phase 30**. Do not start it in the same session.

## Phase 30 — Spawn ignores the firing collider until clear

A spawn can name the collider that fired. Direct overlap and sweep skip it until the shot body no longer overlaps it. Not a hit. ADR-037. Game CR: walking wizard + `straight_base` despawns on the first tick because the muzzle gap is smaller than one walk step.

**In scope**

- Public `SpawnRequest.IgnoreCollider` (`Collider`, default null). At rent, **before** `OnSpawn`, write it into the existing per-instance Direct skip (`PhysicsHitQuery`). Null / destroyed is today’s behaviour
- While that collider still overlaps the instance sphere (or is still in the sweep), Direct TryCast skips it: no `Hit`, no despawn, no Pierce/Bounce spent, no `StatusApplied`. After `RememberOrClearIgnore` drops it, a later contact is a normal hit (World or Target as classified)
- One slot: this is the same skip pierce/bounce already store after a hit. Do **not** add a second ignore, do **not** use `IProjectileProtection`
- Children keep the skip through existing `InheritIgnoreFrom` / `CopyIgnore` (multicast extras at spawn copy the shooter; multiply children copy whatever the parent is skipping at their rent). Seed the parent **before** flushing multicast extras
- Direct only. AreaEffect / GroundField / occupancy unchanged. `IHitQuery` stays internal. Collider is not an ECS field
- Works for any Recipe (straight, arc, homing, ApplyStatus, …). Hitscan / Beam are **phase 31+** but must use the same query skip when they ship
- Play Mode tests (PhysX): spawn overlapping a World collider with `IgnoreCollider` set → first `Tick` raises no `Hit` and the instance stays live; the same spawn without the field World-hits and despawns; after the shot has cleared, walking the collider back into the instance (or a second overlap) is a normal World hit; multicast extras spawned from that instance also skip while overlapping. Homing / ApplyStatus Recipes still Direct-hit a dummy. Existing collider tests stay green
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) `SpawnRequest`, [REQUIREMENTS.md](REQUIREMENTS.md), [ECS.md](ECS.md) query row, package `AGENTS.md` / `README.md` / `Documentation/index.md`, `CHANGELOG` **0.1.6**, XML on the field. Bump `package.json` to **0.1.6**. Do not create a git tag in the phase session
- Yard: turret stays collider-less. No new preset. The game passes the wizard capsule / tower collider

**Out of scope:** Delivery / Hitscan / Beam (**phase 31+**); skipping AreaEffect / GroundField; ignoring a published `TargetId` this way; `IProjectileProtection` as a shooter flag; putting the wizard in Runtime; applying HP; Unity Physics; Entities Graphics

**Proof:** Play Mode overlapping-World tests above. Standing still without `IgnoreCollider` still hits. Walking into the muzzle gap with the field set does not despawn.

**Stop.** Delivery (Straight / Arc / Hitscan) is **phase 31**. Do not start it in the same session.

## Phase 31 — Delivery: Straight, Arc, Hitscan

`Recipe.Delivery` chooses how a shot is delivered. Arc lofts at `Spawn`. Hitscan is one polyline query then expire. Gravity applies only on Arc. ADR-035. No Beam yet. Depends on phase 30 so Hitscan Spawn honors `IgnoreCollider`.

**In scope**

- Public `Delivery { Straight, Arc, Hitscan }`. `Recipe.Delivery` default **Straight**. `RecipeAsset` / `ToRecipe` copy it. Inspector shows it with the other base stats
- **Straight:** today’s kinematic SphereCast. Burst does **not** apply gravity even if `Recipe.Gravity > 0`
- **Arc:** kinematic + gravity. When `HomingAcquirePointSet`, `Spawn` calls `BallisticAim.TrySolve(origin, HomingAcquirePoint, speed, gravity)` **before** `OnSpawn` and writes that loft into launch velocity. Unreachable → no spawn, default `ProjectileId`, no `Expired`. Aim point unset → use `SpawnRequest.Direction` as already-solved, still apply gravity in flight. Do **not** add a second aim-point field. `BallisticAim.TrySolve` stays public for barrel preview. Multicast extras fan around the solved loft
- **Hitscan:** after `OnSpawn` / `OnTravel`, resolve a polyline of `Radius` SphereCasts **inside `Spawn`** (PhysX **and** published volumes), raise `Hit` / `OnHit` / `StatusApplied` / `OnExpire` / `Expired` as today, pool before `Spawn` returns. Unbounded = 10 km max. No view. Gravity ignored. Speed does not move it. OnTick / OnInterval do not run. Homing is read from the authored `OnTick` list as max **degrees of kink** per segment, not °/s (first segment from `Direction` toward the first lock, clamped to `n`; after pierce/bounce re-aim, clamped to `n` from the current / reflected direction). Without Homing, pierce stays collinear. World ends the polyline unless `Bounce` remaining reflects. `Pierce(n)` absorbs `n` Target hits and dies on `n+1` (same as kinematic). Direct query honors `IgnoreCollider` (ADR-037)
- Existing Gravity Edit Mode tests and the Cannonball `RecipeAsset` set `Delivery.Arc` (default Straight would fly level)
- Yard: Cannonball `Delivery.Arc`, `Fire` copies `lastAimPoint` and stops using `TrySolve` as `SpawnRequest.Direction` (barrel may still `TrySolve` / `BallisticClearance` for pitch). One **Hitscan** preset (Radius sphere-cast along barrel forward). Recipe sheet shows Delivery. No debug gizmo
- Edit Mode tests: Arc with aim point meets a reachable dummy (same idea as phase 16); unreachable Arc does not spawn; Straight with `Gravity > 0` stays level; Hitscan vs a published sphere (no collider) raises Target `Hit`; Pierce(2) through two published volumes then dies on the third; World wall ends Hitscan even with Pierce remaining; Bounce(1) reflects Hitscan off World; Homing kinks Hitscan after a pierce toward a second published volume (bend ≤ `n` degrees); Hitscan miss (empty catalog, no wall) expires with no `Hit`; Strong published volume eats Hitscan with no `Hit`. Existing Straight / collider tests stay green
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) `Delivery` / Arc Spawn / Hitscan, [REQUIREMENTS.md](REQUIREMENTS.md), [SANDBOX.md](SANDBOX.md), [ECS.md](ECS.md), package `AGENTS.md` / `README.md` / `Documentation/index.md`, `CHANGELOG` **0.1.7**, XML on `Delivery`. Bump `package.json` to **0.1.7**. Do not create a git tag in the phase session

**Out of scope:** `Delivery.Beam`, hold-to-fire, `Despawn`, `BeamPulseSeconds` (**phase 32**); Play Mode debug lines (**phase 34**); gizmos; `Recipe.Range`; putting input on `IProjectileWorld`; applying HP; Unity Physics; Entities Graphics

**Proof:** Edit Mode Arc / Hitscan tests above. Play Mode Cannonball still hits DummyTarget with the barrel lofted; Spawn is what solved. Hitscan preset fires along the barrel and hits DummyTarget with no flying mesh.

**Stop.** Beam / hold-to-fire is **phase 32**. Do not start it in the same session.

## Phase 32 — Beam (laser) and hold-to-fire

A Beam is a live Recipe instance that pulses a Homing-kinked polyline until `LifetimeSeconds` or `Despawn`. Hold-to-fire is sample-only. ADR-036. Depends on phase 31.

**In scope**

- `Delivery.Beam`. `Recipe.BeamPulseSeconds` (default 0.1; `<= 0` pulses every `Tick`). `RecipeAsset` / `ToRecipe` / inspector / sheet copy both
- Live instance. Origin and direction start at the muzzle. `AimBeam` updates them and redraws without raising `Hit` (the yard calls it each frame while held, from the turret that already aims at the mouse). Runtime does not read the mouse. Burst integrate skipped; lifetime still ticks. `LifetimeSeconds` expires the Beam **even if the fire button is held**
- Each pulse: polyline (published volumes, World clips unless Bounce, Homing on Beam curves at degrees per second along the path via `distance / speed`, the first step leaves along the muzzle before the turn, Hitscan stays a per-segment kink, Pierce n+1 **per pulse**, `IgnoreCollider` skip). The homing lock is kept across redraws while that target stays in front of the muzzle. Without Pierce the pulse stops on the first Target and the next pulse can hit that Target again. Pierce/Bounce remaining reset from the Recipe at the start of each pulse; the instance is **not** killed by Pierce. Contacts are synthetic Direct `OnHit` so Pierce / Bounce / `ApplyStatus` run. First pulse inside `Spawn`. OnInterval is not the pulse clock. The cylinder mesh stays up between pulses and is not a collider
- `IProjectileWorld.Despawn(ProjectileId)`: `OnExpire`, pool, `Expired`. Unknown / already-pooled is a no-op. Works on any live instance
- View: Runtime primitive cylinder **per polyline segment**, scaled to `Radius`. Sample **Laser** preset, red via `PresentationId`. Hits stay the SphereCast
- Yard hold-to-fire: press Spawn, hold keeps that one instance, release `Despawn`. Runtime does not read the mouse. No debug gizmo
- Edit Mode tests: Beam pulse hits a published volume as Direct `Hit`; Pierce(2) on one pulse passes two published volumes and the third contact ends that pulse, instance still live for the next pulse; next pulse can hit again; World clips a pulse; `LifetimeSeconds` expires a Beam with no `Despawn`; `Despawn` raises `Expired` and `OnExpire`; ApplyStatus on a pulsed Target; Strong published eats a pulse with no `Hit`. Existing Hitscan / Arc tests stay green
- Docs: sketch, `Despawn`, [SANDBOX.md](SANDBOX.md) hold-to-fire, package `AGENTS.md` / `README.md` / `Documentation/index.md`, `CHANGELOG` **0.1.8**, XML. Bump `package.json` to **0.1.8**. Do not create a git tag in the phase session

**Out of scope:** Play Mode debug overlay (**phase 34**); gizmos in this phase; WASD; orbit camera; save-to-asset; putting input on `IProjectileWorld`; applying HP; Unity Physics; Entities Graphics

**Proof:** Edit Mode pulse / Pierce-per-pulse / Despawn tests above. Play Mode Laser: hold — red cylinder segments, DummyTarget ticks each pulse; release — beam gone; keep holding past `LifetimeSeconds` — beam ends anyway.

**Stop.** Authored Multiply grid patterns are **phase 33**. Do not start them in the same session.

## Phase 33 — Authored Multiply grid pattern

A ScriptableObject grid authors Multiply pip order. `Spacing` is how far apart children spawn; `Multiply.Spread` is the cone half-angle. Square / Circle / RandomCircle stay. ADR-038.

**In scope**

- `MultiplyPatternAsset` (`Create > Projectile > Multiply Pattern`). Square grid, default **3×3**, minimum **2×2**. Plane is the existing Multiply plane (along travel / hit normal, +X right, +Y up). Offsets from the **geometric center** of the N×N cells
- **Spacing** (meters between adjacent cell centers, default 2). `<= 0` stacks every pip at the origin. This is pattern data, not `Multiply.Spread`
- **Spread** on `Multiply` for this pattern is cone **half-angle in degrees** of the farthest numbered cell from that center. `<= 0` keeps every child parallel to parent forward / hit normal. Square / Circle / RandomCircle still use `Spread` as meters and do not aim
- `IMultiplyPattern.GetDirection(...)` (procedural types return `forward`) and `ResolveCount(requested)` (grid = `min(requested, numbered)`; others = `requested`). `Multiply` spawns that many children and passes `GetDirection` into `RequestMultiplyChild`
- Grid UI on the asset Inspector: left-click empty cell → next integer (`1`, `2`, …); left-click occupied → no-op; right-click → remove that number and compact so remaining are `1..k`. **+** grows N→N+1 (new row at the bottom, new column on the right). **−** shrinks to N−1 (not below 2), drops the last row and column, compact-renumbers. `Multiply(n)` uses cells `1..n`. No numbers → spawn nothing
- RecipeAsset and ModifierKit Pattern dropdowns: Square / Circle / RandomCircle **plus every `MultiplyPatternAsset` in the project** (same `Patterns()` catalog, plus assets). Picking an asset copies grid + Spacing onto a serializable `GridMultiplyPattern` (do not alias the SO; `ToRecipe` / kit clone must copy fields, not `Activator` an empty type)
- One example asset in `Samples~/Yard` and one Multiply preset that uses it. Sample sheet pattern picker uses the same catalog. No Play Mode grid editor. Multicast unchanged
- Edit Mode tests: 3×3 with cells 1 (center), 2 (right of center), 3 (above center) — `Multiply(2)` uses only 1 and 2; `Multiply(5)` still only the numbered count (3); Spacing 2 places adjacent pips 2 m apart; Spread 20 aims the farthest numbered cell 20° off forward; Spread 0 is parallel; Spacing 0 stacks; right-click compact; grow 3→4 keeps existing numbers; shrink 3→2 drops off-grid numbers and compact; empty grid spawns 0; Square / Circle tests stay green (Spread still meters, still parent forward)
- Docs: [ARCHITECTURE.md](ARCHITECTURE.md) `IMultiplyPattern`, [REQUIREMENTS.md](REQUIREMENTS.md), [SANDBOX.md](SANDBOX.md), [ECS.md](ECS.md), package `AGENTS.md` / `README.md` / `Documentation/index.md`, `CHANGELOG` **0.1.10**, XML. Bump `package.json` to **0.1.10**. Do not create a git tag in the phase session

**Out of scope:** Play Mode debug overlay (**phase 34**); Play Mode grid editing; line / arc / cross / triangle procedural patterns; aiming Square / Circle / RandomCircle; changing Multicast; putting input on `IProjectileWorld`; applying HP; Unity Physics; Entities Graphics

**Proof:** Edit Mode grid / cone / compact tests above. Play Mode: pick the sample grid preset, fire — children spawn on the numbered cells and fan by Spread; Square preset still looks like today.

**Stop.** Play Mode debug tooling is **phase 34**. Do not start it in the same session.

## Phase 33b — Shot lifecycle events

Presentation hooks for a shot's life (Trello "Shot lifecycle events for presentation"). ADR-039.

**In scope**

- `Spawned` (`SpawnInfo`) on `IProjectileWorld` for every live instance, game or package-made; internal `SpawnRequest.ParentId` / `Cause` set in `ModifierContext.Enqueue`
- `ExpireInfo.Position` / `Direction` / `Reason` (`ExpireReason`); `HitInfo.Outcome` (`HitOutcome`)
- `HomingLocked` (`HomingLockInfo`): assigned at spawn, acquired, re-acquired; not while kept
- Edit Mode `LifecycleEventTests`; docs; `CHANGELOG` and `package.json` **0.1.12**. No git tag in the phase session

**Out of scope:** ground field and area-effect events (separate card); changing any existing event or behavior; a second façade; the debug overlay (phase 34)

**Proof:** Edit Mode tests: Spawned (game, Multiply child, Multicast extra, Hitscan order, unreachable Arc), every `ExpireReason`, Pierced / Bounced / Ended / Continued, HomingLocked assigned / acquired / re-acquire / not repeated.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 34 — Play Mode debug tooling

Bare phase. Hitscan has no mesh; Beam segments are easy to miss. Add Play Mode debug drawing and refine it after the human sees it. Do not invent a second façade. Depends on phase 32.

**In scope**

- Play Mode debug overlay for instant deliveries: Hitscan polyline (the query has no view), live Beam segments, and published hit volumes if that helps the yard
- Start **sample-side** (`Samples~/Yard`). Do not add Runtime types unless review asks. Toggle or always-on in Play Mode is fine for the first cut; refine colors, thickness, and what is drawn after review
- Docs: [SANDBOX.md](SANDBOX.md) how to see it. No `package.json` bump if nothing ships in Runtime

**Out of scope:** new deliveries; hold-to-fire (phase 32); authored Multiply grids (phase 33); WASD; orbit camera; Unity Physics; Entities Graphics; a second world / debug façade

**Proof:** Play Mode Hitscan — a debug line shows the query through the dummy line. Laser — pulse segments are obvious with the overlay on.

**Stop.**

## Phase 35 — Ground field and area burst events

Fields and `AreaEffect` bursts report to the game so it can play effects. ADR-040.

**In scope**

- `GroundFieldId`; events `GroundFieldPlanted`, `GroundFieldPulsed`, `GroundFieldEnded`, `AreaBurst` on `IProjectileWorld`. Pulsed is raised after that pulse's Area Hits, including empty pulses and the immediate pulse in `Plant`. Ended carries `Expired` or `Disposed`. AreaBurst is raised before its Area Hits; occupancy only when first made
- Edit Mode tests in `FieldAreaEventTests`. Docs, CHANGELOG **0.2.0**, `package.json` bump. No git tag in the phase session

**Out of scope:** view fade or pooling changes; Sandbox consumers; the debug overlay (phase 34)

**Stop.**

## Phase 36 — Hit query on concave mesh and terrain colliders

`Collider.ClosestPoint` does not support a non-convex `MeshCollider` or a `TerrainCollider`: it logs a warning and returns the input point. `PhysicsHitQuery` calls it on every Direct hit, so a shot that lands on concave ground warns twice per hit and builds wrong values. Game CR (Trello card #42). Runs before phase 34. No ADR; `CHANGELOG` only.

**In scope**

- Unsupported = `MeshCollider` with `convex == false`, or `TerrainCollider`, checked by type. `PhysicsHitQuery` never calls `ClosestPoint` on one. Box / Sphere / Capsule / convex Mesh keep today's code unchanged
- One fallback helper used at all six call sites: `ExitSurface`, `TryPickOverlap`, `Overlaps`, `SurfaceNormal`, `Overlap`, `OverlapDisk`
- Pierce exit is computed only when it is read (`TryPierceExit` from `RecordPierceExit`: Pierce charges left, Target or Basic hit), not eagerly on every hit in `TryCast`. `IHitQuery` stays internal; update `ScriptedHitQuery` to match
- Concave mesh pierce exit is the **last** exit: `Collider.Raycast` from beyond the bounds back along `-travel`, on the travel line through the hit point (the far side of the whole body, as convex today). Ray misses → no exit, Multiply falls back to the hit point. `TerrainCollider` has no pierce exit
- `Overlaps` (does the ignore skip still hold?) on an unsupported collider: the collider is in an overlap-sphere query at `radius + 0.01` skin. After a Bounce off concave ground, a Pierce through a concave Target, or a concave `IgnoreCollider`, the skip clears once the shot is clear, as for a box
- `TryPickOverlap` / `SurfaceNormal` on an unsupported collider: `Collider.Raycast` along travel from the origin backed up by `radius + 0.01`, length `2 × (radius + 0.01)` (to the front of the sphere). Point and normal from that ray; the closest-pick uses that point's distance (a concave collider no longer wins at 0). Ray misses → point = origin, normal = `-travel` (today's last fallback), ranked at `radius²`
- 0.2.2: after a pierce exit is found on a concave collider, the ignore also holds while the shot is moving toward the exit, has not passed it, and is inside the collider's bounds plus skin. This stops a second hit on the far side of a body thicker than one tick step. Known limitation: a curved path (gravity) inside a hollow concave Target that keeps heading toward the exit can pass through an inner floor of that same collider. That is the first-exit case and is out of scope
- `Overlap` / `OverlapDisk` on an unsupported `IDamageable` collider: point = `collider.bounds.ClosestPoint(center)`; normal as today (center inside → `Vector3.up`)
- Play Mode tests (PhysX), with `LogAssert.NoUnexpectedReceived`. Positive: a Straight shot onto a concave plane `MeshCollider` raises one World `Hit`, no warning, point within `radius + 0.01` of the plane. Negative: a BoxCollider Pierce exit is unchanged. Also: Arc onto a concave plane and onto a `TerrainCollider` hits with no warning; Pierce(1) + Multiply through a concave Target spawns children past the far side; Bounce(2) off concave ground can hit that ground again; a concave `IgnoreCollider` clears after the shot is clear; an Area overlap on a concave `IDamageable` mesh raises no warning. Existing collider tests stay green
- Docs: [REQUIREMENTS.md](REQUIREMENTS.md) scenario, [ECS.md](ECS.md) query row if it names `ClosestPoint`, `CHANGELOG` **0.2.1**. Bump `package.json` to **0.2.1**. Do not create a git tag in the phase session (merge to `main` tags and releases)

**Out of scope:** first-exit (inside a U or courtyard) pierce; pierce exit on terrain; `Physics.ComputePenetration` or a package-owned probe collider; GroundField plant on terrain (Later); changing hits on supported colliders; the game repo's Greybox workaround; Unity Physics; Entities Graphics

**Proof:** Play Mode tests above. Greybox (game repo, before card #41): firing at a concave `Ground` logs nothing.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 37 — Spawn ignores a set of colliders

A shot can skip a small set of colliders (the wizard and the pet) for its whole life. Game CR (Trello card #66). Runs before phase 34. ADR-041.

**In scope**

- `SpawnRequest.IgnoreColliders` (`IReadOnlyList<Collider>`), with XmlDoc. Copied at spawn into a pooled `List<Collider>` in `PhysicsHitQuery`. Never cleared while the shot lives
- `TryCast` overlap and sweep, `Overlap`, and `OverlapDisk` skip colliders in the set. No LINQ; a short loop
- `CopyIgnore` copies the set to children (own pooled list). The set returns to the pool once the instance is pooled and its pending children have spawned (not in `ClearIgnore`). `GroundField` takes its own copy at plant and returns it when the field ends
- `IHitQuery` additions are internal; update `ScriptedHitQuery` to match (no-op)
- Play Mode tests (PhysX): a "pet" collider between the shooter and a dummy, with `IgnoreColliders` = [shooter, pet]. Straight, Arc, and Hitscan hit the dummy. Pierce(1) through a first dummy, then the pet behind it is still skipped. Bounce off a wall back through the pet. Multicast extras and Multiply children skip the pet. `AreaEffect` and `GroundField` on an `IDamageable` pet raise no Area `Hit` for it, but do for another Target. Caller clears its list after `Spawn`: the live shot still skips. Null or empty list: today's behaviour. `IgnoreCollider` and `IgnoreColliders` together. A destroyed collider in the set does not throw. Existing tests stay green
- Docs: README, package AGENTS.md, `Documentation/index.md`, `CHANGELOG` **0.2.3**. Bump `package.json` to **0.2.3**. No git tag in the phase session

**Out of scope:** skipping published hit targets by `TargetId`; a live (non-copied) list; changing `IgnoreCollider`; the game repo's integration

**Proof:** Play Mode tests above. Greybox (game repo): the wizard's shot passes the fox and hits the enemy behind it.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 38 — Hit query buffers grow when full

`PhysicsHitQuery` NonAlloc buffers (16 colliders; 64 for the skin check) can fill in clutter. Unity then returns an unsorted subset, so the real target can drop out and the shot misses. Ignored colliders still take slots. Found in the phase 37 review; the bug is older. Runs before phase 34. No ADR: internal behaviour, no API change.

**In scope**

- When a query returns a count equal to its buffer length, double that buffer (cap 1024) and run the query again. Applies to the `TryCast` overlap and SphereCast, `Overlap`, `OverlapDisk`, and the skin check. At the cap the full buffer is used as is
- No allocation once a buffer has grown to what the scene needs
- Play Mode tests (PhysX): 40 ignored colliders in one sweep with a dummy just past them, and 100 ignored colliders at the origin around an overlapping dummy, with the dummy still hit; an `AreaEffect` with 100 ignored damageables still hits 20 other targets. All three fail on 0.2.3
- `CHANGELOG` **0.2.4**. Bump `package.json` to **0.2.4**. No git tag in the phase session

**Out of scope:** filtering ignored colliders inside the query (layer masks); a configurable buffer size; changing hit selection

**Proof:** Play Mode tests above.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 39 — OpenUPM distribution

Users get Package Manager update checks by installing from the OpenUPM scoped registry instead of a Git URL. Runs before phase 34. ADR-042.

**In scope**

- `package.json`: `displayName` "Projectile System", `documentationUrl`, `changelogUrl`, `licensesUrl`, `keywords`
- Root README, package README, and `Documentation/index.md`: OpenUPM install first (scoped registry snippet and `openupm add`), then Git URL, then folder copy. Say how to move from a Git URL install to the registry
- `CHANGELOG` **0.2.5**. Bump `package.json` to **0.2.5**. No git tag in the phase session
- Manual, by the repo owner: submit the package at openupm.com, then check the OpenUPM build of `v0.2.5`

**Out of scope:** publishing from CI (npm, GitHub Packages); an in-editor update checker; changing the release workflow; runtime code

**Proof:** After the OpenUPM listing is live, a clean Unity 6 project with the scoped registry lists the package under **My Registries** and offers `0.2.5`.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 40 — Beam max range and live homing target

Two change requests from the WizardGame board: a Beam / Hitscan length cap (card #208) and a game-set homing target on a live Beam (card #210). Runs before phase 34. ADR-043, ADR-044. One version.

**In scope**

- `Recipe.MaxRange` (meters, default 0; `<= 0` keeps 10 km). Caps the total polyline length of each Beam pulse and each Hitscan query; Pierce, Bounce, and homing curve segments share one budget. The last SphereCast stops at the cap (reach = `MaxRange` + `Radius`). No `Hit` past that sweep. Beam view ends at the cap. A Hitscan that reaches the cap without a killing hit expires as `HitscanEnd`. Straight and Arc ignore it. No modifier scales it
- `RecipeAsset` / `ToRecipe` copy `MaxRange`; the Recipe inspector shows it
- `IProjectileWorld.AimBeam(ProjectileId id, Vector3 position, Vector3 direction, HomingTarget target)`: same redraw as the three-argument `AimBeam`, plus it replaces the Beam's lock from that redraw on. The three-argument `AimBeam` leaves the lock as it is. Other deliveries and unknown ids do nothing
- Set lock: kept across redraws and pulses whether or not the target is in front of the muzzle; `Homing(n)` still caps the bend. A Target hit drops it for the rest of that polyline only; the next pulse starts on it again. `default` clears the set lock and any spawn-assigned lock, and the Beam self-acquires as today. Without a `Homing` modifier the target is stored but does not steer
- `HomingLocked` only when the set target differs from the current lock. Not on setting the same target again, not on the pulse-start restore, not on `default`
- Edit Mode tests (published volumes):
  - Beam `MaxRange = 30`: a volume at 25 m is hit; a volume at 40 m is not
  - Hitscan `MaxRange = 30`: same pair
  - `MaxRange = 0`: a volume at 40 m is hit (10 km default)
  - Beam `MaxRange = 30` + `Pierce(1)`, volumes at 20 m and 35 m: only the first is hit (one budget, not reset per segment)
  - Beam view's last point is at 30 m when nothing is hit
  - Beam + Homing spawned with `HomingTarget = Assigned(A)`, then `AimBeam(..., Assigned(B))`: later pulses hit B and never A
  - After the set to B, three-argument `AimBeam` with B off to the side of the muzzle: B is still the lock
  - `AimBeam(..., default)` after a set: the Beam self-acquires the candidate ahead
  - Set to B, then B again every frame: one `HomingLocked`; pulses raise none
  - Set lock B + `Pierce(1)` and C behind B: a pulse hits B then C; the next pulse curves to B again
  - Four-argument `AimBeam` on a Straight id does nothing
  - Existing Beam / Hitscan / Homing tests stay green
- Docs: the sketches, Delivery paragraph, glossary, and scenarios are already written; add phase 40 to the public API line at the end of [ARCHITECTURE.md](ARCHITECTURE.md); [ECS.md](ECS.md) Beam / Hitscan paragraph and the `Homing.cs` / `ProjectileWorld.cs` rows; package `AGENTS.md` / `README.md` / `Documentation/index.md`; XML on `Recipe.MaxRange`, `Delivery`, and both `AimBeam` overloads. `CHANGELOG` **0.2.6**. Bump `package.json` to **0.2.6**. No git tag in the phase session

**Out of scope:** a `Range(n)` modifier or a distance fuse (ADR-028); a range on Straight / Arc; `SetHomingTarget` for other deliveries; a package "in front of the muzzle" check on a set lock; the game's lock circle; yard sample changes; Unity Physics; Entities Graphics

**Proof:** Edit Mode tests above.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 41 — Upright capsule hit target

Change request from the WizardGame board: a published hit target that covers a tall upright model, with a homing point the game sets (card #239, game-side card #238). Runs before phase 34. ADR-045. One version.

**In scope**

- `IProjectileWorld.SetHitTarget(TargetId id, Vector3 bottom, float radius, float height, float homingHeight, ProtectionLevel protection = None)`: an upright capsule. `height` is total, caps included; below `2 * radius` it is raised to that. `homingHeight` is clamped to `[0, height]`. `radius <= 0` removes. The sphere overload is unchanged
- `HitTargetCatalog` stores every volume as an axis segment, a radius, and a homing point; a sphere is a zero-length segment. Direct sweep (Straight, Arc, Hitscan, Beam), area sphere, ground disk (XZ to the axis), pierce exit, and ignore release test the segment. `TryGet` / `TryGetPosition` return the homing point
- `HitInfo.Point` is the contact on the capsule
- Edit Mode tests (`CapsuleHitTargetTests`, scripted query):
  - Straight at head height (2.6 m) hits a capsule (feet at 0, radius 0.6, height 3.2); the old 0.6 m sphere at 1.5 m misses the same shot
  - A shot beside the capsule, outside its radius, misses; one just inside hits
  - Hitscan and a Beam pulse at head height hit; a Beam beside it misses
  - `Pierce(2)` through two capsules hits both in order
  - `AreaEffect` at the upper body hits the capsule but not the old sphere or a capsule beside it; `GroundField` uses the axis XZ distance
  - Homing steers toward the homing point, not the centre, and acquires by it
  - Catalog: sweep down the axis hits the top cap, a start inside hits at zero travel, a short capsule is a sphere with the homing point clamped, re-setting a sphere id as a capsule keeps one entry, radius 0 removes
  - Existing published-target, Homing, Beam, and Hitscan tests stay green
- Docs: ADR-045, the API sketch and the public API line in [ARCHITECTURE.md](ARCHITECTURE.md), the scenarios, the [ECS.md](ECS.md) `HitTargetCatalog.cs` row, package `AGENTS.md` / `README.md` / `Documentation/index.md`, XML on both `SetHitTarget` overloads. `CHANGELOG` **0.2.7**. Bump `package.json` to **0.2.7**. No git tag in the phase session

**Out of scope:** tilted capsules, boxes, or compound shapes; capsule `IDamageable` colliders (PhysX already handles a `CapsuleCollider`); the game's per-model numbers and `HitTargetPublisher` (card #238); yard sample changes; Unity Physics; Entities Graphics

**Proof:** Edit Mode tests above.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 42 — Shooter hit-target skip and authorable status popup

Two change requests from the WizardGame board: a spawn that skips the shooter's own published hit target (card #240) and a status popup that lists only authorable statuses (card #237). Runs before phase 34. ADR-046, ADR-047. One version.

**In scope**

- `SpawnRequest.IgnoreTargetSet` + `IgnoreTarget`: Direct overlap and sweep skip that published target until the body clears it; not a hit; a later contact is normal. Own slot in `HitTargetCatalog`, kept through Pierce / Bounce hits on others. Beam re-arms it each pulse. Children copy it. Released with the instance
- Homing never acquires the shooter (shipped acquisitions skip `ModifierContext.ShooterTarget`; `Homing` drops a custom pick of it). An assigned spawn lock is kept. Area and ground queries unchanged
- `RecipeAssetEditor`'s concrete-type list (modifier, pattern, and status popups) leaves out classes with no public parameterless constructor. `ToRecipe` clone fallback unchanged
- Edit Mode tests (`IgnoreTargetTests`, `RecipeAssetTests`):
  - Without the skip, an arrow from the axis of an Archer capsule (r 1.06, h 3.15) hits its shooter; with it, the arrow skips the shooter and hits another enemy 15 m away
  - After the body clears, the shooter moved into the path is a normal hit
  - Multicast extras copy the skip
  - Homing locks the other enemy, never the shooter; the aim-point first lock does the same with the aim point on the shooter
  - A Beam from inside its shooter hits only the other enemy, once per pulse; a Hitscan from inside it hits the other enemy
  - A Bounce off another enemy inside the shooter keeps the skip; Multiply children spawned inside it copy the skip
  - A custom acquisition that picks the shooter does not lock; a shooter published after `Spawn` is still skipped; a retired shot releases its record
  - The status catalog leaves out a status with no public parameterless constructor; `ToRecipe` keeps such an instance by reference
  - Existing tests stay green
- Docs: ADR-046, ADR-047, the API sketch and public API line in [ARCHITECTURE.md](ARCHITECTURE.md), glossary and scenarios, [ECS.md](ECS.md) `HitTargetCatalog.cs` and `Homing.cs` rows, package `AGENTS.md` / `README.md` / `Documentation/index.md`, XML on the new fields. `CHANGELOG` **0.2.8**. Bump `package.json` to **0.2.8**. No git tag in the phase session

**Out of scope:** a list of ignored targets; a whole-life Direct skip for targets; area / ground skips for the shooter; a `[NotAuthorable]` attribute; the game's muzzle placement; yard sample changes; Unity Physics; Entities Graphics

**Proof:** Edit Mode tests above.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 43 — Predict a Straight or Arc path

Change request from the WizardGame board: the game asks where an Arc shot would fly and what it would hit first, without spawning it, to draw a charge preview (card #245, blocks game card #244). Runs before phase 34. ADR-048. One version.

**In scope**

- `IProjectileWorld.PredictPath(in SpawnRequest, in PathPrediction, List<Vector3>, out PathHit)` and the public structs `PathPrediction` (`StepSeconds`, `MaxSteps`) and `PathHit` (`Point`, `Normal`, `Kind`, `TargetId`, `Time`)
- Same start as `Spawn` (including the Arc ballistic solve), `Speed` / `Gravity` / `Size` on `OnSpawn` and `OnTravel`, the live tick order per fixed step, and `IHitQuery.TryCast` with the request's ignore rules under a reserved id that is released before return
- Ends at the first contact (true), the lifetime, or the step cap (false). Hitscan and Beam return false with an empty list. No events, rent, or queued work
- Tests:
  - Edit Mode (`PathPredictionTests`, scripted query): a capsule target in the path returns Kind Target with its id at the point a spawned twin hits after the same fixed ticks; a `Speed(2)` on `OnTravel` path matches its twin; a lifetime end returns false with the twin's expire position last; no `Spawned` / `Hit` / `Expired`, unchanged active and pooled counts; a start inside the `IgnoreTarget` capsule does not hit it (and does without the skip); Hitscan and Beam return false and empty; a call inside a `Hit` handler leaves the live shot's skip as it was; the list does not grow on a repeat call
  - Play Mode (`PathPredictionPlayModeTests`, PhysX): a level Arc over a flat collider lands within 0.05 m of its twin; a start inside `IgnoreCollider` does not hit it
  - Existing tests stay green
- Docs: ADR-048, the API sketch and public API line in [ARCHITECTURE.md](ARCHITECTURE.md), glossary and a scenario, [ECS.md](ECS.md) row, package `AGENTS.md` / `README.md` / `Documentation/index.md`, XML on the new members. `CHANGELOG` **0.2.9**. Bump `package.json` to **0.2.9**. No git tag in the phase session

**Out of scope:** steered (Homing) prediction; the Multicast turn of the main shot; Bounce or Pierce continuation; Multicast extras or children; a polyline for Hitscan or Beam; custom modifier prediction; the game's preview line and marker (card #244); yard sample changes; Unity Physics; Entities Graphics

**Proof:** Edit and Play Mode tests above.

**Stop.** Play Mode debug tooling is **phase 34**.

## Phase 44 — Critical chance above 100%

Change request from the WizardGame board: the game needs a critical chance above 1 to roll a double crit (card #306, for game card #300). Runs before phase 34. ADR-049. One version.

**In scope**

- `Critical(n)` multiplies the instance chance by `n` with no upper clamp; a result below 0 becomes 0; `n <= 0` is a no-op
- `HitInfo.CriticalChance` on Direct, Area, and occupancy hits carries the unclamped value. `GroundField` hits stay 0
- XML on `Critical`, `Recipe.CriticalChance`, `ModifierContext.CriticalChance`, and `HitInfo.CriticalChance` names the range "0 or more; above 1 is allowed"
- Edit Mode tests (`CombatScaleTests`): `Critical(2)` on 0.75 gives 1.5 on the instance and the `HitInfo`; `Critical(64)` on 0.025 gives 1.6; `Critical(0)` and `Critical(-2)` leave 1.5 as it was; `Critical(2)` on -0.5 gives 0. The phase 10 clamp test is replaced. Existing tests stay green
- Docs: ADR-049, glossary and modifier line in [REQUIREMENTS.md](REQUIREMENTS.md), API sketch and public API line in [ARCHITECTURE.md](ARCHITECTURE.md), [ECS.md](ECS.md) row. `CHANGELOG` **0.2.10**. Bump `package.json` to **0.2.10**. No git tag in the phase session

**Out of scope:** rolling a crit; clamping the Recipe's authored chance; the game's double crit (card #300); yard sample changes; Unity Physics; Entities Graphics

**Proof:** Edit Mode tests above.

**Stop.** Play Mode debug tooling is **phase 34**.

## Later (not scheduled)

- Unity Physics and/or Entities Graphics adapters
- Multiply patterns: line, arc, cross, triangle (procedural `IMultiplyPattern`s; the grid editor is phase 33)
- GroundField plant pose: v1 snaps Y to the world ground plane (`0`). Revisit spawn process and positioning (terrain / ground raycast, sloped floors, a chosen ground height) without changing the disk overlap or pulse contract
- More modifiers, optional Rigidbody solver, projectile-vs-projectile hits
- Save Play Mode tweaks to `RecipeAsset`
- `Range(n)` / an OnExpire-at-distance fuse (ballistic reach stays Speed + Gravity, ADR-028). Beam / Hitscan length is `Recipe.MaxRange` (phase 40, ADR-043)
- Convert remaining sandbox C# Recipes
- WASD turret move; orbit camera
