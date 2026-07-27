# Phase 6: Economy & Player State

## Goal

Give the combat loop stakes. Killing a slime pays money, building a tower spends
it, and a slime that reaches the goal costs a life. Run out of lives and the run
is over.

Pressing Play should give you a budget you can exhaust, a reason to care where
the first two towers go, and a way to lose. This is the phase where the game
stops being a sandbox — Phase 5 made towers work, and towers that work and cost
nothing mean the only strategy is to fill every node and stop thinking.

There is still no HUD. Money and lives change in the Console and in the
Inspector, not on screen; that is Phase 7, and it is deliberately the next phase
because state you cannot see is only tolerable for one commit. This phase is
about a single source of truth, events that announce changes, and a game over
that other systems can react to.

## Prerequisites

Phase 5 must be complete:

- Towers detect slimes inside `Range`, fire projectiles, and kill them.
- `Slime.Die` and `Slime.ReachGoal` both exist and both just `Destroy` the
  slime, each with a comment marking where Phase 6 wires in.
- `Tower.Cost` holds `50` and nothing reads it.
- `TowerPlacer.HandlePress` has a comment marking where currency gets checked.

Those three comments are the whole map for this phase. Every edit below lands on
one of them.

## 1. Add the Phase 6 Scripts

Phase 6 adds one script and edits three:

- `GameManager.cs` — **new.** Owns money, lives, and whether the run is over.
- `Slime.cs` — **edited.** `Die` pays, `ReachGoal` costs a life.
- `TowerPlacer.cs` — **edited.** Checks the price and spends it.
- `WaveSpawner.cs` — **edited.** Stops spawning when the run ends.

`WaypointRoute.cs`, `WaveDefinition.cs`, `BuildNode.cs`, `Tower.cs`, and
`Projectile.cs` do not change. Notably `Tower` does not change: it already has a
`Cost` field and a public accessor, and a tower has no business knowing whether
the player could afford it. The placer reads the price off the prefab.

## 2. Add the GameManager to the Scene

1. Right-click `Level` in the Hierarchy and select **Create Empty**.
2. Rename the new child to `GameManager`.
3. Select it, choose **Add Component**, and add the `GameManager` script.

One object, at the scene root of `Level`, created before Play like `TowerPlacer`
and `WaveSpawner`. It is not a prefab and it is not spawned at runtime — there is
exactly one, it exists for the whole scene, and putting it in the Hierarchy by
hand is what makes its starting values editable in the Inspector.

Do **not** tick anything resembling *Don't Destroy On Load*. The manager holds
the state of *this run* — the money you have now, the lives you have left. Phase
7 adds a menu scene and a restart, and a manager that survived the scene reload
would carry a dead run's numbers into the next one. Reloading the scene is the
restart, and a fresh manager is the point.

## 3. Give the Slime a Reward

On `Assets/Prefabs/Slime.prefab`, two new fields appear on the `Slime` component:

| Field        | Starting value | Meaning                                     |
| ------------ | -------------- | ------------------------------------------- |
| `Reward`     | `10`           | Money paid to the player when it dies.      |
| `Life Cost`  | `1`            | Lives taken when it reaches the goal.       |

`Life Cost` is a field rather than a hardcoded `1` because Phase 8 adds enemy
variety, and a boss slime that costs five lives is a number on a prefab, not a
new code path. The same reasoning put `Health` on the prefab in Phase 2 before
anything could subtract from it.

## 4. Tune the Numbers

On the `GameManager` object:

| Field            | Starting value | Meaning                                       |
| ---------------- | -------------- | --------------------------------------------- |
| `Starting Money` | `100`          | The budget at the start of a run.              |
| `Starting Lives` | `10`           | Leaks allowed before the run ends.             |
| `Log Changes`    | ticked         | Prints money and lives to the Console.         |

`Log Changes` is scaffolding with an expiry date. It exists because this phase
adds state with no way to see it, and it comes out in Phase 7 when the HUD makes
it redundant. Naming it as a field rather than sprinkling bare `Debug.Log` calls
through the code makes it one tick to silence and one deletion to remove.

The starting numbers are chosen against the actual level, and they are worth
following through:

- The map has **8** build nodes.
- The three Phase 3 waves send **5**, **8**, and **12** slimes — **25** total.
- At `10` per kill, a perfect run earns **250**, for **350** total with the
  starting budget.
- At `50` per tower, that is **7** towers, on 8 nodes, and only if nothing gets
  through.

So the map cannot be filled, ever, and every tower after the second is paid for
by the wave before it. That is the answer to the complaint at the end of Phase 5
— that towers being free and unlimited made placement pointless. Scarcity is
what turns *where do I build* into a question with a wrong answer.

