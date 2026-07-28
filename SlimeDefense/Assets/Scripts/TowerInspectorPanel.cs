using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The panel shown when a placed tower is selected: what it is, what it does at
/// its current level, and the two things that can be done to it.
///
/// It owns no rules. Upgrading and selling are transactions, and every
/// transaction in this project goes through <see cref="TowerPlacer"/>, which is
/// where affordability is checked and money is spent. This panel asks for one and
/// redraws itself with whatever came back — which is why it can be deleted or
/// rebuilt without a single balance rule moving.
///
/// Hidden with a CanvasGroup rather than SetActive(false), for the same reason
/// <see cref="EndOfRunPanel"/> is: a deactivated object stops running OnEnable
/// and can never hear the event that would bring it back.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TowerInspectorPanel : MonoBehaviour
{
    [Tooltip("Tower name and level, e.g. 'Frost Tower — Level 2'.")]
    [SerializeField] TMP_Text titleLabel;

    [Tooltip("The stats of the current level.")]
    [SerializeField] TMP_Text statsLabel;

    [Tooltip("Buys the next level. Wired in code; leave its On Click list empty.")]
    [SerializeField] Button upgradeButton;

    [Tooltip("Label on the upgrade button, so it can show the price.")]
    [SerializeField] TMP_Text upgradeLabel;

    [Tooltip("Sells the tower. Wired in code; leave its On Click list empty.")]
    [SerializeField] Button sellButton;

    [Tooltip("Label on the sell button, so it can show the refund.")]
    [SerializeField] TMP_Text sellLabel;

    [Tooltip("The placer that owns selection and transactions. Leave empty to find it.")]
    [SerializeField] TowerPlacer placer;

    CanvasGroup group;
    bool subscribed;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();

        if (placer == null)
        {
            placer = FindAnyObjectByType<TowerPlacer>();
        }

        SetVisible(false);
    }

    // OnEnable and Start both, guarded by the flag — OnEnable can run before the
    // GameManager's Awake, and a listener that quietly skipped subscribing would
    // leave a panel that never opens.
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
        if (subscribed || GameManager.Instance == null || placer == null)
        {
            return;
        }

        subscribed = true;

        placer.TowerSelectionChanged += OnSelectionChanged;

        // Money moving changes whether the upgrade is affordable, so the button's
        // enabled state is event-driven exactly like the picker's.
        GameManager.Instance.MoneyChanged += OnMoneyChanged;

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradePressed);
        }

        if (sellButton != null)
        {
            sellButton.onClick.AddListener(OnSellPressed);
        }

        OnSelectionChanged(placer.SelectedNode);
    }

    void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;

        if (placer != null)
        {
            placer.TowerSelectionChanged -= OnSelectionChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.MoneyChanged -= OnMoneyChanged;
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnUpgradePressed);
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveListener(OnSellPressed);
        }
    }

    void OnSelectionChanged(BuildNode node)
    {
        Refresh();
    }

    void OnMoneyChanged(int money)
    {
        // Only the affordability of the upgrade can change from money alone, but
        // redrawing the whole panel keeps one path instead of two that drift.
        Refresh();
    }

    void Refresh()
    {
        Tower tower = placer != null ? placer.SelectedTower : null;

        if (tower == null || tower.Definition == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        TowerLevel level = tower.CurrentLevel;

        if (titleLabel != null)
        {
            titleLabel.text = $"{tower.Definition.DisplayName} — Level {tower.Level + 1}";
        }

        if (statsLabel != null && level != null)
        {
            // Damage is written as a rate as well as a number, because "2 damage"
            // and "0.7 shots per second" are not comparable across tower types by
            // eye, and comparing them is the entire decision the player is making.
            statsLabel.text =
                $"Range {level.Range:0.#}\n" +
                $"Damage {level.Damage:0.#}\n" +
                $"Rate {level.FireRate:0.##}/s\n" +
                $"DPS {level.Damage * level.FireRate:0.#}";
        }

        bool canAfford = GameManager.Instance != null
                         && GameManager.Instance.CanAfford(tower.UpgradeCost);

        if (upgradeButton != null)
        {
            upgradeButton.interactable = tower.CanUpgrade && canAfford;
        }

        if (upgradeLabel != null)
        {
            // At maximum the button says so rather than showing a price of zero,
            // which would read as a free upgrade that does nothing.
            upgradeLabel.text = tower.CanUpgrade ? $"Upgrade\n{tower.UpgradeCost}" : "Max level";
        }

        if (sellLabel != null)
        {
            sellLabel.text = $"Sell\n+{tower.SellValue}";
        }
    }

    void OnUpgradePressed()
    {
        if (placer != null)
        {
            placer.TryUpgradeSelected();
        }
    }

    void OnSellPressed()
    {
        if (placer != null)
        {
            placer.SellSelected();
        }
    }

    void SetVisible(bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;

        // blocksRaycasts matters as much as alpha: an invisible panel that still
        // swallows clicks would stop the player selecting the next tower, and the
        // symptom — placement works until you open the panel once — is a horrible
        // one to trace.
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
