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

**Phase 43 — Predict a Straight or Arc path (done; phase 34 is next)**

- Package version is **0.2.9** (ADR-048; WizardGame Trello card #245, blocks #244). No git tag in the phase session; CI tags `v0.2.9` on merge. The human moves the card.
- Verified in the Editor on the main checkout: clean compile, Edit Mode **348/348** (337 old + 11 in `Tests/Editor/PathPredictionTests.cs`), Play Mode **56/56** (+2 in `Tests/Runtime/PathPredictionPlayModeTests.cs`).
- Choices made against the card: `Recipe.MaxRange` does not end a prediction (live Straight / Arc ignore it, ADR-043); `PathHit.Time` is the elapsed time of the hitting step, not a sub-step fraction; Hitscan / Beam return false with an empty list. Only the shipped `Speed` / `Gravity` / `Size` run, by type, so no modifier code is called.
- `PredictPath` lives in `ProjectileWorld` (managed loop, no entity). Ignore state uses `ProjectileId(-1)`, armed like `SpawnCore` and released in `finally` (`ClearIgnore`, `ReleaseIgnoreSet`, catalog `Release`). `SpawnCore` now shares `RequireRecipe` / `TryResolveDirection` with it.
- Game side still owns: #244's preview line and marker, raising the manifest to 0.2.9, and updating fakes of `IProjectileWorld` (new member).
- Older notes that still apply: Editor/worktree copy routine and the two connected Editors (`set_active_instance` to `My project@…`); `get_test_job` gives no assertion messages; `npx sigmap` output is not committed; never move or delete a released `v*` tag; do **not** run **Projectile Lab > Rebuild**; `HomingAcquirePoint` is the only aim point; catalog `Compact()` leftover from phase 29; capsule `TryPierceExit` approximation from phase 41.

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
