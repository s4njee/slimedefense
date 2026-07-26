# Phase 4: Placing Towers

## Goal

Give the player something to do. Add a `Tower` prefab, mark the spots on the map
where a tower is allowed to stand, and let a click — or a tap — place one there.

Pressing Play should let you build towers beside the path while slimes walk past
them, ignored. The towers do not detect, aim, or shoot; that is Phase 5. They do
not cost anything either, because there is no money yet — that is Phase 6. This
phase is about input, raycasting, and the rule that says *here, but not there*.

## Prerequisites

Phase 3 must be complete:

- `Assets/Prefabs/Slime.prefab` exists, and a `WaveSpawner` object drives the
  waves.
- Pressing Play produces a stream of slimes with no further input.

Waves running underneath the placement work is the point. Every phase stays
playable, and building a tower while slimes stream past is the first time the
scene has two independent systems running at once.

## 1. Add the Phase 4 Scripts

Phase 4 adds three scripts under `Assets/Scripts`:

- `BuildNode.cs` — one buildable spot on the map. Knows whether it is occupied
  and colors itself accordingly.
- `Tower.cs` — the tower itself. Almost empty for now; Phase 5 fills it in.
- `TowerPlacer.cs` — reads the pointer, raycasts into the world, and places a
  tower on the node under it.

`Slime.cs`, `WaypointRoute.cs`, `WaveDefinition.cs`, and `WaveSpawner.cs` do not
change. Nothing in this phase touches the enemies.

Open the Unity project so the editor imports the files. They should appear under
`Assets/Scripts` with no console errors.

## 2. Create the BuildNode Layer

The placement raycast has to hit build nodes and nothing else. The terrain has a
collider stretched across the whole map, so a ray cast at everything would hit
the ground first, every time, and never reach a node.

A layer mask fixes this at the source: the ray is told which layers to consider
and ignores everything else.

1. Select **Edit > Project Settings > Tags and Layers**.
2. Find the first empty **User Layer** slot.
3. Name it `BuildNode`.

Filtering the ray is cheaper than casting at everything and sorting the hits
afterward, and it is much harder to get wrong later — a decorative rock dropped
on the map in Phase 9 cannot accidentally become clickable.

## 3. Create the BuildNode Prefab

A node is a flat pad the player can see and click.

1. Select **GameObject > 3D Object > Cylinder**.
2. Rename it to `BuildNode`.
3. Set its Transform Scale to `0.9, 0.05, 0.9`, which flattens the cylinder into
   a pad.
4. Set its **Layer** to `BuildNode` in the dropdown at the top of the Inspector.
5. **Keep the Capsule Collider.** The slime had its collider removed in Phase 2
   because nothing needed to hit it. This is the opposite case: the collider is
   the only thing the placement ray can hit, and a node without one is invisible
   to the raycast while still looking perfectly fine in the Scene view.
6. Select **Add Component** and add the `BuildNode` script.

Then turn it into a prefab by dragging the `BuildNode` object from the Hierarchy
into `Assets/Prefabs`.

## 4. Create the Tower Prefab

Another placeholder shape. A real tower model can replace it in Phase 8 or 9
without touching the placement code.

1. Select **GameObject > 3D Object > Cube**.
2. Rename it to `Tower`.
3. Set its Transform Scale to `0.6, 1.4, 0.6`, so it reads as a tower rather
   than a crate.
4. Remove the **Box Collider**. Nothing shoots at towers, and a collider on the
   tower would sit between the pointer and the node underneath it — the first
   tower placed would block the ray to its own node.
5. Select **Add Component** and add the `Tower` script.
6. Optionally create a material in `Assets/Materials` named `TowerMaterial` and
   assign it.

Drag the `Tower` object into `Assets/Prefabs` to make it a prefab, then delete
the copy left in the Hierarchy. Like slimes, every tower from here on is created
at runtime.

Set the serialized fields on the prefab:

| Field   | Starting value | Meaning                                        |
| ------- | -------------- | ---------------------------------------------- |
| `Cost`  | `50`           | Unused until Phase 6. Placeholder only.        |
| `Range` | `6`            | Unused until Phase 5. Drawn as a gizmo now.    |

Both are placeholders in the same spirit as the slime's `Health` in Phase 2: the
field exists so the number has a home before the system that reads it does. The
`Range` gizmo is worth having early — it is the only way to judge, by eye,
whether the nodes you place actually cover the path.

## 5. Place the Build Nodes

Nodes are authored by hand in the Hierarchy, the same way waypoints were in
Phase 1.

1. Right-click `Level` in the Hierarchy and select **Create Empty**.
2. Rename the new child to `BuildNodes`.
3. Drag `Assets/Prefabs/BuildNode.prefab` into the Scene view, onto the grass
   beside the dirt path.
