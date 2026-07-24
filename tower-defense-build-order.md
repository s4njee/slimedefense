# Tower Defense in Unity — Build Order Roadmap

A staged plan for building a cross-platform tower defense game in Unity with C#. The guiding principle: **stay in a playable state at every stage.** Each phase adds one working system on top of a game you can already press Play on. This keeps you motivated and makes bugs easy to isolate — you always know which system you just touched.

Alongside each phase are the **Unity/C# concepts it teaches**, because the point of this project is that finishing it makes you genuinely more employable, not just that it exists.

---

## Phase 0 — Setup & Foundations

Get the environment and your fundamentals in place before writing game logic.

- Install **Unity Hub** and the current LTS version of the Unity Editor (LTS = the stable, long-term-support release — use this, not the bleeding-edge version).
- Create a new **2D project** (or 3D if you want the classic 3D-tower look — 2D is simpler to start and plenty employable).
- Add the modules you'll need for cross-platform export: **Android Build Support**, **iOS Build Support**, and **WebGL Build Support**.
- Set up version control from day one: initialize a **Git repo** with Unity's `.gitignore`. Recruiters look at GitHub; get in the habit early.
- Brush up on the C# you'll lean on constantly: classes, methods, `public`/`private`, lists, and the `MonoBehaviour` lifecycle (`Awake`, `Start`, `Update`).

**Concepts:** Unity project structure, the Editor layout (Scene/Game/Hierarchy/Inspector), MonoBehaviour lifecycle, Git for Unity.

---

## Phase 1 — The Map & The Path

Give enemies somewhere to walk before anything else exists.

- Build a simple level layout: a ground plane or tilemap, and a defined **path** from a spawn point to a goal.
- Represent the path as a series of **waypoints** (empty GameObjects the enemy will move between), or use Unity's **NavMesh** if you go 3D.
- No enemies yet — just the level and the path markers.

**Concepts:** GameObjects & Transforms, the Scene hierarchy, Prefabs (make the level reusable), waypoints vs NavMesh navigation.

---

## Phase 2 — A Single Enemy That Moves

The first thing that actually *does* something.

- Create one **Enemy prefab** with a script that walks from waypoint to waypoint to the goal.
- Give it a `health` value and a `speed` value as public/serialized fields so you can tune them in the Inspector.
- When it reaches the goal, it despawns (later: it costs you a life).

**Concepts:** Vector math and movement (`MoveTowards`), `[SerializeField]` for Inspector tuning, prefab instantiation, the update loop.

---

## Phase 3 — Wave Spawning

Turn one enemy into a stream of them.

- Build a **Spawner / WaveManager** that instantiates enemies on a timer.
- Start hardcoded (spawn 10 enemies, 1 second apart), then make waves data-driven.
- This is a great first use of **ScriptableObjects**: define each wave (which enemies, how many, spacing) as a data asset instead of code. This is a genuinely important Unity pattern and worth learning here.

**Concepts:** Coroutines (`IEnumerator` / `yield`), instantiation at runtime, **ScriptableObjects** for data-driven design, separating data from behavior.

---

## Phase 4 — Placing Towers

Give the player agency.

- Create a **Tower prefab** and a placement system: click a valid spot (a build node or a free grid cell) to place a tower.
- Validate placement — can't build on the path, can't build on an occupied tile.
- No shooting yet; towers just sit there.

**Concepts:** Mouse/touch input (design this to work for both — important for cross-platform), raycasting to detect clicks, grid or node-based placement logic, UI-to-world interaction.

---

## Phase 5 — Towers That Shoot

The core combat loop.

- Give towers a **detection radius**, **targeting logic** (nearest enemy? first in line? lowest health?), and a **fire rate**.
- Spawn **projectiles** that travel to the target and deal damage, OR do instant hitscan damage — start with whichever is simpler.
- Enemies take damage, and **die when health hits zero**.

**Concepts:** Physics overlap queries (`OverlapCircle` / triggers), target selection algorithms, projectile movement, a damage/health system, and your first taste of **object pooling** (see Phase 8) if projectiles get numerous.

---

## Phase 6 — Economy & Player State

Now it's a game with stakes.

