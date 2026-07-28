using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Right-side tower rail shown only while a placed tower is selected. It presents
/// comparable stats, the next linear upgrade, and a two-click sell action.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TowerInspectorPanel : MonoBehaviour
{
    [Header("States")]
    [SerializeField] GameObject emptyState;
    [SerializeField] GameObject selectedContent;
    [SerializeField] GameObject panelShadow;

    [Header("Identity")]
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text levelLabel;
    [SerializeField] TMP_Text portraitLabel;

    [Tooltip("Optional legacy combined stats label.")]
    [SerializeField] TMP_Text statsLabel;

    [Header("Stats")]
    [SerializeField] TMP_Text damageValueLabel;
    [SerializeField] TMP_Text rangeValueLabel;
    [SerializeField] TMP_Text fireRateValueLabel;
    [SerializeField] Image damageFill;
    [SerializeField] Image rangeFill;
    [SerializeField] Image fireRateFill;

    [Tooltip("Game-wide maximum used to make damage bars comparable between towers.")]
    [Min(0.01f)] [SerializeField] float maximumDamage = 10f;
    [Tooltip("Game-wide maximum used to make range bars comparable between towers.")]
    [Min(0.01f)] [SerializeField] float maximumRange = 12f;
    [Tooltip("Game-wide maximum used to make fire-rate bars comparable between towers.")]
    [Min(0.01f)] [SerializeField] float maximumFireRate = 3.5f;

    [Header("Upgrade")]
    [SerializeField] Button upgradeButton;
    [Tooltip("Optional legacy single upgrade label.")]
    [SerializeField] TMP_Text upgradeLabel;
    [SerializeField] TMP_Text upgradeTitleLabel;
    [SerializeField] TMP_Text upgradeEffectLabel;
    [SerializeField] TMP_Text upgradePriceLabel;

    [Header("Sell")]
    [SerializeField] Button sellButton;
    [SerializeField] TMP_Text sellLabel;
    [SerializeField] TMP_Text refundLabel;

    [SerializeField] TowerPlacer placer;

    CanvasGroup group;
    bool subscribed;
    bool sellConfirmationArmed;
    Coroutine sellConfirmationRoutine;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();

        if (placer == null)
        {
            placer = FindAnyObjectByType<TowerPlacer>();
        }

        if (upgradeButton != null && upgradeButton.image != null)
        {
            upgradeButton.image.raycastTarget = true;
        }

        if (sellButton != null && sellButton.image != null)
        {
            sellButton.image.raycastTarget = true;
        }

        SetSelectionVisible(false);
    }

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
        if (placer == null)
        {
            placer = FindAnyObjectByType<TowerPlacer>();
        }

        if (subscribed || GameManager.Instance == null || placer == null)
        {
            return;
        }

        subscribed = true;
        placer.TowerSelectionChanged += OnSelectionChanged;
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
        ResetSellConfirmation();
        Refresh();
    }

    void OnMoneyChanged(int money)
    {
        Refresh();
    }

    void Refresh()
    {
        Tower tower = placer != null ? placer.SelectedTower : null;

        if (tower == null || tower.Definition == null)
        {
            SetSelectionVisible(false);
            return;
        }

        SetSelectionVisible(true);
        TowerLevel level = tower.CurrentLevel;

        if (titleLabel != null)
        {
            titleLabel.text = tower.Definition.DisplayName;
        }

        if (levelLabel != null)
        {
            levelLabel.text = $"LV {tower.Level + 1}";
        }

        if (portraitLabel != null)
        {
            portraitLabel.text = tower.Definition.DisplayName.ToUpperInvariant();
        }

        if (statsLabel != null && level != null)
        {
            statsLabel.text =
                $"Range {level.Range:0.#}\n" +
                $"Damage {level.Damage:0.#}\n" +
                $"Rate {level.FireRate:0.##}/s";
        }

        if (level != null)
        {
            SetStat(damageValueLabel, damageFill, level.Damage, maximumDamage, "0.#");
            SetStat(rangeValueLabel, rangeFill, level.Range, maximumRange, "0.#");
            SetStat(fireRateValueLabel, fireRateFill, level.FireRate, maximumFireRate, "0.##", "/s");
        }

        bool canAfford = GameManager.Instance != null
                         && GameManager.Instance.CanAfford(tower.UpgradeCost);

        if (upgradeButton != null)
        {
            upgradeButton.interactable = tower.CanUpgrade && canAfford;
        }

        if (upgradeLabel != null)
        {
            upgradeLabel.text = tower.CanUpgrade ? $"Upgrade\n{tower.UpgradeCost}" : "Max level";
        }

        TowerLevel nextLevel = tower.CanUpgrade
            ? tower.Definition.GetLevel(tower.Level + 1)
            : null;

        if (upgradeTitleLabel != null)
        {
            upgradeTitleLabel.text = tower.CanUpgrade
                ? $"LEVEL {tower.Level + 2} UPGRADE"
                : "MAX LEVEL";
        }

        if (upgradeEffectLabel != null)
        {
            upgradeEffectLabel.text = nextLevel != null
                ? DescribeChanges(level, nextLevel)
                : "All upgrades installed";
        }

        if (upgradePriceLabel != null)
        {
            upgradePriceLabel.text = tower.CanUpgrade ? tower.UpgradeCost.ToString("N0") : "-";
            upgradePriceLabel.color = tower.CanUpgrade && !canAfford
                ? new Color32(224, 138, 120, 255)
                : new Color32(236, 238, 228, 255);
        }

        if (sellLabel != null)
        {
            sellLabel.text = sellConfirmationArmed ? "CONFIRM" : "SELL";
        }

        if (refundLabel != null)
        {
            refundLabel.text = tower.SellValue.ToString("N0");
        }
    }

    static void SetStat(TMP_Text valueLabel, Image fill, float value, float maximum,
                        string format, string suffix = "")
    {
        if (valueLabel != null)
        {
            valueLabel.text = value.ToString(format) + suffix;
        }

        if (fill != null)
        {
            fill.fillAmount = Mathf.Clamp01(value / Mathf.Max(0.01f, maximum));
        }
    }

    static string DescribeChanges(TowerLevel current, TowerLevel next)
    {
        if (current == null || next == null)
        {
            return "Stats improve";
        }

        List<string> changes = new List<string>();

        if (!Mathf.Approximately(current.Damage, next.Damage))
        {
            changes.Add($"DMG {current.Damage:0.#} -> {next.Damage:0.#}");
        }

        if (!Mathf.Approximately(current.Range, next.Range))
        {
            changes.Add($"RANGE {current.Range:0.#} -> {next.Range:0.#}");
        }

        if (!Mathf.Approximately(current.FireRate, next.FireRate))
        {
            changes.Add($"RATE {current.FireRate:0.##} -> {next.FireRate:0.##}");
        }

        return changes.Count > 0 ? string.Join("  |  ", changes) : "Model upgrade";
    }

    void OnUpgradePressed()
    {
        if (placer != null)
        {
            placer.TryUpgradeSelected();
            Refresh();
        }
    }

    void OnSellPressed()
    {
        if (placer == null)
        {
            return;
        }

        if (!sellConfirmationArmed)
        {
            sellConfirmationArmed = true;

            if (sellLabel != null)
            {
                sellLabel.text = "CONFIRM";
            }

            if (sellConfirmationRoutine != null)
            {
                StopCoroutine(sellConfirmationRoutine);
            }

            sellConfirmationRoutine = StartCoroutine(CancelSellConfirmation());
            return;
        }

        ResetSellConfirmation();
        placer.SellSelected();
    }

    IEnumerator CancelSellConfirmation()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        sellConfirmationRoutine = null;
        sellConfirmationArmed = false;

        if (sellLabel != null)
        {
            sellLabel.text = "SELL";
        }
    }

    void ResetSellConfirmation()
    {
        sellConfirmationArmed = false;

        if (sellConfirmationRoutine != null)
        {
            StopCoroutine(sellConfirmationRoutine);
            sellConfirmationRoutine = null;
        }

        if (sellLabel != null)
        {
            sellLabel.text = "SELL";
        }
    }

    void SetSelectionVisible(bool visible)
    {
        if (emptyState != null)
        {
            emptyState.SetActive(false);
        }

        if (selectedContent != null)
        {
            selectedContent.SetActive(visible);
        }

        if (panelShadow != null)
        {
            panelShadow.SetActive(visible);
        }

        if (group != null)
        {
            // Keep this component enabled so it can hear the next selection
            // event, but make the entire rail invisible and non-interactive.
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
