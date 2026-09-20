# Decisions

Short ADRs. Newest first. Do not relitigate a decision in code; add a new ADR if it should change.

## ADR-026 — GroundField views come from a factory

- **Date:** 2026-09-20
- **Decision:** `GroundField` views are created by `IGroundFieldViewFactory.Create(radius, presentationId, poolParent)`, the same one-method swap as `IProjectileViewFactory` (ADR-021). `GroundField.PresentationId` selects the look the way `Recipe.PresentationId` does. Default is `PrimitiveGroundFieldViewFactory` (thin cylinder, scaled to `2 * radius` on XZ). `PrefabGroundFieldViewFactory` maps id → prefab and does **not** overwrite `localScale`. Unknown or empty ids fall back to the primitive. Views are pooled per presentation id. `ProjectileView` is reused; prefabs may already carry it. `createViews: false` never calls the factory. Colliders on created views stay disabled. Hits stay the XZ disk. No Addressables, no Entities Graphics.
- **Why:** Games need more than one puddle mesh without a factory per prefab. Reusing `IProjectileViewFactory` would take a `Recipe` the field does not have. The id lives on the modifier because the field is not a Recipe.
- **Consequences:** Three new public types (`IGroundFieldViewFactory`, `PrimitiveGroundFieldViewFactory`, `PrefabGroundFieldViewFactory`) and `GroundField.PresentationId`. `ProjectileWorld` gains an optional `groundFieldViewFactory` argument; `new ProjectileWorld(poolParent, viewFactory)` keeps today’s primitive disk.

## ADR-025 — Package id is `com.veerdonk.projectile-system`

- **Date:** 2026-09-19
- **Decision:** The UPM id and folder are `com.veerdonk.projectile-system` (one `com.` prefix). C# namespace and asmdefs follow: `Veerdonk.ProjectileSystem` (Editor / Tests suffixes unchanged). Display name stays `com.veerdonk.projectile-system`. This is a breaking rename at **0.1.0** before anyone depends on the double prefix. Docs, sandbox `using`s, and `packages-lock.json` update in the same change. Do not publish the old id.
- **Why:** `com.davidvdveerdonk.com.veerdonk.projectile-system` is a template doubled with the author name. Unity ids are reverse-DNS; `com.veerdonk.projectile-system` matches the display name already on `package.json`. Namespace `Davidvdveerdonk.ComVeerdonkProjectileSystem` repeats the same mistake for every consumer `using`.
- **Consequences:** `git mv` the package folder. InternalsVisibleTo and test asmdefs change with it. GitHub repo name can stay. No second façade, no new modifiers. Phase 23 runs **after** phases 17–22, so XML docs, leftover deletion, coverage, and the Sonar pass include Protection, the view factory, GroundField, RecipeAsset, and the sample-scene leftovers — not only the phase-16 façade.

## ADR-024 — Sample scene is the Play Mode demo; turret stays in Assets

- **Date:** 2026-09-17
- **Decision:** The Play Mode demo is a **sample scene** in `Assets/` (turret, mouse aim, preset dropdown, recipe sheet, reset). It replaces the keyboard-slot lab HUD. Camera, Input System, dummy HP, turret, and sample UI stay out of the runtime package. Presets are `RecipeAsset`s (ADR-023). Play Mode edits a **working `Recipe` copy** from `ToRecipe()`; it must not dirty the assets, add/remove modifiers, or write back. Changing preset or **Reset** rebuilds that copy from the asset. **Reset** also `Dispose`s the `ProjectileWorld` (live shots, pool, ECS allocations), restores dummy HP/status, and recenters the turret. Mouse aim is a camera ray against the ground plane; yaw only; spawn at the barrel tip along flattened forward. Clicks on UI do not fire. The turret has no PhysX collider. The recipe sheet shows stats and the six lists (name + editable magnitudes/properties). Child Recipes are a name only. Dropdown is a curated set of shipped modifiers, not every old lab key. A small active/pool line is enough live readout.
- **Why:** The lab HUD does not scale (24 keys, one row per `n`). A sample should look like using the package: pick a Recipe, aim, fire, read what the shot is. Rebuilding the phase-20 inspector in Play Mode would duplicate authoring and tempt people to compose in the demo. Mutating `RecipeAsset` in Play Mode dirties source assets. `Dispose` on reset is required because the Entities `World` holds native allocations and leftover instances would keep ticking. Ground-plane yaw matches today’s horizontal shots; pitch waits on gravity. Package Samples/ vs Assets/ split stays later — this is still the sandbox, just a better scene.
- **Consequences:** No new package types. Sandbox driver, turret, and UI replace `SandboxLab`’s key grid. Later modifiers add a preset when they ship, they do not revive the key HUD. Barrel pitch and a ballistic sample preset are phase 22. Orbit camera, WASD, hold-to-fire, and save-to-asset stay later.

