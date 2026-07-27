# SlimeDefense Notes

## Note Instructions

Each time a note is added, timestamp it and label it with the relevant phase.
Notes are listed newest first, so add new entries at the top of the Notes
section.

## Notes

### 2026-07-27 11:35 -05:00 | Phase 7

Custom sprites are enabled. The slime is now a billboarded animated sprite rather
than the imported 3D mesh, with a jump cycle playing as it walks the route.

New assets: `Assets/Sprites/slimesprite-transparent.png`, the `slimejump` clip and
its controller under `Assets/Animation`, and `Scripts/SpriteBillboard.cs`, which
copies the camera's rotation in `LateUpdate`. Running in `LateUpdate` is what
keeps it compatible with the Phase 2 facing fix: `Slime.Update` still aims the
root down the path, and the billboard turns only the sprite child afterwards, so
the path decides where the slime is going and the camera decides which way the
picture faces.

Projectiles now home on `Slime.AimPosition` instead of `transform.position`.
The root stays on the ground along the path while the sprite stretches and leaves
it during the jump, so aiming at the root meant shots arriving underneath an
airborne slime. `AimPosition` returns the sprite renderer's bounds center, which
follows whatever frame is currently displayed.

The Meshy Blue Pebble mesh is no longer referenced by the prefab. `CREDITS.md`
still lists it and the Sketchfab candidates, and needs revisiting to say what is
actually in the game.

Two things this changed by accident, both worth handling before Phase 8
duplicates the prefab into enemy variants:

- The oversized detection sphere left over from Phase 5 went away with the mesh
  child it was attached to. The live collider is now `1.435` on `SpriteVisual`,
  which the root's scale of `3` makes about `4.3` units in world space — sane
  compared to the old one, still large next to a tower `Range` of `6`.
- That collider is **not** a trigger. Phase 5 required one so slimes could be
  found by queries without taking part in collision resolution.

![Animated sprite slimes walking the route](screenshots/phase7-2.webp)

### 2026-07-27 10:35 -05:00 | Phase 7

Phase 7 is complete. Money, lives, and the wave counter are on screen, the run
starts from a button instead of on its own, and it ends with a panel that says
which way it went and offers to play again.

`Hud.cs` and `EndOfRunPanel.cs` are new. `GameManager.cs` gained a live slime
count, a victory condition, and `Restart`; `WaveSpawner.cs` announces wave
progress and tells the manager when its list is exhausted; `Slime.cs` registers
and unregisters itself; `TowerPlacer.cs` ignores presses that landed on the HUD,
which is the guard Phase 4 said belonged here.

Nothing polls. Every label is written when the value behind it changes, which is
what the Phase 6 events were built for a phase early.

Three bugs, and all three were silent — nothing logged, nothing threw:

- The Canvas was created while `Level` was selected, so it became a child of a
  world object a thousand units from the origin and serialized with a scale of
  zero. Every label was present, enabled, and white, and none of them rendered.
  A `Screen Space - Overlay` canvas has to be a root object.
- The Start Wave button's `On Click ()` had `WaveSpawner.cs` — the *script asset*
  from the Project window — in its object slot rather than the `WaveSpawner`
  object from the Hierarchy. Unity accepted it, the function dropdown had no
  component methods to offer, and the saved method name was empty. The button
  highlighted and animated and called nothing. It is wired in code now, through a
  typed `Button` field that cannot be given the wrong thing.
- Both new listeners subscribed in `OnEnable` and guarded `GameManager.Instance`
  for null. When `OnEnable` ran before the manager's `Awake`, that guard skipped
  the subscription for the whole run, and the money label sat on its editor text
  — which read `Money: 100` and matched the starting money, so it looked correct
  and never moved. Both now subscribe from `OnEnable` and `Start`, with a flag
  making whichever arrives first the only one that acts.

Still to do before this is shippable: `Main.unity` is not in Build Settings — the
list still holds only the deleted `SampleScene` — so `Restart` has nothing to
load outside the editor, and a build would ship no scenes.