4. Make it a child of `BuildNodes`.
5. Duplicate it with **Ctrl+D** and position each copy at another buildable
   spot.
6. Place `8` to `12` nodes in total, on both sides of the path.

Positioning guidance:

- Keep nodes on the grass. A node on the dirt is a tower standing in the road.
- Sit each one just above the terrain surface. A pad buried in the ground is
  hard to click and looks like a bug.
- Cluster a few near the corners of the path. Corners are where a slime spends
  the longest inside a tower's range, which is what makes them the interesting
  places to build.

`BuildNodes` is a plain container, unlike `Path`, whose children are meaningful
to `WaypointRoute`. Order does not matter here and nothing reads the parent —
grouping is purely for a readable Hierarchy.

## 6. Add the Placer to the Scene

1. Right-click `Level` in the Hierarchy and select **Create Empty**.
2. Rename the new child to `TowerPlacer`.
3. Select it, choose **Add Component**, and add the `TowerPlacer` script.
4. Drag `Assets/Prefabs/Tower.prefab` into the `Tower Prefab` field.
5. Set `Build Node Mask` to the `BuildNode` layer only. Clear every other entry
   in the dropdown.

There is one placer for the whole scene rather than a script on each node.
Pointer input is a single global question — *what is under the pointer right
now* — and answering it once per frame beats asking every node whether it was
the one that got clicked.

## 7. Tune the Placer in the Inspector

| Field               | Starting value | Meaning                                          |
| ------------------- | -------------- | ------------------------------------------------ |
| `Tower Prefab`      | `Tower`        | What gets built. Phase 8 turns this into a choice. |
| `Build Node Mask`   | `BuildNode`    | Which layers the placement ray may hit.          |
| `Placement Camera`  | empty          | Falls back to `Camera.main` when left empty.     |
| `Max Ray Distance`  | `500`          | How far the ray travels before giving up.        |

`Max Ray Distance` needs to comfortably exceed the distance from the camera to
the far end of the terrain. A value tuned for a 60-unit map silently stops
working the day the map gets bigger, and the symptom — distant nodes ignored,
near ones fine — reads as a broken script rather than a short ray.

If `Placement Camera` is left empty the placer uses `Camera.main`, which is the
camera tagged `MainCamera`. That works today and breaks quietly in Phase 7 if a
second camera ever gets that tag, so the field is there to be explicit when it
matters.

The `BuildNode` prefab has its own fields:

| Field            | Starting value | Meaning                                     |
| ---------------- | -------------- | ------------------------------------------- |
| `Available Color`| pale green     | Nothing built here yet.                     |
| `Hover Color`    | white          | The pointer is over this node.              |
| `Occupied Color` | dark gray      | A tower already stands here.                |
| `Tower Offset`   | `0, 0, 0`      | Local offset applied to the placed tower.   |

`Tower Offset` exists for the case where the tower's pivot is at its center
rather than its base, which sinks the model halfway into the pad. Raise the `y`
until it sits right. A replacement model in Phase 9 will almost certainly need a
different value, and this keeps that a per-prefab tweak rather than a code edit.

## 8. Play Test

Press **Play**. Waves start as they did in Phase 3.

Expected behavior:

- Moving the pointer over a node turns it to `Hover Color`; moving off returns
  it to `Available Color`.
- Clicking a node builds a tower on it.
- The node turns to `Occupied Color` and clicking it again does nothing.
- Clicking the terrain, a slime, or empty sky does nothing and logs nothing.
- Slimes keep walking their route, entirely indifferent to the towers.
- The Console stays clean.

Then try the things that should fail, because a placement rule that has never
been tested against a bad click is not yet a rule:

- Click and hold on an empty node. One tower, not a stack of them.
- Click a node, then drag the pointer off it before releasing.
- Build on every node and confirm the twelfth behaves like the first.

## How the Click Becomes a Tower

Screen coordinates and world coordinates are different spaces, and a raycast is
what bridges them. `ScreenPointToRay` turns a pixel into a line through the
world, and `Physics.Raycast` reports the first collider that line meets:

```csharp
Vector2 screenPoint = Pointer.current.position.ReadValue();
Ray ray = placementCamera.ScreenPointToRay(screenPoint);

if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, buildNodeMask))
{
    hovered = hit.collider.GetComponentInParent<BuildNode>();
}
else
{
    hovered = null;
}
```

Three details in that block carry most of the weight:

- **`buildNodeMask` is the placement rule.** "You cannot build on the path" is
  not a check in an `if` statement anywhere — it is the absence of a node there.
  The ray simply finds nothing, and the click is ignored. Rules expressed as
  data are much harder to get subtly wrong than rules expressed as conditions.
- **`GetComponentInParent`, not `GetComponent`.** The ray hits a *collider*. On
  a plain pad the collider and the script are on the same object, but the moment
  a node gets a nicer model with the collider on a child, `GetComponent` returns
  null and clicks stop registering. Searching upward costs nothing and survives
  that change.
