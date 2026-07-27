using UnityEngine;

/// <summary>
/// A tower standing on a <see cref="BuildNode"/>. Phase 5 turns it into a
/// weapon: it watches a radius, picks the slime furthest along the route, and
/// fires a <see cref="Projectile"/> at it on a fixed cooldown.
///
/// <see cref="Range"/> stopped being decorative here. The gizmo it draws is now
/// the literal radius of the physics query below, so what a selected tower shows
/// in the Scene view is exactly what it reaches.
///
/// Phase 6 added money without touching this file. <see cref="Cost"/> was
/// already here with a public accessor, and <see cref="TowerPlacer"/> reads it
/// off the prefab before building — a tower has no business knowing whether the
/// player could afford it.
/// </summary>
public class Tower : MonoBehaviour
{
    [Tooltip("What this tower costs to build. TowerPlacer reads this off the prefab and charges " +
             "it, so a new tower type in Phase 8 arrives with its price attached.")]
    [Min(0)]
    [SerializeField] int cost = 50;

    [Tooltip("Detection radius. Slimes inside it can be shot; slimes outside it are invisible " +
             "to this tower. Measured to the slime's collider, so an oversized slime collider " +
             "quietly extends every tower's reach.")]
    [Min(0f)]
    [SerializeField] float range = 6f;

    [Tooltip("Damage dealt by each projectile that lands. At 3, a 10-health slime takes four hits.")]
    [Min(0f)]
    [SerializeField] float damage = 3f;

    [Tooltip("Shots per second. Four hits at 1.5 is about 2.7 seconds of sustained fire, " +
             "which is roughly how long a slime spends crossing a 6-unit radius through the middle. " +
             "So a tower kills what it catches squarely and loses what grazes it — that margin is " +
             "what makes where you put the tower the interesting decision.")]
    [Min(0.01f)]
    [SerializeField] float fireRate = 1.5f;

    [Tooltip("The projectile fired at targets. One prefab serves every tower; damage is passed " +
             "to it at launch rather than baked into it.")]
    [SerializeField] Projectile projectilePrefab;

    [Tooltip("Where shots spawn from. Leave empty to fire from the tower's own origin, which " +
             "sits at its base — so shots appear to come out of the ground.")]
    [SerializeField] Transform firePoint;

    [Tooltip("Layers detection considers. Set this to the Slime layer only. A mask answers " +
             "'is this a slime' before the query runs, instead of the query returning terrain, " +
             "nodes, and other towers for this script to sort out afterwards.")]
    [SerializeField] LayerMask slimeMask;

    // Seconds until this tower may fire again.
    float cooldown;

    // Allocated once and reused by every query, because the non-allocating
    // overload of OverlapSphere fills a buffer instead of returning a fresh
    // array. A dozen towers each allocating an array per shot is exactly the
    // per-frame garbage that shows up as stutter on mobile in Phase 10.
    //
    // The size is a hard ceiling: slimes beyond the 32nd inside the radius are
    // simply not seen. Ample today, and worth remembering before Phase 8 makes
    // waves much denser.
    readonly Collider[] hits = new Collider[32];

    /// <summary>What this tower costs to build.</summary>
    public int Cost => cost;

    /// <summary>How far this tower reaches.</summary>
    public float Range => range;

    void Start()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError($"{name} has no projectile prefab assigned, so it can never shoot anything. " +
                           "Assign Projectile.prefab on the Tower prefab.", this);
            enabled = false;
            return;
        }

        // A mask of 0 matches no layers, so every query comes back empty and the
        // tower sits there doing nothing with no error to explain it. Same trap
        // as TowerPlacer's build mask, same reason for saying it out loud.
        if (slimeMask == 0)
        {
            Debug.LogWarning($"{name} has an empty Slime Mask, so it can never detect a slime. " +
                             "Set it to the Slime layer.", this);
        }
    }

    void Update()
    {
        cooldown -= Time.deltaTime;

        // The early return is doing real work. Searching every frame and then
        // throwing the result away because the tower is still reloading does the
        // expensive part of the job sixty times a second to use it once or
        // twice. Searching only when the answer can be acted on costs nothing
        // and scales to a map full of towers.
        if (cooldown > 0f)
        {
            return;
        }

        Slime target = FindTarget();

        if (target == null)
        {
            return;
        }

        Fire(target);
        cooldown = 1f / fireRate;
    }

    /// <summary>
    /// Returns the slime furthest along the route inside <see cref="range"/>, or
    /// null when the radius is empty.
    ///
    /// First-in-line, not nearest. Nearest is the obvious rule and the wrong
    /// default: it happily re-targets a fresh slime that wandered a little
    /// closer while the one about to reach the goal walks out of range untouched,
    /// which reads to the player as the tower being broken. The slime closest to
    /// the goal is the most urgent threat by definition, so that is where damage
    /// goes. Phase 8 makes this a per-tower choice.
    /// </summary>
    Slime FindTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, range, hits, slimeMask);

        Slime best = null;
        float bestProgress = float.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent, not GetComponent: the query returns a
            // collider, which may well sit on a child once the slime model gains
            // structure. Searching upward survives that. GetComponent returns
            // null and the tower silently never fires.
            Slime slime = hits[i].GetComponentInParent<Slime>();

            if (slime == null)
            {
                continue;
            }

            if (slime.RouteProgress > bestProgress)
            {
                bestProgress = slime.RouteProgress;
                best = slime;
            }
        }

        return best;
    }

    void Fire(Slime target)
    {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;

        Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        projectile.Launch(target, damage);
    }

    // Drawn only while this tower is selected, unlike WaypointRoute's gizmos,
    // which are always visible. A route exists once; towers exist a dozen at a
    // time, and a dozen overlapping range spheres hide the map instead of
    // explaining it. Select a tower during Play to see what it will cover.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