## ADR-023 — Recipes are authored as ScriptableObjects; Spawn still takes Recipe

- **Date:** 2026-09-17
- **Decision:** Unity authoring is a `RecipeAsset` ScriptableObject with the same base stats as `Recipe` and six `[SerializeReference]` modifier lists. A custom inspector shows those six lists and an **Add** menu of `IModifier` types whose `[ModifierStage]` includes that list. `RecipeAsset.ToRecipe()` builds the `Recipe` that `IProjectileWorld.Spawn` already consumes. `Recipe` stays a plain C# class (`new Recipe()` in tests). Multiply/Multicast child slots on the asset are other `RecipeAsset` references, resolved on `ToRecipe()` (a cycle map shares one `Recipe` so self-multicast cannot stack-overflow in the builder). No EditorWindow graph, no one-SO-per-modifier, no second façade. Unimplemented modifier classes simply do not appear until they exist. Game `IModifier` types with the attribute show up through `TypeCache`; there is no registry.
- **Why:** The definition layer was already “ScriptableObjects / serializable data.” Making `Recipe` itself a `ScriptableObject` would break Edit Mode tests and put `UnityEngine.Object` on `SpawnRequest`. A graph would be a second composition model (ADR-001). Nested `Recipe` copies would duplicate children instead of referencing them. The six lists *are* the tool — a custom inspector on the asset matches how we author, and stage attributes are the same “one method / one place to extend” shape as `IMulticastPattern`. `SerializeReference` is how Unity stores mixed interface lists; `SerializeField IModifier[]` cannot.
- **Consequences:** Three new public types (`RecipeAsset`, `RecipeStage`, `ModifierStageAttribute`). Existing `IModifier` types become `[Serializable]` with a serializer ctor; public parameterized ctors stay for tests. `ToRecipe()` must run before `Spawn`. Default inspector type pickers are not enough — they would offer Pierce on `OnSpawn`. A visual pattern editor and converting every sandbox C# Recipe remain later.

## ADR-022 — GroundField is a planted disk, not a Recipe

- **Date:** 2026-09-17
- **Decision:** `GroundField` is a modifier on `OnHit`, `OnExpire`, and `OnTravel`. It plants a lingering **horizontal disk** owned by `ProjectileWorld`, ticked from the same `Tick`. Size (`radius`), `lifetimeSeconds`, `damage`, optional `IStatusEffect`, and `intervalSeconds` are authored on the modifier, not on `Recipe` / `EffectRadius`. The overlap is XZ distance ≤ radius (Y ignored). Pulses use the existing `Hit` (`HitSource.Area`) and `StatusApplied` events, immediately on plant and then every interval, the same way `AreaEffect` talks to the game. The field is not a projectile: no SphereCast, no stage lists, no child Recipe, no `Expired`. `IHitQuery.OverlapDisk` is the query; modifiers only queue a plant via `ModifierContext`. `intervalSeconds <= 0` never pulses. Occupancy enter/exit and per-frame `OnTick` auras stay later. No second façade.
- **Why:** Phase 13’s interval `AreaEffect` is an aura on the **flying** instance; it dies with the shot. A fire pool has to outlive the projectile, sit still, and hit a circle on the ground rather than a sphere (a sphere of puddle radius would tick a dummy on a ledge above). Spawning a Speed-0 child Recipe would still SphereCast, still use a sphere overlap, and would pretend a zone is a bullet. The field is not a Recipe because it has no stage lists to compose; stuffing one in would be a second instance type behind the Recipe door. Pulse period lives on the modifier because ADR-012’s `Interval(n)` belongs to a projectile’s `OnInterval` list, and the field is not one.
- **Consequences:** One new public modifier (`GroundField`) and `OverlapDisk` on `IHitQuery` / `ScriptedHitQuery`. Views come from `IGroundFieldViewFactory` (ADR-026). v1 plants at `(x, 0, z)` (world ground plane). Terrain snap / sloped floors stay Later. Do not scale field radius/damage with `EffectRadius(n)` / `Damage(n)` in this phase. Do not raise `Expired` for fields.

## ADR-021 — Views come from a factory; hits stay a sphere

