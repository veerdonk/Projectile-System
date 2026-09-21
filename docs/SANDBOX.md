# Sandbox

The sandbox is the lab for proving a phase. It is **not** the package. Code that needs a camera, input, or dummy HP stays under `Assets/`.

## Current project

- Editor: Unity 6 LTS `6000.6.0f1`
- Default Play Mode scene: `Assets/Scenes/Sandbox.unity` (phase 21 sample). `SampleScene.unity` is unused lighting leftover used only as the rebuild template
- Package: `Packages/com.davidvdveerdonk.com.veerdonk.projectile-system`

## Sample layout (phase 21 / 21.b)

- Ground plane and a yard of dummies/walls, viewed from behind and above the turret (not the old side-on lane)
- Turret: sphere body + cylinder barrel, no PhysX collider. Shots spawn at the barrel tip along flattened forward
- Mouse aim: camera ray vs the ground plane (Y = 0); turret yaws only. Pointer over the on-screen sheet does not aim or fire
- Fire: click in the world, on-screen **Fire**, or Space. One driver `Tick`s
- Preset dropdown of `RecipeAsset`s. Play Mode edits a working `Recipe` from `ToRecipe()`; the asset on disk does not change. Switching preset or **Reset** copies again
- Recipe sheet: base stats plus the six lists with **Add** / **Remove**. Magnitudes stay editable. Add menus are `[ModifierStage]`-filtered (Pierce is OnHit, not OnSpawn). Multiply child / Multicast extra pick a sample preset (`ToRecipe()`). Child Recipes are a name, not a nested editor. Working copy only — the `RecipeAsset` does not change
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
| 13 | **OnHit AreaEffect** | DummyTarget and DummySplash both lose area damage (DummyTarget also loses Recipe damage) |
| 13 | **Area-only OnHit** | Contact dummy loses area damage, not Recipe damage |
| 13 | **OnTravel AreaEffect** | DummyMuzzle is hit before the shot moves |
| 13 | **Interval aura** | Dummy beside the lane ticks each interval while the shot is in range |
| 14 | **Cannonball** | Visible drop from the muzzle; lands short of DummyTarget at Straight speed |
| 15 | **Cannonball Gravity(2)** | Drops harder than Cannonball |
| 19 | **OnHit GroundField** | Flat disk at DummyTarget; DummyTarget and splash dummy tick field damage each interval |
| 19 | **OnExpire GroundField** | Lifetime-only shot; disk appears where it died, not at DummyTarget |
| 19 | **GroundField status** | Dummy standing in the disk shows `dummy` each pulse |
| 20 | Create > Projectile > Recipe | Inspector shows six lists; OnHit Add offers Pierce; OnSpawn Add does not |
| 21 | Sample scene, Pierce preset | Mouse-aim the turret, fire; first two dummies live through |
| 21 | **Pierce**, tweak count to 1 on the sheet | Dies on the second dummy; asset still 2 after Play Mode |
| 21 | **Reset** | HP/status restored, in-flight shots gone, Pierce on the sheet is 2 again |
| 21 | Click the dropdown | Aim does not fire |
| 21.b | Straight + Add Pierce and Homing | First dummy pierced; shot can steer; Straight asset unchanged after Play Mode |
| 21.b | OnSpawn Add | Does not list Pierce |
| 21.b | Reset after compose | Sheet matches Straight again |
| 22 | **Ballistic** preset | Barrel pitches; shot hits DummyTarget. Raise Speed on the sheet — flatter loft. Lower Speed until unreachable — no spawn. No high-arc toggle |
| 22.b | **Ballistic**, tweak Speed | Fast is nearly straight; slowest reachable on the level is ~45°. `TrySolve` has no `highArc` argument |
| 23 | Package Manager | Id is `com.veerdonk.projectile-system`; template Example sample is gone; keyboard-slot lab HUD is gone |

AreaEffect, GroundField, and Recipe gravity are sample presets. Barrel pitch / `BallisticAim` are phase 22; dropping `highArc` is phase 22.b (ADR-028).

Open `Assets/Scenes/Sandbox.unity` before Play Mode.

If a proof needs a new sandbox object (second dummy, wall), add it in the sandbox, not in the package.

## What not to put in the sandbox

- A second projectile implementation
- Copies of simulation types
- Networking, inventory, player combat
