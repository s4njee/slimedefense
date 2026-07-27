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
    [Tooltip("The tower built on click. Phase 8 turns this single prefab into a selection.")]
    [SerializeField] Tower towerPrefab;

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

        if (towerPrefab == null)
        {
            Debug.LogError($"{name} has no tower prefab assigned, so clicks cannot build anything.", this);
            enabled = false;
            return;
        }

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
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Null when no pointing device is present — a gamepad-only session, or
        // the first frames before a device is detected. Reading position off it
        // regardless is a NullReferenceException per frame.


        // A press that landed on the HUD is not a press on the world behind it.
// Checked during hover rather than at press time so the node under the button
// does not highlight either — a node that lights up under the cursor and then
// refuses to build reads as a bug.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            found = null;
        }

        if (!overUi && Pointer.current != null)
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

    // Builds on the hovered node when the pointer goes down.
    void HandlePress()
    {
        if (Pointer.current == null || hovered == null)
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

        if (hovered.IsOccupied)
        {
            // Not an error and not worth a log line. Clicking an occupied node
            // is something players do constantly, and the node's color has
            // already said no.
            return;
        }

        // Building on a board that has already been lost only ever reads as a
        // bug. Placement is the one system that has to stand down on its own —
        // there is no global pause, by choice: Time.timeScale = 0 would also stop
        // the coroutines Phase 7's game-over screen needs to animate itself in.
        if (GameManager.Instance.IsGameOver)
        {
            return;
        }

        // Read off the prefab asset before anything is instantiated, which is why
        // Tower needed no changes this phase: a tower type added in Phase 8
        // arrives with its price attached instead of needing an entry in a lookup
        // table here.
        int price = towerPrefab.Cost;

        // Ask before building. The alternative — spend, build, refund on failure
        // — is a second code path that has to stay in sync with the first one
        // forever. Refusing costs nothing and logs nothing: a player clicking a
        // node they cannot pay for is the most ordinary thing they do.
        if (!GameManager.Instance.CanAfford(price))
        {
            return;
        }

        // Phase 7 adds an EventSystem.current.IsPointerOverGameObject() guard
        // above all of this, so a tap on the HUD does not also build a tower in
        // the world behind it.
        Tower built = hovered.Place(towerPrefab);

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