- **Date:** 2026-09-16
- **Decision:** `ProjectilePool` does not `CreatePrimitive` itself. It calls `IProjectileViewFactory.Create(recipe, poolParent)`. `Recipe.PresentationId` selects the look. The default factory is the current sphere primitive, scaled to `2 * Radius`. A `PrefabViewFactory` maps ids to prefabs and does not rescale them. Unknown ids fall back to the primitive. `ProjectileView` is public so prefabs can carry it. Colliders on views stay disabled. SphereCast of `Recipe.Radius` is still the hit shape. Tests with `createViews: false` never call the factory. No Addressables, no Entities Graphics, no mesh colliders for hits.
- **Why:** Games need their own meshes and particles without forking the pool or putting `UnityEngine.Object` on simulation components. `PresentationId` has been on the Recipe since phase 1 and was unused. A factory is the same shape as `IHitQuery` / `IMulticastPattern`: one method, swap the implementation. Scaling a user prefab to the sim radius would squash authored art; the lab sphere can keep scaling.
- **Consequences:** Three new public types (`IProjectileViewFactory`, `PrimitiveViewFactory`, `PrefabViewFactory`) and a public `ProjectileView`. `ProjectileWorld` gains an optional factory argument. v1 ships two prefabs (`sphere`, `cube`) for tests and the sandbox. Hit/expire one-shot VFX stay game listeners on `Hit` / `Expired`.

## ADR-020 — Protection is a collider marker, not HP

- **Date:** 2026-09-16
- **Decision:** `IProjectileProtection` marks a collider with `ProtectionLevel.Basic` or `Strong`, the same way `IDamageable` marks `HitKind.Target` (ADR-004). **Basic** runs `OnHit` (pierce and bounce still consume; `AreaEffect` still runs) but does not flush `ApplyStatus` for that contact. Direct `Hit` is still raised with `HitInfo.Protection = Basic` so the game can refuse HP. **Strong** despawns the instance with no Direct `Hit`, no `StatusApplied`, no `OnHit`, and no `OnExpire`; `Expired` still fires. Area overlap skips Strong; Basic may be included with `Protection = Basic`. `Kind` stays Target if `IDamageable` is present. Pierce consumes on `HitKind.Target` **or** `Protection = Basic`. The package still does not subtract HP.
- **Why:** Invulnerability and shields change *projectile* lifetime and events (bounce, pierce, splash, despawn). That is hit classification, not a health model. Putting it in the game would fork bounce/pierce/area for every consumer. HP, shield hit points, and i-frames stay in the game; they can implement the interface and ignore `Hit` when `Protection != None`.
- **Consequences:** Two new public types (`IProjectileProtection`, `ProtectionLevel`) and `HitInfo.Protection`. Sandbox ships a Basic dummy and a Strong shield. Do not add factions, shield HP, or a second façade.

## ADR-019 — Ballistic aim is a fire-time helper, not a path

- **Date:** 2026-09-16
- **Decision:** `BallisticAim.TrySolve` returns a launch direction for a given origin, target, speed, and gravity (world down). Low arc is the default; `highArc` selects the other solution. `false` means unreachable. Games copy the direction into `SpawnRequest`. The instance still flies through the phase 14 Burst integrate. Do not evaluate a closed-form parabola instead of the per-tick SphereCast. Do not add a second `IProjectileWorld` method that spawns-and-aims.
- **Why:** Hitting a dummy with a cannonball is an aiming problem, not a new motion solver. Replacing the tick integrate with `x(t)` would skip the hit pipeline’s segment casts. A helper keeps `SpawnRequest.Direction` as the only aim input.
- **Consequences:** One new public type (`BallisticAim`). Sandbox may call it before `Spawn`. Unreachable shots are the caller’s problem.

## ADR-018 — Gravity(n) scales instance gravity, like Speed

- **Date:** 2026-09-16
- **Decision:** `Gravity(n)` uses the same scale contract and stages as `Speed(n)`: `OnTravel`, `OnInterval`, `OnHit`; not `OnSpawn`; `n <= 0` is a no-op. It multiplies instance gravity. It does **not** run on `OnTick` and does not add `g * dt` itself — Burst already integrates (ADR-017). Putting a gravity *apply* on OnTick would double-apply and compound every frame.
- **Why:** Option 2 was “a Gravity modifier.” Once gravity is a Recipe stat applied in the solver, the modifier’s job is to *change* that stat, which is already how `Speed(n)` works. OnTick is for steering (Homing), not for re-applying acceleration.
- **Consequences:** One new public modifier (`Gravity`). `ModifierContext.Gravity` must be writable (phase 14). Do not add antigravity or a second gravity vector in this phase.

