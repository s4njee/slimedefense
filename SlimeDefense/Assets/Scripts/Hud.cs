using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the run's state: money, lives, and which wave is running. One listener
/// holding three labels rather than a script per label — three subscriptions with
/// three lifetimes to get right is more machinery for the same three strings.
///
/// Nothing here runs in Update. Every label is written when the value behind it
/// changes and left alone in between, which is what the events added in Phase 6
/// were for. Polling would re-tessellate a text mesh sixty times a second to show
/// a number that moves twice a wave.
///
/// Attach this to an empty child of the Canvas and assign the labels.
/// </summary>
public class Hud : MonoBehaviour
{
    [Tooltip("Shows the current balance.")]
    [SerializeField] TMP_Text moneyLabel;

    [Tooltip("Shows lives remaining.")]
    [SerializeField] TMP_Text livesLabel;

    [Tooltip("Shows how far through the wave list the run is.")]
    [SerializeField] TMP_Text waveLabel;

    [Tooltip("The Start Wave button. Its click is wired here in code rather than through the " +
             "Inspector's On Click list, and it is hidden once the run is underway — StartWaves " +
             "plays the whole list, so there is nothing left to press it for.")]
    [SerializeField] Button startWaveButton;

    [Tooltip("The spawner the wave label reads from. Leave empty to find the one in the scene.")]
    [SerializeField] WaveSpawner spawner;

    void Awake()
    {
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<WaveSpawner>();
        }
    }

    // True while this HUD is attached to the manager's events, so subscribing
    // twice is impossible and unsubscribing without a subscription is a no-op.
    bool subscribed;

    // Subscription is attempted from both OnEnable and Start, and that is not
    // belt-and-braces — it is the fix for a real ordering bug.
    //
    // OnEnable/OnDisable is the right pairing for UI, which gets toggled: a
    // hidden panel should not react to events. But OnEnable can run before the
    // GameManager's Awake, and then Instance is null. Guarding the null and
    // moving on — which is what this script used to do — means the HUD silently
    // never subscribes, and the labels sit on whatever text they were given in
    // the editor for the whole run. Money reading "Money: 100" and never moving
    // is exactly what that looks like.
    //
    // Start is guaranteed to run after every Awake in the scene, so the second
    // attempt always finds the manager. Whichever call gets there first wins and
    // the other does nothing.
    void OnEnable()
    {
        Subscribe();
    }

    void Start()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (subscribed || GameManager.Instance == null)
        {
            return;
        }

        subscribed = true;

        GameManager.Instance.MoneyChanged += OnMoneyChanged;
        GameManager.Instance.LivesChanged += OnLivesChanged;

        // Subscribe, then seed. Events report *changes*, so a listener that
        // arrives after the last one has missed it and would show whatever the
        // label said in the editor until the player earns a coin. Read the
        // current values once here and let the events cover the rest.
        OnMoneyChanged(GameManager.Instance.Money);
        OnLivesChanged(GameManager.Instance.Lives);

        if (spawner != null)
        {
            spawner.WaveChanged += OnWaveChanged;
            spawner.WavesStarted += OnWavesStarted;

            OnWaveChanged(spawner.CurrentWave, spawner.WaveCount);
        }

        if (startWaveButton != null)
        {
            // Wired in code, deliberately. The Inspector's On Click list is the
            // more common way to do this and it has one silent failure mode:
            // dropping the *script asset* into the object slot instead of the
            // scene object is accepted without complaint, leaves the method name
            // empty, and produces a button that highlights, animates, and calls
            // nothing. A typed Button field cannot be given the wrong thing —
            // Unity rejects the drop.
            startWaveButton.onClick.AddListener(OnStartWavePressed);
            startWaveButton.gameObject.SetActive(spawner == null || !spawner.IsRunning);
        }
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

        if (spawner != null)
        {
            spawner.WaveChanged -= OnWaveChanged;
            spawner.WavesStarted -= OnWavesStarted;
        }

        if (startWaveButton != null)
        {
            startWaveButton.onClick.RemoveListener(OnStartWavePressed);
        }
    }

    void OnStartWavePressed()
    {
        if (spawner == null)
        {
            Debug.LogError($"{name} has no WaveSpawner, so the Start Wave button has nothing to start.", this);
            return;
        }

        spawner.StartWaves();
    }

    void OnMoneyChanged(int money)
    {
        if (moneyLabel != null)
        {
            moneyLabel.text = $"Money: {money}";
        }
    }

    void OnLivesChanged(int lives)
    {
        if (livesLabel != null)
        {
            livesLabel.text = $"Lives: {lives}";
        }
    }

    void OnWaveChanged(int current, int total)
    {
        if (waveLabel != null)
        {
            waveLabel.text = $"Wave: {current} / {total}";
        }
    }

    void OnWavesStarted()
    {
        if (startWaveButton != null)
        {
            startWaveButton.gameObject.SetActive(false);
        }
    }
}
