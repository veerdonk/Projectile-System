# MCP

Two MCP servers, different jobs. Pairing rules in [AGENTS.md](../AGENTS.md) still apply — MCP does not skip phase stop lines or authorize new public types.

| Server | Who | Namespace | For |
| --- | --- | --- | --- |
| [SigMap](https://sigmap.io/guide/mcp.html) | **Local Cursor only** | `sigmap` | Package / sandbox C# signatures, ranked files, blast radius, line ranges |
| [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (`com.coplaydev.unity-mcp`) | Local Editor sessions | `user-unityMCP` | Open Editor: scenes, GameObjects, Play Mode, console, tests, asset import |

**Cloud / background agents do not use SigMap.** Do not add it under [cursor.com/agents](https://cursor.com/agents) MCP, do not debug a missing `sigmap` namespace, and do not run `npx sigmap` as a substitute. Use Grep / Read / Glob. Unity Editor MCP is also local-only (no Editor on the cloud VM).

## SigMap (local Cursor)

Project config: `.cursor/mcp.json` (`npx -y sigmap --mcp`), `gen-context.config.json`, `.contextignore`. Index the projectile package, `Assets/` C#, and `docs/` — not `Library/`, not `com.coplaydev.unity-mcp`.

```text
npx sigmap
```

That writes `.github/copilot-instructions.md` (hot/cold signature map). Local agents should `query_context` / `get_callee_signatures` / `get_lines` instead of opening whole files. After C# edits, `npx sigmap` or the live-index notify tools. `get_method_impact` is JS/TS/Python/Java/Go/Rust only — for C# use `get_impact` on the file. Do not run `sigmap --setup` here: it starts a watcher and its default adapters would overwrite [AGENTS.md](../AGENTS.md).

If `sigmap` is missing in a **local** session: Customize → MCPs, enable the project server, reload. A missing namespace in a cloud run is expected — skip SigMap.

## Unity MCP — start and recover

Cursor client: namespace `user-unityMCP`, HTTP endpoint `http://127.0.0.1:8080/mcp` (see `~/.cursor/mcp.json`).

Need **both**: the local HTTP server on port 8080 **and** Unity Editor with the MCP bridge connected (`Window > MCP for Unity`).

If the server is down, did not start, or Cursor shows `user-unityMCP` as error / tools missing, start it manually:

```text
C:\Users\David\.local\bin\uvx.exe --from "mcpforunityserver==10.2.0" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools
```

Then:

1. Confirm Unity is open on this project (`6000.6.0f1`) and `Assets/Scenes/Sandbox.unity` when proving a phase.
2. In Unity: **Window > MCP for Unity**. Start the Unity Bridge if it shows Stopped. Use **Auto-Setup** / **Start Local HTTP Server** when you want Unity to launch the server itself.
3. In Cursor: reconnect the MCP server (`mcp_auth` on `user-unityMCP` if discovery failed). `namespaceStatus` should be `ready`.

`no_unity_session` means the HTTP server is up but the Editor is not bridged — fix step 2, then retry. Do not hand-write scene YAML to work around a down MCP.

## Use it this way

Inspect `user-unityMCP` and call `GetDynamicTools` for a tool’s schema before invoking it.

**Read with resources, mutate with tools.** Resource URIs are `mcpforunity://...` (slashes, not underscores). Payloads live under `data`.

| Goal | Use |
| --- | --- |
| Editor ready / compiling | `mcpforunity://editor/state` → `data.advice.ready_for_tools`, `data.compilation.is_compiling` |
| Connected Unity instances | `mcpforunity://instances` then `set_active_instance` if more than one |
| Project extras | `mcpforunity://custom-tools` |
| Scene hierarchy | `manage_scene` (`get_hierarchy`, page with `page_size` ~50) |
| Find objects | `find_gameobjects` then `mcpforunity://scene/gameobject/{id}` |
| Create / move / delete objects | `manage_gameobject` |
| Add/remove/set components | `manage_components` |
| Play Mode | `manage_editor` |
| Console after scripts compile | `read_console` (errors first) |
| Edit Mode tests | `run_tests` |

After creating or editing scripts, wait until compilation finishes (`ready_for_tools`) and check `read_console` before using new types.

Keep payloads small: page searches, `include_properties=false` until you need properties, no asset previews unless asked.

## Project constraints

- Lab objects and Play Mode proofs stay in `Assets/` and `Assets/Scenes/Sandbox.unity`. Runtime simulation stays in the projectile package.
- Do not leave stray MCP test objects in the sandbox. Name probes clearly and delete them when done.
- `unity_docs` / `unity_reflect` need the `docs` tool group (`manage_tools`) before trusting API names.
- Do not use MCP to edit `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.
