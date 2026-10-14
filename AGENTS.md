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

**Phase 36 — hit query on concave mesh and terrain colliders (implemented; do not start phase 34)**

- Package version is **0.2.1** (phase 36). **0.2.2** fixes the review follow-ups: a shorter overlap-pick ray, a trigger- and layer-filtered skin check, and a hold on the ignore after a concave pierce (`concaveExitByInstance`) until the shot passes the exit. No git tag in the phase session. Trello card #42 (jN71GH1X) is Done.
- All changes are in `Runtime/PhysicsHitQuery.cs`. `SupportsClosestPoint` gates every `ClosestPoint` call. Hits now store a `PierceContact` (collider, point, incoming), and the exit is computed in `TryPierceExit`. `IHitQuery` and `ScriptedHitQuery` did not change, because the exit was already read only through `TryPierceExit`.
- New tests are in `Tests/Runtime/ConcaveColliderPlayModeTests.cs` (7 for 0.2.1, plus a thick-target pierce test for 0.2.2). They fail on 0.2.0 and pass on 0.2.1. The BoxCollider pierce-exit negative case is the existing `PhysicsHitPlayModeTests.Pierce1_Multiply_SpawnsChildPastDummyAlongTravel`.
- The open Unity Editor points at the main checkout, not at Claude worktrees. To test worktree code, copy the files over, run the tests, and restore them.

**Also from phase 35 (ground field and area burst events):**

- That phase shipped as **0.2.0**. No git tag in the phase session. Phase 34 (Play Mode debug overlay) is still next; phase 35 was inserted for Trello card i9g6LSBf. Move the card list only when the human says so.
- New files: `Runtime/GroundFieldEvents.cs`, `Tests/Editor/FieldAreaEventTests.cs`. New `.meta` files are made by Unity on import; check they are committed.
- Events live on `IProjectileWorld` (ADR-040). `GroundFieldSet` takes optional planted/pulsed/ended callbacks; `SimulationBridge.AreaBurst` carries bursts. Pulsed comes after that pulse's Area Hits; AreaBurst comes before its Area Hits; occupancy burst only on first overlap.
- Sample path / Rebuild / Input System / `AssemblyVersion` 0.0.0.0 / codecoverage / Sonar notes from phase 24 still apply. Do **not** run **Projectile Lab > Rebuild** to refresh presets (it overwrites `Samples~/Yard/Scenes/Yard.unity`). Catalog `Compact()` leftover from phase 29 still applies.

**Also from phase 33b (shot lifecycle events, merged from main):**

- That phase shipped as 0.1.12. No git tag in the phase session. Phase 34 is the Play Mode debug overlay. Do not add gizmos.
- `Spawned` is raised in `SpawnCore` before `RunSpawnStages`, so `Direction` is the Arc-solved, pre-OnSpawn direction. Assigned `HomingLocked` follows it there. Acquired locks queue on `ModifierContext.AcquiredHomingLocks` and flush in `RunStage`, `RunIntervals`, and `KinkHitscan`.
- `ExpireReason` is threaded through `Despawn(..., reason)`. A Hitscan ray that ends on a hit is `Hit`. `HitOutcome` compares Pierce / Bounce charges before and after the OnHit stage.
- A Beam re-lock after a silent `AimBeam` redraw can raise `HomingLocked` again (Beam is outside the card's compositions). Ground field and area-effect events are a separate card.
- Authored Multiply grid (phase 33): `Patterns()` stays the procedural types; `PatternOptions()` appends every `MultiplyPatternAsset`. Do **not** run **Projectile Lab > Rebuild** to refresh the grid preset: Rebuild writes `Assets/Samples/<version>/` and copies the scene back over `Samples~/Yard/Scenes/Yard.unity`, which swaps recipe guids. Copy changed sample scripts into the gitignored import (or reimport) before expecting Play Mode to match.
- `HomingAcquirePoint` is still the only aim point. Do not add `AimPoint`.
- OnHit Multiply keeps the hit normal unless Pierce still has charges on that Target or Basic hit. Then children spawn past the body along the travel direction from the start of the hit (list order does not matter). Spent charges, a World hit Pierce does not absorb, and no Pierce modifier keep the normal.
- Sample path / Rebuild / Input System / `AssemblyVersion` 0.0.0.0 / codecoverage / Sonar notes from phase 24 still apply. Catalog `Compact()` leftover from phase 29 still applies until a later phase touches those files.

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
