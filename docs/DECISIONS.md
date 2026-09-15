# Decisions

Short ADRs. Newest first. Do not relitigate a decision in code; add a new ADR if it should change.

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
- **Consequences:** Two new public types (`IMulticastPattern`, `RandomConePattern`) and one new public modifier (`Multicast`). `ModifierContext` exposes the world-seeded stream as `NextFloat` instead of a cone helper, so patterns written outside the package stay deterministic; the cone geometry lives in `RandomConePattern.Sample`, which `Multiply` reuses for its own spread. Adding a pattern is a new `IMulticastPattern`, not a change to `Multicast` or the spawn path.

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
