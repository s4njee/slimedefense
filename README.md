# SlimeDefense

> **Read these two files first — before changing anything in this repo.**
>
> 1. **[`notes.md`](notes.md)** — the running log of what has actually been
>    built, decided, and verified. Newest entries are at the top, each stamped
>    with a date and a phase. Read it to find out where the project currently
>    stands and why things are the way they are. When you finish a piece of
>    work, add a note at the top of its Notes section.
> 2. **[`tower-defense-build-order.md`](tower-defense-build-order.md)** — the
>    phased roadmap the whole project follows. It defines what each phase
>    covers, in what order, and which Unity/C# concepts it exists to teach.
>    Read it to find out what comes next.
>
> Together they answer "where are we?" and "what's next?". The per-phase
> step-by-step instructions live in [`docs/`](docs/).

A 3D tower defense game built in Unity with C#. Slimes walk a fixed route from
a spawn point to a goal, and the player spends money placing towers along the
way to stop them before they arrive.

The project is built one phase at a time, following the roadmap above. The
guiding rule is that the game stays playable at the end of every phase — each
phase adds one working system on top of something you can already press Play
on.

## Requirements

- **Unity 6000.5.5f1** (Unity 6 LTS) — the exact version is pinned in
  `SlimeDefense/ProjectSettings/ProjectVersion.txt`. Install it through Unity
  Hub.
- Build support modules are added as they are needed. WebGL and Android are
  the eventual targets (Phase 10).

## Running it

**[Play the current build in your browser](https://play.unity.com/en/games/71a052ff-c9ab-407e-a74e-bf77544a5248/slime-defense-test)** — no Unity install needed.

To run it from source:

1. Open Unity Hub → **Add** → select the `SlimeDefense/` folder (not the repo
   root).
2. Open the only scene, `Assets/Scenes/Main.unity`.
3. Press **Play**.

## Repository layout

| Path | What it is |
| --- | --- |
| `SlimeDefense/` | The Unity project itself. Open this folder in Unity. |
| `SlimeDefense/Assets/Scripts/` | All gameplay C#. |
| `SlimeDefense/Assets/Scenes/Main.unity` | The single game scene. |
| `notes.md` | Running build log, newest first. **Start here.** |
| `tower-defense-build-order.md` | The phased roadmap, Phase 0 → Phase 10. |
| `docs/phase1.md` … `docs/phase8.md` | Step-by-step instructions per phase. |
| `CREDITS.md` | Third-party asset attribution. Required, see below. |
| `screenshots/` | Images referenced by `notes.md`. |

## Current state

Phases 0 through 7 are complete and Phase 8 is partway through. The game has a
waypoint route, wave spawning driven by `WaveDefinition` assets, tower
placement on build nodes, three tower types that shoot and apply their own
on-hit effects, money and lives tracked by a `GameManager`, and a HUD with an
end-of-run panel.

`notes.md` is the authoritative answer to this question — it is updated as work
lands, so trust it over this section if the two disagree.

## Gameplay scripts

| Script | Responsibility |
| --- | --- |
| `WaypointRoute.cs` | Defines the path slimes follow, with Scene-view gizmos. |
| `Slime.cs` | Walks the route, carries health, dies or reaches the goal. |
| `WaveSpawner.cs` / `WaveDefinition.cs` | Spawns waves from ScriptableObject data. |
| `BuildNode.cs` / `TowerPlacer.cs` | Valid build spots and the placement flow. |
| `Tower.cs` / `TowerDefinition.cs` | Targeting and firing; stats come from an asset. |
| `SplashTower.cs` / `FrostTower.cs` | Tower variants overriding what a hit does. |
| `TowerPicker.cs` | The panel for choosing which tower type to place. |
| `Projectile.cs` | Travels to a target and deals damage on arrival. |
| `GameManager.cs` | Money, lives, and run state — one source of truth. |
| `Hud.cs` / `EndOfRunPanel.cs` | HUD readouts and the game-over/victory screen. |
| `OrbitCamera.cs` / `SpriteBillboard.cs` | Camera control and camera-facing sprites. |

## Asset credits

Third-party models and textures are attributed in [`CREDITS.md`](CREDITS.md).
Most are CC BY, which requires the title, author, source, license, **and** a
statement of what was changed. Add an entry there whenever an asset comes into
the project, and keep all five parts — every entry also has to reach the in-game
credits screen.

## Conventions

- Commit early and often, with clear messages. The history is part of the
  point.
- Prefer ScriptableObjects and prefabs over hardcoded values, so things stay
  tunable in the Inspector.
- Decouple systems with events rather than having the UI poll the
  `GameManager` every frame.
- Add a timestamped, phase-labelled note to the top of `notes.md` whenever
  something meaningful is finished or decided.
