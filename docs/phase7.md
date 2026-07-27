# Phase 7: UI & HUD

## Goal

Make the state visible and the game controllable. Put money, lives, and the wave
number on screen; replace the automatic first wave with a button the player
presses; and end the run with a screen that says what happened and offers to go
again.

Pressing Play should now produce something a stranger can sit down in front of.
Every number Phase 6 tracked has been real for a whole phase, and until now the
only way to read it was the Console — which is fine for the person who wrote it
and useless to everyone else.

This is also the phase where the events built in Phase 6 pay for themselves. The
HUD does not ask the `GameManager` for anything every frame; it is told when
something changes and does nothing in between.

There are still no tower types to choose between, so the roadmap's
tower-selection panel is deferred to Phase 8, where there will be something to
select. This phase is about the Canvas, anchors that survive a phone, event-driven
updates, and the end of a run.

## Prerequisites

Phase 6 must be complete:

- A `GameManager` object is in the scene, and its `Money`, `Lives`, and
  `IsGameOver` are correct in the Console during a run.
- `MoneyChanged`, `LivesChanged`, and `GameOver` fire, with only `WaveSpawner`
  subscribed.
- Building a tower spends its `Cost`; killing a slime pays its `Reward`; a slime
  reaching the goal takes a life.

Also, before the restart button can work at all: **`Main.unity` must be in Build
Settings.** `SceneManager.LoadScene` addresses scenes by their index in that
list, and a scene missing from it cannot be loaded even though it is the one
currently running. This project has already been bitten once by that list — an
early WebGL build shipped the empty `SampleScene` because it was the only entry —
so verify it before writing any restart code.

## 1. Add the Phase 7 Scripts

Phase 7 adds two scripts and edits four:

- `Hud.cs` — **new.** Owns the money, lives, and wave labels.
- `EndOfRunPanel.cs` — **new.** The won/lost screen and its restart button.
- `GameManager.cs` — **edited.** Gains a live slime count, a victory condition,
  and `Restart`.
- `WaveSpawner.cs` — **edited.** Announces which wave is running and when the
  last one has finished spawning.
- `Slime.cs` — **edited.** Registers and unregisters itself so something can
  count what is alive.
- `TowerPlacer.cs` — **edited.** Ignores pointer presses that landed on the HUD.

`WaypointRoute.cs`, `WaveDefinition.cs`, `BuildNode.cs`, `Tower.cs`, and
`Projectile.cs` do not change. Two full phases without touching `Tower.cs` is not
an accident — it holds its own numbers and answers questions about itself, so
systems can be built around it rather than into it.

## 2. Import TextMeshPro Essentials

The first time you create a text object, Unity offers to import **TMP Essential
Resources**. Accept it. It adds the default font asset and shaders under
`Assets/TextMesh Pro`, and text renders as magenta boxes without them.

Use TextMeshPro objects rather than the legacy `Text` component. Legacy `Text`
renders from a bitmap atlas and goes soft the moment it is scaled — which is
guaranteed here, since the Canvas Scaler exists precisely to scale everything.

## 3. Build the Canvas

1. **Click empty space in the Hierarchy to deselect first**, then select
   **GameObject > UI > Canvas**. Unity creates the `Canvas` and, alongside it, an
   `EventSystem` object.

   Deselecting first is not fussiness. Unity parents a new object to whatever is
   currently selected, and every previous phase has told you to create things
   under `Level` — so the habit built over six phases produces a Canvas nested
   inside a world object sitting a thousand units from the origin. A
   `Screen Space - Overlay` Canvas has to be a **root** object: that is where
   Unity drives its RectTransform to match the screen each frame. Nested, it
   keeps whatever local scale it was serialized with, which is typically zero,
   and every label under it renders at zero size — present, enabled, white, and
   completely invisible.

   If the HUD ever vanishes, check this first: select the `Canvas` and confirm
   its Rect Transform scale reads `1` and its width and height match the Game
   view.
