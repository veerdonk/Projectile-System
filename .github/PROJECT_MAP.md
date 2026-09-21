# workspace — Architecture Wiki

_Deterministically generated from signatures + dependency graph by SigMap v8.49.2 — no LLM. Regenerate: `sigmap wiki`._

## Overview
The codebase spans **81 indexed files** across **3 top-level modules**, with ~5735 tokens of extracted signatures.
Context health: **100/100 (A)**.

## Modules
| Module | Files | Sig tokens | Key files |
|--------|-------|------------|-----------|
| `Packages` | 69 | ~4656 | `Packages/com.veerdonk.projectile-system/Runtime/ProjectileWorld.cs`, `Packages/com.veerdonk.projectile-system/Runtime/IModifier.cs`, `Packages/com.veerdonk.projectile-system/Tests/Editor/GravityTests.cs` |
| `docs` | 7 | ~904 | `docs/DECISIONS.md`, `docs/PHASES.md`, `docs/REQUIREMENTS.md` |
| `Assets` | 5 | ~175 | `Assets/Sandbox/SandboxDummyTarget.cs`, `Assets/Sandbox/SandboxLab.cs`, `Assets/Sandbox/Editor/SandboxLabSceneBuilder.cs` |

The largest module by signature volume is `Packages` (69 files, ~4656 tokens) — start there for the core logic.

## Conventions
No dominant conventions detected (repo too small or styles mixed).

## Navigating
- `sigmap ask "<question>"` — ranked, budgeted mini-context for any task
- `sigmap --impact <file>` / `--callers <symbol>` — blast radius before you change something
- `sigmap evidence "<query>"` — machine-consumable Evidence Pack (JSON) for agents/CI
- MCP: `get_architecture_overview`, `get_map`, `get_callee_signatures` for live agent access
