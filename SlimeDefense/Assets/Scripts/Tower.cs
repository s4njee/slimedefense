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

    [Tooltip("Empty child the level models are parented to, at the tower's own origin. Leaving " +
             "this empty means the tower keeps whatever model is built into its prefab and never " +
             "changes it.")]
    [SerializeField] Transform modelRoot;

    [Tooltip("Name of a transform inside a level model to fire from, if it has one. A taller " +
             "upgraded model wants a higher muzzle, and putting that marker in the model keeps " +
             "the two from drifting apart. Falls back to the tower's own Fire Point.")]
    [SerializeField] string modelFirePointName = "Muzzle";

    // Seconds until this tower may fire again.
    float cooldown;

    // The level model currently on show, and the fire point the prefab was
    // authored with. The authored one is kept so a level whose model has no
    // muzzle marker falls back to it rather than to the previous model's.
    GameObject modelInstance;
    Transform authoredFirePoint;

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

    /// <summary>
    /// Which rung of the definition's ladder this tower is on. Zero is as built.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>Everything the player has spent on this tower — the build plus every upgrade.</summary>
    public int TotalSpent { get; private set; }

    /// <summary>True when there is another rung to buy.</summary>
    public bool CanUpgrade => definition != null && Level < definition.MaxLevel;

    /// <summary>What the next upgrade costs, or 0 at maximum level.</summary>
    public int UpgradeCost => definition != null ? definition.UpgradeCost(Level) : 0;

    /// <summary>
    /// What selling returns. Rounded down, so selling and rebuilding is never
    /// accidentally profitable through a rounding seam.
    /// </summary>
    public int SellValue => definition != null
        ? Mathf.FloorToInt(TotalSpent * definition.SellRefund)
        : 0;

    /// <summary>What this tower cost to build.</summary>
    public int Cost => definition != null ? definition.Cost : 0;

    /// <summary>How far this tower reaches, at its current level.</summary>
    public float Range => CurrentLevel != null ? CurrentLevel.Range : 0f;

    /// <summary>Damage this tower deals per shot that lands, at its current level.</summary>
    protected float Damage => CurrentLevel != null ? CurrentLevel.Damage : 0f;

    /// <summary>Shots per second at the current level.</summary>
    public float FireRate => CurrentLevel != null ? CurrentLevel.FireRate : 1f;

    /// <summary>The stat row this tower is currently running on.</summary>
    public TowerLevel CurrentLevel => definition != null ? definition.GetLevel(Level) : null;

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
    void Awake()
    {
        // Captured before any model swap can overwrite it.
        authoredFirePoint = firePoint;
    }

    public void Initialize(TowerDefinition newDefinition)
    {
        definition = newDefinition;
        Level = 0;
        TotalSpent = definition != null ? definition.Cost : 0;

        RefreshModel();
    }

    /// <summary>
    /// Advances this tower one rung and records what it cost. Returns false when
    /// it is already at the top.
    ///
    /// Deliberately does no money handling. A tower has no business knowing
    /// whether the player could afford it — the caller checks and spends, exactly
    /// as <see cref="TowerPlacer"/> does when building one. That keeps every
    /// transaction in the same place instead of split between a controller and a
    /// component that mostly wants to shoot things.
    /// </summary>
    public bool Upgrade()
    {
        if (!CanUpgrade)
        {
            return false;
        }

        TotalSpent += UpgradeCost;
        Level++;

        RefreshModel();

        // The cooldown is left running rather than reset. A faster fire rate
        // should not also hand out a free shot the instant it is bought, and
        // Update divides by the new rate on the next tick regardless.
        return true;
    }

    /// <summary>
    /// Shows the model belonging to the current level, if it has one of its own.
    ///
    /// A level with no model keeps whatever is already on show, so only the
    /// levels that actually change appearance need a prefab — a three-rung tower
    /// that only changes at the top is two empty fields, not two duplicates.
    /// </summary>
    void RefreshModel()
    {
        GameObject prefab = CurrentLevel != null ? CurrentLevel.Model : null;

        if (modelRoot == null || prefab == null)
        {
            return;
        }

        if (modelInstance != null)
        {
            Destroy(modelInstance);
        }

        // worldPositionStays: false is the argument that matters. It keeps the
        // prefab's own local position, rotation, and scale relative to the new
        // parent — and that local position is where each mesh's base offset
        // lives, since every Meshy model pivots about its own centre rather than
        // its feet. The two-argument overload defaults to true, recomputes the
        // offset from world space, and buries or floats the model instead.
        modelInstance = Instantiate(prefab, modelRoot, false);

        // Hide anything authored into the prefab under the model root. A tower
        // that ships with a model baked in should stop showing it the moment a
        // level supplies one of its own — otherwise the first upgrade leaves two
        // towers standing inside each other.
        //
        // Hidden rather than destroyed, because it belongs to the prefab rather
        // than to this script, and destroying another system's objects is a
        // habit that eventually deletes something that mattered.
        foreach (Transform child in modelRoot)
        {
            if (child.gameObject != modelInstance)
            {
                child.gameObject.SetActive(false);
            }
        }

        AdoptFirePoint();
    }

    // Lets a model carry its own muzzle, so a taller upgrade fires from its own
    // top rather than from wherever the level-one model's top used to be.
    void AdoptFirePoint()
    {
        firePoint = authoredFirePoint;

        if (modelInstance == null || string.IsNullOrEmpty(modelFirePointName))
        {
            return;
        }

        foreach (Transform child in modelInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == modelFirePointName)
            {
                firePoint = child;
                return;
            }
        }
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

        // Covers a tower dropped into the scene by hand, which never gets an
        // Initialize call. Guarded so a tower built through BuildNode does not
        // instantiate its model twice.
        if (modelInstance == null)
        {
            RefreshModel();
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
        cooldown = 1f / FireRate;
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

            // A flyer a tower cannot reach is not a target it should be holding
            // its shot for. Skipping here rather than at fire time means a
            // ground-only tower goes on shooting whatever else is in range
            // instead of standing idle with a flyer overhead.
            if (slime.IsFlying && !definition.CanHitFlying)
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
