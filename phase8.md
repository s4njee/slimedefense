# Phase 8: Upgrades & Depth

## Goal

Turn a working game into one worth replaying. Give the player more than one kind
of tower, something to spend money on besides new towers, and enemies that punish
building only one thing. Then stop instantiating and destroying everything at
runtime.

Pressing Play should now involve a decision the previous phases could not ask:
*which* tower, *where*, and *upgrade this one or build another*. Phase 6 made
money scarce, and scarcity is only interesting when there is more than one thing
to spend it on.

This is the largest phase in the roadmap, and it is four independent features
wearing one number. It is sliced into four parts below, each of which leaves the
game playable and each of which is its own commit.

## Prerequisites

Phase 7 must be complete:

- The HUD shows money, lives, and wave, and updates from events.
- A run can be started, won, lost, and restarted.
- `Main.unity` is in Build Settings — the restart depends on it, and this phase
  adds enough churn that you do not want to be debugging a broken reload at the
  same time.

Two loose ends from earlier phases come due here, and both are cheap now and
expensive later:

- **The slime's collider.** `Slime.prefab` still carries a sphere collider with
  radius `6.89` on a child scaled `200`. Every enemy variant below is made by
  duplicating that prefab, so an oversized detection volume stops being one
  mistake and becomes three.
- **The `hits` buffer.** `Tower.FindTarget` fills a fixed array of `32`. Phase 5
  flagged it as ample "before Phase 8 makes waves much denser." This is that
  phase.

## How This Phase Is Sliced

The roadmap's own guiding principle is *if a phase is big, slice it smaller*, and
this one is big enough that ignoring the advice means a week without pressing
Play. Four parts, in this order, each finishing at something you can play:

| Part | Adds | Why here |
| ---- | ---- | -------- |
| **A** | Tower types and a selection panel | The reason the other three matter |
| **B** | Upgrade and sell | Needs types to be worth choosing between |
| **C** | Enemy variety and wave groups | Needs towers worth countering |
| **D** | Object pooling | A refactor; wants every call site to exist first |

Pooling is last on purpose. It changes how every slime and projectile is created
and released, and doing that before the set of things being created is settled
means doing it twice. It is also the only part with no visible effect, which
makes it the worst possible place to be when you want to see progress.

---

# Part A — Tower Types

## A1. Add the Scripts

- `TowerDefinition.cs` — **new.** A ScriptableObject holding one tower's stats.
- `Tower.cs` — **edited.** Reads its stats from a definition; `Fire` becomes
  `virtual`.
- `SplashTower.cs` — **new.** Overrides `Fire` to damage everything near the hit.
- `FrostTower.cs` — **new.** Overrides `Fire` to slow instead of hurt.
- `Slime.cs` — **edited.** Gains `ApplySlow`.
- `TowerPlacer.cs` — **edited.** Builds the *selected* tower rather than the only
  one.

## A2. Create the Tower Definitions

Create three assets with **Create > SlimeDefense > Tower**, in
`Assets/Towers`:

| Asset | Cost | Range | Damage | Fire Rate | Role |
| ----- | ---- | ----- | ------ | --------- | ---- |
| `Tower_Pebble` | `50` | `6` | `3` | `1.5` | The Phase 5 tower. Reliable, cheap. |
| `Tower_Splash` | `90` | `5` | `2` | `0.7` | Hits everything within `1.5` of impact. |
| `Tower_Frost`  | `70` | `7` | `0` | `1.0` | No damage. Slows to `50%` for `2s`. |

Numbers that are deliberate rather than arbitrary: splash costs nearly two
pebbles and does less damage per hit, so it is only worth it against groups —
which is what Part C introduces. Frost does *no* damage at all, which forces it
to be a support pick rather than a strictly-better pebble tower. A support tower
that also deals damage is not a choice, it is an upgrade.

## A3. Build the Selection Panel

1. Add an empty `TowerPicker` under `Canvas`, anchored bottom-left.
2. Add three **Button - TextMeshPro** children, one per tower, labelled with the
   name and cost.