## ADR-017 — Recipe.Gravity is a motion stat, default 0

- **Date:** 2026-09-16
- **Decision:** Downward acceleration lives on `Recipe.Gravity` (m/s²), default **0**. Copy onto the instance at spawn. The kinematic Burst job applies it: `velocity += (0,-1,0) * gravity * dt`, then `candidate = position + velocity * dt`. `Gravity <= 0` keeps today’s straight integrate. Do not read `Physics.gravity`. Do not bake a spline. Bounce reflects whatever velocity the integrate has produced.
- **Why:** Bullets and fireballs must stay straight without an extra modifier. A cannonball is the same solver with a non-zero Recipe field, the way speed already works. The arc is the integral of acceleration, so it stays calculated. Unity Rigidbody gravity is a later solver (ADR-005) and would break Edit Mode tests.
- **Consequences:** One new Recipe/instance field. Existing Recipes need no change. `Gravity(n)` is phase 15 (ADR-018). Ballistic aim is phase 16 (ADR-019). Homing on the same shot will fight the drop; list order still applies, but this phase does not add a Homing+gravity scenario.

## ADR-016 — AreaEffect overlaps Targets; area damage is not Recipe.Damage

- **Date:** 2026-09-16
- **Decision:** `AreaEffect` is one modifier on `OnHit`, `OnTravel`, and `OnInterval`. The overlap sphere uses instance `EffectRadius`. Area damage and optional `IStatusEffect` live on the modifier, not on `Recipe.Damage` / `ApplyStatus`. Each Target in the sphere raises the existing `Hit` / `StatusApplied` events with `HitSource.Area`. `includeDirectHit` / `includeDirectStatus` (default true) control whether the **contact** still raises Direct `Hit` and `ApplyStatus`. Contact `HitInfo` is still sampled before `OnHit` so phase 10 OnHit `Damage(n)` does not rewrite that event; the Direct event is raised **after** the `OnHit` list so it can be suppressed. OnInterval is an **aura**: the sphere is centered on the live instance each interval crossing, not a zone left in the world. Not `OnTick`. `EffectRadius = 0` is a no-op. World colliders and other projectiles are ignored. The package does not apply HP or tick DoT. `IHitQuery.Overlap` is the query; modifiers only queue via `ModifierContext`.
- **Why:** Splash, muzzle burst, and a traveling aura are the same overlap, three existing stages. Radius is already a Recipe stat; area damage must not steal `Recipe.Damage` or a straight shot without `AreaEffect` would explode. Hit-before-OnHit made “area only” impossible; sampling early and raising late keeps pierce damage timing and lets the flags work. Per-frame `OnTick` overlap would melt dummy HP; the interval clock is already the pulse rate.
- **Consequences:** One new public modifier (`AreaEffect`), `HitSource` on `HitInfo`, overlap on `IHitQuery` / `ScriptedHitQuery`, and `ModifierContext` request/suppress APIs. Do not add a second façade or a seventh stage. Do not scale area damage with `Damage(n)` in this phase. Lingering ground fields and occupancy tracking are later.

## ADR-015 — Multiply places children through `IMultiplyPattern`

- **Date:** 2026-09-16
- **Decision:** `Multiply` does not aim children with `RandomConePattern`. It asks an `IMultiplyPattern` for a world-space **offset** per child (`GetOffset(context, forward, spread, index, count)`). Children keep the parent travel direction (interval/expire) or the hit normal (OnHit). `Multiply.Spread` is meters between adjacent pips; `0` stacks. Default pattern is `SquarePattern`. v1 also ships `CirclePattern` and `RandomCirclePattern`. Do **not** merge this with `IMulticastPattern` (that type aims; this type places). Do not add a pattern editor or an unused offset-list class in this phase. Line, arc, cross, and triangle patterns are later implementations of the same interface.
- **Why:** Shotgun/burst layout is positional, not a cone of velocities. Multicast already proved “strategy on the modifier, one method, index + count.” Square (dice then grid), circle (triangle then rings), and random disk are the layouts we want to fire in the lab; a visual editor is more work than those three types and still could not replace the `n > 9` / `n > 10` overflow rules with a static list. A later editor authors a new `IMultiplyPattern` (local offsets × spread) without changing `Multiply` or `IProjectileWorld`.
- **Consequences:** Four new public types (`IMultiplyPattern`, `SquarePattern`, `CirclePattern`, `RandomCirclePattern`) and `Multiply.Spread` / `Multiply.Pattern`. Layouts are specified in [PHASES.md](PHASES.md) phase 11. `RandomCirclePattern` rejects samples closer than the child diameter (`2 * Radius`) and does not grow the disk; `Spread = 0` still stacks. `Spread(n)` still copies and scales the instance spacing in phase 12 (ADR-014). Multicast is unchanged (ADR-006).

