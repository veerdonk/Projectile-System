# Sandbox

The sandbox is the lab for proving a phase. It lives in the package sample `Packages/com.veerdonk.projectile-system/Samples~/Yard/` (ADR-029). Import **Projectile Yard** from Package Manager, or run **Projectile Lab > Rebuild**, which copies the yard into gitignored `Assets/Samples/`. Dummy HP, camera, and input stay out of Runtime.

## Current project

- Editor: Unity 6 LTS `6000.6.0f1`
- Play Mode scene: imported `Yard.unity` at `Assets/Samples/com.veerdonk.projectile-system/<version>/Projectile Yard/Scenes/Yard.unity`. Package Manager names that folder from the sample display name. Rebuild writes the scene there and back to `Samples~/Yard/Scenes/Yard.unity`
- Package: `Packages/com.veerdonk.projectile-system` (`Veerdonk.ProjectileSystem`). Sample scripts are `Veerdonk.ProjectileSystem.Samples`

## Sample layout (phase 21 / 21.b / 22)

- Ground plane and a yard of dummies/walls, viewed from behind and above the turret (not the old side-on lane)
- Turret: sphere body + cylinder barrel on a **Gun** pitch pivot, no PhysX collider. Horizontal presets spawn at the barrel tip along flattened forward. **Cannonball** is `Delivery.Arc` (phase 31): `Spawn` calls `BallisticAim.TrySolve` toward the aim point (speed 32, gravity 80). The sample may still `TrySolve` / `BallisticClearance` to pitch the barrel. No `highArc` HUD. If the flatter path hits another collider first (Splash on the way to Bounce), `BallisticClearance` searches the lowest extra loft that still hits the aimed target **for the barrel**; Arc Spawn stays the flatter root toward the aim point
- Mouse aim: camera ray hits colliders first (dummy tops), else the ground plane (Y = 0). Turret yaws. Horizontal presets pitch the barrel toward that point. Cannonball pitches to the solved loft (faster Speed is flatter). If the flatter path hits a different collider, the sample searches the **minimum extra pitch** that still hits the aimed cube. Pointer over the on-screen sheet does not aim or fire
- Fire: click in the world, on-screen **Fire**, or Space. One driver `Tick`s. Unreachable Arc solves do not spawn. **Laser** (phase 32): hold to keep one Beam instance, release `Despawn`. `LifetimeSeconds` still ends it while held
- Preset dropdown of `RecipeAsset`s. Play Mode edits a working `Recipe` from `ToRecipe()`; the asset on disk does not change. Switching preset or **Reset** copies again
- Recipe sheet: base stats (including **Delivery** after phase 31, **BeamPulseSeconds** after phase 32) plus the six lists with **Add** / **Remove**. Magnitudes stay editable. Add menus are `[ModifierStage]`-filtered (Pierce is OnHit, not OnSpawn). Multiply child / Multicast extra pick a sample preset (`ToRecipe()`). Child Recipes are a name, not a nested editor. Working copy only — the `RecipeAsset` does not change
- Cannonball HUD: last-fire line (`solved` / `unreachable`). No high-arc toggle
- **Reset:** dummy HP/status, turret yaw and barrel pitch, working copy, `Dispose` + new `ProjectileWorld` (clears live shots and the pool)
- Colored dummy cubes and status flash. Tiny HUD: active count, pool available, DummyTarget HP/status
- Yard still has a pierce line of dummies, a bounce wall, DummyBounce off-lane, splash dummy, muzzle dummy, Basic dummy, Strong shield, and a second wall

If the yard is missing in Edit Mode, use **Projectile Lab > Rebuild**. Agents should drive the Editor through [MCP.md](MCP.md) rather than editing the scene YAML by hand.

## How to prove a phase