2. **Select the `EventSystem` and read its Inspector.** With this project's
   Input System settings, it will be carrying a `Standalone Input Module` that
   cannot work, and Unity shows a button offering to replace it with
   `InputSystemUIInputModule`. Click it.
3. On the `Canvas`, set **Render Mode** to `Screen Space - Overlay`.
4. On the `Canvas Scaler` component:
   - **UI Scale Mode**: `Scale With Screen Size`
   - **Reference Resolution**: `1920 x 1080`
   - **Screen Match Mode**: `Match Width Or Height`
   - **Match**: `0.5`

Step 2 is the one that wastes an afternoon if skipped. The legacy module reads
the old `Input` class, which this project disables outright, so the symptom is a
HUD that renders perfectly and ignores every click — with nothing in the Console,
because nothing threw. It is the same constraint that shaped `TowerPlacer` in
Phase 4, arriving from the other direction.

`Constant Pixel Size`, the Canvas Scaler default, means a 40-pixel button is 40
pixels on every device: comfortable on a laptop, a speck on a phone. `Scale With
Screen Size` treats your layout as a 1920x1080 design and scales it to whatever
it lands on. `Match` at `0.5` splits the difference between fitting by width and
by height, which is what keeps a 16:9 layout sane on both an ultrawide monitor
and a 20:9 phone.

## 4. Lay Out the HUD

Create these as children of the `Canvas` (**GameObject > UI > Text -
TextMeshPro**):

| Object       | Anchor       | Content at start |
| ------------ | ------------ | ---------------- |
| `MoneyLabel` | top-left     | `Money: 100`     |
| `LivesLabel` | top-left     | `Lives: 10`      |
| `WaveLabel`  | top-right    | `Wave: 0 / 3`    |

Set each anchor with the anchor preset widget at the top-left of the Rect
Transform, and hold **Alt** while clicking a preset to move the object to that
corner at the same time. Give each one a margin of 20 to 40 units from its edges.

Then add an empty child of `Canvas` named `Hud`, and put the `Hud` script on it.
Drag the three labels into its fields.

The script is deliberately separate from the labels it drives. A `MonoBehaviour`
on each label, each subscribing to its own event, would work and would scatter
the HUD across three objects with three subscription lifetimes to get right. One
listener holding three references is easier to reason about and easier to delete.

## 5. Anchor Everything Before Testing Anything

Select the `Canvas` and switch the Game view to a phone aspect — `1080 x 2340`
is a reasonable Android target. Then check every HUD element is still on screen
and still legible.

An anchor is the answer to "what does this element stay attached to when the
screen changes shape." The default centre anchor means an element keeps its
offset from the middle of the screen, so a label placed at the top-left of a
1920x1080 design view slides toward the middle of a narrow screen and off the top
of a tall one. Anchoring it to the top-left corner instead means it stays 30
units from the corner on every device.

Get this right now, while there are four elements. It is a five-minute job today
and an afternoon of nudging once there is a tower-selection panel, an upgrade
panel, and a pause menu on top of it.

Phones with a notch or a rounded corner will still clip a corner-anchored label,
because the Canvas covers the whole display rather than the safe area. That is a
Phase 10 problem — it needs a real device to judge — and the fix is a small script
that insets a `RectTransform` to `Screen.safeArea`.

## 6. Replace Autostart With a Button

1. Select **GameObject > UI > Button - TextMeshPro**, rename it `StartWaveButton`,
   and anchor it to the bottom-centre.
2. Set its label to `Start Wave`.
3. On the `WaveSpawner` object, untick **Auto Start**.
4. Drag `StartWaveButton` into the `Start Wave Button` field on the `Hud`
   component. **Leave the button's own `On Click ()` list empty** — `Hud` wires
   the click itself.

`StartWaves` has been public since Phase 3, with a comment saying the Phase 7 HUD
button would call it. This is that button, and the method needs no changes — it
already refuses a second concurrent run and logs why.