No tower selection panel. There is one tower type, so it would be a panel with
one button; Phase 8 adds the types and the panel together.

![HUD, start button, and the end-of-run panel in Play mode](screenshots/phase7.webp)

### 2026-07-27 09:37 -05:00 | Phase 6

Phase 6 is complete. Killing a slime pays, building a tower spends, and a slime
reaching the goal costs a life. At zero lives the run ends: spawning stops and
placement stops.

`GameManager.cs` is new and owns all of it — money, lives, and whether the run is
over — with `MoneyChanged`, `LivesChanged`, and `GameOver` events for Phase 7's
HUD to subscribe to. `Slime.cs` gained `Reward` and `Life Cost`, and the `Die`
and `ReachGoal` pair that Phase 5 deliberately kept separate finally diverged.
`TowerPlacer.cs` checks affordability, builds, and only then charges, so a
refused placement never takes the player's money. `WaveSpawner.cs` calls its own
`StopWaves` on game over. `Tower.cs` needed no changes at all — it already
carried its own price.

The numbers are tuned against this level rather than picked: 8 build nodes, 25
slimes across the three waves, 10 per kill and 50 per tower, so a perfect run
affords 7 towers and the map can never be filled. That is the answer to Phase 5
ending with towers that were free and unlimited.

Play testing turned up a prefab bug rather than a code one: `Slime.prefab` had
two `Slime` components, one on the root and one added to the Meshy model child.
Since the root's collider was disabled, towers targeted the child, and killing it
destroyed only the model — leaving an invisible slime that walked on and cost a
life. The duplicate has been removed. The child's collider is still the enabled
one at radius `6.89` on an object scaled `200`, which is worth checking against
what `Range` is supposed to mean.

Money and lives are still only visible in the Console via `Log Changes`. There is
no HUD, no victory condition, and no restart — that is Phase 7, and the events
above are what it will hang off.

### 2026-07-27 06:10 -05:00 | Phase 5

Phase 5 is complete. Towers detect slimes inside their `Range`, fire projectiles
at the one furthest along the route, and slimes die at zero health. Where the
tower goes now decides which slimes get through.

Every item on the Phase 5 completion checklist in `phase5.md` is satisfied.
`Projectile.cs` is new; `Tower.cs` gained detection, targeting, and firing;
`Slime.cs` gained `RouteProgress`, `TakeDamage`, and a `Die` separate from
`ReachGoal`. Slimes sit on the `Slime` layer with a trigger collider, and the
tower's `Slime Mask` matches it, so detection never considers terrain, nodes, or
other towers.

Targeting is first-in-line rather than nearest, because the slime closest to the
goal is the most urgent threat. Projectiles home on their target and null-check
it every frame, so a slime dying mid-flight is a shot that gives up rather than a
`MissingReferenceException`.

Killing a slime still pays nothing and letting one through still costs nothing.
`Die` and `ReachGoal` are the two hooks waiting for Phase 6 to wire money and
lives into.

![Towers shooting slimes in Play mode](screenshots/phase5.webp)

### 2026-07-26 10:09 -05:00 | Phase 4

Phase 4 is complete. Clicking a build node beside the path places a tower on it,
the node highlights on hover and locks once occupied, and waves keep running
underneath the whole time.

Every item on the Phase 4 completion checklist in `phase4.md` is satisfied. The
placement ray filters on the `BuildNode` layer, so there is no rule forbidding
towers on the path — there is simply no node there to hit.

The tower is a Meshy AI generated model imported as `.fbx`, with its textures
extracted from the FBX's embedded data into `Assets/TowerTextures`. Its license
terms still need to be confirmed and recorded in `CREDITS.md`.

No targeting, shooting, damage, or currency code exists yet — those are Phases 5
and 6.

![Placing towers on build nodes in Play mode](screenshots/phase4.webp)

### 2026-07-25 19:23 -05:00 | Phase 3

Phase 3 has started. `WaveSpawner` is attached to the scene with its `Route`
pointing at `Path` and three `Wave Definition` elements assigned.