Always: enter Play Mode from the imported `Yard.unity`, fire once, confirm the phase’s scenario in [REQUIREMENTS.md](REQUIREMENTS.md), then exit Play Mode.

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
| 5 | **Homing** | Click DummyBounce (off-lane) and fire — shot turns toward DummyBounce first (phase 28 aim-point lock). Without an aim point, travel-direction lock is unchanged |
| 6 | **Pierce+Homing** | Click the first dummy, fire: pierce it; instance then homes onto DummyBounce (retarget is still travel-direction) |
| 6 | **Bounce+Multiply** | Aim at +X walls; two extra instances at each bounce; parent stays live |
| 7 | **Speed on travel** | Leaves the muzzle twice as fast as Straight |
| 8 | **ApplyStatus** | DummyTarget HUD shows `dummy` and the cube flashes green |
| 12 | **Size** | Sphere looks and hits larger |
| 13 | **OnHit AreaEffect** | DummyTarget and DummySplash both lose area damage (DummyTarget also loses Recipe damage) |
| 13 | **Area-only OnHit** | Contact dummy loses area damage, not Recipe damage |
| 13 | **OnTravel AreaEffect** | DummyMuzzle is hit before the shot moves |
| 13 | **Interval aura** | Dummy beside the lane ticks each interval while the shot is in range |
| 14 / 16 / 22 | **Cannonball** | Aim at DummyTarget, fire — the shot hits and the barrel is lofted. Raise Speed on the sheet — flatter loft. Lower Speed until last fire is `unreachable` — no spawn. No high-arc toggle |
| 15 | **Cannonball Gravity(2)** | Flattened spawn; drops harder than an unsolved cannonball |
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
| 22 | **Cannonball** | Click DummyTarget (including its top) — barrel aims at the cube, not the floor behind. Fire hits. Aim at Bounce at speed 40 — lofts just over Splash. Raise Speed — flatter when the path is clear. Lower Speed until unreachable — no spawn. No high-arc toggle |
| 22.b | **Cannonball**, tweak Speed | Fast is nearly straight; slowest reachable on the level is ~45°. `TrySolve` has no `highArc` argument |
| 23 | Package Manager | Id is `com.veerdonk.projectile-system`; template Example sample is gone; keyboard-slot lab HUD is gone |
| 24 | Package Manager → Import **Projectile Yard** | `Yard.unity` Play Mode; Pierce still pierces the first two dummies. Runtime has no `SampleLab` |
| 25 | Edit Mode published volumes (package 0.1.1) | Straight shot vs a sphere with no collider raises Target `Hit`. Pierce / Bounce / AreaEffect / GroundField tests in [REQUIREMENTS.md](REQUIREMENTS.md). Yard collider dummies unchanged; no new preset |
| 26 | Edit Mode published volumes | Homing acquires from `SetHitTarget` only. ApplyStatus on a published Target. Basic/Strong published protection. OnTick AreaEffect Hits each Tick; occupancy enter once then exit. Yard collider presets unchanged |
| 27 | Edit Mode compose + Create menu | `Compose` Straight + Pierce kit pierces two dummies. Illegal stage throws. **Create > Projectile > Modifier Kit** exists. `Samples~/Yard/Recipes/PierceKit.asset` is Pierce(2) on OnHit. Yard Straight asset unchanged |
| 28 | Homing + DummyBounce click | Homing preset, click DummyBounce, fire — first lock is DummyBounce. Pierce+Homing still retargets with travel-direction after the pierce |
| 29 | Edit Mode catalog index | Re-Set of existing ids keeps `TryGet` order and Count. Remove compact-left. Yard unchanged |
| 30 | Play Mode ignore firing collider | Spawn overlapping a World wall with `IgnoreCollider` set — shot stays live. Same spawn without the field despawns. Yard turret stays collider-less |
| 31 | **Cannonball** (`Delivery.Arc`) | Aim at DummyTarget, fire — Spawn lofted the shot; barrel still shows the preview. Lower Speed until unreachable — no spawn |
| 31 | **Hitscan** | No flying mesh; DummyTarget takes a Direct hit when you fire. Pierce Hitscan through the dummy line uses n+1 |
| 32 | **Laser** | Hold Fire — red cylinder segments, DummyTarget ticks each pulse. Release — beam gone. Hold past lifetime — beam ends anyway |
| 33 | Hitscan / Laser debug overlay | Play Mode overlay draws the Hitscan polyline and Beam segments |

AreaEffect, GroundField, Recipe gravity, barrel pitch, needed-arc `BallisticAim` (Cannonball), Hitscan, and Laser are sample presets.

Open the imported `Yard.unity` before Play Mode. `Assets/Samples/` is gitignored; Import or Rebuild creates it.

If a proof needs a new yard object (second dummy, wall), add it in the **sample**, not in Runtime.

## What not to put in the sandbox / sample

- A second projectile implementation
- Copies of simulation types
- Networking, inventory, player combat
