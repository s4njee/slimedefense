using UnityEngine;

/// <summary>
/// A tower standing on a <see cref="BuildNode"/>. It watches a radius, picks a
/// slime according to its definition's targeting rule, and fires a
/// <see cref="Projectile"/> at it on a fixed cooldown.
///
/// Phase 8 moved the numbers out to a <see cref="TowerDefinition"/> asset and
/// made the firing behavior overridable. The split is the rule this project uses
/// throughout: things that vary by *value* are data, and things that vary by
/// *behavior* are code. A splash tower is not a pebble tower with a splash
/// number set above zero — it does something structurally different when its
/// shot lands, so it is a subclass. A tower that costs more and reaches further
/// is a different asset.
///
/// <see cref="Range"/> is the literal radius of the physics query below, so the
/// gizmo a selected tower draws is exactly what it reaches.
/// </summary>
public class Tower : MonoBehaviour
{
    [Tooltip("The stats this tower runs on. Assigned on the prefab for a tower placed by hand, " +
             "and overwritten by TowerPlacer with the selected definition at build time.")]
    [SerializeField] TowerDefinition definition;

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
    // Raised from 32 to 64 for this phase. The size is a hard ceiling — slimes
    // beyond the last slot are simply not seen — and Phase 8 waves send groups of
    // mixed types instead of one prefab on a timer, so the old headroom was no
    // longer obviously enough.
    readonly Collider[] hits = new Collider[64];

    /// <summary>The stats this tower is running on.</summary>
    public TowerDefinition Definition => definition;

    /// <summary>What this tower cost to build.</summary>
    public int Cost => definition != null ? definition.Cost : 0;

    /// <summary>How far this tower reaches.</summary>
    public float Range => definition != null ? definition.Range : 0f;

    /// <summary>Damage this tower deals per shot that lands.</summary>
    protected float Damage => definition != null ? definition.Damage : 0f;

    /// <summary>Layers this tower's detection considers. Subclasses reuse it for their own queries.</summary>
    protected LayerMask SlimeMask => slimeMask;

    /// <summary>
    /// Hands this tower its stats. Called by <see cref="BuildNode"/> immediately
    /// after instantiating it, before the first Update runs.
    ///
    /// Passed in rather than baked into the prefab so the definition is the one
    /// place a tower type's numbers live. A prefab that also carried its own copy
    /// would be two sources of truth that agree right up until someone edits one.
    /// </summary>
    public void Initialize(TowerDefinition newDefinition)
    {
        definition = newDefinition;
    }

    void Start()
    {
        if (definition == null)
        {
            Debug.LogError($"{name} has no TowerDefinition, so it has no range, damage, or fire rate. " +
                           "Assign one on the prefab or build it through TowerPlacer.", this);
            enabled = false;
            return;
        }

        if (definition.ProjectilePrefab == null)
        {
            Debug.LogError($"{name}'s definition ({definition.name}) has no projectile prefab, so it " +
                           "can never shoot anything.", this);
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
        cooldown = 1f / definition.FireRate;
    }

    /// <summary>
    /// Returns the best slime inside <see cref="Range"/> by this tower's
    /// targeting rule, or null when the radius is empty.
    /// </summary>
    protected Slime FindTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Range, hits, slimeMask);

        Slime best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent, not GetComponent: the query returns a
            // collider, which now genuinely does sit on a child — the sprite
            // visual carries it. Searching upward survives that; GetComponent
            // returns null and the tower silently never fires.
            Slime slime = hits[i].GetComponentInParent<Slime>();

            if (slime == null)
            {
                continue;
            }

            float score = Score(slime);

            if (score > bestScore)
            {
                bestScore = score;
                best = slime;
            }
        }

        return best;
    }

    // One comparable number per targeting rule, so the loop above stays a single
    // "highest score wins" pass instead of three near-identical loops. Nearest
    // and lowest-health are negated because the loop takes the maximum and both
    // of those want the minimum.
    float Score(Slime slime)
    {
        switch (definition.Targeting)
        {
            case TargetingMode.Nearest:
                return -(slime.AimPosition - transform.position).sqrMagnitude;

            case TargetingMode.LowestHealth:
                return -slime.Health;

            // First in line is the default for a reason. Nearest is the obvious
            // rule and the wrong default: it happily re-targets a fresh slime
            // that wandered a little closer while the one about to reach the goal
            // walks out of range untouched, which reads to the player as the
            // tower being broken. The slime closest to the goal is the most
            // urgent threat by definition.
            default:
                return slime.RouteProgress;
        }
    }

    /// <summary>
    /// Launches a shot at <paramref name="target"/>. Override to change what
    /// leaves the tower; override <see cref="OnProjectileHit"/> to change what
    /// happens when it lands.
    /// </summary>
    protected virtual void Fire(Slime target)
    {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;

        Projectile projectile = Instantiate(definition.ProjectilePrefab, origin, Quaternion.identity);
        projectile.Launch(target, this);
    }

    /// <summary>
    /// Called by a <see cref="Projectile"/> this tower fired, when it arrives.
    /// The base tower deals its damage to the one slime it was aimed at.
    ///
    /// Putting this on the tower rather than on a projectile subclass keeps every
    /// tower type's behavior in the tower type, and leaves exactly one projectile
    /// class that knows how to travel and nothing else — which is what it was in
    /// Phase 5 and what it should stay.
    /// </summary>
    public virtual void OnProjectileHit(Slime target, Vector3 impactPoint)
    {
        if (target == null)
        {
            return;
        }

        target.TakeDamage(Damage);
    }

    // Drawn only while this tower is selected, unlike WaypointRoute's gizmos,
    // which are always visible. A route exists once; towers exist a dozen at a
    // time, and a dozen overlapping range spheres hide the map instead of
    // explaining it. Select a tower during Play to see what it will cover.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, Range);
    }
}
