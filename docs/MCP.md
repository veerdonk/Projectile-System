# Unity MCP

[MCP for Unity](https://github.com/CoplayDev/unity-mcp) (`com.coplaydev.unity-mcp`) is the preferred way for agents to talk to the open Editor: scenes, GameObjects, Play Mode, console, tests, and asset import. Pairing rules in [AGENTS.md](../AGENTS.md) still apply — MCP does not skip phase stop lines or authorize new public types.

Cursor client: namespace `user-unityMCP`, HTTP endpoint `http://127.0.0.1:8080/mcp` (see `~/.cursor/mcp.json`).

## Start and recover

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