The button hides itself once the run is underway, because `StartWaves` plays the
*whole* list — the gaps between waves are `Time Between Waves`, inside the
coroutine, not something the player presses through. A genuine wave-at-a-time
button would mean restructuring `RunWaves` to play one wave per call, which is a
Phase 8 conversation once wave pacing is a difficulty knob rather than a
constant.

Both this button and the restart button are wired with
`button.onClick.AddListener` in code, and the reason is worth stating because the
Inspector's `On Click ()` list is what most Unity UI uses and what you will read
in other people's projects constantly.

The Inspector version has two failure modes, both silent. The connection is
invisible to a text search, so renaming `StartWaves` turns the reference into a
no-op with no compiler error. And the object slot accepts the wrong thing without
complaint: drag `WaveSpawner.cs` from the Project window instead of the
`WaveSpawner` object from the Hierarchy — an easy slip, since both are called
WaveSpawner and one is right there in the file list — and Unity stores a
reference to the *script asset*. The Function dropdown then has no component
methods to offer, the method name is saved empty, and you get a button that
highlights, animates on press, and does nothing. Nothing is logged, because as
far as Unity is concerned you configured an empty event.

A typed `Button` field on the listener cannot be given the wrong object — Unity
rejects the drop — and renaming the handler is a compile error. That is worth
more here than matching the convention.

## 7. Build the End-of-Run Panel

1. Create a **UI > Panel** as a child of `Canvas`, named `EndOfRunPanel`.
2. Set its `Image` colour to something dark with an alpha around `180`, so the
   board stays visible behind it.
3. Add a **Text - TextMeshPro** child named `ResultLabel`, anchored centre.
4. Add a **Button - TextMeshPro** child named `RestartButton`, labelled
   `Play Again`, anchored below the label.
5. Add a **Canvas Group** component to the panel. `EndOfRunPanel` requires one
   and will add it for you, but adding it yourself makes the dependency visible
   in the Inspector.
6. Put `EndOfRunPanel.cs` on the panel and assign the label and button.
7. **Leave the panel active in the Hierarchy.** Its `Awake` hides it by setting
   the Canvas Group's `alpha`, `interactable`, and `blocksRaycasts` to zero and
   false.

Those last two steps matter more than they look. The obvious way to hide a panel
is `SetActive(false)`, and it is a trap here: a deactivated GameObject does not
run `OnEnable`, so the panel would unsubscribe from — or never subscribe to — the
very events that tell it to come back, and nothing would ever show it again. A
Canvas Group leaves the object alive and listening while making it invisible.

`blocksRaycasts` is the half people forget. Alpha zero alone leaves a
full-screen invisible sheet sitting over the board, swallowing every click meant
for a build node, and the resulting bug — placement stops working after the panel
is added, with nothing in the Console — is genuinely hard to trace back to a
transparent rectangle.

The restart button is wired in code, with `onClick.AddListener`, rather than
through the Inspector like `Start Wave`. Both approaches appear in this project on
purpose: the Inspector version is what you will read in other people's scenes,
and the code version is the one where renaming the method is a compiler error
instead of a button that silently stops working.

## 8. Wire the Placer to Ignore UI Taps

Phase 4 left a comment saying this guard belonged in Phase 7, and Phase 6 left it
alone. It is now load-bearing: the `Start Wave` button sits over the play area,
and without this, pressing it also builds a tower on whatever node is behind it.

In `TowerPlacer.UpdateHover`, before the raycast:

```csharp
// A press that landed on the HUD is not a press on the world behind it.
// Checked during hover rather than at press time so the node under the button
// does not highlight either — a node that lights up under the cursor and then
// refuses to build reads as a bug.
if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
{
    found = null;
}
```

Test this on a touchscreen specifically, not only with a mouse. `IsPointerOverGameObject`
is answered from the UI module's own view of the pointer, and on touch that view
can lag the raw pointer by a frame — the mouse case looks perfect while a tap
near the button's edge occasionally builds anyway. If it misbehaves on device,
the reliable fallback is testing the pointer position against the button's
`RectTransform` directly.

## 9. Tune the Fields

`Hud` holds nothing but references:

