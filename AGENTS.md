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
| `Packages/com.veerdonk.projectile-system/` | Product runtime/editor/tests |
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

**Phase 23 — Cleanup**

- Package folder is `Packages/com.veerdonk.projectile-system/`, namespace `Veerdonk.ProjectileSystem` (ADR-025).
- Running tests through MCP still rewrites `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json` with different line endings. There is no content change; `git checkout --` it rather than committing it.
- Play Mode demo is `Assets/Scenes/Sandbox.unity`. **Projectile Lab > Rebuild** creates it from a new default camera/light scene (the old `SampleScene.unity` template is gone).
- Local SonarQube: `http://localhost:9000/dashboard?id=com.veerdonk.projectile-system`. Auth is `SONAR_TOKEN` in the environment; do not commit it. `sonar-project.properties` has host/sources only. C# analysis needs `dotnet-sonarscanner` (not the generic CLI). Temporarily rename `sonar-project.properties` for begin/build/end. `Directory.Build.props` sets `SonarQubeTestProject=false` on Runtime/Editor so Unity assemblies count as MAIN code. Runtime `[assembly: AssemblyVersion]` must stay `0.0.0.0` — `0.1.0.0` changes identity enough that Burst `LifetimeJob` stops applying. Package version lives in `package.json`. Editor may use `0.1.0.0`.

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