Lives at `10` against 25 slimes means building nothing loses the run partway
through wave 2, which is soon enough to feel like a rule rather than an
afterthought.

Tune these two numbers first when the game feels wrong. They are the difficulty
curve, they are on one object, and nothing else has to change to move them.

## 5. Play Test

Press **Play** and watch the Console.

Expected behavior:

- The starting money and lives are logged once at startup.
- Building a tower subtracts `50` and logs the new total.
- The third tower cannot be built until slimes have been killed for it.
- Clicking a node you cannot afford does nothing — no tower, no charge, no
  error.
- Killing a slime adds `10`.
- A slime reaching the goal subtracts a life and logs it.
- At zero lives, one game-over line is logged, spawning stops, and clicking
  nodes no longer builds.
- The Console stays clean otherwise.

Then test the cases where the money is most likely to leak:

- Click an **occupied** node while broke, and while rich. Neither should change
  the balance.
- Click the terrain and the sky repeatedly. The balance should not move.
- Spend down to exactly `0` and confirm the balance never goes negative.
- Let several slimes reach the goal within a second of each other at 1 life
  remaining. **Game over should be announced exactly once.**
- After game over, let the slimes still walking finish their route. Nothing
  should throw and no second game over should appear.

That fifth case is this phase's equivalent of Phase 5's mid-flight target death.
Waves are dense by wave 3, the goal takes several slimes in quick succession, and
an unguarded game over fires once per arriving slime.

## The Singleton, Used Carefully

Everything in this phase asks the same object the same question, so that object
is reachable statically:

```csharp
public static GameManager Instance { get; private set; }

void Awake()
{
    if (Instance != null && Instance != this)
    {
        Debug.LogError($"{name}: a second GameManager is in the scene. Destroying this one.", this);
        Destroy(gameObject);
        return;
    }

    Instance = this;

    Money = startingMoney;
    Lives = startingLives;
}

void OnDestroy()
{
    // Only clear the static if this is the instance it points at. A duplicate
    // destroying itself in Awake must not null out the real one on its way out.
    if (Instance == this)
    {
        Instance = null;
    }
}
```

The roadmap says *the singleton pattern (used carefully)*, and the care is in
three specific places.

**Assignment happens in `Awake`, and every reader waits until `Start`.** Unity
runs every `Awake` in the scene before it runs any `Start`, so a manager that
claims `Instance` in `Awake` is guaranteed to be there for anything that looks
it up in `Start` — regardless of Hierarchy order, which is otherwise
unspecified. Look up `Instance` from another script's `Awake` and it works or
does not depending on which object Unity happened to initialize first, which is
the least debuggable class of bug there is. The heavier fix, **Project Settings
> Script Execution Order**, exists and is worth knowing about, but it is a
project-wide setting used to paper over a two-line convention.

**A second manager is an error, not a silent overwrite.** The common version of
this pattern quietly destroys the newcomer. That hides a real mistake — two
managers means two sets of starting values, and whichever one lost is still
referenced by anything that cached it.

**Nothing caches `Instance` in a field.** Read it at the moment of use. A cached
reference outlives the object it points at, and the fake-null trap from Phase 5
applies here too: a destroyed manager reports `== null` while the cached field
still looks perfectly valid to `?.`.

Which is the one syntax rule worth stating outright:

```csharp
// Right: Unity's overloaded == knows about destroyed objects.
if (GameManager.Instance != null)
{
    GameManager.Instance.AddMoney(reward);
}

// Wrong: ?. is a plain null check. It skips Unity's lifetime check entirely and
// treats a destroyed manager as alive.
GameManager.Instance?.AddMoney(reward);
```

Unity's own analyzer flags the second form (UNT0008) for exactly this reason.

## Events Instead of Polling

Money and lives are read-only from the outside, and every change goes through one
method that raises an event:

```csharp
public int Money { get; private set; }

/// <summary>Raised with the new balance whenever money changes.</summary>
public event Action<int> MoneyChanged;

public void AddMoney(int amount)
{
    if (amount <= 0)
    {
        return;
    }

    SetMoney(Money + amount);
}

/// <summary>
/// Spends <paramref name="amount"/> and returns false without spending anything
/// if the player cannot afford it. Callers that need to know before acting can
/// ask <see cref="CanAfford"/> first.
/// </summary>
public bool TrySpend(int amount)
{
    if (!CanAfford(amount))
    {
        return false;
    }

    SetMoney(Money - amount);
    return true;
}

public bool CanAfford(int amount) => Money >= amount;

void SetMoney(int value)
{
    // Clamped here rather than at each call site, because "money cannot go
    // negative" is a property of money, not a thing every caller remembers.
    Money = Mathf.Max(0, value);

    if (logChanges)
    {
        Debug.Log($"Money: {Money}");
    }

    // ?. on a C# event is the correct idiom and unrelated to the Unity object
    // rule above: this is a delegate, and null means nobody is listening.
    MoneyChanged?.Invoke(Money);
}
```

