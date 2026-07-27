# Phase 5: Towers That Shoot

## Goal

Close the combat loop. Give the tower a detection radius, a rule for choosing
which slime to shoot, and a fire rate. Spawn a projectile that travels to its
target and deals damage. Slimes lose health, and at zero they die.

Pressing Play should let you build a tower beside the path and watch it kill
things. This is the first phase where the player's decision — *where to put that
tower* — changes the outcome, and the first phase where a slime can fail to
reach the goal.

There is still no money, no lives, and no game over. Killing a slime pays
nothing and letting one through costs nothing; that is Phase 6. This phase is
about detection, target selection, and a damage system.

## Prerequisites

Phase 4 must be complete:

- `Assets/Prefabs/BuildNode.prefab` and `Assets/Prefabs/Tower.prefab` exist,
  and a `TowerPlacer` in the scene builds towers on nodes.
- Waves spawn from Phase 3 and walk the route without input.
- The `Range` field on `Tower` is still decorative — a gizmo and nothing more.

By the end of this phase `Range` stops being decorative, which is the clearest
single sign that Phase 5 landed.

## 1. Add the Phase 5 Scripts

Phase 5 adds one script and edits two:

- `Projectile.cs` — **new.** Travels toward a slime and deals damage on arrival.
- `Tower.cs` — **edited.** Gains detection, targeting, and firing.
- `Slime.cs` — **edited.** Gains `TakeDamage`, death, and a way for towers to
  ask how far along the route it is.

`WaypointRoute.cs`, `WaveDefinition.cs`, `WaveSpawner.cs`, `BuildNode.cs`, and
`TowerPlacer.cs` do not change. Placement is finished and this phase does not
reach into it.

## 2. Create the Slime Layer

Detection has the same problem placement had in Phase 4, for the same reason. A
sphere query against everything finds the terrain, the build nodes, other
towers, and the slimes, and then has to sort out which is which. A layer mask
answers the question before it is asked.

1. Select **Edit > Project Settings > Tags and Layers**.
2. Find the first empty **User Layer** slot after `BuildNode`.
3. Name it `Slime`.

Detection filtered by layer stays correct when Phase 9 scatters decorative rocks
across the map. Detection filtered by tag, or by calling
`GetComponent<Slime>()` on every hit, does not — it just gets slower and more
fragile with every prop added.

## 3. Give the Slime a Collider

Phase 2 removed the slime's collider, and the reasoning at the time was sound:
nothing needed to hit it. That changes here. A tower finds slimes with a physics
query, and a physics query finds colliders.

On `Assets/Prefabs/Slime.prefab`:

1. Select **Add Component** and add a **Sphere Collider**.
2. Tick **Is Trigger**.
3. Set **Radius** so it roughly wraps the model — around `0.5` for the Blue
   Pebble mesh, adjusted by eye.
4. Set the prefab's **Layer** to `Slime`, and accept the prompt to apply the
   layer to child objects.

**Is Trigger** matters. A solid collider participates in collision resolution:
slimes would shove each other off the path, and a projectile would bounce off
instead of passing through. A trigger is found by queries while pushing nothing
around, which is exactly what is wanted — the slime's position is decided by
`Slime.Update` walking the route, and physics should have no vote.

Set the radius by what the tower should *feel* like it reaches, not by what
wraps the mesh most tightly. Detection measures to the collider, so an
over-large radius quietly extends every tower's range.

## 4. Create the Projectile Prefab

1. Select **GameObject > 3D Object > Sphere**.
2. Rename it to `Projectile`.
3. Set its Transform Scale to `0.2, 0.2, 0.2`.
4. Remove the **Sphere Collider**. The projectile does not use physics to find
   its target — it flies at a remembered `Slime` and deals damage when it
   arrives. A collider here would only find things it should ignore.
5. Select **Add Component** and add the `Projectile` script.
6. Optionally create a material in `Assets/Materials` named `ProjectileMaterial`
   and assign it, so the shot reads against the grass.