| Field              | Assigned to        | Meaning                                    |
| ------------------ | ------------------ | ------------------------------------------ |
| `Money Label`      | `MoneyLabel`       | Shows the current balance.                  |
| `Lives Label`      | `LivesLabel`       | Shows lives remaining.                      |
| `Wave Label`       | `WaveLabel`        | Shows wave progress.                        |
| `Start Wave Button`| `StartWaveButton`  | Hidden once the run is underway.            |
| `Spawner`          | `WaveSpawner`      | Empty falls back to the one in the scene.   |

`EndOfRunPanel`:

| Field           | Starting value        | Meaning                          |
| --------------- | --------------------- | -------------------------------- |
| `Result Label`  | `ResultLabel`         | Where the outcome is written.     |
| `Restart Button`| `RestartButton`       | Reloads the scene.                |
| `Victory Text`  | `All waves cleared.`  | Shown when the run is won.        |
| `Defeat Text`   | `The slimes got through.` | Shown when lives run out.     |

The two result strings are serialized fields rather than literals in the code, so
the wording is a designer's decision and not a recompile. That is the same
instinct that put wave counts on ScriptableObjects in Phase 3.

## 10. Play Test

Press **Play**. Nothing should spawn until you ask it to.

Expected behavior:

- The HUD reads `Money: 100`, `Lives: 10`, `Wave: 0 / 3` before anything happens.
- `Start Wave` spawns wave 1 and the wave label updates.
- Building a tower drops the money label immediately.
- Killing a slime raises it immediately.
- A slime reaching the goal drops the lives label.
- Pressing `Start Wave` while a wave is spawning does nothing bad.
- Clicking the button does not build a tower on the node behind it.
- Losing the last life shows the panel with the defeat text, and spawning stops.
- Clearing all three waves shows the panel with the victory text.
- `Play Again` restarts with full money and lives and no slimes on the board.
- The Console stays clean.

Then the cases that break UI specifically:

- Resize the Game view between 16:9, ultrawide, and a tall phone aspect. Every
  element stays on screen and inside its corner.
- Lose the last life **while several slimes are still walking.** The panel
  appears once, not once per slime.
- Kill the very last slime of the last wave. Victory, not silence.
- Let the very last slime of the last wave reach the goal and take your last
  life. That is a defeat, not a victory, and definitely not both.
- Restart, then immediately restart again from the second run's panel.

The last three are where end-of-run logic goes wrong, because they are the cases
where "the waves are finished" and "the run is over" arrive in the same frame.

## Subscribe, Then Seed

Every listener in this phase follows the same shape, and it is the one piece of
event-driven UI that is genuinely easy to get wrong:

```csharp
bool subscribed;

void OnEnable() => Subscribe();
void Start() => Subscribe();
void OnDisable() => Unsubscribe();

void Subscribe()
{
    if (subscribed || GameManager.Instance == null)
    {
        return;
    }

    subscribed = true;

    GameManager.Instance.MoneyChanged += OnMoneyChanged;
    GameManager.Instance.LivesChanged += OnLivesChanged;

    // Events report *changes*. A listener that subscribes after the value was
    // last set has missed it, and shows whatever the label happened to say in
    // the editor until the player earns a coin. So read the current value once,
    // here, and let the event handle everything after.
    OnMoneyChanged(GameManager.Instance.Money);
    OnLivesChanged(GameManager.Instance.Lives);
}

void Unsubscribe()
{
    if (!subscribed)
    {
        return;
    }

    subscribed = false;

    if (GameManager.Instance != null)
    {
        GameManager.Instance.MoneyChanged -= OnMoneyChanged;
        GameManager.Instance.LivesChanged -= OnLivesChanged;
    }
}
```

Subscribing from **both** `OnEnable` and `Start` is the part that is easy to get
wrong, and getting it wrong produces a HUD that looks perfect and never updates.
`OnEnable` can run before the `GameManager`'s `Awake`, and then `Instance` is
null. Checking for null and returning — the obvious defensive move — means the
listener silently never attaches, for the entire run. The money label then sits
on the text it was given in the editor, which is almost certainly the starting
money, so the bug is invisible until the first coin fails to appear.

