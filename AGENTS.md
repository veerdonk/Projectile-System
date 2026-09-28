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

**Phase 32 — Beam (built); 33 debug overlay still queued**

- First Beam pulse is inside `Spawn` after `AnchorBeam` (post OnSpawn / OnTravel). Later pulses are in `ProjectileSimulationSystem.OnUpdate` after move, when `BeamPulseElapsed` (lifetime job) reaches `Recipe.BeamPulseSeconds`. `<= 0` pulses every Tick as well as the spawn pulse. A held sample beam that lifetime-expires keeps its id until release so it does not immediately spawn another.
- Polyline is `ResolvePolyline` shared with Hitscan (`keepInstance`). Beam contact uses `Despawn(..., expireInstance: false)`: Strong ends the pulse with no Hit and no pool; Pierce/Bounce `CancelDespawn` continues the polyline; a killing contact raises Hit / OnHit and does **not** run OnExpire. Without Pierce the pulse stops on the first Target. `PrepareBeamPulse` resets pierce/bounce from the Recipe and restores `BeamOrigin` / `BeamDirection`. The next pulse can hit that same Target again: `PhysicsHitQuery` drops the last collider skip once the origin no longer overlaps it. `FinishBeamPulse` draws cylinders then restores pose again. `SetBeamSegments` keeps the last polyline up between pulses.
- OnTick Homing is skipped for Beam during the tick. A homing pulse steps ~0.5 m and applies Homing with `dt = step / speed` (degrees per second along the path, not one straight kink). The first step leaves along the muzzle before the turn. The lock is kept across redraws while that target stays in front of the muzzle (~60°); a Target hit drops it for the rest of that polyline, and `AimBeam` drops it the same way so the mesh matches the damage pulse. Hitscan kink stays `dt = 1`. `IntegrateJob` and `TryMoveOrHit` return immediately for Beam. Lifetime still ticks.
- `AimBeam(id, position, direction)` updates a live Beam's anchor and redraws without raising `Hit`. The yard calls it every frame while held, from the current muzzle pose (the turret already follows the mouse). Runtime still does not read the mouse. Damage stays on `BeamPulseSeconds`.
- `IProjectileWorld.Despawn` → `DespawnInstance` (OnExpire, pool, `Expired`). Unknown / disabled ids are a no-op. Not Beam-only.
- Views: `PrimitiveViewFactory` makes an empty Beam root. `ProjectileView.SetBeamSegments` builds one cylinder per segment (diameter `2 * Radius`) in world space and copies the root material. Each segment is a mesh copy; the length texture coordinate runs from 0 at the first point to 1 at the last, so a material does not restart on every cylinder. Root scale does not change that diameter. Those cylinders have no colliders. Direct queries skip any collider under a `ProjectileView`, so the beam is not a World hit and Bounce cannot reflect off it. After a bounce or pierce, the next segment holds that skip so the beam does not immediately strike the same surface again. Sample `PresentationId` `laser` is a red cylinder template. The yard aims a held beam along the barrel (`muzzle.Direction`), not the flattened ground aim. Hitscan still has no view.
- Package is **0.1.8**. No git tag in the phase session; the `main` workflow creates `v0.1.8`.
- Yard Laser preset: hold mouse / Space / Fire; the held beam follows the current muzzle aim (`AimBeam` each frame). Release `Despawn`. Do **not** add gizmos (phase 33).
- Do **not** run **Projectile Lab > Rebuild** just to refresh presets after a `package.json` bump. Rebuild writes `Assets/Samples/<version>/` and copies that scene back over `Samples~/Yard/Scenes/Yard.unity`, which swaps recipe guids. The editor compiles the gitignored import, not `Samples~`. Copy changed sample scripts into that import (or reimport) before expecting Play Mode to match.
- Modifier Kit Multiply Pattern is package **0.1.9** on a branch from `main`. `ModifierKitEditor` uses `RecipeAssetEditor.Patterns()` (no empty choice). A null or unknown value shows Square Pattern and is not written until the popup index changes.
- `HomingAcquirePoint` is still the only aim point. Do **not** add `AimPoint`.
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
