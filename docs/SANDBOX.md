# Sandbox

The sandbox is the lab for proving a phase. It is **not** the package. Code that needs a camera, input, or dummy HP stays under `Assets/`.

## Current project

- Editor: Unity 6 LTS `6000.6.0f1`
- Default Play Mode scene: `Assets/Scenes/Sandbox.unity` (phase 21 sample). `SampleScene.unity` is unused lighting leftover used only as the rebuild template
- Package: `Packages/com.davidvdveerdonk.com.veerdonk.projectile-system`

## Sample layout (phase 21)

- Ground plane and a yard of dummies/walls, viewed from behind and above the turret (not the old side-on lane)
- Turret: sphere body + cylinder barrel, no PhysX collider. Shots spawn at the barrel tip along flattened forward
- Mouse aim: camera ray vs the ground plane (Y = 0); turret yaws only. Pointer over the on-screen sheet does not aim or fire
- Fire: click in the world, on-screen **Fire**, or Space. One driver `Tick`s
- Preset dropdown of `RecipeAsset`s. Play Mode edits a working `Recipe` from `ToRecipe()`; the asset on disk does not change. Switching preset or **Reset** copies again
- Recipe sheet: base stats plus the six lists (magnitudes editable; no add/remove). Child Recipes are a name
- **Reset:** dummy HP/status, turret yaw, working copy, `Dispose` + new `ProjectileWorld` (clears live shots and the pool)
- Colored dummy cubes and status flash. Tiny HUD: active count, pool available, DummyTarget HP/status
- Yard still has a pierce line of dummies, a bounce wall, DummyBounce off-lane, splash dummy, muzzle dummy, Basic dummy, Strong shield, and a second wall

If the yard is missing in Edit Mode, use **Projectile Lab > Rebuild**. Agents should drive the Editor through [MCP.md](MCP.md) rather than editing the scene YAML by hand.

## How to prove a phase

Always: enter Play Mode from `Assets/Scenes/Sandbox.unity`, fire once, confirm the phase’s scenario in [REQUIREMENTS.md](REQUIREMENTS.md), then exit Play Mode.

| Phase | What to fire | What you should see |
| --- | --- | --- |
| 0 / 21 | Play Mode, look at the yard | Camera behind/above the turret; dropdown, sheet, Fire, Reset; dummy line and wall present |
| 1 | **Straight** | Hits DummyTarget, dummy HP drops, pool count returns |
| 1 / 3 | **Multiply on hit (Square)** | Parent dies on the dummy; children leave the hit on Square pips (Spread 2 m) |
| 1b | Same presets | Same proofs; instances are ECS entities under the same HUD |
| 2 | **Pierce** | Three +Z dummies: first two live-through, third kills the instance |
| 2 | **Bounce** | Aim at the side wall, reflects, then dies on DummyBounce |
| 3 | **Interval multiply** | Extra instances every 0.5s along travel |
| 4 | **Multicast** | Three instances leave the muzzle immediately |
| 5 | **Homing** | Aim ~25° off the dummy line; strong turn hits DummyTarget |
| 6 | **Pierce+Homing** | First dummy is pierced; instance then homes onto DummyBounce |
| 6 | **Bounce+Multiply** | Aim at +X walls; two extra instances at each bounce; parent stays live |
| 7 | **Speed on travel** | Leaves the muzzle twice as fast as Straight |
| 8 | **ApplyStatus** | DummyTarget HUD shows `dummy` and the cube flashes green |
| 12 | **Size** | Sphere looks and hits larger |
| 20 | Create > Projectile > Recipe | Inspector shows six lists; OnHit Add offers Pierce; OnSpawn Add does not |
| 21 | **Pierce**, tweak count to 1 on the sheet | Dies on the second dummy; asset still 2 after Play Mode |
| 21 | **Reset** | HP/status restored, in-flight shots gone, Pierce on the sheet is 2 again |
| 21 | Click the dropdown | Aim does not fire |
| 23 | Package Manager | Id is `com.veerdonk.projectile-system`; template Example sample is gone |

AreaEffect, GroundField, gravity, and ballistic aim are in the package. They are **not** sample presets yet (ballistic barrel pitch is phase 22).

Open `Assets/Scenes/Sandbox.unity` before Play Mode.

If a proof needs a new sandbox object (second dummy, wall), add it in the sandbox, not in the package.

## What not to put in the sandbox

- A second projectile implementation
- Copies of simulation types
- Networking, inventory, player combat