3. Put `TowerPicker.cs` on the parent and assign the buttons and definitions.
4. Assign the `TowerPlacer` so the picker can tell it what is selected.

Wire the buttons **in code**, with `onClick.AddListener`, and give the picker a
typed `Button[]` field. Phase 7 explains why at length; the short version is that
the Inspector's `On Click ()` accepts a script asset without complaint and
produces a button that animates and calls nothing.

The picker should also grey out anything the player cannot afford, which is the
first genuinely useful thing `MoneyChanged` gets subscribed to besides a label.

## A4. Play Test

- Each button selects its tower, and the selection is visible.
- Building uses the selected tower and charges *its* cost.
- Splash towers visibly damage several slimes with one shot.
- Frost towers slow slimes without killing them, and the slow wears off.
- Buttons for towers you cannot afford are greyed and do nothing.

---

# Part B — Upgrade and Sell

## B1. Add the Scripts

- `TowerDefinition.cs` — **edited.** Gains an array of upgrade levels.
- `Tower.cs` — **edited.** Gains `Level`, `Upgrade`, and `SellValue`.
- `BuildNode.cs` — **edited.** Gains `Clear`, so a sold tower frees its node.
- `TowerInspectorPanel.cs` — **new.** The panel shown when a placed tower is
  selected.
- `TowerPlacer.cs` — **edited.** Clicking an *occupied* node selects it instead
  of doing nothing.

`BuildNode` has held a `Tower` reference since Phase 4 with a comment saying
Phase 8's upgrade and sell actions would need it. This is the phase that collects
on that: the node already knows what stands on it, so selection is a lookup
rather than a search.

## B2. Model Upgrades as Data

An upgrade is another row of stats, not a multiplier:

```csharp
[Serializable]
public class TowerLevel
{
    public int Cost;
    public float Range;
    public float Damage;
    public float FireRate;
}
```

`TowerDefinition` holds `TowerLevel[] levels`, and `Tower.Level` indexes it.
Level 0 is the tower as built, so the array *is* the tower's stats and there is no
separate base-stats block to keep in sync.

The tempting alternative is `damage *= 1.25f` per level. It is fewer numbers to
author and it is worse: compounding multipliers make level 5 an accident rather
than a decision, balancing means solving an exponential instead of reading a
table, and there is no way to give a tower a level that trades range for fire
rate. Explicit rows are more typing and stay legible at level 6.

## B3. Selling, and What It Refunds

Sell returns a fraction of everything spent on the tower — the build cost plus
every upgrade — and the fraction is a serialized field, not a literal:

```csharp
public int SellValue => Mathf.FloorToInt(TotalSpent * definition.SellRefund);
```

Start `Sell Refund` at `0.7`. At `1.0` selling is free undo, and the optimal play
becomes rebuilding the whole board for every wave, which is tedious rather than
strategic. Below about `0.5` nobody ever sells and the feature is decoration.

`GameManager.AddMoney` is the refund path and needs no changes — Phase 6 built it
public specifically so this could exist without new economy code.

## B4. Play Test

- Clicking a placed tower opens the panel; clicking empty ground closes it.
- Upgrade is disabled at max level and when unaffordable.
- Upgrading visibly changes the range gizmo and the tower's behaviour.
- Selling refunds ~70% of everything spent and frees the node to build on again.
- Selling a tower mid-flight of its own projectile throws nothing.

---

# Part C — Enemy Variety

## C1. Add the Scripts

- `Slime.cs` — **edited.** Gains a damage multiplier and a flying flag.
- `WaveDefinition.cs` — **edited.** Becomes a list of groups.
- `WaveSpawner.cs` — **edited.** Loops groups within a wave.
- `Tower.cs` — **edited.** Skips flyers it cannot hit.

## C2. Create the Slime Variants

Duplicate `Slime.prefab` twice. **Fix the collider on the original first** — the
variants inherit it, and a detection volume larger than the map is not a thing to
copy three times.

