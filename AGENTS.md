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

**Phases 49, 50 and 51 — AreaEffect own radius; Homing re-acquire; Multicast spawn offsets (done; phase 34 is next)**

- Change requests veerdonk/Projectile-System#82, #90, #91 (game cards UntitledTowerGame#509, #565, #637). All three ship in **0.2.14** on one branch, `phases-49-51-area-radius-homing-reacquire-multicast-offset` (cut from `origin/main` 5203f9d, upstream unset). CHANGELOG has `## [0.2.14] - Unreleased`; `package.json` is still **0.2.13**: bump it and date the CHANGELOG when the release is cut. No git tag. #79 was already shipped in 0.2.11 (phase 46) and is closed.
- Built by three parallel agents in worktrees (`worktree-agent-*` branches, merged with `--no-ff`), then an overseer review, an independent review per CR, fixes, and a verification pass by the same reviewers. Verified in the Editor on the main checkout: clean compile, Edit Mode **470/470**, Play Mode **56/56**.
- User decisions (2026-11-08): own radius scaled by a tracked internal `EffectRadiusScale`, not the instance/recipe ratio; offsets through an optional `IMulticastOffsetPattern`; the original moves to its lane too; one release; grid patterns lie as the shooter sees them everywhere (`MultiplyPatternPlane.MirrorX` on travel-facing planes, not on hit normals, `Up`, or exactly vertical); Arc offset-pattern extras solve to aim + offset (`SpawnRequest.LoftTarget` / `LoftTurn`).
- `GridMulticastPattern` offsets are measured from the grid's geometric center, not cell 1: a row `{1,2,3}` moves the original one spacing sideways.
- The original's `Spawned` reports the request position (raised before OnSpawn); extras report their lane. `AimBeam` replaces a Beam's lane. `PredictPath` ignores offsets.
- A Beam's spawn-assigned lock now re-acquires like other deliveries; only the `AimBeam` set lock is held. A custom acquisition's pick in neither catalog is ignored each tick.
- Left as is, documented: a spawn-assigned Beam after a Target hit can pick by aim point next pulse and does not remember it (pre-#90, ADR-056); the Recipe inspector never draws `AreaEffect.occupancy` (pre-existing); a nearly vertical shot's top row is about -Z while exactly vertical is +Z; Hitscan / Beam / plain OnTick have no own-radius test.
- `.github/context-cold.md` and `.github/copilot-instructions.md` show as modified (SigMap regeneration); they were not committed.
- Older notes that still apply: Unity runs field initializers on `[SerializeReference]` objects whose data lacks the field; `ClosestInTravelDirectionAcquisition` reads the cone and fallback from internal context (ADR-052); `run_tests` can start before a requested compile finishes (check the test DLL time in `Library/ScriptAssemblies`); two connected Editors (`set_active_instance` to `My project@…`); `get_test_job` gives no assertion messages unless `include_failed_tests`; `npx sigmap` output is not committed; never move or delete a released `v*` tag; do **not** run **Projectile Lab > Rebuild**; `HomingAcquirePoint` is the only aim point; catalog `Compact()` leftover from phase 29; capsule `TryPierceExit` approximation from phase 41; `PredictPath` false return does not say lifetime vs `MaxSteps` (phase 43).

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
