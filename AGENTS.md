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
| `docs/RELEASING.md` | Signed release workflow and the owner's one-time setup |
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

**Phase 56 — Rectangle and sector ground fields (done; phase 34 is next)**

- Change request veerdonk/Projectile-System#99 (game card UntitledTowerGame #572, REQ-057). Ships in **0.2.20** (`package.json` bumped, CHANGELOG dated 2026-11-15; update to the merge date if later). Branch `cr-99-shaped-ground-fields` from `origin/main` 37561bd (#101 merged). ADR-062.
- Shape: `GroundDisk` (internal) carries the shape, so `OverlapDisk`, `AppendDisk`, and `ScriptedHitQuery` needed no change; its `Radius` is the reach from the anchor for a shape. The direction comes from the new internal `ModifierContext.TravelDirection` (velocity, else the `Projectile.BeamDirection` set at spawn). The ground is sampled at the shape's middle (`GroundFieldSize.SampleCircle`).
- Tests via Unity MCP with the Editor open: Edit Mode **537/537** (17 new in `GroundFieldShapeTests`), Play Mode **60/60** (3 new in `GroundFieldShapePlayModeTests`). Two Editors are connected (`My project` and `UntitledTowerGame`); `set_active_instance` to `My project@…` first, and again after each domain reload.
- `Samples~/Yard/SampleRecipeSheet.cs` gained a shape button; `Samples~` is not compiled in this project, so it was not compile-checked here.
- Older notes that still apply: Unity runs field initializers on `[SerializeReference]` objects whose data lacks the field; `ClosestInTravelDirectionAcquisition` reads the cone and fallback from internal context (ADR-052); `run_tests` can start before a requested compile finishes (check the test DLL time in `Library/ScriptAssemblies`); `get_test_job` gives no assertion messages unless `include_failed_tests`; `npx sigmap` output is not committed; never move or delete a released `v*` tag; do **not** run **Projectile Lab > Rebuild**; `HomingAcquirePoint` is the only aim point; catalog `Compact()` leftover from phase 29; capsule `TryPierceExit` approximation from phase 41; `PredictPath` false return does not say lifetime vs `MaxSteps` (phase 43).

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
