# Phase 2: A Single Enemy That Moves

## Goal

Get one slime walking from `SpawnPoint`, through the numbered waypoints, to
`GoalPoint`, where it despawns. Its `speed` and `health` are editable in the
Inspector.

This is the first phase where pressing Play produces motion. There is no wave
spawning, no tower, no shooting, and no damage yet — those are Phases 3 through
5. The `health` field is added now only so the slime is ready to receive damage
later.

## Prerequisites

Phase 1 must be complete. In particular, `Assets/Main.unity` needs the `Path`
object with its ordered children:

```text
Path
|-- SpawnPoint
|-- Waypoint_01
|-- Waypoint_02
|-- Waypoint_03
|-- Waypoint_04
`-- GoalPoint
```

The slime follows these Transform positions, not the painted dirt texture. If
the waypoints and the dirt path have drifted apart, fix that before starting.

## 1. Add the Scripts Folder

The two scripts for this phase already exist in the repository at:

- `Assets/Scripts/WaypointRoute.cs`
- `Assets/Scripts/Slime.cs`

Open the Unity project so the editor imports them. Both should appear under
`Assets/Scripts` in the Project window with no console errors.

## 2. Attach the Route Component

`WaypointRoute` reads the child objects of whatever it is attached to and treats
Hierarchy order as route order.

1. Select `Path` in the Hierarchy.
2. In the Inspector, select **Add Component**.
3. Search for `WaypointRoute` and add it.

Nothing to configure — the component has no fields. It derives the route from
the children directly, so adding, removing, or reordering waypoints in the
Hierarchy changes the route with no code edits.

The component also draws the route in the Scene view: cyan spheres at each
waypoint, connected by lines. Use this to confirm the order runs spawn → goal
and not in some scrambled sequence. If the line zig-zags backwards, drag the
waypoints into the correct order under `Path`.

## 3. Create the Slime Prefab

Use a placeholder shape for now. A real slime model can replace it in Phase 8
or 9 without touching the movement code.

1. Select **GameObject > 3D Object > Sphere**.
2. Rename it to `Slime`.
3. Set its Transform Scale to `0.8, 0.8, 0.8`.
4. Remove the **Sphere Collider** component. Nothing needs to collide with the
   slime until towers start detecting it in Phase 5.
5. Select **Add Component** and add the `Slime` script.
6. Optionally create a green material in `Assets/Materials` named
   `SlimeMaterial` and assign it, so the slime reads clearly against the
   terrain.

Then turn it into a prefab:

1. Create the folder `Assets/Prefabs` if it does not exist.
2. Drag the `Slime` object from the Hierarchy into `Assets/Prefabs`.
3. Confirm the Hierarchy entry turns blue, which means it is now a prefab
   instance.

The Phase 3 spawner will instantiate this prefab, so every tuning change should
be made to the prefab rather than to a single scene copy.

## 4. Tune the Slime in the Inspector

Select the `Slime` prefab and set the serialized fields:

| Field            | Starting value | Meaning                                          |
| ---------------- | -------------- | ------------------------------------------------ |
| `Speed`          | `3`            | Units travelled per second.                      |
| `Health`         | `10`           | Unused until Phase 5. Placeholder only.          |
| `Arrive Distance`| `0.15`         | How close counts as reaching a waypoint.         |

These are `[SerializeField]` private fields rather than `public` ones. They are
editable in the Inspector but not reachable from other scripts, which keeps
other systems from writing to them by accident. Getting comfortable with this
distinction is one of the points of the phase.

If `Arrive Distance` is set too small, a fast slime can overshoot a waypoint and
circle it without ever arriving. `Vector3.MoveTowards` clamps at the target, so
`0.15` is safe at these speeds — but this is the kind of bug worth recognizing.

## 5. Play Test

1. Position the `Slime` instance anywhere in the scene. `Start` snaps it to the
   first route point, so the placement does not matter.
2. Press **Play**.

Expected behavior:

- The slime jumps to `SpawnPoint` on the first frame.
- It walks the route, turning to face each waypoint in turn.
- It disappears when it reaches `GoalPoint`.
- The Console stays clean.

While it runs, select the slime and watch its position in the Inspector. Try
changing `Speed` during Play to see the movement respond immediately. Values
changed during Play revert when you stop, which is exactly what makes it a safe
way to find the number you want.

## How the Movement Works

The script moves the slime one step per frame toward its current target and
advances the target index on arrival:

```csharp
transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
```

Two details in that line matter more than they look:

- **`Time.deltaTime`** is the seconds elapsed since the last frame. Multiplying
  by it makes `speed` mean "units per second" instead of "units per frame". Skip
  it and the slime moves faster on a 144 Hz monitor than a 60 Hz one — a
  genuinely common bug, and one that surfaces as unfair difficulty rather than
  as an error.
- **`MoveTowards` clamps** at the target rather than passing through it, so a
  large per-frame step cannot overshoot.

The target's `y` is overwritten with the slime's own `y` before moving. Without
that, waypoints sitting at slightly different terrain heights would leave a
vertical gap the slime never closes, and it would stall short of the waypoint
forever.

## Why the Route Lives on a Separate Component

`Slime` does not hold a list of waypoints. It asks `WaypointRoute` for point
`n`. That split is deliberate:

- The route is authored once in the Hierarchy, not re-entered on every enemy.
- Phase 3 can spawn fifty slimes that all share one route object.
- A second route, for a map with two lanes, means a second `Path` object — no
  code change.

`SetRoute` exists for exactly that Phase 3 handoff: the spawner instantiates a
slime and hands it the route. The `FindFirstObjectByType` fallback in `Start` is
only a convenience for a slime dragged into the scene by hand during this
phase.

## Phase 2 Completion Checklist

Phase 2 is complete when:

- `Assets/Scripts/WaypointRoute.cs` and `Assets/Scripts/Slime.cs` compile with
  no console errors.
- The `WaypointRoute` component is on the `Path` object.
- The Scene view draws the cyan route in spawn-to-goal order.
- `Assets/Prefabs/Slime.prefab` exists.
- `Speed`, `Health`, and `Arrive Distance` are visible and editable on the
  prefab.
- Pressing Play walks one slime the full route.
- The slime despawns at `GoalPoint` and leaves nothing behind in the Hierarchy.
- No spawner, tower, or damage code has been added yet.

After this checklist is complete, Phase 3 will add a spawner that instantiates
slimes on a timer and turns waves into ScriptableObject data assets.