Drag `Projectile` into `Assets/Prefabs` to make it a prefab, then delete the
copy left in the Hierarchy. Like slimes and towers, every projectile is created
at runtime.

## 5. Wire Up the Tower Prefab

On `Assets/Prefabs/Tower.prefab`:

1. Drag `Assets/Prefabs/Projectile.prefab` into the `Projectile Prefab` field.
2. Set `Slime Mask` to the `Slime` layer only. Clear every other entry.
3. Optionally create an empty child named `Muzzle`, positioned at the top of the
   tower, and assign it to `Fire Point`. Leave it empty to fire from the tower's
   own origin — which sits at its base, so shots appear to come out of the
   ground.

## 6. Tune the Fields

`Tower` gains four fields alongside the two it already had:

| Field               | Starting value | Meaning                                   |
| ------------------- | -------------- | ----------------------------------------- |
| `Cost`              | `50`           | Still unused. Phase 6 spends it.          |
| `Range`             | `6`            | Detection radius. No longer decorative.   |
| `Damage`            | `3`            | Dealt per projectile that lands.          |
| `Fire Rate`         | `1.5`          | Shots per second.                         |
| `Projectile Prefab` | `Projectile`   | What gets fired.                          |
| `Slime Mask`        | `Slime`        | Which layers detection considers.         |

`Projectile` has two of its own:

| Field      | Starting value | Meaning                                  |
| ---------- | -------------- | ---------------------------------------- |
| `Speed`    | `20`           | Units per second toward the target.      |
| `Lifetime` | `3`            | Seconds before it gives up and despawns. |

Starting numbers worth understanding rather than copying: slime `Health` is
`10`, so at `3` damage a slime takes four hits. At `1.5` shots per second that
is about 2.7 seconds of sustained fire. A slime at `Speed` `3` crosses a
`6`-unit radius in roughly four seconds passing straight through the middle, and
far less if it clips the edge. So one tower kills a slime it catches squarely
and loses one that grazes it — which is the tension that makes tower *placement*
the interesting decision. If one tower comfortably kills everything, there is no
reason to build a second, and the game is solved before Phase 6 starts.

`Lifetime` is a safety net, not a tuning knob. A projectile whose target dies
mid-flight, or that somehow never arrives, has to despawn on its own or it flies
forever and leaks.

## 7. Play Test

Press **Play**. Build a tower next to the path.

Expected behavior:

- A slime entering the tower's range gets shot at.
- Projectiles travel visibly from the tower to the slime, not instantly.
- After four hits a slime disappears.
- A slime that stays outside the range is ignored.
- The tower stops firing when nothing is in range and resumes when something
  enters.
- Slimes that survive still reach the goal and despawn as before.
- The Console stays clean.

Then test the awkward cases, because a combat system that has only been watched
from the front is not yet tested:

- Let a slime die *while a projectile is mid-flight* toward it. Nothing should
  throw, and no `MissingReferenceException` should appear.
- Build two towers whose ranges overlap. Both should fire, and the slime should
  die faster — not take damage twice per hit.
- Select a firing tower during Play and confirm the gizmo sphere matches where
  it actually reaches.
- Let a whole wave walk past a single tower and confirm some get through.

That first case is the one that bites. It is the most common crash in this
phase, and it stays hidden until a wave gets dense enough for two towers to
shoot the same slime.

## How a Tower Finds Something to Shoot

Detection is one physics query per tower, run only when the tower is ready to
fire rather than every frame:

```csharp
void Update()
{
    cooldown -= Time.deltaTime;

    if (cooldown > 0f)
    {
        return;
    }

    Slime target = FindTarget();

    if (target == null)
    {
        return;
    }

    Fire(target);
    cooldown = 1f / fireRate;
}
```

