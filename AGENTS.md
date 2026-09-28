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

**Phase 33 — authored Multiply grid (scheduled; implement this phase only)**

- `origin/main` is **0.1.9** (`6b37226`). IgnoreCollider / Delivery / Beam are already on main (0.1.6–0.1.8). The leftover debug overlay is **phase 34**. Do not implement 34 in the same session.
- `RecipeAsset.ClonePattern` is `Activator` by type. That wipes `GridMultiplyPattern` fields. Copy Spacing + the cell numbers (`ToRecipe` / kit clone the same way). Do not leave a `UnityEngine.Object` alias on the live Recipe.
- `RecipeAssetEditor.Patterns()` is TypeCache of `IMultiplyPattern` types (ModifierKit uses it too). The Pattern dropdowns must also list every `MultiplyPatternAsset` in the project; picking an asset copies onto `GridMultiplyPattern`.
- Grid places with **Spacing** (meters between cell centers). `Multiply.Spread` on a grid is cone **half-angle degrees** of the farthest numbered cell from the geometric center. Square / Circle / RandomCircle still read Spread as meters and `GetDirection` returns parent forward. `Spread(n)` still scales instance `Multiply.Spread` only — not Spacing.
- Default grid 3×3, minimum 2×2. Grow N→N+1 adds a bottom row and a right column. Shrink drops the last row and column, then compact-renumbers. Empty (no numbers) → spawn 0. `Multiply(n)` uses cells `1..n`.
- One sample asset in `Samples~/Yard` and one Multiply preset that uses it. No Play Mode grid editor. Multicast unchanged. Keep Square / Circle / RandomCircle as the default procedural patterns.
- Bump `package.json` to **0.1.10**. No git tag in the phase session. Do **not** add gizmos (phase 34).
- Do **not** run **Projectile Lab > Rebuild** just to refresh presets after a `package.json` bump. Rebuild writes `Assets/Samples/<version>/` and copies that scene back over `Samples~/Yard/Scenes/Yard.unity`, which swaps recipe guids. The editor compiles the gitignored import, not `Samples~`. Copy changed sample scripts into that import (or reimport) before expecting Play Mode to match.
- `HomingAcquirePoint` is still the only aim point. Do not add `AimPoint`.
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
