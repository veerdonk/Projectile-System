# Sandbox

The sandbox is the lab for proving a phase. It is **not** the package. Code that needs a camera, input, or dummy HP stays under `Assets/`.

## Current project

- Editor: Unity 6 LTS `6000.6.0f1`
- Default scene today: `Assets/Scenes/Sandbox.unity` (phase 0 lab; `SampleScene.unity` is unused lighting leftover)
- Package: `Packages/com.davidvdveerdonk.com.veerdonk.projectile-system`

## Lab layout (phase 0 builds this)

- Ground plane and a straight firing lane
- Camera looking down the lane
- Spawn point with a visible gizmo
- At least one dummy target (cube) with HP updated from `IProjectileWorld.Hit`
- Three dummies on the +Z lane for Pierce(2); a bounce dummy off-lane; a side wall (`World`) for Bounce(1)
- Fire control (on-screen **Fire**, Space, or the SandboxLab inspector **Fire** context menu)
- On-screen readout: last spawn request, active instances, pool available count, dummy HP

## How to prove a phase

Always: enter Play Mode from the sandbox scene, fire once, confirm the phase’s scenario in [REQUIREMENTS.md](REQUIREMENTS.md), then exit Play Mode.

| Phase | What to fire | What you should see |
| --- | --- | --- |
| 0 | Space or on-screen **Fire** | Camera looks down the lane; HUD shows a spawn request; dummy and wall are present; nothing flies |
| 1 | Straight Recipe | Hits dummy, dummy HP drops, pool count returns |
| 1 | Lifetime-only Recipe | Dies at 3s, no child |
| 1 / 3 | Multiply on hit (key **3**) | Parent dies on the dummy; **three** children leave the hit point along the hit normal with a small random cone |
| 1b | Same three Recipes as phase 1 | Same proofs; instances are ECS entities under the same HUD |
| 2 | Pierce(2) (key **4**) | Three +Z dummies: first two live-through, third kills the instance |
| 2 | Bounce(1) (key **5**) | Fires toward the side wall `(1,0,1)`, reflects, then dies on DummyBounce |
| 3 | Multiply on interval (key **6**) | Extra instances every 0.5s, roughly along travel |
| 3 | Multiply on expire (key **7**) | Child when the parent dies at 1s (before the dummy) |
| 4 | Multicast(3) | Three instances immediately |
| 5 | Homing(100) vs Homing(10) | Strong hits, weak misses |

Open `Assets/Scenes/Sandbox.unity` before Play Mode. If the lane objects are missing in Edit Mode, use **Projectile Lab > Rebuild Sandbox Scene**. Agents should drive the Editor through [MCP.md](MCP.md) (`manage_editor` for Play Mode, `manage_gameobject` for lab objects) rather than editing the scene YAML by hand.

If a proof needs a new sandbox object (second dummy, wall), add it in the sandbox, not in the package.

## What not to put in the sandbox

- A second projectile implementation
- Copies of simulation types
- Networking, inventory, player combat
