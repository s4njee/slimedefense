using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Turns a click or a tap into a tower. Each frame it casts a ray from the
/// pointer into the world, finds the <see cref="BuildNode"/> under it, and
/// builds there when the pointer is pressed.
///
/// One placer serves the whole scene. Pointer input is a single global question
/// — what is under the pointer right now — and answering it once per frame is
/// cheaper and simpler than every node asking whether it was the one clicked.
///
/// Input comes from the Input System package rather than the legacy Input class,
/// which this project's settings disable outright. `Pointer.current` resolves to
/// the mouse on desktop and WebGL and to the touchscreen on Android, so one code
/// path covers every target platform.
/// </summary>
public class TowerPlacer : MonoBehaviour
{
    [Tooltip("The tower type selected when the game starts, before the player picks one. " +
             "Phase 8 turned the single prefab this used to hold into a choice.")]
    [SerializeField] TowerDefinition defaultDefinition;

    [Tooltip("Layers the placement ray may hit. Set this to the BuildNode layer only — " +
             "this mask is what makes building on the path impossible.")]
    [SerializeField] LayerMask buildNodeMask;

    [Tooltip("Camera the ray is cast from. Leave empty to use Camera.main.")]
    [SerializeField] Camera placementCamera;

    [Tooltip("How far the ray travels. Must comfortably exceed the distance from the " +
             "camera to the far end of the terrain.")]
    [Min(1f)]
    [SerializeField] float maxRayDistance = 500f;

    // The node under the pointer, or null. Held between frames so the highlight
    // can be cleared from the node the pointer just left.
    BuildNode hovered;

    // Whether the pointer was over the HUD when the hover was last updated.
    bool pointerOverUi;

    /// <summary>
    /// The tower type the next click will build. Set by
    /// <see cref="TowerPicker"/>; never null once Start has run, so a player who
    /// never touches the picker can still build.
    /// </summary>
    public TowerDefinition Selected { get; private set; }

    /// <summary>Raised when the selection changes, so the picker can show which button is active.</summary>
    public event System.Action<TowerDefinition> SelectionChanged;

    /// <summary>
    /// The node whose tower is currently selected, or null. Clicking an occupied
    /// node selects it; clicking anywhere else in the world clears it.
    /// </summary>
    public BuildNode SelectedNode { get; private set; }

    /// <summary>The tower currently selected for upgrade or sale, or null.</summary>
    public Tower SelectedTower => SelectedNode != null ? SelectedNode.Tower : null;

    /// <summary>
    /// Raised with the selected node, or null when the selection is cleared, so
    /// the inspector panel can show and hide itself without polling.
    /// </summary>
    public event System.Action<BuildNode> TowerSelectionChanged;

    /// <summary>
    /// Chooses the tower type built by subsequent clicks. Affordability is
    /// deliberately not checked here — the player is allowed to select a tower
    /// they cannot yet afford, and find out at the node rather than being unable
    /// to look at it.
    /// </summary>
    public void Select(TowerDefinition definition)
    {
        if (definition == null || !definition.IsValid)
        {
            Debug.LogWarning($"{name} was asked to select a missing or incomplete tower definition.", this);
            return;
        }

        if (Selected == definition)
        {
            return;
        }

        Selected = definition;
        SelectionChanged?.Invoke(Selected);
    }

    /// <summary>
    /// Clears the placed-tower selection, hiding the inspector panel.
    /// </summary>
    public void ClearTowerSelection()
    {
        if (SelectedNode == null)
        {
            return;
        }

        SelectedNode = null;
        TowerSelectionChanged?.Invoke(null);
    }

    /// <summary>
    /// Buys the next level for the selected tower, or returns false if there is
    /// nothing selected, nothing left to buy, or not enough money.
    ///
    /// The transaction lives here rather than on <see cref="Tower"/> or on the
    /// panel: this script already checks affordability and spends when building,
    /// and one place that touches the balance is much easier to keep correct than
    /// three that each nearly do.
    /// </summary>
    public bool TryUpgradeSelected()
    {
        Tower tower = SelectedTower;

        if (tower == null || !tower.CanUpgrade || GameManager.Instance == null)
        {
            return false;
        }

        int price = tower.UpgradeCost;

        // Check, apply, then charge — the same order as building, and for the
        // same reason. Upgrade() refuses at max level, and charging before that
        // refusal would take money for a level the player never got.
        if (!GameManager.Instance.CanAfford(price))
        {
            return false;
        }

        if (!tower.Upgrade())
        {
            return false;
        }

        GameManager.Instance.TrySpend(price);
        TowerSelectionChanged?.Invoke(SelectedNode);
        return true;
    }

    /// <summary>
    /// Sells the selected tower, refunds a fraction of what it cost, and frees
    /// its node.
    /// </summary>
    public bool SellSelected()
    {
        Tower tower = SelectedTower;

        if (tower == null)
        {
            return false;
        }

        // Read before the tower is destroyed. Afterwards the reference is a
        // fake-null whose every member access throws, and SellValue is a member.
        int refund = tower.SellValue;
        BuildNode node = SelectedNode;

        if (!node.Clear())
        {
            return false;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(refund);
        }

        ClearTowerSelection();
        return true;
    }

    void SelectTower(BuildNode node)
    {
        SelectedNode = node;
        TowerSelectionChanged?.Invoke(SelectedNode);
    }

