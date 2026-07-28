using UnityEngine;

/// <summary>
/// A health bar that rides above a slime.
///
/// Built from two sprites rather than a world-space Canvas, deliberately. A
/// Canvas per enemy is its own rebuild and its own draw call, and by Phase 8's
/// waves there are a lot of enemies — a dozen Canvases updating every frame is a
/// real cost for two rectangles. Two SpriteRenderers and a localScale are almost
/// free, and they pool cleanly in Part D because there is no layout to rebuild.
///
/// Nothing here polls. The bar redraws when <see cref="Slime.HealthChanged"/>
/// fires, which is a handful of times per slime rather than sixty times a second
/// per slime.
///
/// Put this on a child of the slime, positioned above its head, with the fill
/// assigned. The fill's sprite wants its **pivot on the left**, so scaling x
/// shortens it from the right rather than from the middle.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Tooltip("The slime this bar reports. Leave empty to find it on a parent.")]
    [SerializeField] Slime slime;

    /// <summary>Where a fill sprite's pivot sits, which decides how it has to be drained.</summary>
    public enum FillOrigin
    {
        /// <summary>Unity's default. Scaling alone shrinks it toward its middle, so it is also shifted.</summary>
        Center,

        /// <summary>Pivot on the left edge. Scaling alone already drains it rightwards.</summary>
        Left,
    }

    [Tooltip("The coloured part. Its local x scale is driven from full down to zero.")]
    [SerializeField] Transform fill;

    [Tooltip("Where the fill sprite's pivot sits. Unity's built-in square and most imported " +
             "sprites are Center, which shrinks toward the middle and reads as a blob rather " +
             "than a bar — so the fill is shifted as well as scaled to keep its left edge still. " +
             "Set this to Left only if the sprite's pivot really is on its left edge.")]
    [SerializeField] FillOrigin fillOrigin = FillOrigin.Center;

    [Tooltip("Optional renderer on the fill, tinted from full to empty as health drops.")]
    [SerializeField] SpriteRenderer fillRenderer;

    [Tooltip("Colour at full health.")]
    [SerializeField] Color fullColor = new Color(0.45f, 0.85f, 0.35f);

    [Tooltip("Colour approached as health runs out.")]
    [SerializeField] Color emptyColor = new Color(0.9f, 0.25f, 0.2f);

    [Tooltip("Hide the bar entirely while the slime is undamaged. Keeps a wave of untouched " +
             "slimes from becoming a wall of full bars, and makes the ones being shot obvious.")]
    [SerializeField] bool hideWhenFull = true;

    [Tooltip("Turn the bar to face the camera each frame. Leave on unless something else " +
             "already billboards this object.")]
    [SerializeField] bool faceCamera = true;

    Camera targetCamera;
    Renderer[] renderers;
    bool subscribed;

    // The fill as authored: its full-health scale, where it sits, and how wide
    // its sprite is before scaling. Captured once so draining is measured against
    // the bar you designed rather than against whatever it was last frame —
    // otherwise each update shrinks the result of the last one.
    float fullScaleX = 1f;
    Vector3 basePosition;
    float spriteWidth = 1f;

    void Awake()
    {
        if (slime == null)
        {
            slime = GetComponentInParent<Slime>();
        }

        if (fill != null && fillRenderer == null)
        {
            fillRenderer = fill.GetComponent<SpriteRenderer>();
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        targetCamera = Camera.main;

        if (fill != null)
        {
            fullScaleX = fill.localScale.x;
            basePosition = fill.localPosition;
        }

        if (fillRenderer != null && fillRenderer.sprite != null)
        {
            // Unscaled width in local units. bounds already accounts for pixels
            // per unit, which is what makes this work for any sprite rather than
            // only for a 1x1 square.
            spriteWidth = fillRenderer.sprite.bounds.size.x;
        }
    }

    // OnEnable and OnDisable, not Start and OnDestroy. A pooled slime in Part D
    // is deactivated and reactivated rather than destroyed and recreated, and
    // this pairing is the one that survives that: the bar re-subscribes and
    // re-reads the health it was reset to, every time it comes back.
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
        if (!subscribed)
        {
            return;
        }

        subscribed = false;

        if (slime != null)
        {
            slime.HealthChanged -= OnHealthChanged;
        }
    }

    void Subscribe()
    {
        if (subscribed || slime == null)
        {
            return;
        }

        subscribed = true;
        slime.HealthChanged += OnHealthChanged;

        // Subscribe, then seed — the same rule the HUD needed. A bar that only
        // listened would sit at whatever width it was authored with until the
        // slime took its first hit.
        OnHealthChanged(slime.HealthNormalized);
    }

    void LateUpdate()
    {
        if (!faceCamera)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
            {
                return;
            }
        }

        // Copying the camera's rotation rather than looking at its position: a
        // bar that aims at the camera swings as the slime crosses the screen,
        // while one that copies the rotation stays parallel to the view and
        // reads as a flat overlay. Same choice SpriteBillboard makes.
        transform.rotation = targetCamera.transform.rotation;
    }

    void OnHealthChanged(float normalized)
    {
        if (fill != null)
        {
            float t = Mathf.Clamp01(normalized);

            Vector3 scale = fill.localScale;
            scale.x = fullScaleX * t;
            fill.localScale = scale;

            if (fillOrigin == FillOrigin.Center)
            {
                // A centre-pivot sprite shrinks toward its middle, which draws a
                // blob in the centre of the bar rather than a bar draining
                // rightwards. Holding the left edge still turns the same scale
                // into the shape people expect.
                float fullWidth = spriteWidth * fullScaleX;
                float leftEdge = basePosition.x - (fullWidth * 0.5f);

                Vector3 position = fill.localPosition;
                position.x = leftEdge + (fullWidth * t * 0.5f);
                fill.localPosition = position;
            }
        }

        if (fillRenderer != null)
        {
            fillRenderer.color = Color.Lerp(emptyColor, fullColor, normalized);
        }

        if (!hideWhenFull)
        {
            return;
        }

        bool visible = normalized < 0.999f;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
