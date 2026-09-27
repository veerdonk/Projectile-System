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

**Phase 30 — ignore firing collider (scheduled, not built); 31–33 Delivery / Beam / debug still queued**

- Implement **exactly one** of 30, then 31, then 32, then 33. This session only inserted the CR as phase 30 and shifted Delivery.
- Phase 30: public `SpawnRequest.IgnoreCollider`. Seed the existing Direct one-collider skip at rent, **before** `OnSpawn`. Not a hit. Drop when clear (`RememberOrClearIgnore`). Children via `CopyIgnore`. Not `IProjectileProtection`. Direct only. Play Mode PhysX tests. Yard turret stays collider-less. Package **0.1.6**.
- Then Delivery (31, 0.1.7), Beam (32, 0.1.8), sample debug (33). Hitscan/Beam must honor the skip.
- `HomingAcquirePoint` is the Arc loft target, Hitscan/Beam aim, and Homing first lock. Do **not** add `AimPoint`.
- Gravity Burst is Arc-only. Existing Gravity Edit Mode tests and Cannonball **must** set `Delivery.Arc` or they fly straight.
- Hitscan Homing `n` is degrees of kink, not °/s. Pierce is still n+1 (`Pierce(2)` dies on the third published volume).
- Hitscan / Beam must test **published volumes** (ADR-030), not only colliders. No gizmos in 31 or 32 (that is phase 33, sample-side).
- Beam: `LifetimeSeconds` still caps while held. Pierce n+1 is **per pulse**; the instance stays until lifetime or `Despawn`. Polyline kinks toward targets in one pulse; max bend is Homing degrees. World clips a pulse. Runtime primitive cylinders; sample red via `PresentationId`. Hold-to-fire is sample-only (press Spawn, release Despawn). Re-aim while held is Later.
- Arc Spawn is `BallisticAim.TrySolve` from the aim point. `BallisticClearance` extra loft stays sample barrel visual.
- No git tag in a phase session; `.github/workflows/release-package.yml` on `main` tags `v{version}`.
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
