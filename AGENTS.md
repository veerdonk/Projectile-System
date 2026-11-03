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

**Phase 46 — AreaEffect on OnExpire (done; phase 34 is next)**

- Ships in **0.2.11** together with phase 45 (user decision: the game's #506 and #517 cards land on one version). This branch is stacked on `phase-45-multicast-patterns`; merge this PR into that branch first, then PR #80 into `main`, or CI tags `v0.2.11` without it. No version bump, no git tag. Change request veerdonk/Projectile-System#79, game card veerdonk/UntitledTowerGame#517.
- Verified in the Editor on the main checkout: clean compile, Edit Mode **380/380** (twelve new in `AreaEffectOnExpireTests`), Play Mode **56/56**. A subagent review found nothing major; its minors are fixed or documented in ADR-051.
- The burst sits at `ExpireInfo.Position`: a lifetime expiry is checked before that tick's move, and a Hitscan or Beam reports its muzzle. An Arc that lands on the ground is a World hit and does not burst. If the game wants "end of the ray" for Hitscan, that is a new change request.
- Known limit, shared with the OnHit flush: a `Hit` / `AreaBurst` handler that despawns or spawns *another* instance during a flush reuses the world's area buffers. Same-instance `Despawn` during its own OnExpire is guarded (`expiringIds`).
- Older notes that still apply: phase 45 notes (`MulticastPatternOptionIndex(null)`, `EditorJsonUtility` drops `extraRecipe`); `run_tests` can start before a requested compile finishes (check the test DLL time in `Library/ScriptAssemblies`); Editor/worktree copy routine and the two connected Editors (`set_active_instance` to `My project@…`); `get_test_job` gives no assertion messages unless `include_failed_tests`; `npx sigmap` output is not committed; never move or delete a released `v*` tag; do **not** run **Projectile Lab > Rebuild**; `HomingAcquirePoint` is the only aim point; catalog `Compact()` leftover from phase 29; capsule `TryPierceExit` approximation from phase 41; `PredictPath` false return does not say lifetime vs `MaxSteps` (phase 43).

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
