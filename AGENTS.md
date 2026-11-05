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

**Phases 47 and 48 — Homing acquire cone and fallback; Multiply direction and lock range (done; phase 34 is next)**

- Change requests veerdonk/Projectile-System#83 and #84, game card veerdonk/UntitledTowerGame#561. Both phases ship in **0.2.12** (one branch, `phase-47-48-homing-cone-multiply-up`: a plan commit, then the code). CHANGELOG has `## [0.2.12] - Unreleased`; `package.json` is still **0.2.11**: bump it (and date the CHANGELOG) when the release is cut. No git tag. The user approved doing both phases in one session.
- Verified in the Editor on the main checkout: clean compile, Edit Mode **406/406** (13 new in `HomingAcquireConeTests`, 13 in `MultiplyDirectionLockTests`), Play Mode **56/56**.
- The cone and the fallback live in `ClosestInTravelDirectionAcquisition`, read from internal `ModifierContext.HomingAcquireHalfAngleDegrees` / `HomingFallbackTarget`. A custom `IHomingAcquisition` sees neither (ADR-052). The row keeps one ignored id plus `HomingIgnoredPassed`; `HomingIgnoredTarget` on the context is only the "still ahead" phase.
- At the default 90° the fallback can still change a pick in one edge case: a shot that curves round until the target it hit is ahead again, with another candidate also ahead, now takes the other one. Multiply children of a Target hit also carry the fallback at 90°. No existing test depended on either.
- `Multiply` `Up` turns the pattern's output about world up (`Quaternion.LookRotation(flat travel)`), so it relies on `MultiplyPatternPlane.ToWorld(up)` giving local +X = world +X and local +Y = world +Z. A custom pattern with its own basis is turned the same way.
- Unity runs field initializers on `[SerializeReference]` objects whose stored data lacks the field (checked with `EditorJsonUtility`; `SerializedHomingWithoutTheHalfAngle_Reads90`), so old assets get 90.
- The game repo's Claude hook blocks the Write/Edit tools on any `com.veerdonk.projectile-system` path, this repo included; the files here were written through Bash.
- Older notes that still apply: phase 46 notes (OnExpire burst at `ExpireInfo.Position`; area buffers shared during a flush); phase 45 notes (`MulticastPatternOptionIndex(null)`, `EditorJsonUtility` drops `extraRecipe`); `run_tests` can start before a requested compile finishes (check the test DLL time in `Library/ScriptAssemblies`); two connected Editors (`set_active_instance` to `My project@…`; the hash is in `~/.unity-mcp/unity-mcp-status-*.json`); `get_test_job` gives no assertion messages unless `include_failed_tests`; `npx sigmap` output is not committed; never move or delete a released `v*` tag; do **not** run **Projectile Lab > Rebuild**; `HomingAcquirePoint` is the only aim point; catalog `Compact()` leftover from phase 29; capsule `TryPierceExit` approximation from phase 41; `PredictPath` false return does not say lifetime vs `MaxSteps` (phase 43).

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