The early return is doing real work. A tower that searches every frame and then
discards the result because it is still reloading does the expensive part of the
job sixty times a second to use it once or twice. With a dozen towers on the map
that is a dozen wasted sphere queries per frame. Searching only when the result
can be acted on costs nothing and scales.

`FindTarget` is where the design decision lives:

```csharp
Slime FindTarget()
{
    // Non-allocating overload: fills a reusable buffer instead of returning a
    // fresh array every call. OverlapSphere allocating once per tower per shot
    // is exactly the kind of per-frame garbage that shows up as stutter on
    // mobile in Phase 10.
    int count = Physics.OverlapSphereNonAlloc(transform.position, range, hits, slimeMask);

    Slime best = null;
    float bestProgress = float.NegativeInfinity;

    for (int i = 0; i < count; i++)
    {
        Slime slime = hits[i].GetComponentInParent<Slime>();

        if (slime == null)
        {
            continue;
        }

        if (slime.RouteProgress > bestProgress)
        {
            bestProgress = slime.RouteProgress;
            best = slime;
        }
    }

    return best;
}
```

`GetComponentInParent` again, and for the same reason as Phase 4: the query
returns a *collider*, which may well sit on a child of the slime once the model
gets more structure. Searching upward survives that; `GetComponent` returns null
and the tower silently never fires.

The `hits` buffer is a fixed-size array allocated once. It has a ceiling — a
tower whose range contains more slimes than the buffer holds simply does not see
the overflow — so size it generously (`32` is ample here) and remember it exists
before Phase 8 makes waves much denser.

`RouteProgress` is new on `Slime`, and it is the whole targeting rule expressed
as one number:

```csharp
/// <summary>
/// How far along the route this slime is, as waypoint index minus a small
/// fraction of the distance still to walk on the current leg. Towers use it to
/// shoot whatever is closest to the goal.
/// </summary>
public float RouteProgress
{
    get
    {
        if (route == null || route.Count == 0)
        {
            return 0f;
        }

        float remaining = Vector3.Distance(transform.position, route.GetPoint(targetIndex));
        return targetIndex - (remaining * 0.001f);
    }
}
```

Index alone is too coarse — every slime between the same pair of waypoints ties,
and the tower picks whichever the physics query happened to return first, which
is not stable frame to frame. Subtracting a small scaled distance breaks the tie
toward whichever is nearer the next waypoint, and keeps the ordering consistent
so a tower does not flicker between two targets mid-reload.

## Why First-In-Line, and Not Nearest

The roadmap offers nearest, first in line, or lowest health. This project uses
first in line, meaning the slime furthest along the route.

Nearest is the obvious implementation and the wrong default. It maximizes the
tower's convenience rather than the player's outcome: a tower will happily
re-target a fresh slime that wandered a little closer while the one about to
reach the goal walks out of range untouched. The player loses a life to a slime
that was inside the tower's radius the whole time, which reads as the tower
being broken rather than the targeting being naive.

First in line matches what the player is actually trying to prevent. The slime
closest to the goal is the most urgent threat by definition, so damage goes
where it matters. It is also the standard behavior players already expect from
the genre, which means the game teaches them nothing wrong.

The cost is that first in line leaks damage — a slime about to die can walk out
of range while the tower keeps firing at it, wasting shots that could have
started on the next one. Lowest-health targeting fixes that specific waste and
creates a worse one, spraying at whatever is weakest regardless of where it is.

Phase 8 makes this a per-tower choice, when there are multiple tower types worth
differentiating. Today it is one rule, in one method, easy to change.

## Projectiles, Not Hitscan

The roadmap allows instant hitscan damage, and it is genuinely simpler: no
prefab, no travel, no lifetime, no mid-flight target death. This project fires
projectiles anyway, for two reasons.

The first is that the shot is the only feedback the combat loop has. There is no
UI, no audio, no hit flash, and no damage number until Phases 7 and 9. A visible
object leaving the tower and arriving at a slime is the entire explanation of
what just happened. Hitscan combat with no feedback layer looks like slimes
dying at random.

