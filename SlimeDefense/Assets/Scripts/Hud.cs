using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event-driven in-game HUD for money, lives, wave count, and the one-shot
/// start-game action. Presentation lives in the scene; this component only
/// translates game state into labels and life indicators.
/// </summary>
public class Hud : MonoBehaviour
{
    [Header("Values")]
    [SerializeField] TMP_Text moneyLabel;
    [SerializeField] TMP_Text livesLabel;
    [SerializeField] TMP_Text waveLabel;
    [SerializeField] TMP_Text waveTotalLabel;

    [Header("Lives")]
    [Tooltip("Container for individual pips. Hidden when max lives is greater than eight.")]
    [SerializeField] GameObject lifePipsRoot;
    [SerializeField] Image[] lifePips;
    [Tooltip("Compact icon used instead of pips for large life counts.")]
    [SerializeField] GameObject lifeIcon;

    [Header("Start")]
    [SerializeField] Button startWaveButton;
    [SerializeField] TMP_Text startWaveLabel;

    [Tooltip("The spawner the wave label reads from. Leave empty to find the one in the scene.")]
    [SerializeField] WaveSpawner spawner;

    bool subscribed;

    void Awake()
    {
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<WaveSpawner>();
        }

        if (startWaveLabel != null)
        {
            startWaveLabel.text = "START GAME";
        }

        if (startWaveButton != null && startWaveButton.image != null)
        {
            startWaveButton.image.raycastTarget = true;
        }
    }

    // OnEnable can run before GameManager.Awake. Start is the guaranteed second
    // attempt, and the guard keeps the subscription single.
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
            Debug.LogError($"{name} has no WaveSpawner, so the start button has nothing to start.", this);
            return;
        }

        spawner.StartWaves();
    }

    void OnMoneyChanged(int money)
    {
        if (moneyLabel != null)
        {
            moneyLabel.text = money.ToString("N0");
        }
    }

    void OnLivesChanged(int lives)
    {
        if (livesLabel != null)
        {
            livesLabel.text = lives.ToString("N0");
        }

        int maxLives = GameManager.Instance != null ? GameManager.Instance.MaxLives : lives;
        bool usePips = maxLives > 0 && maxLives <= 8 && lifePips != null && lifePips.Length > 0;

        if (lifePipsRoot != null)
        {
            lifePipsRoot.SetActive(usePips);
        }

        if (lifeIcon != null)
        {
            lifeIcon.SetActive(!usePips);
        }

        if (!usePips)
        {
            return;
        }

        Color filled = new Color32(224, 138, 120, 255);
        Color empty = new Color32(236, 238, 228, 36);

        for (int i = 0; i < lifePips.Length; i++)
        {
            Image pip = lifePips[i];

            if (pip == null)
            {
                continue;
            }

            pip.gameObject.SetActive(i < maxLives);
            pip.color = i < lives ? filled : empty;
        }
    }

    void OnWaveChanged(int current, int total)
    {
        if (waveLabel != null)
        {
            waveLabel.text = current.ToString("00");
        }

        if (waveTotalLabel != null)
        {
            waveTotalLabel.text = $"/ {total:00}";
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
