using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <summary>Which wave is spawning, counting from 1. Zero before the run starts.</summary>
    public int CurrentWave { get; private set; }

    /// <summary>How many waves this spawner will play.</summary>
    public int WaveCount => waves != null ? waves.Length : 0;

    /// <summary>Raised with (current, total) as each wave begins.</summary>
    public event Action<int, int> WaveChanged;

    /// <summary>
    /// Raised when the sequence starts. The HUD hides its Start Wave button on
    /// this: <see cref="StartWaves"/> plays the whole list, so there is nothing
    /// left to press it for.
    /// </summary>
    public event Action WavesStarted;

    /// <summary>
    /// Raised when the last wave has finished spawning. Not the end of the run —
    /// those slimes are still walking.
    /// </summary>
    public event Action WavesFinished;

    void Start()
    {
        // Start rather than OnEnable, which is the more usual pairing for
        // subscriptions and the wrong one here: OnEnable can run before the
        // manager's Awake and find nothing to subscribe to. Every Awake in the
        // scene has finished by the time any Start runs. Nothing in this scene
        // gets toggled, so pairing with OnDestroy loses nothing.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver += OnGameOver;
        }

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

        // Before the first wave rather than during it. An empty pool costs
        // exactly what no pool costs for the first slime of each type, and a
        // wave's worth of first slimes all arrive within a second of each other.
        PrewarmSlimes();

        if (autoStart)
        {
            StartWaves();
        }
    }

    void OnDestroy()
    {
        // An event holds a strong reference to its subscriber, so a destroyed
        // spawner that never unsubscribed stays in the manager's invocation list
        // and throws the next time the event fires.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver -= OnGameOver;
        }
    }

    // Nothing more elaborate than this. Slimes already on the route keep walking
    // and cost nothing when they arrive, because LoseLife returns immediately
    // once the run is over. What "stopped" means is each system's own decision.
    void OnGameOver()
    {
        StopWaves();
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

        WavesStarted?.Invoke();
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

            CurrentWave = i + 1;
            WaveChanged?.Invoke(CurrentWave, WaveCount);

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

        WavesFinished?.Invoke();

        // The manager is told directly rather than left to find this object and
        // subscribe. Every other system in the project calls into the manager and
        // the manager raises events outward; reversing that for one caller would
        // mean the manager holding a reference to the spawner and caring whether
        // it exists yet.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyAllWavesSpawned();
        }
    }

    // Spawns one wave's groups in order, each group's slimes spaced apart in time.
    IEnumerator RunWave(WaveDefinition wave)
    {
        if (wave.DelayBeforeWave > 0f)
        {
            yield return new WaitForSeconds(wave.DelayBeforeWave);
        }

        // Part C added this outer loop and nothing else. Phase 3 predicted the
        // inner loop would survive a wave becoming a list of groups, and it did:
        // everything below the group check is the code it has always been.
        for (int g = 0; g < wave.Groups.Length; g++)
        {
            WaveGroup group = wave.Groups[g];

            if (group == null || !group.IsValid)
            {
                Debug.LogWarning($"{name}: group {g} of {wave.name} has no slime prefab. Skipping it.", this);
                continue;
            }

            if (group.DelayBeforeGroup > 0f)
            {
                yield return new WaitForSeconds(group.DelayBeforeGroup);
            }

            // `new WaitForSeconds(...)` allocates, so creating one per slime would
            // produce garbage proportional to the group size. The interval is
            // constant within a group, so one instance is reused for the whole
            // loop. The same instinct — reuse instead of recreate — is what Part
            // D's object pooling formalizes for the slimes themselves.
            WaitForSeconds gap = new WaitForSeconds(group.Spacing);

            for (int i = 0; i < group.Count; i++)
            {
                Spawn(group.SlimePrefab);

                // No trailing gap after the last slime of a group; the next
                // group's Delay Before Group covers the seam, and Time Between
                // Waves covers the end of the wave.
                if (i < group.Count - 1)
                {
                    yield return gap;
                }
            }
        }
    }

    // Creates one slime and gives it the route to follow.
    void Spawn(Slime prefab)
    {
        // Spawning at the route's first point avoids a one-frame flash at the
        // world origin. SetRoute snaps the slime there as well, but not until
        // the line after this one.
        Slime slime = ObjectPool.Spawn(prefab, route.GetPoint(0), Quaternion.identity, slimeParent);

        // Still the line that matters, and Part D made it matter more. It runs
        // immediately, before the slime's own Start — and a reused slime gets no
        // Start at all, which is why SetRoute is now also what counts it onto
        // the board.
        slime.SetRoute(route);
    }

    // Builds each slime type's copies before the run rather than during it.
    //
    // The count is the largest single group of that type in the whole list,
    // because that is the most of it that can be spawned back to back. It is a
    // floor, not a ceiling: two groups of the same type overlapping on the route
    // simply grow the pool at the moment they do, exactly as an unwarmed pool
    // would.
    void PrewarmSlimes()
    {
        Dictionary<Slime, int> largestGroup = new Dictionary<Slime, int>();

        foreach (WaveDefinition wave in waves)
        {
            if (wave == null || !wave.IsValid)
            {
                continue;
            }

            foreach (WaveGroup group in wave.Groups)
            {
                if (group == null || !group.IsValid)
                {
                    continue;
                }

                largestGroup.TryGetValue(group.SlimePrefab, out int most);
                largestGroup[group.SlimePrefab] = Mathf.Max(most, group.Count);
            }
        }

        foreach (KeyValuePair<Slime, int> entry in largestGroup)
        {
            ObjectPool.Prewarm(entry.Key.gameObject, entry.Value);
        }
    }
}