![WaveSpawner configured in the Unity Inspector](screenshots/phase3.webp)

### 2026-07-25 06:58 -05:00 | Phase 2

Phase 2 is complete. One slime walks the full route from `SpawnPoint` to
`GoalPoint` and despawns on arrival, with `Speed`, `Health`, and
`Arrive Distance` editable on the prefab at `Assets/Prefabs/Slime.prefab`.

Every item on the Phase 2 completion checklist in `phase2.md` is satisfied. No
spawner, tower, or damage code exists yet — those are Phases 3 through 5.

![Slime walking the waypoint route in Play mode](screenshots/phase2_2.webp)

### 2026-07-25 06:29 -05:00 | Phase 2

`WaypointRoute` is attached to `Path` and the route gizmos are visible along the
path. Spawn is green, the goal is red, the points between are cyan, and yellow
lines connect them in order.

![Waypoint route gizmos in the Scene view](screenshots/phase2_1.png)

### 2026-07-25 05:15 -05:00 | Phase 2

Alternate slime model under consideration. This one has no face to remove, and
it contains two slimes at different sizes, which could cover two enemy types in
Phase 8.

Source: [Slimes by MartinDL](https://sketchfab.com/3d-models/slimes-61de4624789d468b864d826ec02b636c)
(63.5k triangles, 31.8k vertices, published 2022-11-16)

License: CC Attribution (CC BY), the same terms as the other candidate.

![Slimes model on Sketchfab](screenshots/slime-model-alt.jpg)

### 2026-07-25 05:12 -05:00 | Phase 2

This model will be used for the slime enemy, modified to remove the face.

Source: [Rimuru Slime by tommdraws](https://sketchfab.com/3d-models/rimuru-slime-612ff2c805114744b66d3c29c7942371)
(45.4k triangles, 21.8k vertices)

License: CC Attribution (CC BY). Modification is allowed, so removing the face
is fine. Credit is required, and the modification has to be disclosed. The
attribution text is in `CREDITS.md` and needs to reach an in-game credits screen
in Phase 7.

![Rimuru Slime model on Sketchfab](screenshots/slime-model.png)

### 2026-07-25 05:08 -05:00 | Phase 2

Phase 2 has started. Step-by-step instructions are in `phase2.md`. The two
scripts it needs are committed at `Assets/Scripts/WaypointRoute.cs` and
`Assets/Scripts/Slime.cs`. The remaining work is in the Unity editor: attach
`WaypointRoute` to `Path` and build the `Slime` prefab.

### 2026-07-24 09:36 -05:00 | Phase 1

Terrain has been added to the scene.

![Phase 1 terrain in Unity](screenshots/phase1.png)

### 2026-07-24 09:19 -05:00 | Phase 1

Phase 1 planning has started. The map, grassy terrain, dirt path, and waypoint
setup instructions are documented in `phase1.md`.

### 2026-07-24 08:19 -05:00 | Phase 0

Phase 0 is complete. The Unity 3D project has been generated, Git is initialized, and a Unity-focused `.gitignore` has been added.

### 2026-07-24 08:13 -05:00 | Phase 0

The `SlimeDefense` folder was generated by Unity.

### 2026-07-24 08:13 -05:00 | Phase 0

Keep the project in Git from the beginning with a Unity-focused `.gitignore`.

### 2026-07-24 08:13 -05:00 | Phase 0

Add build support modules as needed:

- WebGL Build Support
- Android Build Support
- iOS Build Support, later if Apple/mobile builds become a goal

### 2026-07-24 08:13 -05:00 | Phase 0

Use the current **Unity LTS** editor version through Unity Hub.

### 2026-07-24 08:13 -05:00 | Phase 0

The recommended starting movement system is **waypoints** rather than NavMesh. This keeps the first playable version simple while still allowing a fully 3D presentation.

### 2026-07-24 08:13 -05:00 | Phase 0

This project is being built as a **3D Unity project** so towers, enemies, projectiles, terrain, and props can use 3D models.