- **`hovered` is recomputed every frame,** including the frame of the press.
  That ordering is what makes touch work at all — see below.

The click itself is one line, and its verb tense is the interesting part:

```csharp
if (Pointer.current.press.wasPressedThisFrame && hovered != null && !hovered.IsOccupied)
{
    hovered.Place(towerPrefab);
}
```

`wasPressedThisFrame` is true on exactly the frame the button goes down.
`isPressed` is true for every frame it stays down — swap them and holding the
mouse button builds a tower per frame, sixty per second, until you let go. It is
a five-character difference and a very memorable bug.

## Designing the Input for Touch Now, Not Later

This project has `activeInputHandler` set to the Input System package only, so
the old `Input.mousePosition` API does not merely misbehave here — it throws an
`InvalidOperationException` at runtime. Every input in this phase comes from
`UnityEngine.InputSystem`.

That constraint turns out to be useful. `Pointer.current` resolves to whatever
pointing device is actually driving the app: the mouse on desktop and WebGL, the
touchscreen on Android. One code path covers both, with no `#if UNITY_ANDROID`
anywhere, which is exactly what the roadmap means by designing for
cross-platform from the start.

Touch differs from mouse in one way that matters, and the code above already
accounts for it. A finger has no hover state — its first contact with the screen
is the hover *and* the press, in the same frame. Because `hovered` is recomputed
before the press is checked, the node under the finger is already known by the
time the tap is handled. Reverse those two blocks and the mouse still works
perfectly while every tap places a tower one node behind where you meant, or
nothing at all on the first tap. It is the sort of bug that only appears on the
device, late.

One deliberate omission: there is no check for whether the pointer is over a UI
element, because there is no UI yet. When the Phase 7 HUD arrives, a tap on the
Start Wave button will also raycast into the world behind it and build a tower.
The guard for that is `EventSystem.current.IsPointerOverGameObject()`, and Phase
7 is where it belongs.

## Why Build Nodes Instead of a Grid

The roadmap offers either a build node or a free grid cell. This project uses
nodes, and the reason is the map from Phase 1.

The dirt path was painted freehand, with broad smooth turns. A grid laid over
that would need to work out which cells the path passes through — from a
*texture*, which carries no such information — and the honest way to do it would
be to author that data by hand anyway. Nodes skip the middle step: placing one
is the same act as declaring the spot buildable.

What nodes buy:

- **Validation is free.** Placement can only target a node, so "not on the path"
  and "not on an occupied tile" need no geometry queries at all.
- **The layout is a design decision.** Where towers may stand is level design,
  visible and adjustable in the Scene view, not an emergent property of grid
  math.
- **They are honest about the map.** An organic, curving path and a rigid square
  lattice fight each other visually. Pads on the grass do not.

What they cost: freeform placement is out. A game where the player picks any
open spot on the terrain needs the grid — or a collider-overlap test against the
path — and this is not that game.

`BuildNode.IsOccupied` is a bool holding a `Tower` reference today. Phase 8's
upgrade and sell actions need to reach the tower standing on a node, and that
reference is already the thing they will ask for.

## What Towers Do Not Do Yet

Nothing. A placed tower is a cube with a range gizmo and two numbers.

That is worth sitting with for a moment, because the temptation at the end of
this phase is to add shooting immediately — the tower is *right there*. Resist
it for one commit. Placement has its own set of ways to be broken, and every one
of them is far easier to recognize now than it will be underneath a targeting
loop and a projectile system.

Phase 5 gives `Tower` its detection radius, its target selection, and its fire
rate, and `Range` stops being decorative.

## Phase 4 Completion Checklist

Phase 4 is complete when:

- `BuildNode.cs`, `Tower.cs`, and `TowerPlacer.cs` compile with no console
  errors.
- A `BuildNode` user layer exists in Tags and Layers.
- `Assets/Prefabs/BuildNode.prefab` and `Assets/Prefabs/Tower.prefab` exist.
- The `BuildNode` prefab is on the `BuildNode` layer and keeps its collider.
- Eight or more nodes sit beside the path under a `BuildNodes` container.
- A `TowerPlacer` object is in the scene with `Tower Prefab` and
  `Build Node Mask` assigned.
- Hovering a node highlights it; hovering away clears the highlight.
- Clicking a node places exactly one tower and marks the node occupied.
- Clicking an occupied node, the terrain, or a slime does nothing.
- Holding the button down places one tower, not many.
- Waves still spawn and walk the route while towers are being placed.
- No targeting, shooting, damage, or currency code has been added yet.

After this checklist is complete, Phase 5 will give towers a detection radius,
targeting logic, and a fire rate, so slimes can finally take damage and die.
