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

**Phase 41 — Upright capsule hit target (built in a cloud session; not compiled or tested yet; phase 34 is next)**

- Package version is **0.2.7** (ADR-045; Trello card #239, game-side #238). No git tag in the phase session; CI tags `v0.2.7` on merge. The human moves the cards.
- **The cloud session had no Unity Editor, .NET SDK, or Unity MCP.** Nothing was compiled. The first local session must compile, read the console, and run the full Edit Mode suite (305 old tests plus 17 in `Tests/Editor/CapsuleHitTargetTests.cs`) before review.
- `HitTargetCatalog.Volume` is now `Low` / `High` (axis segment), `Radius`, `Aim` (homing point), `Protection`. The sphere `Set` stores `Low == High == Aim == position`, so sphere behaviour should be unchanged; every query goes through `ClosestOnAxis`. Ray entry is `TryEnterCapsule` (side wall within the segment, else the earlier end sphere; parallel rays skip the wall).
- `NoteHit` now takes the contact point (`IgnoredContact`). `TryPierceExit` is the axis point nearest that contact plus `travel * radius`: the same approximation as the old sphere (centre + dir * radius). A steep shot through a capsule can get an exit point inside it; the ignore holds until it separates.
- `SetHitTarget` crefs in `HitInfo.cs` became `<c>` text, because the overload made the cref ambiguous.
- `npx sigmap` was not committed. It rewrites `.github/copilot-instructions.md` and `.github/context-cold.md` wholesale, and recent phases did not commit them either.
- The open Unity Editor points at the main checkout, not at Claude worktrees. To test worktree code, copy the **whole** package folder over, `refresh_unity` with `scope: all` + `mode: force`, run the tests, then `git checkout -- Packages/com.veerdonk.projectile-system` there and delete the untracked files the copy added. `get_test_job` does not return assertion messages.
- Older notes that still apply: never move or delete a released `v*` tag (OpenUPM keeps what it built); `PhysicsHitQuery` NonAlloc buffers grow when full (phase 38); the ignore-set lifetime rules of phase 37; do **not** run **Projectile Lab > Rebuild** (it overwrites `Samples~/Yard/Scenes/Yard.unity`); `HomingAcquirePoint` is still the only aim point; catalog `Compact()` leftover from phase 29; a Beam set lock (phase 40) reads the hit target's homing point like any lock.

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
