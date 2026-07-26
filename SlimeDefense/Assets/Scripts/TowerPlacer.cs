using UnityEngine;
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

        // Null when no pointing device is present — a gamepad-only session, or
        // the first frames before a device is detected. Reading position off it
        // regardless is a NullReferenceException per frame.
        if (Pointer.current != null)
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

        // Phase 6 checks and spends currency here. Phase 7 adds an
        // EventSystem.current.IsPointerOverGameObject() guard above, so a tap on
        // the HUD does not also build a tower in the world behind it.
        hovered.Place(towerPrefab);
    }
}
