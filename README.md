# Projectile System

Unity 6 LTS UPM package **`com.veerdonk.projectile-system`**. MIT licensed.

Games spawn through `IProjectileWorld`, tick once per frame, and listen to `Hit` / `Expired` / `StatusApplied`. The package does not apply HP, spend mana, or own the player, camera, or input.

## Add the package to a Unity project

Requires **Unity 6 LTS `6000.6`**. Package Manager will pull **Entities 6.6**.

**Window → Package Manager → + → Add package from git URL:**

```text
https://github.com/veerdonk/Projectile-System.git?path=/Packages/com.veerdonk.projectile-system
```

To pin a release, append `#` and the tag (for example `#v0.1.1`). Merges to `main` that introduce a new `package.json` version create tag `v{version}` and a GitHub Release (notes from `CHANGELOG.md` plus merged PRs). The repo must be public, or the other person needs access.

Alternatively, copy `Packages/com.veerdonk.projectile-system/` into the other project’s `Packages/` folder.

This repository already embeds the package. Open it in Unity 6 LTS. Import sample **Projectile Yard** (or run **Projectile Lab > Rebuild**) and open `Scenes/Yard.unity`.

## Basic use

Namespace: `Veerdonk.ProjectileSystem`.

1. `new ProjectileWorld(...)`.
2. Author a `Recipe`, or **Create > Projectile > Recipe** and `ToRecipe()`.
3. `Spawn(new SpawnRequest { Recipe, Position, Direction })`.
4. `Tick(dt)` each frame from one driver.
5. Listen to `Hit` for HP in your game. Mark targets with `IDamageable`.
6. `Dispose()` on teardown.

See `Packages/com.veerdonk.projectile-system/README.md` for a copy-paste driver, `Documentation/index.md` for the public API, and `AGENTS.md` in that folder for game-side agent rules.