| Prefab | Health | Speed | Reward | Life Cost | Notes |
| ------ | ------ | ----- | ------ | --------- | ----- |
| `Slime` | `10` | `3` | `10` | `1` | Baseline. |
| `Slime_Runner` | `6` | `6` | `8` | `1` | Outruns slow fire rates. |
| `Slime_Armored` | `40` | `1.8` | `35` | `3` | `Damage Multiplier` `0.5`. |

Armour is a **field on `Slime`**, not a subclass:

```csharp
public void TakeDamage(float amount)
{
    health -= amount * damageMultiplier;
    ...
}
```

Phase 5 predicted this exact line — "that is what lets Phase 8 add an armored
slime that halves incoming damage, without editing the projectile at all" — and
it holds because damage was always the slime's business to interpret.

## C3. The Rule for Subclass Versus Field

This phase adds variety twice and answers the question differently each time,
which is the whole lesson:

- **Enemies vary by data.** Health, speed, reward, armour, and life cost are all
  numbers. Three prefabs of one `Slime` class, no inheritance. A `SlimeRunner`
  subclass whose only content is different numbers is a class you cannot tune in
  the Inspector without recompiling.
- **Towers vary by behaviour.** A splash tower does not have a "splash amount"
  number that the pebble tower sets to zero; it does something structurally
  different when it fires. That is a `virtual Fire` and two overrides.

The test is whether the variant needs different *code* or different *values*. Get
it backwards and you end up with either a class explosion or a base class full of
flags that are meaningless for most instances.

Flying is the interesting edge: it is a `bool` on the slime and a `bool` on the
tower, checked in `FindTarget`. It is data on both sides, but it changes
targeting, so it is a field that a behaviour reads — not a subclass.

## C4. Wave Groups

`WaveDefinition`'s prefab/count/spacing trio becomes an array:

```csharp
[Serializable]
public class WaveGroup
{
    public Slime SlimePrefab;
    [Min(1)] public int Count = 5;
    [Min(0f)] public float Spacing = 1f;
    [Min(0f)] public float DelayBeforeGroup;
}
```

Phase 3's comment promised the spawner's loop would survive this "largely
intact," and it does: `RunWave` gains an outer loop over groups and the inner
per-slime loop is unchanged. That is the payoff of the wave data having been a
ScriptableObject from the start rather than fields on the spawner.

Rebalance the waves now that a wave can mix types. A reasonable curve: wave 1
baseline only, wave 2 baseline plus runners, wave 3 opens with runners and closes
with two armored slimes.

## C5. Rebalance the Economy

Part A changed the price list and Part C changed the payouts, so Phase 6's
arithmetic — 8 nodes, 25 slimes, 7 towers affordable — no longer describes
anything. Redo it rather than guessing:

1. Total the rewards for a perfect run across all waves.
2. Add `Starting Money`.
3. Divide by the *average* tower cost, not the cheapest.

Aim for the player affording roughly 60–70% of the nodes on a perfect run. Too
far above and money stops being a constraint; too far below and the map's build
nodes are decoration.

---

# Part D — Object Pooling

## D1. Add the Scripts

- `PrefabPool.cs` — **new.** A thin wrapper over Unity's `ObjectPool<T>`.
- `Slime.cs` — **edited.** Resets its own state and releases instead of
  destroying.
- `Projectile.cs` — **edited.** Same.
- `WaveSpawner.cs` — **edited.** Gets slimes from a pool.
- `Tower.cs` — **edited.** Gets projectiles from a pool.

## D2. Use Unity's Pool, Not Your Own

`UnityEngine.Pool.ObjectPool<T>` has shipped since 2021 and does exactly this job:

```csharp
pool = new ObjectPool<Slime>(
    createFunc: () => Instantiate(prefab, parent),
    actionOnGet: s => s.gameObject.SetActive(true),
    actionOnRelease: s => s.gameObject.SetActive(false),
    actionOnDestroy: s => Destroy(s.gameObject),
    collectionCheck: true,
    defaultCapacity: 32,
    maxSize: 256);
```