## ADR-014 — Stat modifiers are scales; Spread(n) scales Multiply spacing

- **Date:** 2026-09-16
- **Decision:** `Damage(n)`, `Critical(n)`, `Mana(n)`, `EffectRadius(n)`, `Size(n)`, and `Spread(n)` use the same scale contract as `Speed(n)`: `n > 1` increases, `0 < n < 1` decreases, `n <= 0` is a no-op. They run on `OnTravel`, `OnInterval`, and `OnHit`, not `OnSpawn`. `Size(n)` scales `Radius`. `Critical(n)` clamps to `[0, 1]`. `ManaCost` is copied onto the instance at spawn so `Mana(n)` mutates that copy; `Recipe.ManaCost` stays the authored fire-time cost. **Spread is not a Recipe stat** and does not jitter the original shot. `Spread(n)` scales the instance copy of `Multiply.Spread` (pip spacing for `IMultiplyPattern`, ADR-015; default scale 1). Multicast still uses `IMulticastPattern` (ADR-006). `OnHit` scales do not rewrite the `Hit` event that already fired.
- **Why:** Phase 9 stored combat numbers on the Recipe. Spread belongs to how Multiply lays out children, not to every shot's muzzle. A Recipe field would jitter singles that never multiply. A scale modifier can widen pip spacing the way `Speed(n)` widens speed.
- **Consequences:** Six new public modifier types. Phase 11 authors `Multiply.Spread` and the patterns (ADR-015); this ADR's `Spread(n)` / `Size` / `EffectRadius` wait until phase 12. `ModifierContext.Radius` must be writable. Views and SphereCast follow `Size`. Do not roll crit, run AoE queries, or spend mana. Do not add additive (`+5` damage) variants in these phases.

## ADR-013 — Combat and cast numbers are Recipe stats

- **Date:** 2026-09-16
- **Decision:** Damage, critical chance, mana cost, effect radius, and size are **authored stats**, not named modifiers. They live on `Recipe` next to `Speed`, `Radius`, and `LifetimeSeconds`. Size **is** `Radius` — do not add a second field. Damage, crit chance, effect radius, and radius copy onto the live instance at spawn and onto `HitInfo` at contact so a later modifier can mutate them the way `Speed(n)` mutates `Speed`. `ManaCost` stays on the Recipe only until phase 10 copies it for `Mana(n)`; the package never spends it. **Spread is not a Recipe field** — it is authored on `Multiply` (ADR-015). This phase does not add scale modifiers.
- **Why:** A straight shot must have damage without putting `Damage(10)` on a stage list. Modifiers are behaviors with a magnitude; they should *change* stats, not *be* the only place a stat exists. That is already how speed works: `Recipe.Speed` from phase 1, `Speed(n)` later. Mana and crit application are game combat (ADR-004); storing the numbers is enough for a game to read them.
- **Consequences:** `HitInfo` grows combat fields. Default `Damage` is 1 so sandbox HP that used to subtract 1 per hit keeps working. Default crit, mana, and effect radius are 0. `ModifierContext` must expose the mutable combat stats or a later modifier cannot write them. Do not roll crit, run AoE queries, or spend mana in the package. Combat scale modifiers are phase 10; Multiply layout is phase 11 (ADR-015); `Size` / `EffectRadius` / `Spread(n)` are phase 12 (ADR-014).

## ADR-012 — One interval per instance, set by `Interval(n)` on `OnSpawn`

