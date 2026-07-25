using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a list of <see cref="WaveDefinition"/> assets, instantiating slimes on
/// a timer and handing each one the route to walk. Attach this to an empty
/// GameObject under `Level` — not under `Path`, whose children are the route
/// points themselves.
///
/// The sequencing is a coroutine rather than an Update with timer variables, so
/// the wave order reads top to bottom in the order it actually happens.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [Tooltip("The route handed to every spawned slime. Drag the Path object here.")]
    [SerializeField] WaypointRoute route;

    [Tooltip("Waves to play, in list order. Reorder here to reorder the game's difficulty curve.")]
    [SerializeField] WaveDefinition[] waves;

    [Tooltip("Seconds before the first wave begins, so the scene is visible before anything moves.")]
    [Min(0f)]
    [SerializeField] float startDelay = 2f;

    [Tooltip("Seconds between the end of one wave's spawning and the start of the next.")]
    [Min(0f)]
    [SerializeField] float timeBetweenWaves = 5f;

    [Tooltip("Begin spawning on Play. Phase 7 replaces this with a Start Wave button that calls StartWaves().")]
    [SerializeField] bool autoStart = true;

    [Tooltip("Optional container for spawned slimes. Leave empty to spawn them at the root " +
             "of the Hierarchy; assign an empty object to keep the Hierarchy readable.")]
    [SerializeField] Transform slimeParent;

    // The handle returned by StartCoroutine. Held so the sequence can be stopped
    // on demand and so a second StartWaves() call cannot run two sequences at
    // once. Null means nothing is currently spawning.
    Coroutine runningWaves;

    /// <summary>True while a wave sequence is spawning.</summary>
    public bool IsRunning => runningWaves != null;

    void Start()
    {
        // Convenience for a spawner dropped into the scene without wiring, and
        // the same fallback Slime uses. Explicit assignment in the Inspector is
        // still the intended setup.
        if (route == null)
        {
            route = FindFirstObjectByType<WaypointRoute>();
        }

        if (route == null || route.Count < 2)
        {
            Debug.LogError($"{name} has no usable WaypointRoute. Assign the Path object to the Route field.", this);
            enabled = false;
            return;
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogWarning($"{name} has no waves assigned, so nothing will spawn.", this);
            return;
        }

        if (autoStart)
        {
            StartWaves();
        }
    }

    /// <summary>
    /// Begins the wave sequence. Public so the Phase 7 HUD button can call it
    /// once <see cref="autoStart"/> is switched off.
    /// </summary>
    public void StartWaves()
    {
        if (runningWaves != null)
        {
            Debug.LogWarning($"{name} is already running its waves; ignoring the second start.", this);
            return;
        }

        // StartCoroutine returns a handle to the running sequence. The coroutine
        // itself belongs to this MonoBehaviour: disabling this object or
        // destroying it stops the sequence mid-wave, silently and without error.
        runningWaves = StartCoroutine(RunWaves());
    }

    /// <summary>
    /// Stops spawning immediately. Slimes already on the route keep walking —
    /// this only halts the sequence that creates new ones.
    /// </summary>
    public void StopWaves()
    {
        if (runningWaves == null)
        {
            return;
        }

        StopCoroutine(runningWaves);
        runningWaves = null;
    }

    // Plays every wave in order. A coroutine is a method that can pause partway
    // through and resume on a later frame: `yield return` hands control back to
    // Unity, and what is yielded says when to come back.
    IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < waves.Length; i++)
        {
            WaveDefinition wave = waves[i];

            if (wave == null || !wave.IsValid)
            {
                Debug.LogWarning($"{name}: wave {i} is missing or has no slime prefab. Skipping it.", this);
                continue;
            }

            // Yielding another IEnumerator runs it to completion before this
            // method resumes, which keeps one wave's spawning in its own method
            // without needing a second coroutine handle to track.
            yield return RunWave(wave);

            // No trailing pause after the final wave — the sequence just ends.
            if (i < waves.Length - 1)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        // Clearing the handle marks the sequence finished, so IsRunning is
        // accurate and StartWaves() can legitimately run the list again.
        runningWaves = null;
    }

    // Spawns one wave's slimes, spaced apart in time.
    IEnumerator RunWave(WaveDefinition wave)
    {
        if (wave.DelayBeforeWave > 0f)
        {
            yield return new WaitForSeconds(wave.DelayBeforeWave);
        }

        // `new WaitForSeconds(...)` allocates, so creating one per slime would
        // produce garbage proportional to the wave size. The interval is
        // constant within a wave, so one instance is reused for the whole loop.
        // The same instinct — reuse instead of recreate — is what Phase 8's
        // object pooling formalizes for the slimes themselves.
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
    }

    // Creates one slime and gives it the route to follow.
    void Spawn(Slime prefab)
    {
        // Instantiating at the route's first point avoids a one-frame flash at
        // the world origin. SetRoute snaps the slime there as well, but not
        // until the line after this one.
        Slime slime = Instantiate(prefab, route.GetPoint(0), Quaternion.identity, slimeParent);

        // This runs immediately, while the new slime's Start() does not run
        // until later in the frame. So by the time Slime.Start checks for a
        // route, it already has one, and its FindFirstObjectByType fallback from
        // Phase 2 never fires for a spawned slime.
        slime.SetRoute(route);
    }
}