One private setter is the whole reason this holds together. Every path that
changes money — a kill, a purchase, and Phase 8's sell refund — funnels through
`SetMoney`, so the clamp, the log, and the event happen exactly once each and
cannot be forgotten by a new caller.

`TrySpend` returning a `bool` rather than throwing or logging is what lets the
placer treat *cannot afford* as an ordinary answer. The player clicking a node
they cannot pay for is not an error; it is Tuesday.

Nothing subscribes to `MoneyChanged` yet, and that is fine. It costs three lines
now and saves the Phase 7 HUD from doing this in `Update`:

```csharp
// What Phase 7 must not do.
void Update()
{
    moneyLabel.text = GameManager.Instance.Money.ToString();
}
```

That version rebuilds a text mesh sixty times a second to show a number that
changes maybe twice a wave, and it reaches into the manager from the UI, so the
two can never be tested apart. The event version updates on the frames the value
actually moved. The roadmap calls this out as a hallmark of clean Unity code, and
this is the phase where the hooks get built even though the listener does not
exist yet.

Subscribers pair `Start` with `OnDestroy`, not `OnEnable` with `OnDisable`:

```csharp
void Start()
{
    // Start, not OnEnable: every Awake in the scene has finished by now, so
    // Instance is guaranteed to be set. OnEnable can run before the manager's
    // Awake and find nothing there.
    if (GameManager.Instance != null)
    {
        GameManager.Instance.GameOver += OnGameOver;
    }
}

void OnDestroy()
{
    if (GameManager.Instance != null)
    {
        GameManager.Instance.GameOver -= OnGameOver;
    }
}
```

`OnEnable`/`OnDisable` is the more common pairing and the right habit for objects
that get toggled — but it loses the race against a singleton that has not
initialized yet. Nothing in this scene toggles, so the safe pair wins. The
unsubscribe is not optional either: an event holds a strong reference to its
subscriber, so a destroyed listener that never unsubscribed stays alive in the
manager's invocation list and throws the moment the event fires.

## Who Spends the Money

The purchase lives in `TowerPlacer`, and the order of the three steps is the
whole design:

```csharp
if (hovered.IsOccupied)
{
    return;
}

int price = towerPrefab.Cost;

// Ask before building. The alternative — spend, build, refund on failure — is a
// second code path that has to stay in sync with the first one forever.
if (!GameManager.Instance.CanAfford(price))
{
    return;
}

Tower built = hovered.Place(towerPrefab);

// Place returns null when it refuses. Spending before this line charges for
// towers that were never built, and the balance drifts down over a long run in
// a way that looks like a rounding bug and is not.
if (built == null)
{
    return;
}

GameManager.Instance.TrySpend(price);
```

Check, build, then charge. The player is never charged for a tower that does not
exist, and there is no refund path to keep correct.

The price comes from `towerPrefab.Cost` — read off the prefab asset before
anything is instantiated. `Tower` is untouched by this phase precisely because
this works: the prefab already carries its own price tag, and a tower type added
in Phase 8 arrives with its cost attached rather than needing an entry in a
lookup table somewhere in the placer.

Placement is also the one system that should refuse to work after a loss, so the
same method returns early on `GameManager.Instance.IsGameOver`. Building towers on a board
that has already been lost is the sort of thing that only reads as a bug.

`BuildNode` is deliberately not involved. A node knows whether it is occupied and
what color to be; it does not know what things cost. Phase 8's sell and upgrade
actions will go through the placer for the same reason.

## Why the Slime Pays, Not the Projectile

Phase 5 left two methods that did the same thing and promised they would diverge.
This is the divergence:

```csharp
void Die()
{
    if (GameManager.Instance != null)
    {
        GameManager.Instance.AddMoney(reward);
    }

    // Phase 9 will spawn a death effect here.
    Destroy(gameObject);
}

void ReachGoal()
{
    if (GameManager.Instance != null)
    {
        GameManager.Instance.LoseLife(lifeCost);
    }

    Destroy(gameObject);
}
```

The payout is on the slime, not on the projectile that killed it, for the same
reason `TakeDamage` is on the slime: the killer should not need to know what its
victim is worth. A Phase 8 area-of-effect tower that kills six slimes at once pays
out six rewards without a line of new economy code, and a slime type worth `40`
is a number on a prefab.