- Add **currency**: enemies drop money when killed; towers cost money to place.
- Add **lives**: enemies that reach the goal cost you a life; hit zero and it's game over.
- Centralize this in a **GameManager** so systems read one source of truth.

**Concepts:** Managing global game state, the **singleton pattern** (used carefully), **events / C# `Action` delegates** to notify the UI when money or lives change (this decouples systems and is a hallmark of clean Unity code).

---

## Phase 7 — UI & HUD

Make the state visible and the game controllable.

- Build a HUD showing **money, lives, and current wave**.
- Add a **tower-selection panel**, a **start-next-wave** button, and a **game-over / victory** screen.
- Wire the UI to update via the events you set up in Phase 6, not by polling every frame.

**Concepts:** Unity UI (Canvas, anchors, responsive layout for different screen sizes — critical for cross-platform), event-driven UI updates, scene/state management (menu → game → game over).

---

## Phase 8 — Upgrades & Depth

Turn a demo into a game people actually play.

- Let players **upgrade placed towers** (more damage, range, or fire rate) or **sell** them.
- Add **2–3 tower types** with distinct roles (single-target, area-of-effect, slow/support).
- Add **enemy variety** (fast/weak, slow/tanky, maybe a flying type that only some towers hit).
- Introduce **object pooling** properly now — reuse enemy and projectile objects instead of constantly instantiating/destroying them. This is a real performance skill that matters a lot on mobile and web, and it's exactly the kind of thing that signals competence to a reviewer.

**Concepts:** Object pooling (performance optimization), inheritance/interfaces for tower and enemy variants, balancing and tuning, designing for extensibility.

---

## Phase 9 — Polish & Feel ("Juice")

The difference between "student project" and "this feels good."

- Add **audio** (shots, hits, enemy deaths, UI clicks, background music).
- Add **visual feedback**: hit flashes, death effects/particles, muzzle flashes, floating damage or money numbers.
- Add **screen feedback**: subtle camera shake, tween/scale animations on placement and UI.
- Consider a **save/load system** so progress persists (JSON serialization is a clean, teachable approach).

**Concepts:** Unity Audio (AudioSource/Mixer), particle systems, tweening/animation, serialization for save/load, and the broad concept of "game feel."

---

## Phase 10 — Cross-Platform Build & Ship

Prove it runs everywhere, and get it in front of people.

- Build and test on your target platforms: **Windows/Mac** (easy), **WebGL** (great for portfolios — instantly playable in a browser), and **Android** (test touch input and performance on a real device).
- Watch for platform gotchas: input differences, WebGL memory limits, mobile performance (this is where your object pooling pays off), and UI scaling across aspect ratios.
- **Publish it:** upload the WebGL build to **itch.io** and push the clean, well-organized source to **GitHub**. A playable link plus a readable repo is worth more to your employability than any amount of "it's almost done" on your hard drive.

**Concepts:** Build pipelines and platform settings, platform-specific optimization, deployment, and presenting your work publicly.

---

## Guiding Principles Throughout

- **Always playable.** Never spend three days on a system with nothing to press Play on. If a phase is big, slice it smaller.
- **Prefabs and ScriptableObjects over hardcoding.** Data-driven design is a core Unity skill and makes iteration painless.
- **Decouple with events.** Don't let your UI reach into your GameManager every frame — have systems announce changes and let listeners react.
- **Commit often.** Small, frequent Git commits with clear messages. Your repo history is part of your portfolio.
- **Ship the imperfect version.** A finished, playable, slightly-rough game beats an unfinished ambitious one every single time — for learning *and* for getting hired.

---

## Suggested Learning Focus (for employability)

As you go, deliberately get comfortable with these — they're what separate a hireable Unity dev from someone who followed a tutorial:

1. **C# fundamentals** — classes, interfaces, inheritance, events/delegates, collections.
2. **ScriptableObjects** — Unity's data-driven design workhorse.
3. **Object pooling** — the performance pattern reviewers look for.
4. **Event-driven architecture** — decoupled, maintainable systems.
5. **The build pipeline** — actually shipping to multiple platforms.

Good luck — a well-built tower defense is one of the best "I understand game architecture" pieces you can put in a portfolio.
