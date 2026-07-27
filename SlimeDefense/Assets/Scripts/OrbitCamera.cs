using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the player orbit, pan, and zoom the camera around a point on the ground,
/// the way three.js OrbitControls does: the camera's position is never edited
/// directly, it is rebuilt every frame from a pivot, a yaw, a pitch, and a
/// distance.
///
/// **Right-drag orbits, middle-drag pans, the wheel zooms.** OrbitControls uses
/// left-drag to orbit and that is not available here: left-click builds towers,
/// and <see cref="TowerPlacer"/> acts on `wasPressedThisFrame` — the instant the
/// button goes down, before a drag could possibly be detected. Left-drag orbiting
/// would place a tower on whatever node the drag started over, every time. Moving
/// orbit to the right button costs nothing and leaves placement untouched.
///
/// On touch: one finger is still a tap to build, and two fingers pan, pinch to
/// zoom, and twist to turn. Pitch stays mouse-only, because the obvious touch
/// gesture for it — a two-finger vertical drag — is the same gesture as panning,
/// and a camera that sometimes tilts when you meant to slide is worse than one
/// that never tilts at all.
///
/// Roll is fixed at zero by construction. That matters beyond looking level:
/// <see cref="SpriteBillboard"/> copies the camera's rotation onto every slime,
/// so any roll here tilts every sprite in the game.
///
/// Put this on the Main Camera itself. Do not add a second camera for it — both
/// <see cref="TowerPlacer"/> and <see cref="SpriteBillboard"/> resolve
/// `Camera.main`, and a second object tagged MainCamera is the quiet breakage
/// Phase 4 warned about.
/// </summary>
[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    [Header("Orbit")]
    [Tooltip("Degrees turned per pixel of drag.")]
    [Min(0.01f)]
    [SerializeField] float orbitSpeed = 0.22f;

    [Tooltip("Lowest the camera may look from. Below about 10 degrees the view slides under the " +
             "terrain and the horizon fills the screen.")]
    [Range(1f, 89f)]
    [SerializeField] float pitchMin = 15f;

    [Tooltip("Highest the camera may look from. Straight down flattens the billboarded sprites " +
             "into slivers, so stop short of 90.")]
    [Range(1f, 89f)]
    [SerializeField] float pitchMax = 80f;

    [Header("Pan")]
    [Tooltip("World units moved per pixel of drag, per unit of distance. Scaled by distance so " +
             "panning feels the same zoomed in and zoomed out — a fixed rate crawls when you are " +
             "far away and flies when you are close.")]
    [Min(0f)]
    [SerializeField] float panSpeed = 0.0016f;

    [Tooltip("Height of the plane the pivot slides along. The terrain sits at y = 0, so 0 keeps " +
             "the focus on the ground.")]
    [SerializeField] float groundHeight;

    [Tooltip("Keep the pivot inside the terrain's footprint, so the player cannot pan off the " +
             "map and lose the level entirely. Leave empty to find the terrain in the scene.")]
    [SerializeField] Terrain boundsTerrain;

    [Tooltip("Untick to let the pivot go anywhere. Useful while building the level, unhelpful " +
             "for anyone playing it.")]
    [SerializeField] bool limitToTerrain = true;

    [Header("Zoom")]
    [Tooltip("How much one notch of scroll changes the distance. Applied multiplicatively, so a " +
             "notch covers the same proportion of the way in whether you are close or far.")]
    [Min(0.01f)]
    [SerializeField] float zoomSpeed = 0.12f;

    [Min(1f)]
    [SerializeField] float distanceMin = 8f;

    [Min(1f)]
    [SerializeField] float distanceMax = 120f;

    [Header("Feel")]
    [Tooltip("Seconds of catch-up applied to every change. 0 is instant and precise; around 0.1 " +
             "gives OrbitControls' damped glide. Uses unscaled time, so the camera still moves " +
             "if a future pause sets Time.timeScale to 0.")]
    [Range(0f, 0.5f)]
    [SerializeField] float damping = 0.08f;

    [Tooltip("Let two fingers twist to turn the camera. Off if players report the view spinning " +
             "while they meant to pinch — the two gestures overlap more on small screens.")]
    [SerializeField] bool enableTwistOrbit = true;

    // Where the camera is looking. Everything else is spherical coordinates
    // around this point, which is the whole of OrbitControls' model.
    Vector3 pivot;
    float yaw;
    float pitch;
    float distance;

    // What the transform is actually interpolating toward. Held separately so
    // damping smooths the result rather than the input.
    Vector3 targetPivot;
    float targetYaw;
    float targetPitch;
    float targetDistance;

    Vector2 lastTouchMidpoint;
    float lastTouchGap;
    float lastTouchAngle;
    bool twoFingerActive;

    void Awake()
    {
        if (boundsTerrain == null)
        {
            boundsTerrain = FindAnyObjectByType<Terrain>();
        }

        // Seed from wherever the camera was left in the Scene view, so the shot
        // an artist framed by hand is the shot the game opens on. Roll is read
        // and discarded here, which is what quietly un-tilts every billboarded
        // sprite the first time this runs.
        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);

        // The pivot is where the camera's forward ray crosses the ground plane,
        // which is the point it already appears to be looking at. Distance falls
        // out of the same intersection, so nothing jumps on the first frame.
        if (!TryGroundIntersection(out pivot, out distance))
        {
            distance = Mathf.Clamp((distanceMin + distanceMax) * 0.5f, distanceMin, distanceMax);
            pivot = transform.position + (transform.forward * distance);
            pivot.y = groundHeight;
        }

        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        distance = Mathf.Clamp(distance, distanceMin, distanceMax);
        pivot = ClampPivot(pivot);

        targetPivot = pivot;
        targetYaw = yaw;
        targetPitch = pitch;
        targetDistance = distance;

        Apply();
    }

    void LateUpdate()
    {
        ReadMouse();
        ReadTouch();

        targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);
        targetDistance = Mathf.Clamp(targetDistance, distanceMin, distanceMax);
        targetPivot = ClampPivot(targetPivot);

        if (damping <= 0f)
        {
            pivot = targetPivot;
            yaw = targetYaw;
            pitch = targetPitch;
            distance = targetDistance;
        }
        else
        {
            // Frame-rate independent exponential catch-up. Unscaled, so the
            // camera keeps responding if Time.timeScale is ever zeroed.
            float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / damping);

            pivot = Vector3.Lerp(pivot, targetPivot, t);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);
            pitch = Mathf.Lerp(pitch, targetPitch, t);
            distance = Mathf.Lerp(distance, targetDistance, t);
        }

        Apply();
    }

    // Rebuilds the transform from the four values. This is the entire camera:
    // position is derived, never accumulated, so no amount of dragging can drift
    // the roll or leave the camera somewhere it cannot get back from.
    void Apply()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        transform.rotation = rotation;
        transform.position = pivot - (rotation * Vector3.forward * distance);
    }

    void ReadMouse()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();

        if (mouse.rightButton.isPressed)
        {
            targetYaw += delta.x * orbitSpeed;
            targetPitch -= delta.y * orbitSpeed;
        }
        else if (mouse.middleButton.isPressed)
        {
            Pan(-delta);
        }

        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Multiplicative, so one notch covers the same fraction of the
            // remaining distance whether you are close or far. Linear zoom
            // crawls when far out and slams into the minimum when close in.
            targetDistance *= Mathf.Exp(-Mathf.Sign(scroll) * zoomSpeed);
        }
    }

    void ReadTouch()
    {
        Touchscreen screen = Touchscreen.current;

        if (screen == null)
        {
            twoFingerActive = false;
            return;
        }

        Vector2 a = Vector2.zero;
        Vector2 b = Vector2.zero;
        int count = 0;

        foreach (var touch in screen.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            if (count == 0)
            {
                a = touch.position.ReadValue();
            }
            else if (count == 1)
            {
                b = touch.position.ReadValue();
            }

            count++;
        }

        // One finger is a tap to build, which TowerPlacer owns. Anything above
        // two is almost certainly a palm, and guessing at it produces a camera
        // that lurches for no reason the player can see.
        if (count != 2)
        {
            twoFingerActive = false;
            return;
        }

        Vector2 midpoint = (a + b) * 0.5f;
        float gap = Vector2.Distance(a, b);
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

        // The frame the gesture begins has no previous sample to compare
        // against, and treating the first frame as movement makes every pinch
        // start with a jump.
        if (!twoFingerActive)
        {
            twoFingerActive = true;
            lastTouchMidpoint = midpoint;
            lastTouchGap = gap;
            lastTouchAngle = angle;
            return;
        }

        Pan(-(midpoint - lastTouchMidpoint));

        if (lastTouchGap > 0.01f && gap > 0.01f)
        {
            // Pinch is a ratio, which matches the multiplicative zoom above and
            // means the same finger movement does the same thing at any zoom.
            targetDistance *= lastTouchGap / gap;
        }

        if (enableTwistOrbit)
        {
            targetYaw += Mathf.DeltaAngle(lastTouchAngle, angle);
        }

        lastTouchMidpoint = midpoint;
        lastTouchGap = gap;
        lastTouchAngle = angle;
    }

    // Slides the pivot across the ground plane in screen-relative directions, so
    // dragging right always moves the world right regardless of which way the
    // camera is currently facing.
    void Pan(Vector2 delta)
    {
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = flat * new Vector3(delta.x, 0f, delta.y);

        // Scaled by distance: panning a screen's width should cover a screen's
        // worth of world, and a screen is wider in world units when zoomed out.
        targetPivot += move * (panSpeed * distance);
    }

    Vector3 ClampPivot(Vector3 value)
    {
        value.y = groundHeight;

        if (!limitToTerrain || boundsTerrain == null || boundsTerrain.terrainData == null)
        {
            return value;
        }

        Vector3 origin = boundsTerrain.transform.position;
        Vector3 size = boundsTerrain.terrainData.size;

        value.x = Mathf.Clamp(value.x, origin.x, origin.x + size.x);
        value.z = Mathf.Clamp(value.z, origin.z, origin.z + size.z);

        return value;
    }

    // Where the camera's forward ray crosses the ground plane, if it does at all.
    bool TryGroundIntersection(out Vector3 point, out float rayDistance)
    {
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        Ray ray = new Ray(transform.position, transform.forward);

        if (ground.Raycast(ray, out rayDistance) && rayDistance > 0.01f)
        {
            point = ray.GetPoint(rayDistance);
            return true;
        }

        point = Vector3.zero;
        rayDistance = 0f;
        return false;
    }

    // Euler angles come back in 0..360, so a camera looking slightly downward
    // reads as 350 rather than -10 and would clamp to the wrong end.
    static float NormalizePitch(float value)
    {
        return value > 180f ? value - 360f : value;
    }
}
