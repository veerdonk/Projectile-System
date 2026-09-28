# Agent guide

This is a Unity 6 LTS (`6000.6.0f1`) embedded UPM package plus a sandbox lab. Spec: `docs/`. Pairing: implement **one phase**, then stop for human review.

## Map

| Path | Role |
| --- | --- |
| `docs/REQUIREMENTS.md` | Glossary, goals, scenarios |
| `docs/ARCHITECTURE.md` | Stages, API, invariants |
| `docs/PHASES.md` | What to build now; stop lines |
| `docs/DECISIONS.md` | ADRs |
| `docs/SANDBOX.md` | Play Mode proofs |
| `docs/ECS.md` | Hybrid ECS map (what each piece does) |
| `docs/MCP.md` | SigMap (signatures) and Unity MCP (Editor): start, recover, how agents should call them |
| `Packages/com.veerdonk.projectile-system/` | Product runtime/editor/tests, and `Samples~/Yard` |
| `Assets/` | Host project. The imported yard under `Assets/Samples/` is gitignored |

## Pairing protocol

1. Read `docs/PHASES.md` and implement **exactly one phase**.
2. If that phase needs new public types, use Plan mode first. Match the API sketch in `docs/ARCHITECTURE.md`. Do not add types until the human agrees.
3. Match the six stage lists. Do not introduce wrap-the-next-spell, graphs, or a second world/façade.
4. Do not generate unused extension points “for later.”
5. Prefer Edit Mode tests. Play Mode only for pooling and physics queries.
6. **Stop** at the phase stop line. Do not start the next phase unless the human asks. After phase 1b or any later ECS change, the stop message must walk through the pieces using [docs/ECS.md](docs/ECS.md) and the files just added. Assume GameObject/C# fluency, not ECS fluency.
7. After the human refines, continue from that code.
8. Architectural change → add an ADR in `docs/DECISIONS.md` before code.
9. Git: work on a branch; integrate through human GitHub review. Do not push unless asked.
10. Before you stop, replace **Notes from the last phase** with anything the next session needs that is not an ADR or a phase bullet.

## Notes from the last phase

One session's leftovers, overwritten by the next — not a spec, and safe to delete. A rule the next phase must follow belongs in `docs/PHASES.md`, an architectural choice in `docs/DECISIONS.md`, a DOTS trap in `docs/ECS.md`, an Editor or MCP trap in `docs/MCP.md`.

**Phase 31 — Delivery Straight / Arc / Hitscan (built); 32–33 Beam / debug still queued**

- `Recipe.Delivery` default Straight. Arc loft is `BallisticAim.TrySolve` in `SpawnCore` **before** rent when `HomingAcquirePointSet`; unreachable returns `default` without `nextId++` or `Expired`. Aim unset keeps `SpawnRequest.Direction` and still integrates gravity. Burst gravity is `projectile.Delivery == Delivery.Arc` in `IntegrateJob`.
- Hitscan: `ProjectileSimulationSystem.ResolveHitscan` after `RunSpawnStages`, inside `Spawn`. Polyline uses `IHitQuery.TryCast` (published volumes + the phase 30 ignore slot). Homing on `OnTick` is applied with `dt = 1` so `n` is degrees of kink, not °/s. OnTick / OnInterval do not otherwise run. No view (`ProjectilePool.Rent` / `Prewarm` skip Hitscan views). 10 km cap, 32 segment cap. Pierce n+1 and Bounce reuse `Despawn`.
- Do **not** add `Delivery.Beam`, `BeamPulseSeconds`, or `Despawn` here. That is phase 32. No gizmos (phase 33).
- Package is **0.1.7**. No git tag in the phase session; the `main` workflow creates `v0.1.7`.
- Yard: Cannonball and Cannonball Gravity(2) are `Delivery.Arc`. `SampleLab.Fire` copies `lastAimPoint` and does not write `TrySolve` onto `Direction`. Barrel pitch still uses `BallisticClearance`. Hitscan preset is `Samples~/Yard/Recipes/Hitscan.asset` (guid `c31d4e5f60718293a4b5c6d7e8f90123`), listed in `Yard.unity` after Cannonball Gravity(2). Turret stays collider-less.
- Do **not** run **Projectile Lab > Rebuild** just to refresh presets after a `package.json` bump. Rebuild writes `Assets/Samples/<version>/` and copies that scene back over `Samples~/Yard/Scenes/Yard.unity`, which swaps recipe guids. The editor compiles the gitignored import under `Assets/Samples/.../0.1.0`, not `Samples~`. Copy changed sample scripts into that import (or reimport) before expecting Play Mode to match.
- `HomingAcquirePoint` is still the only aim point. Do **not** add `AimPoint`.
- Beam (phase 32): live instance, `BeamPulseSeconds`, polyline per pulse, Pierce n+1 **per pulse** does not kill the instance, `Despawn`, primitive cylinder per segment, sample hold-to-fire. Reuse `ResolveHitscan` / the same kink and `TryCast` skip rather than a second query.
- No git tag in a phase session. Sample path / Rebuild / Input System / `AssemblyVersion` 0.0.0.0 / codecoverage / Sonar notes from phase 24 still apply. Catalog `Compact()` leftover from phase 29 still applies until a later phase touches those files.

## Do not

- Build the whole system in one session
- Add Unity Physics, Entities Graphics, networking, Addressables, or Zenject “just in case” (hybrid ECS is ADR-005; do not widen the DOTS stack without an ADR)
- Put player, camera, input, or HP logic in the runtime package
- Use `FindObjectOfType` in the package or `Update` on every instance
- Edit `Library/`, `Temp/`, `Logs/`, or `UserSettings/`
- Add comments to code an experienced developer can understand easily

## SigMap

Prefer SigMap MCP (`sigmap`) over dumping whole C# files: `query_context`, `get_callee_signatures`, `get_lines`, `verify_suggestion`. Generate / refresh the index with `npx sigmap`. Details: [docs/MCP.md](docs/MCP.md). Pairing stop lines still apply.

## Unity MCP

Prefer the Editor MCP over scene YAML or guessing compile state. Setup, the manual server command, and tool workflow: [docs/MCP.md](docs/MCP.md).

## Tests

Unity Test Framework is a package dependency. Edit Mode tests live in `Packages/.../Tests/Editor` (and simulation tests may live in `Tests/Runtime` if they do not need Play Mode). Run via Unity Test Runner.
