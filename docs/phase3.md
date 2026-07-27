# Phase 3: Waves of Slimes

## Goal

Replace the single hand-placed slime with a spawner that instantiates slimes on
a timer, and describe each wave as a ScriptableObject asset instead of numbers
buried in code.

Pressing Play should produce a stream of slimes walking the route, wave after
wave, with no slime in the scene at edit time. There is still no tower, no
shooting, and no damage — those are Phases 4 and 5. Nothing counts kills or
lives yet either; that is Phase 6.

## Prerequisites

Phase 2 must be complete:

- `Assets/Prefabs/Slime.prefab` exists and walks the route on its own.
- The `WaypointRoute` component is on the `Path` object.

Delete any loose `Slime` instance left in the Hierarchy from Phase 2. From this
phase on, every slime is created at runtime by the spawner, and a leftover
scene copy makes the wave counts confusing.

## 1. Add the Phase 3 Scripts

The two scripts for this phase already exist in the repository at:

- `Assets/Scripts/WaveDefinition.cs` — a ScriptableObject describing one wave.
- `Assets/Scripts/WaveSpawner.cs` — a MonoBehaviour that plays a list of those
  waves.

`Slime.cs` and `WaypointRoute.cs` do not change. `Slime.SetRoute` was written in
Phase 2 for exactly this handoff, and the spawner is its first real caller.

Open the Unity project so the editor imports both files. They should appear
under `Assets/Scripts` with no console errors.

## 2. Create the Wave Assets

`WaveDefinition` carries `[CreateAssetMenu]`, so waves are created from the
Project window like any other asset.

1. Create the folder `Assets/Waves`.
2. Right-click it and select **Create > SlimeDefense > Wave**.
3. Name the asset `Wave_01`.
4. Repeat twice more for `Wave_02` and `Wave_03`.

Then fill in each asset:

| Field                | Wave_01 | Wave_02 | Wave_03 | Meaning                              |
| -------------------- | ------- | ------- | ------- | ------------------------------------ |
| `Slime Prefab`       | `Slime` | `Slime` | `Slime` | Which prefab to instantiate.         |
| `Count`              | `5`     | `8`     | `12`    | How many slimes this wave sends.     |
| `Spacing`            | `1.2`   | `1`     | `0.7`   | Seconds between one slime and the next. |
| `Delay Before Wave`  | `0`     | `0`     | `0`     | Extra pause before this wave starts. |

Drag `Assets/Prefabs/Slime.prefab` into the `Slime Prefab` field of each asset.

These three assets are the entire difficulty curve for now. Tuning the game
means editing them — not recompiling.

## 3. Add the Spawner to the Scene

1. Right-click `Level` in the Hierarchy and select **Create Empty**.
2. Rename the new child to `WaveSpawner`.
3. Select it, choose **Add Component**, and add the `WaveSpawner` script.
4. Drag the `Path` object into the `Route` field.
5. Set `Waves` to size `3` and drag `Wave_01`, `Wave_02`, and `Wave_03` into the
   three slots in that order.

The list order is the play order, so reordering waves is a drag in the
Inspector.

The spawner sits under `Level` rather than under `Path` on purpose. `Path` uses
its children as route points, so anything parented to it becomes a waypoint.

## 4. Tune the Spawner in the Inspector

| Field                | Starting value | Meaning                                          |
| -------------------- | -------------- | ------------------------------------------------ |
| `Route`              | `Path`         | The route handed to every spawned slime.         |
| `Waves`              | 3 assets       | The waves to play, in list order.                |
| `Start Delay`        | `2`            | Seconds before the first wave begins.            |
| `Time Between Waves` | `5`            | Seconds between the end of one wave and the next. |
| `Auto Start`         | checked        | Begin on Play. Phase 7 replaces this with a button. |
| `Slime Parent`       | empty          | Optional container for spawned slimes.           |

`Start Delay` matters more than it looks. Without it, the first slime appears on
frame one, before you have had a chance to look at the scene.

If you leave `Slime Parent` empty the slimes are created at the root of the
Hierarchy, which is fine but noisy. Creating an empty `Slimes` object under
`Level` and assigning it keeps the Hierarchy readable while forty of them are
alive.

## 5. Play Test

Press **Play** and watch the Hierarchy as much as the Game view.

Expected behavior:

