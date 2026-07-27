using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The screen shown when the run ends, either way. It listens for
/// <see cref="GameManager.GameOver"/> and <see cref="GameManager.Victory"/>,
/// writes the matching line, and offers a restart.
///
/// The panel object stays active for the whole run and hides itself with a
/// CanvasGroup instead of SetActive(false). That is not a style preference: a
/// deactivated GameObject does not run OnEnable, so a panel that switched itself
/// off could never hear the event telling it to come back. Alpha and raycast
/// blocking make it invisible and unclickable while leaving it listening.
///
/// Attach this to the panel, which must carry a CanvasGroup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class EndOfRunPanel : MonoBehaviour
{
    [Tooltip("Where the outcome is written.")]
    [SerializeField] TMP_Text resultLabel;

    [Tooltip("Reloads the scene. Wired in code rather than through the Inspector's On Click " +
             "list, so renaming the method is a compiler error instead of a button that " +
             "silently stops working.")]
    [SerializeField] Button restartButton;

    [Tooltip("Shown when every wave has been cleared.")]
    [SerializeField] string victoryText = "All waves cleared.";

    [Tooltip("Shown when lives run out. A serialized field rather than a literal, so the " +
             "wording is a decision rather than a recompile.")]
    [SerializeField] string defeatText = "The slimes got through.";

    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    bool subscribed;

    // Subscribed from both OnEnable and Start for the same reason as Hud:
    // OnEnable can run before the GameManager's Awake, and a panel that quietly
    // failed to subscribe would never appear at all — which looks exactly like a
    // game with no end screen rather than a listener that was never attached.
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

        GameManager.Instance.GameOver += OnGameOver;
        GameManager.Instance.Victory += OnVictory;

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartPressed);
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
            GameManager.Instance.GameOver -= OnGameOver;
            GameManager.Instance.Victory -= OnVictory;
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(OnRestartPressed);
        }
    }

    void OnGameOver()
    {
        Show(defeatText);
    }

    void OnVictory()
    {
        Show(victoryText);
    }

    void Show(string message)
    {
        if (resultLabel != null)
        {
            resultLabel.text = message;
        }

        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;

        // Without these the hidden panel is a full-screen invisible sheet that
        // still swallows every click meant for the board behind it.
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    void OnRestartPressed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Restart();
        }
    }
}
