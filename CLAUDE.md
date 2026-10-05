@AGENTS.md

# Claude Code notes

Ported from `.cursor/rules/`. `AGENTS.md` is the source of truth; the points below only add what it omits.

- Pairing: `docs/REQUIREMENTS.md`, `ARCHITECTURE.md`, `PHASES.md`, `DECISIONS.md`, `ECS.md`. Plan first when adding public types; match `IProjectileWorld`, no second façade. Runtime package: spawn, simulate, pool, hits, events. Sandbox: camera, input, dummy HP.
- Hybrid ECS per ADR-005: no Unity Physics or Entities Graphics without an ADR.
- Path-scoped rules live in `.claude/rules/` (`unity-csharp.md` for `*.cs`, `docs-adr.md` for `docs/`).
- SigMap (`sigmap` MCP, see `.mcp.json`): `query_context` → `get_callee_signatures`/`search_signatures` → `get_lines`/`explain_file`. After edits call `sigmap_notify_file_created` (or `npx sigmap`); `verify_suggestion` before trusting new calls. For the task loop use the `sigmap-task` skill.
- Unity MCP (`unityMCP` in `.mcp.json`, tools appear as `mcp__unityMCP__*`): follow `docs/MCP.md`. Never hand-write `.unity` YAML when it is available. Read `mcpforunity://` resources, mutate with tools, check the tool schema first, `read_console` after compiles, delete probe objects when done. If tools are missing, start the HTTP server per `docs/MCP.md` and start the bridge in **Window > MCP for Unity**.
- `docs/MCP.md` mentions Cursor and `user-unityMCP`; in Claude Code the server is `unityMCP`.