`Start` is guaranteed to run after every `Awake` in the scene, so the second
attempt always finds the manager. The `subscribed` flag makes whichever call
arrives first the only one that does anything, and makes re-enabling the object
work as well.

Subscribe *and* seed. Miss the seeding and the HUD is correct for the whole game
except the first few seconds, which is the worst kind of bug: invisible in
testing, obvious to a first-time player.

Note the pairing has changed from Phase 6. `WaveSpawner` used `Start`/`OnDestroy`
because it never toggles and needed the manager to exist first. UI objects *do*
toggle, so they use `OnEnable`/`OnDisable`, which is the pairing that keeps a
hidden element from reacting to events it should be ignoring — and they add the
`Start` call above to cover the one case that pairing cannot handle on its own.

Wiring the button is the other half of this. Drag the **`WaveSpawner` object from
the Hierarchy** into the `On Click ()` object slot — not `WaveSpawner.cs` from the
Project window. Dropping the script *asset* there is an easy mistake and a
completely silent one: Unity accepts it, the Function dropdown then has no
component methods to offer, and the entry is saved with an empty method name. The
button looks wired, highlights on hover, and calls nothing. If the dropdown is
not offering `WaveSpawner > StartWaves ()`, you have the asset rather than the
object.

## Why the HUD Does Not Poll

The tempting version is three lines and no events:

```csharp
void Update()
{
    moneyLabel.text = $"Money: {GameManager.Instance.Money}";
    livesLabel.text = $"Lives: {GameManager.Instance.Lives}";
}
```

It works. It is also wrong in three ways that all get worse with time.

It does real work sixty times a second to display a number that changes twice a
wave. Assigning `TextMeshProUGUI.text` is not free even when the string is
identical — it re-tessellates the mesh — and string interpolation allocates every
frame, which is per-frame garbage of exactly the kind Phase 5 avoided in the
tower's targeting query.

It reaches from the UI into the game state, so the HUD cannot be tested or
reasoned about without a live `GameManager`, and the dependency points the wrong
way: presentation should not be something the simulation is unaware of *and*
subject to.

And it silently does nothing when the value stops living where it used to. The
event version fails loudly at the subscription, in one place.

## Knowing When the Run Is Won

Losing was easy in Phase 6 — lives hit zero, and that is a single number. Winning
needs two facts at once: the spawner has no more slimes to send, *and* none of
the ones it sent are still walking.

The count is kept by `GameManager`, incremented and decremented by the slimes
themselves:

```csharp
public int SlimesAlive { get; private set; }

public void RegisterSlime()
{
    SlimesAlive++;
}

public void UnregisterSlime()
{
    SlimesAlive = Mathf.Max(0, SlimesAlive - 1);
    CheckForVictory();
}
```

`Slime` calls `RegisterSlime` from `Start` and `UnregisterSlime` from both `Die`
and `ReachGoal` — the same two methods, and for the same reason as the money and
the life. Not from `OnDestroy`, which would count down during scene teardown and
when Play mode stops, and which stops firing at all in Phase 8 when slimes are
pooled instead of destroyed.

`WaveSpawner` supplies the other half by announcing that its coroutine has run
out of waves:

```csharp
// At the end of RunWaves, where the handle is already being cleared.
runningWaves = null;
WavesFinished?.Invoke();
```

And the check itself, which has to be careful about the order things arrive in:

```csharp
void CheckForVictory()
{
    // Defeat wins ties. The last slime of the last wave reaching the goal and
    // taking the last life satisfies "no slimes left" and "no waves left" in the
    // same frame as game over, and announcing a victory on top of a defeat is
    // worse than announcing nothing.
    if (IsGameOver || runEnded || !allWavesSpawned || SlimesAlive > 0)
    {
        return;
    }

    runEnded = true;
    Victory?.Invoke();
}
```