It also must not be in `OnDestroy`. That is the tempting shortcut — every death
destroys the object, so why not pay there — and it is wrong twice over.
`OnDestroy` fires when the scene unloads and when Play mode stops, so leaving Play
mid-wave would pay out for every slime still walking, into a manager that may
already be gone. Death is a game event; object destruction is a memory event.
They coincide today and will stop coinciding in Phase 8, when pooled slimes are
returned to a pool instead of destroyed at all.

## Game Over Without a Screen

Losing the last life ends the run, and the guard against ending it twice is the
first line:

```csharp
public void LoseLife(int amount = 1)
{
    // Wave 3 sends slimes 0.7 seconds apart, so several can reach the goal in
    // the same breath. Without this, each one announces the loss again.
    if (IsGameOver || amount <= 0)
    {
        return;
    }

    Lives = Mathf.Max(0, Lives - amount);

    if (logChanges)
    {
        Debug.Log($"Lives: {Lives}");
    }

    LivesChanged?.Invoke(Lives);

    if (Lives == 0)
    {
        IsGameOver = true;
        Debug.Log("Game over.");
        GameOver?.Invoke();
    }
}
```

What actually happens on game over is deliberately small. `WaveSpawner` hears the
event and calls its own `StopWaves`, which it has had since Phase 3 and which has
been unused until now. `TowerPlacer` checks `IsGameOver` and stops building.
Slimes already on the route keep walking, and reaching the goal costs nothing
because `LoseLife` returns immediately.

Note what is *not* here: `Time.timeScale = 0f`. Freezing time is the one-line
version of game over and it is a poor fit. It stops coroutines, so the Phase 7
game-over screen cannot animate in and a restart button cannot wait a frame for
anything; it is global, so it also stops systems that have no business caring;
and it is a mode that must be un-set on every exit path, which is how projects
end up with a menu that opens frozen. Each system deciding for itself what
"stopped" means is more code and stays correct. Phase 7 owns the screen, the
restart, and whatever pausing turns out to mean.

There is no win condition either. Surviving all three waves ends with the spawner
finishing quietly, exactly as it has since Phase 3 — `RunWaves` clears its handle
and returns. Victory needs a "the last slime is gone" check, which needs a live
count of slimes on the map, which is Phase 7's job alongside the screen that would
announce it.

## Why Money and Lives Are Ints

Both are `int`, and `Health` and `Damage` stay `float`.

Money and lives are counted things — you have 3 lives, never 2.9997. Health is a
measured thing that gets subtracted from in fractions and compared against zero.
Float money is a specific and unpleasant bug: `50.000004` in the balance means
`CanAfford(50)` says no while a HUD rounding to whole numbers displays `50`, and
the player is looking at a purchase the game insists they cannot make.

`Reward` is an `int` for the same reason. When Phase 8 adds a tower upgrade that
increases damage by a percentage, the fractional value belongs on the damage, not
on the currency.

## What the Economy Still Does Not Do

Money and lives change with nothing on screen to show it. That is the honest cost
of splitting Phase 6 from Phase 7, and it is the right split — a HUD built on top
of an economy that does not work yet is two systems failing at once, and the
events added here mean the HUD is subscription and layout rather than logic.

Also still missing, and each landing where the roadmap puts it:

- **Selling and upgrading towers.** Phase 8. `AddMoney` is the refund path and
  already exists.
- **A win screen, a lose screen, and a restart.** Phase 7.
- **Per-wave bonuses and interest.** Not planned, but `AddMoney` is where they
  would go.
- **Persistence between sessions.** Phase 9, along with save/load.

## Phase 6 Completion Checklist

Phase 6 is complete when:

- `GameManager.cs` exists, and `Slime.cs`, `TowerPlacer.cs`, and `WaveSpawner.cs`
  compile with no console errors.
- A single `GameManager` object is in the scene with `Starting Money` and
  `Starting Lives` set.
- `Assets/Prefabs/Slime.prefab` has `Reward` and `Life Cost` fields.
- Building a tower spends its `Cost`, and the balance is logged when it changes.
- A node that cannot be afforded builds nothing and charges nothing.
- Clicking an occupied node never changes the balance.
- Killing a slime adds its `Reward`.
- A slime reaching the goal subtracts its `Life Cost`.
- Money never goes below zero.
- Reaching zero lives announces game over exactly once, even with several slimes
  arriving together.
- Waves stop spawning and towers stop building after game over.
- `MoneyChanged`, `LivesChanged`, and `GameOver` are public events that nothing
  subscribes to yet except `WaveSpawner`.
- No UI, Canvas, or HUD code has been added yet.

After this checklist is complete, Phase 7 builds the HUD on top of these events,
adds a start-wave button and a game-over screen, and finally makes the numbers
this phase tracks visible to the person playing.