    void Start()
    {
        if (placementCamera == null)
        {
            // Camera.main returns the camera tagged MainCamera. Convenient now,
            // and worth assigning explicitly once Phase 7 adds more cameras.
            placementCamera = Camera.main;
        }

        if (placementCamera == null)
        {
            Debug.LogError($"{name} has no camera to cast from and found none tagged MainCamera. " +
                           "Assign one to the Placement Camera field.", this);
            enabled = false;
            return;
        }

        if (defaultDefinition == null || !defaultDefinition.IsValid)
        {
            Debug.LogError($"{name} has no usable default tower definition, so clicks cannot build " +
                           "anything until the player picks a tower.", this);
            enabled = false;
            return;
        }

        Select(defaultDefinition);

        // A mask of 0 matches no layers at all, so every ray misses and every
        // click silently does nothing. That reads as broken code rather than an
        // empty dropdown, which is exactly why it is worth saying out loud.
        if (buildNodeMask == 0)
        {
            Debug.LogWarning($"{name} has an empty Build Node Mask, so no node can ever be hit. " +
                             "Set it to the BuildNode layer.", this);
        }

        // Checked here and not stored. Every Awake in the scene has run by now,
        // so a missing manager is a missing GameObject rather than an ordering
        // problem — and towers being free is not a state worth leaving the game
        // quietly running in.
        if (GameManager.Instance == null)
        {
            Debug.LogError($"{name} found no GameManager, so towers would cost nothing. " +
                           "Add a GameManager to the scene.", this);
            enabled = false;
        }
    }

    void Update()
    {
        // Order matters, and not only for tidiness. A finger has no hover state:
        // its first contact with the screen is the hover and the press in the
        // same frame. Updating the hover first means the node under the finger
        // is already known by the time the press is handled. Swap these two and
        // the mouse still works perfectly while touch places towers a node
        // behind — a bug that only ever shows up on the device.
        UpdateHover();
        HandlePress();
    }

    // Finds the node under the pointer and moves the highlight to it.
    void UpdateHover()
    {
        BuildNode found = null;

        // A pointer over the HUD is not a pointer on the world behind it. Checked
        // here during hover rather than at press time, so the node under a button
        // does not highlight either — a node that lights up under the cursor and
        // then refuses to build reads as a bug.
        //
        // Worth testing on a touchscreen specifically. This is answered from the
        // UI module's view of the pointer, which on touch can lag the raw pointer
        // by a frame, so the mouse case can look perfect while a tap near a
        // button's edge occasionally still builds.
        // Recorded rather than used and discarded, because HandlePress needs the
        // same answer: a press on the HUD must not clear the tower selection, or
        // pressing Upgrade would deselect the very tower it is upgrading before
        // the button's own click is delivered.
        pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Null when no pointing device is present — a gamepad-only session, or
        // the first frames before a device is detected. Reading position off it
        // regardless is a NullReferenceException per frame.
        if (!pointerOverUi && Pointer.current != null)
        {
            Vector2 screenPoint = Pointer.current.position.ReadValue();
            Ray ray = placementCamera.ScreenPointToRay(screenPoint);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, buildNodeMask))
            {
                // GetComponentInParent, not GetComponent: the ray hits a
                // collider, and the day a node gets a nicer model with the
                // collider on a child, GetComponent returns null and clicks stop
                // registering for no visible reason.
                found = hit.collider.GetComponentInParent<BuildNode>();
            }
        }

        if (found == hovered)
        {
            return;
        }

        if (hovered != null)
        {
            hovered.SetHovered(false);
        }

        hovered = found;

        if (hovered != null)
        {
            hovered.SetHovered(true);
        }
    }

    // Builds on the hovered node when the pointer goes down, or selects the tower
    // already standing there.
    void HandlePress()
    {
        if (Pointer.current == null)
        {
            return;
        }

        // wasPressedThisFrame is true on exactly the frame the button goes down.
        // isPressed is true for every frame it stays down, which would build a
        // tower per frame for as long as the button is held.
        if (!Pointer.current.press.wasPressedThisFrame)
        {
            return;
        }

        // The HUD owns this press entirely. Returning rather than falling through
        // is what stops a click on the Upgrade button from also clearing the
        // selection it is acting on.
        if (pointerOverUi)
        {
            return;
        }

        // A press on empty ground means "never mind", which is the only way to
        // dismiss the inspector panel without a close button.
        if (hovered == null)
        {
            ClearTowerSelection();
            return;
        }

        if (hovered.IsOccupied)
        {
            // Phase 4 ignored this click and said so in a comment. Part B gives it
            // a job: the node already holds a reference to the tower standing on
            // it, so selecting one is a lookup rather than a search.
            SelectTower(hovered);
            return;
        }

        // Building somewhere else means the player has moved on from whatever was
        // selected.
        ClearTowerSelection();

        // Building on a board that has already been lost only ever reads as a
        // bug. Placement is the one system that has to stand down on its own —
        // there is no global pause, by choice: Time.timeScale = 0 would also stop
        // the coroutines Phase 7's game-over screen needs to animate itself in.
        if (GameManager.Instance.IsGameOver)
        {
            return;
        }

        if (Selected == null || !Selected.IsValid)
        {
            return;
        }

        // Read off the definition asset before anything is instantiated. This is
        // why adding a tower type needs no changes here: a new type arrives with
        // its own price rather than needing an entry in a lookup table.
        int price = Selected.Cost;

        // Ask before building. The alternative — spend, build, refund on failure
        // — is a second code path that has to stay in sync with the first one
        // forever. Refusing costs nothing and logs nothing: a player clicking a
        // node they cannot pay for is the most ordinary thing they do.
        if (!GameManager.Instance.CanAfford(price))
        {
            return;
        }

        Tower built = hovered.Place(Selected);

        // Place returns null when it refuses. Charging before this line pays for
        // towers that were never built, and the balance drifts down over a long
        // run in a way that looks like a rounding bug and is not.
        if (built == null)
        {
            return;
        }

        GameManager.Instance.TrySpend(price);
    }
}