`CheckForVictory` runs on both inputs — when a slime unregisters and when the
spawner reports it is done — because either can be the last one to arrive. A
check that only runs on slime death misses the run where the player kills
everything before the final wave finishes spawning; a check that only runs when
the spawner finishes misses the far more common case of the last slime dying
afterward.

## Restarting Is a Scene Reload

The restart button reloads the current scene and nothing else:

```csharp
public void Restart()
{
    // Time.timeScale is not reset by a scene load. If a pause button is ever
    // added, forgetting this line means restarting into a frozen game — a bug
    // that looks like the reload failed.
    Time.timeScale = 1f;

    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}
```

This is why Phase 6 refused `DontDestroyOnLoad` on the `GameManager`. A manager
that survived the reload would carry the dead run's money, lives, and
`IsGameOver` into the new one, and every one of those would have to be reset by
hand — reimplementing, badly, what the scene load already does perfectly. Letting
the manager die with its run means the reset is free and cannot be incomplete.

It is also why this phase still uses one scene. The roadmap mentions menu → game
→ game over as scene management, and that is a real pattern, but a separate menu
scene here would buy a title screen and cost a way to carry a difficulty choice
between scenes — a problem this game does not yet have. Panels inside the running
scene are less machinery for the same result. When Phase 10 wants a title screen
in front of the itch.io build, the additive scene or the extra panel can be
argued about then, with a finished game to argue about.

## Why uGUI and Not UI Toolkit

Unity ships two UI systems and this project uses uGUI — the Canvas, `RectTransform`,
and TextMeshPro stack.

UI Toolkit is the newer one, styles with USS, and is genuinely nicer for dense
editor tooling. For runtime game UI it is still the less-travelled path, and two
things here specifically favour uGUI: the `EventSystem`, which is what makes the
`IsPointerOverGameObject` guard a one-liner rather than a hand-rolled hit test,
and world-space canvases, which are how Phase 9's floating damage numbers and any
per-tower upgrade popup will be positioned in the world rather than on the screen.

There is also the plain employability argument the roadmap makes. Canvas, anchors,
and Canvas Scaler are what almost every existing Unity project you will be asked
to work on uses, and being fluent in the thing that is already everywhere beats
being fluent in the thing that is newer.

## What the UI Still Does Not Do

- **No tower selection.** There is one tower type, so a selection panel would be
  a panel with one button. Phase 8 adds the types and the panel together.
- **No upgrade or sell UI.** Selecting a placed tower and acting on it is Phase 8,
  and `BuildNode` has held a `Tower` reference since Phase 4 waiting for it.
- **No pause.** `Time.timeScale` is the tool, the reset above is the trap, and
  neither is needed until there is something worth pausing for.
- **No audio, no juice.** A button that does not click and a coin that does not
  chime is Phase 9's whole subject.
- **No safe-area handling.** Needs a real device, so it belongs with the Phase 10
  build work.

## Phase 7 Completion Checklist

Phase 7 is complete when:

- `Hud.cs` and `EndOfRunPanel.cs` exist, and the edited scripts compile with no
  console errors.
- A `Canvas` is in the scene with `Scale With Screen Size` and a `1920x1080`
  reference resolution.
- The `EventSystem` uses `InputSystemUIInputModule`, not the legacy module.
- Money, lives, and wave are on screen and correct **before** the first wave.
- Each label updates on change and nothing reads `GameManager` in an `Update`.
- `Auto Start` is off and `Start Wave` begins the run.
- Pressing a UI button never builds a tower behind it.
- Losing the last life shows the defeat panel exactly once.
- Clearing every wave shows the victory panel.
- A last slime that takes the last life is a defeat, not a victory.
- `Play Again` restarts to full money, full lives, an empty board, and wave 0.
- The HUD stays on screen and legible at 16:9, ultrawide, and a tall phone aspect.
- `Main.unity` is in Build Settings, so the reload can find it.

After this checklist is complete, Phase 8 adds tower types, upgrades, enemy
variety, and object pooling — the first phase with something for a selection panel
to select.