- Nothing happens for `Start Delay` seconds.
- Five slimes spawn one at a time, spaced by `Spacing`, and walk the route.
- Each despawns at `GoalPoint`, so the Hierarchy count rises then falls.
- After the last slime of a wave spawns and `Time Between Waves` elapses, the
  next wave starts, denser than the one before it.
- After `Wave_03` finishes, spawning stops and the Console stays clean.

While it runs, select a wave asset and change `Spacing`. Unlike Inspector edits
during Play on a scene object, **changes to a ScriptableObject asset persist
after you stop.** That is convenient for tuning and an easy way to lose track of
what you changed — worth knowing before it surprises you.

## How the Spawn Loop Works

The spawner is a coroutine, not an `Update` with a timer variable:

```csharp
WaitForSeconds gap = new WaitForSeconds(wave.Spacing);

for (int i = 0; i < wave.Count; i++)
{
    Spawn(wave.SlimePrefab);

    // No trailing gap after the last slime; Time Between Waves covers it.
    if (i < wave.Count - 1)
    {
        yield return gap;
    }
}
```

A coroutine is a method that can pause partway through and resume on a later
frame. `yield return` hands control back to Unity, and `WaitForSeconds` tells it
when to come back. The wave sequence reads top to bottom as the order it
happens, which the `Update`-with-counters version does not.

Three things worth carrying forward:

- **Coroutines are owned by the MonoBehaviour that started them.** Disable the
  `WaveSpawner` object and the wave stops mid-spawn without an error. That is
  useful for a pause, and confusing the first time it happens by accident.
- **`WaitForSeconds` scales with `Time.timeScale`.** Setting `Time.timeScale = 0`
  in a later pause menu freezes waves along with everything else, which is what
  you want. `WaitForSecondsRealtime` is the version that ignores it.
- **`new WaitForSeconds(...)` allocates.** Creating one per slime would produce
  garbage proportional to the wave size, so the interval is built once above the
  loop and reused. Harmless either way at five slimes; the habit is the same one
  Phase 8's object pooling formalizes for the slimes themselves.

The spawn itself is two lines, and the second one is the point of the phase:

```csharp
Slime slime = Instantiate(prefab, route.GetPoint(0), Quaternion.identity, slimeParent);
slime.SetRoute(route);
```

`SetRoute` runs immediately after `Instantiate`, while `Start` on the new slime
does not run until later in the frame. By the time `Slime.Start` checks whether
it has a route, the spawner has already supplied one, so the
`FindFirstObjectByType` fallback from Phase 2 never fires. That fallback is now
dead weight for spawned slimes and a convenience only for one dragged in by
hand.

## Why Waves Are ScriptableObjects

A hardcoded `SpawnWave(10, 1f)` would work today. Moving the numbers into assets
buys three things:

- **Data lives outside code.** Adding `Wave_04` is creating an asset, not
  editing and recompiling a script.
- **One asset, many users.** A `Wave` asset can be referenced by several levels.
  Unlike a MonoBehaviour, it exists once in the project rather than once per
  GameObject.
- **It is the Unity idiom.** ScriptableObjects for tower stats, enemy types, and
  level data is the pattern reviewers expect to see, and this is the smallest
  place to learn it.

The wave holds one prefab and one count today. Phase 8 adds enemy variety, which
turns that pair into a list of groups — a change to the asset's shape, with the
spawner's loop mostly intact.

## What "Wave Finished" Means Right Now

The spawner moves to the next wave when the current one has finished *spawning*,
not when its slimes are gone. Slimes from `Wave_01` will still be on the path
when `Wave_02` begins, which is normal for the genre.

Phase 6 gives the spawner a way to know when the field is actually clear, once
something is tracking slimes as they die and reach the goal.

## Phase 3 Completion Checklist

Phase 3 is complete when:

- `Assets/Scripts/WaveDefinition.cs` and `Assets/Scripts/WaveSpawner.cs` compile
  with no console errors.
- Three wave assets exist under `Assets/Waves`.
- **Create > SlimeDefense > Wave** creates a new wave asset.
- A `WaveSpawner` object is in the scene with `Route` and `Waves` assigned.
- No `Slime` instance is left in the scene at edit time.
- Pressing Play spawns slimes on a timer with no further input.
- All three waves play in order, each denser than the last.
- Every slime walks the full route and despawns at `GoalPoint`.
- Editing `Count` or `Spacing` on a wave asset changes the game with no code
  change.
- No tower, placement, or damage code has been added yet.

After this checklist is complete, Phase 4 will add a Tower prefab and a
placement system that lets the player click a valid spot to build on.
