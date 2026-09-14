# Decisions

Short ADRs. Newest first. Do not relitigate a decision in code; add a new ADR if it should change.

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
