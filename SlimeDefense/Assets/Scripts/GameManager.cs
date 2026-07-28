using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The single source of truth for a run: how much money the player has, how many
/// lives are left, and whether the run has ended.
///
/// Every other system reads this one and nothing keeps its own copy. A tower does
/// not know what it costs the player, a slime does not know what a life is worth,
/// and neither of them can disagree with this object about the balance.
///
/// Changes are announced through events rather than left for interested parties
/// to notice. Nothing subscribes to <see cref="MoneyChanged"/> or
/// <see cref="LivesChanged"/> yet — the Phase 7 HUD is the first listener — and
/// they exist now so that HUD is subscription and layout rather than an Update
/// loop polling this object sixty times a second for a number that changes twice
/// a wave.
///
/// Attach this to a single empty GameObject under `Level`. Not a prefab, not
/// spawned at runtime, and deliberately not marked DontDestroyOnLoad: this holds
/// the state of *this run*, and Phase 7's restart is a scene reload that should
/// get a fresh one.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Tooltip("The budget at the start of a run. At 50 per tower this buys two, and every " +
             "tower after that is paid for by the wave before it.")]
    [Min(0)]
    [SerializeField] int startingMoney = 100;

    [Tooltip("Leaks allowed before the run ends. Against the 25 slimes the three wave assets " +
             "send, 10 means building nothing loses partway through wave 2 — soon enough to " +
             "read as a rule rather than an afterthought.")]
    [Min(1)]
    [SerializeField] int startingLives = 10;

    [Tooltip("Print money and lives to the Console as they change. Scaffolding with an expiry " +
             "date: this phase adds state with nothing on screen to show it, and Phase 7's HUD " +
             "makes it redundant.")]
    [SerializeField] bool logChanges = true;

    /// <summary>
    /// The manager for the current scene, or null before its Awake has run.
    ///
    /// Read this at the moment of use rather than caching it in a field. A cached
    /// reference outlives the object it points at, and a destroyed MonoBehaviour
    /// is not a plain null — it reports `== null` while a stale field still looks
    /// perfectly valid to `?.`, which is a plain null check and skips Unity's
    /// lifetime check entirely.
    /// </summary>
    public static GameManager Instance { get; private set; }

    /// <summary>The player's current balance. Never negative.</summary>
    public int Money { get; private set; }

    /// <summary>Lives remaining. Zero means the run is over.</summary>
    public int Lives { get; private set; }

    /// <summary>The number of lives a fresh run begins with.</summary>
    public int MaxLives => startingLives;

    /// <summary>True once lives have run out. Systems check this to stand down.</summary>
    public bool IsGameOver { get; private set; }

    /// <summary>
    /// How many slimes are currently walking. Half of the victory condition — the
    /// other half is the spawner having nothing left to send.
    /// </summary>
    public int SlimesAlive { get; private set; }

    // True once the spawner's coroutine has run out of waves. Not the same as
    // the run being won: the last wave's slimes are usually still on the board.
    bool allWavesSpawned;

    // Guards against announcing an outcome twice, or announcing both.
    bool runEnded;

    /// <summary>Raised with the new balance whenever money changes.</summary>
    public event Action<int> MoneyChanged;

    /// <summary>Raised with the new count whenever lives change.</summary>
    public event Action<int> LivesChanged;

    /// <summary>
    /// Raised once, when the last life is lost. Listeners subscribe in Start and
    /// unsubscribe in OnDestroy — an event holds a strong reference to its
    /// subscriber, so a destroyed listener that never unsubscribed stays in this
    /// invocation list and throws the next time the event fires.
    /// </summary>
    public event Action GameOver;

    /// <summary>
    /// Raised once, when the last wave has been spawned and the last slime it
    /// sent is gone. Defeat wins ties — see <see cref="CheckForVictory"/>.
    /// </summary>
    public event Action Victory;

    void Awake()
    {
        // Claimed in Awake because Unity runs every Awake in the scene before it
        // runs any Start. That ordering is the whole convention: anything that
        // looks up Instance from its own Start is guaranteed to find it,
        // regardless of Hierarchy order, which is otherwise unspecified.
        if (Instance != null && Instance != this)
        {
            // Not a silent overwrite, which is how this pattern is usually
            // written. Two managers means two sets of starting values, and
            // whichever one lost is still referenced by anything holding it.
            Debug.LogError($"{name}: a second GameManager is already in the scene. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Money = startingMoney;
        Lives = startingLives;
    }

    void Start()
    {
        if (logChanges)
        {
            Debug.Log($"Run started with {Money} money and {Lives} lives.");
        }
    }

    void OnDestroy()
    {
        // Only clear the static if this is the instance it points at. A duplicate
        // destroying itself in Awake must not null out the real one on its way
        // out.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>True when the player can pay <paramref name="amount"/>.</summary>
    public bool CanAfford(int amount) => Money >= amount;

    /// <summary>
    /// Pays the player. Phase 8's sell action is the second caller, which is why
    /// this is a public method rather than something the slime does inline.
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetMoney(Money + amount);
    }

    /// <summary>
    /// Spends <paramref name="amount"/>, or returns false and spends nothing when
    /// the player cannot afford it.
    ///
    /// A bool rather than a thrown exception or a logged error, because a player
    /// clicking a node they cannot pay for is not a fault — it is the most
    /// ordinary thing they do. Callers that need to know before committing to
    /// anything ask <see cref="CanAfford"/> first.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || !CanAfford(amount))
        {
            return false;
        }

        SetMoney(Money - amount);
        return true;
    }

    /// <summary>
    /// Takes lives and ends the run at zero. Called by slimes reaching the goal.
    /// </summary>
    public void LoseLife(int amount = 1)
    {
        // Wave 3 sends slimes 0.7 seconds apart, so several can reach the goal in
        // the same breath. Without this guard each one announces the loss again,
        // and Phase 7 gets a game-over screen that opens once per arriving slime.
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

        if (Lives > 0)
        {
            return;
        }

        IsGameOver = true;
        runEnded = true;

        // Logged unconditionally. Unlike the running totals, this one is the
        // result of the run and is worth seeing even with the scaffolding off.
        Debug.Log("Game over.");

        GameOver?.Invoke();
    }

    /// <summary>
    /// Counts a slime onto the board. Called from <see cref="Slime"/>'s Start.
    /// </summary>
    public void RegisterSlime()
    {
        SlimesAlive++;
    }

    /// <summary>
    /// Counts a slime off the board, however it left, and checks whether that was
    /// the last one.
    ///
    /// Called from Die and ReachGoal rather than OnDestroy, for the same reason
    /// the money and the life are: OnDestroy also fires on scene teardown and
    /// when Play mode stops, and stops firing at all in Phase 8 when slimes are
    /// pooled instead of destroyed.
    /// </summary>
    public void UnregisterSlime()
    {
        SlimesAlive = Mathf.Max(0, SlimesAlive - 1);
        CheckForVictory();
    }

    /// <summary>
    /// Told by <see cref="WaveSpawner"/> that its wave list is exhausted. Not a
    /// victory on its own — the last wave is usually still walking.
    /// </summary>
    public void NotifyAllWavesSpawned()
    {
        allWavesSpawned = true;
        CheckForVictory();
    }

    /// <summary>
    /// Reloads the current scene, which is the entire reset. This is why the
    /// manager is not marked DontDestroyOnLoad: one that survived would carry the
    /// dead run's money, lives, and IsGameOver into the new one, and each would
    /// have to be cleared by hand — reimplementing, incompletely, what the scene
    /// load already does.
    /// </summary>
    public void Restart()
    {
        // Time.timeScale survives a scene load. Nothing sets it today, and the
        // day a pause button does, forgetting this line means restarting into a
        // frozen game that looks like the reload failed.
        Time.timeScale = 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Runs on both of its inputs, because either can be the last to arrive. A
    // check that only ran on slime deaths would miss the player who kills
    // everything before the final wave finishes spawning; one that only ran when
    // the spawner finished would miss the far more common case of the last slime
    // dying afterwards.
    void CheckForVictory()
    {
        // Defeat wins ties. The last slime of the last wave reaching the goal and
        // taking the last life satisfies "no waves left" and "no slimes left" in
        // the same frame as game over, and a victory announced on top of a defeat
        // is worse than announcing nothing.
        if (runEnded || IsGameOver || !allWavesSpawned || SlimesAlive > 0)
        {
            return;
        }

        runEnded = true;

        Debug.Log("All waves cleared.");

        Victory?.Invoke();
    }

    // Every path that changes money funnels through here — a kill, a purchase,
    // and Phase 8's sell refund — so the clamp, the log, and the event happen
    // exactly once each and cannot be forgotten by a new caller.
    void SetMoney(int value)
    {
        // Clamped here rather than at each call site, because "money cannot go
        // negative" is a property of money, not something every caller remembers.
        Money = Mathf.Max(0, value);

        if (logChanges)
        {
            Debug.Log($"Money: {Money}");
        }

        // `?.` on a C# event is the correct idiom and unrelated to the Unity
        // object rule in Instance's summary: this is a delegate, and null here
        // genuinely means nobody is listening.
        MoneyChanged?.Invoke(Money);
    }
}
