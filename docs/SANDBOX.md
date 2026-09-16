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
- Three dummies on the +Z lane for Pierce(2); a bounce dummy off-lane; a side wall (`World`) for Bounce(1); a second wall at −X for Bounce(2)+Multiply(2)
- Fire control (on-screen **Fire**, Space, or the SandboxLab inspector **Fire** context menu). Modifier Recipes have **- / +** to change `n` for the next shot (Pierce, Bounce, Multiply, Multicast, Homing °/s, Speed / Damage / Critical / Mana / Spread / EffectRadius / Size scale)
- On-screen readout: last spawn request, active instances, pool available count, dummy HP, last status id on each dummy, selected Recipe damage / crit / mana / spread / radius / effect radius

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
| 2 | Bounce(1) (key **5**) | Fires toward the side wall `(1,0,1)`, reflects, then dies on DummyBounce (remaining bounce is 0). A Bounce shot that hits a dummy first reflects and still drops dummy HP |
| 3 | Multiply on interval (key **6**) | Extra instances every 0.5s, roughly along travel |
| 3 | Multiply on expire (key **7**) | Child when the parent dies at 1s (before the dummy) |
| 4 | Multicast(3) (key **8**) | Three instances leave the spawn point immediately in a small random cone |
| 5 | Homing(100) (key **9**) vs Homing(10) (key **0**) | Fire 25° off +Z; closest dummy in front is DummyTarget. Strong curves in and hits; weak misses |
| 6 | Pierce(1)+Homing(300) (key **Q**) | First dummy on the lane is pierced; lock drops; instance then homes onto DummyBounce (closest remaining candidate in front). 300°/s is enough to leave DummyTarget instead of looping back into it |
| 6 | Bounce(2)+Multiply(2) (key **E**) | Fires +X; two World walls; two extra instances at each bounce; parent stays live |
| 7 | Speed(2) on travel | Leaves the muzzle twice as fast as the straight Recipe |
| 7 | Speed(0.5) on interval | Slows by half every 0.5s |
| 7 | Bounce(1)+Speed(2) | Reflects off the wall and continues faster |
| 8 | ApplyStatus dummy | Dummy HUD shows `dummy` on a Target hit; wall hits do not |
| 9 | Straight Recipe with Damage 5 | Dummy loses 5 HP; HUD shows damage, mana, spread, radius |
| 10 | Damage(2) on travel | Dummy loses twice the Recipe damage |
| 10 | Pierce + OnHit Damage(2) | First dummy takes base damage; second takes double |
| 11 | Size(2) on travel | Sphere looks and hits larger |
| 11 | Spread(2) on travel | Leaves the muzzle farther off-axis than the phase 9 spread Recipe |

Open `Assets/Scenes/Sandbox.unity` before Play Mode. If the lane objects are missing in Edit Mode, use **Projectile Lab > Rebuild Sandbox Scene**. Agents should drive the Editor through [MCP.md](MCP.md) (`manage_editor` for Play Mode, `manage_gameobject` for lab objects) rather than editing the scene YAML by hand.

If a proof needs a new sandbox object (second dummy, wall), add it in the sandbox, not in the package.

## What not to put in the sandbox

- A second projectile implementation
- Copies of simulation types
- Networking, inventory, player combat