- **Date:** 2026-09-16
- **Decision:** How many seconds pass between `OnInterval` runs belongs to the instance, not to each modifier on the list. `Interval(n)` is an `OnSpawn` modifier that writes that period; every modifier on `OnInterval` then fires on the same crossings, in list order, once per crossing. `Multiply` and `Speed` no longer take `intervalSeconds`. A Recipe with an `OnInterval` list but no `Interval(n)` has a period of 0 and never runs that list.
- **Why:** Two modifiers on one `OnInterval` list carried two periods that the author had to repeat and keep in sync, and nothing made "every 0.5s" a property of the shot. The instance already owns the clock those periods were read against (`Projectile.ElapsedSeconds`), so the period sits next to it. `Interval` is a modifier rather than a `Recipe.IntervalSeconds` field because base stats are the ones every instance has (speed, radius, lifetime, presentation) and a period only means something for a Recipe that carries `OnInterval`; authoring it in a list keeps it beside the modifiers it drives. This is not the `Timer` that [ADR-003](#adr-003--lifetime-is-recipe-data-not-a-timer-modifier) rejected: `Interval` cannot end an instance, so `LifetimeSeconds` is still the only despawn clock.
- **Consequences:** One new public type (`Interval`) and one changed signature on each of `Multiply` and `Speed`. `Projectile.IntervalSeconds` rides on the entity row and `ModifierContext.IntervalSeconds` exposes it, so a future modifier could retime a live instance. `Prewarm` reads the period from the parent Recipe's `OnSpawn` `Interval` instead of from the `Multiply` modifier. Mixed periods on one Recipe are no longer expressible; a Recipe that wants them needs a second Recipe.

## ADR-011 — Status is a hit request, not package combat

- **Date:** 2026-09-16
- **Decision:** `ApplyStatus` is an `OnHit` modifier for `HitKind.Target`. It requests an `IStatusEffect`; it does not tick burn, poison, shock, duration, or stacking. The package raises `IProjectileWorld.StatusApplied` **after** the `OnHit` list (the existing `Hit` event stays HP-only and still fires before `OnHit`). The world does not call `IStatusReceiver` or `GetComponent` for status — tests have no GameObjects. Sandbox (or the game) implements `IStatusReceiver` and applies from `StatusApplied`. v1 ships one effect, `DummyStatusEffect` (`Id` `"dummy"`). World hits never request status.
- **Why:** Games will want burn/poison/shock later without the package owning combat. That split already exists for HP (ADR-004). Status cannot ride on `Hit` because that event fires before `OnHit` modifiers run. An apply interface on the dummy plus an event keeps Edit Mode tests and the sandbox on the same contract.
- **Consequences:** Five new public types (`IStatusEffect`, `DummyStatusEffect`, `IStatusReceiver`, `ApplyStatus`, `StatusInfo`) and one new façade event. Do not add per-status classes, DoT systems, or a second world. Pierce + `ApplyStatus` on the same `OnHit` list raises `StatusApplied` for each Target hit, including a killing hit. Multiple `ApplyStatus` on one `OnHit` list each raise `StatusApplied` in list order.

## ADR-010 — Homing retargets after a Target hit; Bounce reflects Target too

- **Date:** 2026-09-15
- **Decision:** A `Target` hit clears that instance's `HomingTarget`. The hit `TargetId` is ignored by acquisition while it stays in front of travel (positive dot), so a pierce shot does not re-lock the dummy it is still overlapping. Other catalog candidates may lock on the next `OnTick`. When the ignored candidate is missing or no longer in front, the ignore drops. `Bounce(n)` consumes remaining bounce on `Target` as well as `World`, reflects around `HitInfo.Normal`, and still raises `Hit` so sandbox dummy HP drops. `SpawnRequest.HomingTarget` is only the initial lock; it does not survive a `Target` hit. There is still no public retarget API.
- **Why:** Pierce then homing should seek the next dummy once the first is hit, without a new façade. Bouncing off a dummy is the same kinematic reflect as a wall; damage is already the `Hit` event, not despawn.
- **Consequences:** Phase 6 Pierce+Homing no longer pins DummyBounce on `SpawnRequest`. `ClosestInTravelDirectionAcquisition` skips `ModifierContext.HomingIgnoredTarget`. The `Projectile` row stores `HomingHasIgnoredTarget`. `Bounce(1)` into a dummy with remaining bounce reflects instead of dying (phase 2 wall-then-dummy still dies because the wall spent the remaining bounce).

## ADR-009 — Homing acquires once, then steers

- **Date:** 2026-09-15
- **Decision:** `Homing(n)` is maximum turn rate in **degrees per second**. Each instance steers toward one `HomingTarget`. If `SpawnRequest.HomingTarget` is assigned, that target is used. Otherwise the modifier's `IHomingAcquisition` may pick one during `OnTick` until it succeeds; after that the instance keeps the lock until a `Target` hit ([ADR-010](#adr-010--homing-retargets-after-a-target-hit-bounce-reflects-target-too)). v1 ships `ClosestInTravelDirectionAcquisition`: nearest registered candidate whose offset from the instance has a positive dot with travel. Games push live poses with `IProjectileWorld.SetHomingCandidate` / `RemoveHomingCandidate`. There is no retarget API. Later player-view or random locks are new `IHomingAcquisition` implementations, not a change to `Homing` or spawn.
- **Why:** Phase 5 needs missiles that find a target themselves, and the next lock rules (aim from the player, pick at random) should not force a second façade. A strategy on the modifier matches ADR-006. `SpawnRequest` still wins as an initial lock. Simulation components cannot hold Unity objects, so the game registers ids and positions; Homing only reads those.
- **Consequences:** Three new public types (`IHomingAcquisition`, `ClosestInTravelDirectionAcquisition`, `HomingCatalog`). Poses live on the catalog, not on `ModifierContext`. `HomingTarget.Id` is `TargetId`. An assigned spawn target still needs a catalog entry to steer. If the locked candidate disappears, the instance flies straight rather than picking again until a later `Target` hit drops the empty lock's ignore rules ([ADR-010](#adr-010--homing-retargets-after-a-target-hit-bounce-reflects-target-too)).

## ADR-008 — Prewarm follows spawn chains

- **Date:** 2026-09-15
- **Decision:** `ProjectileWorld.Prewarm(recipe, count)` also prewarms the Recipes that `recipe` chains into through `Multicast` and `Multiply`, following the generations of ADR-007. Multicast extras count `n-1` per parent per generation. Multiply children count the modifier's count on `OnHit` and `OnExpire`, and on `OnInterval` the number in the air at once — spawns over one child lifetime, capped by the parent's. The walk stops at 8 levels and clamps to 512 instances per Recipe per call.
- **Why:** Prewarm exists so firing does not allocate, and a chained Recipe multiplies demand fast. Asking every caller to hand-count each child Recipe is the kind of arithmetic that silently rots when a Recipe changes; the counts are already in the modifiers. The caps matter because a chain that re-enters through both modifiers (multicast extra whose child multiplies back) has no natural bound at author time.
- **Consequences:** The numbers are an estimate, not a guarantee: the pool still creates on demand past them, and interval concurrency ignores hits cutting a parent short. Prewarming a parent now also creates the child views, so callers should stop prewarming children by hand or they double up. Pool totals in tests that snapshot `AvailableCount` before and after stay valid because they compare deltas.

## ADR-007 — Chain depth is Recipe data, counted down

- **Date:** 2026-09-15
- **Decision:** `Recipe.MulticastGenerations` and `Recipe.MultiplyGenerations` (both default 0) say how many further generations of that modifier the instances spawned from this Recipe may run. The Recipe that carries the modifier owns the depth; the budget is handed to each spawned instance and counted down, so the child Recipe's own generations do not extend a chain it did not start. The two budgets are independent — a multicast extra still multiplies normally, and the other way round. At 0, extras run `OnSpawn` with `Multicast` skipped and children run their stages with `Multiply` skipped.
- **Why:** The default has to stay "skip" or spawn explodes, but chained casts are a real authoring wish. A counted-down budget is configurable and cannot loop forever, unlike a plain allow/deny flag: a Recipe whose `Multicast` spawns itself would drain the pool and hang the Editor, because `Spawn` flushes the whole queue before it returns, and a self-multiplying interval Recipe would grow without limit while it flies.
- **Consequences:** `SpawnRequest` carries two internal `ChainBudget` values; a request that did not come from that modifier starts at `1 + Recipe.<modifier>Generations`. Multicast only runs at spawn, so its count passes through `RunSpawnStages`; multiply fires on interval, hit, and expire, so `Projectile.MultiplyGenerationsLeft` lives on the entity and `ModifierContext` exposes it per stage. This changes phase 3 behaviour: a `Multiply` child Recipe that carries its own `Multiply` no longer chains unless the parent pays for it.

## ADR-006 — Multicast aims through a pattern

- **Date:** 2026-09-15
- **Decision:** `Multicast(n, extraRecipe, pattern)` does not compute directions itself. It asks an `IMulticastPattern` for the direction of every one of the `n` instances, including the original at index 0 (the original is re-aimed through `ModifierContext.Velocity`). v1 ships one implementation, `RandomConePattern`, and uses it by default; its randomness comes from the world seed. Extras are queued with a multicast budget rather than trusting the authored sibling Recipe; at the default budget their `OnSpawn` list runs with `Multicast` skipped (ADR-007).
- **Why:** Phase 4 needs a random cone now, but the shape is meant to become configurable (fan, ring, arc). A pattern that receives index and count can own the original's slot, which a symmetric fan needs and a "spread the extras only" rule cannot express. Deciding the extras' multicast at the spawn path instead of trusting the authored sibling Recipe keeps the rule in one place even when the sibling carries `Multicast`.
- **Consequences:** Two new public types (`IMulticastPattern`, `RandomConePattern`) and one new public modifier (`Multicast`). `ModifierContext` exposes the world-seeded stream as `NextFloat` instead of a cone helper, so patterns written outside the package stay deterministic; the cone geometry lives in `RandomConePattern.Sample`. Phase 3 `Multiply` reused that cone; [ADR-015](#adr-015--multiply-places-children-through-imultiplypattern) replaces Multiply layout with `IMultiplyPattern` offsets. Adding a multicast pattern is a new `IMulticastPattern`, not a change to `Multicast` or the spawn path.

## ADR-005 — Hybrid ECS for live instances

- **Date:** 2026-09-14
- **Decision:** Live instance state lives in an isolated Entities `World` owned by `ProjectileWorld`. Components are blittable (`IComponentData`). Inactive instances use the built-in `Disabled` tag (entity recycle). Burst jobs run lifetime and kinematic integrate only. Hits stay 3D PhysX via `IHitQuery`. Presentation stays GameObject `ProjectileView`. Recipe and `IModifier` stay managed classes; the entity stores a `RecipeIndex`, not a Recipe reference. `IProjectileWorld` remains the only public façade. ECS types are internal. `ProjectileWorld` is `IDisposable` because the Entities `World` holds native allocations.
- **Why:** Instance identity, tick ownership, and pooling do not convert from `List<LiveProjectile>` later without a rewrite. Recipes, hit rules, and dummy HP do. Hybrid matches v1 counts (tens to hundreds of live instances). Unity Physics and Entities Graphics are later adapters, not phase 1b work.
- **Consequences:** Package depends on `com.unity.entities` (Burst, Collections, Mathematics come with it). Do not add Unity Physics, Entities Graphics, baking, SubScenes, `ICustomBootstrap`, or `DefaultGameObjectInjectionWorld`. Do not put the simulation `World` in the Player Loop; sandbox and tests call `Tick`. `Runtime/Ecs/` holds internal components and systems. Teaching map: [ECS.md](ECS.md).

## ADR-004 — IDamageable is a Target marker, not HP

- **Date:** 2026-09-14
- **Decision:** The runtime package defines `IDamageable` with an opaque `TargetId` only. A PhysX hit whose collider has `IDamageable` is `HitKind.Target`; all other colliders are `HitKind.World`. The world does not call damage methods or change HP. Sandbox (or the game) implements `IDamageable` and applies HP from `IProjectileWorld.Hit`.
- **Why:** Games need a package-level way to mark Target colliders without layer-mask setup. HP still does not belong in the package.
- **Consequences:** `PhysicsHitQuery` uses `GetComponentInParent<IDamageable>()`. The v1 non-goal is HP application, not the marker interface.

## ADR-003 — Lifetime is recipe data, not a Timer modifier

- **Date:** 2026-09-14
- **Decision:** `Recipe.LifetimeSeconds` is the only despawn clock. There is no `Timer(n)` modifier in v1.
- **Why:** A timer modifier and a lifetime field would be two clocks. The “destroyed after 3 seconds” scenario is phase 1 using `LifetimeSeconds = 3`.
- **Consequences:** Phase 4 is multicast only. Extra expire *behavior* uses `OnExpire` modifiers (phase 3 multiply-on-expire), not a second clock.

## ADR-002 — Child Recipes are explicit

- **Date:** 2026-09-14
- **Decision:** Spawn modifiers take an explicit child Recipe. Children do not inherit the parent’s stage lists by default. Inheritance, if any, is decided per modifier when that modifier is specified.
- **Why:** Silent inherit makes multicast/multiply unpredictable and is hard to test. Phase 1 only needs OnHit spawn of a known Recipe.
- **Consequences:** Phase 1 OnHit spawn carries a child Recipe. Multiply and multicast later must name the Recipe they spawn. Do not copy parent lists unless a future ADR says so.

## ADR-001 — Six lifetime stage lists, not Noita wrap

- **Date:** 2026-09-14
- **Decision:** A Recipe is base stats plus six ordered lists: `OnSpawn`, `OnTravel` (one-shot), `OnTick`, `OnInterval`, `OnHit`, `OnExpire`. Bounce and pierce are `OnHit` modifiers that may cancel despawn. A killing hit runs `OnHit` then `OnExpire`.
- **Why:** Stage lists match how we want to author projectiles. Noita-style wrap-the-next-spell is a different composition model and is a v1 non-goal.
- **Consequences:** Agents must not add wrap, graphs, or a seventh implicit stage. `onDeath` in older notes means `OnExpire`.
