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
| `docs/MCP.md` | Unity MCP: start, recover, how agents should call the Editor |
| `Packages/com.davidvdveerdonk.com.veerdonk.projectile-system/` | Product runtime/editor/tests |
| `Assets/` | Sandbox only |

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

**Phase 9 — combat and cast stats**

- Running tests through MCP rewrites `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json` with different line endings. There is no content change; `git checkout --` it rather than committing it.
- `ModifierContext.Bind` takes 17 positional parameters. Phases 10 and 12 add more. Passing the `Projectile` row instead would be an improvement, but as its own change, not folded into a phase.
- The sandbox HUD is at 18 fire slots and single-key selection has run through `1`–`9`, `0`, `Q`, `E`, `R`, `T`, `Y`, `U`, `I`, `O`, `P`. Phases 11–13 need a different selector.
- Sandbox dummy HP is a `float` now so it can subtract `Hit.Damage`. `Assets/Scenes/Sandbox.unity` still serializes `hp: 10` / `maxHp: 10` as integers; Unity reads them into the float fields without complaint.

## Do not

- Build the whole system in one session
- Add Unity Physics, Entities Graphics, networking, Addressables, or Zenject “just in case” (hybrid ECS is ADR-005; do not widen the DOTS stack without an ADR)
- Put player, camera, input, or HP logic in the runtime package
- Use `FindObjectOfType` in the package or `Update` on every instance
- Edit `Library/`, `Temp/`, `Logs/`, or `UserSettings/`
- Add comments to code an experienced developer can understand easily

## Unity MCP

Prefer the Editor MCP over scene YAML or guessing compile state. Setup, the manual server command, and tool workflow: [docs/MCP.md](docs/MCP.md).

## Tests

Unity Test Framework is a package dependency. Edit Mode tests live in `Packages/.../Tests/Editor` (and simulation tests may live in `Tests/Runtime` if they do not need Play Mode). Run via Unity Test Runner.

Template `*Example*` scripts in the package are placeholders; do not treat them as the architecture.
