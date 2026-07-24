# Phase 1: The Map and the Path

## Goal

Create a simple 3D level with grassy terrain, a visible dirt path, and an
ordered set of waypoints running from a spawn point to a goal. There are no
enemies or movement scripts in this phase.

The dirt texture shows the player where slimes will travel. Invisible waypoint
GameObjects will define the actual movement route in Phase 2.

## 1. Prepare the Scene

1. Open the Unity project in the `SlimeDefense` folder.
2. Open the generated sample scene.
3. Select **File > Save As**.
4. Save the scene as `Assets/Main.unity`.
5. Create these folders under `Assets` if they do not already exist:
   - `Materials`
   - `Textures`
   - `Prefabs`

## 2. Create the Terrain

1. Select **GameObject > 3D Object > Terrain**.
2. Rename the new GameObject to `Terrain`.
3. Open the Terrain Settings in the Inspector.
4. Use these starting dimensions:
   - Width: `60`
   - Length: `40`
   - Height: `10`
5. Keep the terrain flat for now. Hills can be added after the basic enemy
   movement works.

Unity Terrain includes a Terrain Collider, so it can later support object
placement and raycasts without a separate ground collider.

## 3. Add Grass and Dirt Textures

Import two seamless textures into `Assets/Textures`:

- A grass texture
- A dirt texture

Then create the terrain layers:

1. Select `Terrain` in the Hierarchy.
2. In the Terrain Inspector, select **Paint Terrain**.
3. Choose **Paint Texture**.
4. Select **Edit Terrain Layers > Create Layer**.
5. Choose the grass texture and save the layer as
   `GrassTerrainLayer`.
6. Add the dirt texture as another layer named `DirtTerrainLayer`.

The first Terrain Layer fills the entire terrain, so add the grass layer first.

## 4. Paint the Dirt Path

1. Select `DirtTerrainLayer` in the Paint Texture tool.
2. Choose a soft, round brush.
3. Adjust the brush size to make the path approximately `4-6` Unity units
   wide.
4. Paint one continuous route from one side of the map to another.
5. Use broad, smooth turns instead of sharp corners.
6. Leave enough grass on both sides of the path for towers.

Do not place a second flat mesh over the terrain to make the path. Overlapping
surfaces can flicker. Painting the dirt directly onto the Terrain avoids this.

## 5. Create the Waypoint Route

Create this hierarchy:

```text
Level
|-- Terrain
|-- Path
|   |-- SpawnPoint
|   |-- Waypoint_01
|   |-- Waypoint_02
|   |-- Waypoint_03
|   `-- GoalPoint
|-- Main Camera
`-- Directional Light
```

To build it:

1. Right-click an empty area in the Hierarchy and select **Create Empty**.
2. Rename the new empty GameObject to `Level`.
3. Make `Terrain`, `Main Camera`, and `Directional Light` children of `Level`
   by dragging each one onto `Level` in the Hierarchy.
4. Right-click `Level` and select **Create Empty**.
5. Rename this empty child to `Path`. This is the parent object that keeps
   every path marker organized.
6. Right-click `Path` and select **Create Empty**.
7. Rename the new child to `SpawnPoint`.
8. Right-click `Path` again, select **Create Empty**, and name the new
   child `Waypoint_01`.
9. Duplicate `Waypoint_01` with **Ctrl+D** and rename each copy sequentially:
   `Waypoint_02`, `Waypoint_03`, `Waypoint_04`, and so on.
10. Create one final empty child under `Path` and name it `GoalPoint`.
11. Confirm that `SpawnPoint`, every numbered waypoint, and `GoalPoint` are
    indented beneath `Path` in the Hierarchy. If one is not a child, drag it
    onto `Path`.
12. Position each child object along the center of the painted dirt path.
13. Place a waypoint at every turn.
14. Add waypoints approximately `5-10` units apart on long straight sections.
15. Keep the waypoint objects at, or slightly above, the terrain surface.
16. Order them in the Hierarchy from the spawn to the goal.

Empty GameObjects have no visible model. Their Transform positions act as
markers that the slime movement script will follow during Phase 2. Enable
Gizmos in the Scene view if their icons are not visible.

The waypoint positions determine where slimes move. The painted path should
visually follow the same route, but it does not control movement.

## 6. Frame the Level

Position the Main Camera so the entire route, or the useful playable area, is
easy to see. Keep the Directional Light angled so the terrain is clearly lit.

Avoid detailed decoration during this phase. Grass-detail objects, trees,
rocks, fences, and hills can make the route harder to adjust before gameplay
exists.

## Waypoints Instead of NavMesh

Use waypoints for the first version. They provide a predictable fixed route,
are easy to inspect, and make the movement script in Phase 2 straightforward.
A NavMesh can be considered later if enemies need dynamic pathfinding.

## Phase 1 Completion Checklist

Phase 1 is complete when:

- `Assets/Main.unity` exists.
- The level has a flat grassy Terrain.
- A clearly visible dirt path crosses the map.
- There is room beside the path for future towers.
- `SpawnPoint` is at the beginning of the path.
- `GoalPoint` is at the end of the path.
- Ordered waypoints connect the spawn point to the goal.
- The dirt path and waypoint route follow the same line.
- The camera and lighting make the complete route easy to inspect.
- No enemy or movement code has been added yet.

After this checklist is complete, Phase 2 will create an Enemy prefab and move
it from waypoint to waypoint.