Writing your own `Stack<T>` version is a good exercise and the wrong choice for a
portfolio project. A reviewer who knows Unity recognises `ObjectPool<T>`
immediately and reads the five callbacks as intent; a hand-rolled pool has to be
read carefully to confirm it does the same thing, and it usually has a subtle bug
around double-release.

Leave `collectionCheck` on while developing. It throws the moment an object is
released twice, which is the pooling bug that otherwise shows up ten minutes later
as two towers sharing one projectile.

## D3. Pooled Objects Must Reset Themselves

This is the part that bites, and this project has three specific traps because
earlier phases added state that persists:

```csharp
// Slime
public void ResetForReuse()
{
    health = maxHealth;    // or a dead slime is reused at zero and dies instantly
    targetIndex = 0;
    despawning = false;    // Phase 7's double-despawn guard
    registered = false;    // Phase 7's live-count guard
    speed = baseSpeed;     // Part A's frost slow must not be permanent
}
```

`Destroy` gives you a clean object every time and hides every one of these.
Reuse does not. A pooled slime that keeps `despawning = true` from its last life
can never die again — it will walk to the goal, take a life, and refuse to
despawn.

Set `maxHealth` and `baseSpeed` from the serialized values in `Awake`, before
anything modifies them, so "reset" has a defined meaning.

## D4. The Payoff of Not Using OnDestroy

Phases 6 and 7 both put death logic in `Die` and `ReachGoal` and explicitly not in
`OnDestroy`, with a comment saying pooled slimes would stop being destroyed at
all. Part D is where that comes true: a released slime is deactivated, not
destroyed, so `OnDestroy` never runs and any payout or count living there would
silently stop happening.

Nothing has to change in `GameManager`. `AddMoney`, `LoseLife`,
`RegisterSlime`, and `UnregisterSlime` are all called from the two methods that
still run. That is what the earlier decision bought.

## D5. Measure It

Pooling is an optimization, and an optimization you did not measure is a story you
are telling yourself.

1. Open **Window > Analysis > Profiler** and watch the **GC Alloc** column during
   wave 3 before the change.
2. Make the change.
3. Watch it again.

You are looking for the per-frame allocation spikes from `Instantiate` and the
periodic garbage collection they cause to flatten. If they do not, something is
still allocating — most likely a `new WaitForSeconds` or a string built in an
`Update` somewhere — and finding that is more valuable than the pooling was.

---

## What Still Is Not Here

- **No audio, no particles, no hit feedback.** Phase 9, and this phase makes it
  more necessary: a splash tower with no impact effect is hard to read at all.
- **No save/load.** Phase 9.
- **No difficulty curve beyond three waves.** The wave assets are data; adding
  waves 4 through 10 is authoring, not programming, and is worth doing once the
  types are balanced.
- **No pooling of towers or projectile effects.** Towers are placed a dozen times
  a run, which is not churn worth managing.

## Phase 8 Completion Checklist

Phase 8 is complete when:

- Three `TowerDefinition` assets exist and three tower types can be built.
- The selection panel selects a tower and greys out unaffordable options.
- A splash tower damages multiple slimes with one shot.
- A frost tower slows slimes and the slow expires.
- Clicking a placed tower opens its panel; upgrading changes its stats and gizmo.
- Selling refunds a fraction of total spend and frees the node.
- Three slime prefabs exist and differ only in serialized values.
- The armored slime takes reduced damage with no change to `Projectile.cs`.
- A wave can mix slime types via groups.
- Slimes and projectiles come from `ObjectPool<T>` and are released, not
  destroyed.
- A pooled slime is reused with full health, no slow, and correct route position.
- `collectionCheck` is on and nothing double-releases.
- The Profiler shows GC allocation during wave 3 measurably lower than before.
- The economy has been recalculated against the new costs and rewards.
- Every part above was its own commit and the game was playable after each.

After this checklist is complete, Phase 9 adds audio, particles, hit feedback, and
save/load — the difference between a game that works and a game that feels good.