The second is that projectiles create the problem object pooling exists to
solve, and they create it here where it is still small. By Phase 8 several
towers firing several shots a second make `Instantiate`/`Destroy` churn something
you can actually measure. Meeting that with the pattern already in mind beats
meeting it as a mystery framerate drop.

The projectile itself is deliberately dumb — it remembers a target and moves:

```csharp
void Update()
{
    // The target can die mid-flight. Unity's overloaded == reports a destroyed
    // object as null, so this catches it and the projectile gives up rather
    // than throwing MissingReferenceException on the next line.
    if (target == null)
    {
        Destroy(gameObject);
        return;
    }

    Vector3 destination = target.transform.position;
    transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

    if (Vector3.Distance(transform.position, destination) <= arriveDistance)
    {
        target.TakeDamage(damage);
        Destroy(gameObject);
    }
}
```

Homing rather than fire-and-forget: it tracks the slime's current position every
frame instead of a position captured at launch. Ballistic shots that can miss a
moving target are a Phase 9 decision about feel, not something to inherit by
accident in Phase 5.

The null check on the first line is the single most important line in this
phase. A destroyed `GameObject` in Unity is not a null reference — it is a live
C# object whose `==` operator reports null while any member access throws. Skip
the check and every overlapping pair of towers eventually produces a
`MissingReferenceException` in the Console at the moment a slime dies.

## Death, and Who Owns It

Damage and death live on `Slime`, not on the projectile:

```csharp
/// <summary>
/// Applies damage and kills the slime at zero. Called by projectiles; Phase 8's
/// area-of-effect towers will call it too.
/// </summary>
public void TakeDamage(float amount)
{
    health -= amount;

    if (health <= 0f)
    {
        Die();
    }
}

void Die()
{
    // Phase 6 will pay the player here before the slime despawns.
    // Phase 9 will spawn a death effect.
    Destroy(gameObject);
}
```

The projectile subtracts nothing itself. It says *take three damage* and the
slime decides what that means — which is what lets Phase 8 add an armored slime
that halves incoming damage, or Phase 9 add a hit flash, without editing the
projectile at all.

Note that `Die` and `ReachGoal` are separate methods that currently do the same
thing. That duplication is intentional and about to pay off: in Phase 6 one of
them awards money and the other costs a life. Collapsing them now because they
look identical means splitting them again one phase later.

## What Towers Still Do Not Do

Killing a slime pays nothing, and a slime reaching the goal costs nothing — it
despawns exactly as it did in Phase 2. Towers are free and unlimited, so the
optimal strategy is to fill every node immediately and stop thinking.

That is Phase 6's job, and it is a small job precisely because this phase put the
hooks where they belong: `Die` and `ReachGoal` are the two places money and lives
get wired in, and both already exist as distinct methods with a comment marking
the spot.

## Phase 5 Completion Checklist

Phase 5 is complete when:

- `Projectile.cs` exists, and `Tower.cs` and `Slime.cs` compile with no console
  errors.
- A `Slime` user layer exists in Tags and Layers.
- `Assets/Prefabs/Slime.prefab` has a trigger collider and sits on the `Slime`
  layer.
- `Assets/Prefabs/Projectile.prefab` exists with no collider.
- The `Tower` prefab has `Projectile Prefab` and `Slime Mask` assigned.
- A placed tower detects slimes inside `Range` and ignores those outside it.
- The tower fires at the slime furthest along the route, not the nearest one.
- Projectiles visibly travel and deal damage on arrival.
- Slimes die at zero health and despawn.
- A slime dying mid-flight throws nothing in the Console.
- Two overlapping towers both fire and neither double-applies damage per hit.
- Surviving slimes still reach the goal and despawn.
- No currency, lives, or UI code has been added yet.

After this checklist is complete, Phase 6 adds money and lives, turning a working
combat loop into a game that can be won or lost.
