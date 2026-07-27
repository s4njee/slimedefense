using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tower selection panel. One button per tower type: pressing one tells
/// <see cref="TowerPlacer"/> what the next click builds, and buttons for towers
/// the player cannot afford are greyed out.
///
/// Phase 7 deferred this panel on the grounds that a selection panel with one
/// button is not a selection. Phase 8 Part A is where there is something to
/// choose between.
///
/// This is also the first thing besides a label to subscribe to
/// <see cref="GameManager.MoneyChanged"/>, which is the point of that event
/// having existed since Phase 6: affordability updates when money moves, not on
/// a timer and not every frame.
/// </summary>
public class TowerPicker : MonoBehaviour
{
    /// <summary>
    /// One row of the panel. A serializable pair rather than two parallel arrays
    /// — parallel arrays go out of step the first time someone inserts a tower
    /// type in the middle, and nothing complains until the wrong tower gets
    /// built.
    /// </summary>
    [Serializable]
    public class Option
    {
        [Tooltip("The tower type this button selects.")]
        public TowerDefinition Definition;

        [Tooltip("The button itself. Wired in code, so leave its On Click list empty.")]
        public Button Button;

        [Tooltip("Optional label. Left empty, the button's own text is not touched.")]
        public TMP_Text Label;
    }

    [Tooltip("The buttons this panel offers, in display order.")]
    [SerializeField] Option[] options;

    [Tooltip("The placer told about the selection. Leave empty to find the one in the scene.")]
    [SerializeField] TowerPlacer placer;

    [Tooltip("Tint applied to the selected tower's button, so the panel shows what is armed.")]
    [SerializeField] Color selectedTint = new Color(1f, 0.92f, 0.55f);

    Color[] originalTints;
    bool subscribed;

    void Awake()
    {
        if (placer == null)
        {
            placer = FindAnyObjectByType<TowerPlacer>();
        }

        // Captured before anything is tinted, so "unselected" has a defined
        // colour to return to even if the buttons were authored with different
        // ones.
        originalTints = new Color[options.Length];

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i].Button != null)
            {
                originalTints[i] = options[i].Button.image != null
                    ? options[i].Button.image.color
                    : Color.white;
            }

            if (options[i].Label != null && options[i].Definition != null)
            {
                options[i].Label.text = $"{options[i].Definition.DisplayName}\n{options[i].Definition.Cost}";
            }
        }
    }

    // OnEnable and Start both, guarded by a flag — the same fix Phase 7 needed.
    // OnEnable can run before GameManager.Awake, and a listener that quietly
    // skipped subscribing would leave every button at whatever interactable state
    // it was authored with, forever.
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

        for (int i = 0; i < options.Length; i++)
        {
            Option option = options[i];

            if (option.Button == null || option.Definition == null)
            {
                continue;
            }

            // Captured into a local so the closure below binds this iteration's
            // definition rather than the loop variable. Capturing `option`
            // directly would work in modern C#, but the local makes the intent
            // obvious and survives the loop being rewritten.
            TowerDefinition definition = option.Definition;
            option.Button.onClick.AddListener(() => OnOptionPressed(definition));
        }

        if (placer != null)
        {
            placer.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(placer.Selected);
        }

        // Subscribe, then seed.
        OnMoneyChanged(GameManager.Instance.Money);
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
        }

        if (placer != null)
        {
            placer.SelectionChanged -= OnSelectionChanged;
        }

        foreach (Option option in options)
        {
            if (option.Button != null)
            {
                // RemoveAllListeners rather than RemoveListener, because the
                // lambdas added above are not the same delegate instances twice
                // and cannot be removed individually. Safe here only because
                // nothing else adds listeners to these buttons — which is why
                // their Inspector On Click lists are meant to stay empty.
                option.Button.onClick.RemoveAllListeners();
            }
        }
    }

    void OnOptionPressed(TowerDefinition definition)
    {
        if (placer == null)
        {
            Debug.LogError($"{name} has no TowerPlacer, so selecting a tower does nothing.", this);
            return;
        }

        placer.Select(definition);
    }

    void OnMoneyChanged(int money)
    {
        foreach (Option option in options)
        {
            if (option.Button == null || option.Definition == null)
            {
                continue;
            }

            // Greyed rather than hidden. A button that disappears when you cannot
            // afford it also takes away the information that the tower exists and
            // what it costs, which is exactly what a player who is short on money
            // is trying to find out.
            option.Button.interactable = money >= option.Definition.Cost;
        }
    }

    void OnSelectionChanged(TowerDefinition selected)
    {
        for (int i = 0; i < options.Length; i++)
        {
            Button button = options[i].Button;

            if (button == null || button.image == null)
            {
                continue;
            }

            button.image.color = options[i].Definition == selected ? selectedTint : originalTints[i];
        }
    }
}
