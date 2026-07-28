using System.Collections.Generic;
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

    // The level-one art the prefab ships with, wherever in the tower it happens
    // to hang. Collected once in Awake, because after the first swap there is no
    // way to tell the prefab's own model from one this script instantiated.
    //
    // Searching the whole tower rather than only the model root is the fix for
    // the upgrade leaving two towers standing inside each other: nothing forces
    // the authored model to be parented under Model Root, and in practice none
    // of the tower prefabs parent it there.
    readonly List<GameObject> authoredModels = new List<GameObject>();

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

    BuildNode buildNode;
    BoxCollider selectionCollider;

    /// <summary>The stats this tower is running on.</summary>
    public TowerDefinition Definition => definition;

    /// <summary>The build pad that owns this tower, used by click selection.</summary>
    public BuildNode BuildNode => buildNode;

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
        // Both captured before any model swap can overwrite them.
        authoredFirePoint = firePoint;
        CollectAuthoredModels();
    }

    // Anything under the tower that draws something, at the moment the tower is
    // born. That is the prefab's own art and nothing else, since no level model
    // has been instantiated yet.
    //
    // Direct children only, and the Model Root itself is skipped: its subtree is
    // where instantiated models go, and anything already sitting inside it is
    // picked up by the second pass as art in its own right.
    void CollectAuthoredModels()
    {
        AddAuthoredModels(transform);

        if (modelRoot != null && modelRoot != transform)
        {
            AddAuthoredModels(modelRoot);
        }
    }

    void AddAuthoredModels(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child == modelRoot)
            {
                continue;
            }

            // A fire point marker is an empty transform and stays; only things
            // that render are art this tower might have to stop showing.
            if (child.GetComponentInChildren<Renderer>(true) != null)
            {
                authoredModels.Add(child.gameObject);
            }
        }
    }

    public void Initialize(TowerDefinition newDefinition)
    {
        definition = newDefinition;
        Level = 0;
        TotalSpent = definition != null ? definition.Cost : 0;

        RefreshModel();
    }

    /// <summary>
    /// Links a runtime tower back to its pad and creates a selection-only hitbox
    /// around the visible model. Tower art does not need gameplay colliders just
    /// to be clickable.
    /// </summary>
    public void AssignBuildNode(BuildNode node)
    {
        buildNode = node;
        RefreshSelectionCollider();
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

        // Keep the height, discard the rest. A model prefab's local Y is
        // meaningful — it is where the mesh's base sits relative to its pivot,
        // and every Meshy model pivots about its centre rather than its feet. Its
        // X and Z never are: a prefab made by dragging an object out of a scene
        // stores that object's scene coordinates, and worldPositionStays: false
        // faithfully applies them as an offset, putting the model a thousand
        // units from the tower that owns it.
        //
        // Towers themselves never hit this because BuildNode.Place instantiates
        // them with an explicit world position, which discards the prefab's
        // transform outright. Only models go through the parenting overload, so
        // only models need this.
        Vector3 local = modelInstance.transform.localPosition;
        modelInstance.transform.localPosition = new Vector3(0f, local.y, 0f);

        CenterModelHorizontally();

        // Hide the art the prefab was authored with. A tower that ships with a
        // model baked in should stop showing it the moment a level supplies one
        // of its own — otherwise the first upgrade leaves two towers standing
        // inside each other.
        //
        // Hidden rather than destroyed, because it belongs to the prefab rather
        // than to this script, and destroying another system's objects is a
        // habit that eventually deletes something that mattered.
        foreach (GameObject authored in authoredModels)
        {
            if (authored != null && authored != modelInstance)
            {
                authored.SetActive(false);
            }
        }

        AdoptFirePoint();
        RefreshSelectionCollider();
    }

    // Slides the model sideways until what it draws sits over the tower's own
    // axis, leaving its height alone.
    //
    // Zeroing the instance's local X and Z above is not enough, because the mesh
    // is rarely the prefab's root object — it is a nested prefab of the imported
    // FBX, one level down, and it carries scene coordinates of its own from
    // whenever it was dragged out of a scene. Frost2 holds its mesh 2.8 units to
    // one side and KeepTower3 holds its 3.3, which the tower's scale multiplies
    // into a model standing a dozen units off its pad.
    //
    // Measured from the rendered bounds rather than fixed up in each model
    // prefab, because that also moves the model's own Muzzle marker with it —
    // shifting the mesh alone inside a prefab would leave the muzzle hanging in
    // the air where the barrel used to be.
    void CenterModelHorizontally()
    {
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(false);

        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // World space, so the tower's rotation and scale need no arithmetic here.
        Vector3 drift = bounds.center - transform.position;
        modelInstance.transform.position -= new Vector3(drift.x, 0f, drift.z);
    }

    void RefreshSelectionCollider()
    {
        if (buildNode == null)
        {
            return;
        }

        if (selectionCollider == null)
        {
            GameObject hitbox = new GameObject("Tower Selection Hitbox");
            hitbox.layer = buildNode.gameObject.layer;
            hitbox.transform.SetParent(transform, false);
            selectionCollider = hitbox.AddComponent<BoxCollider>();
            selectionCollider.isTrigger = true;
        }

        // Active renderers only. Once an upgrade hides the level-one art, that
        // art is still in the hierarchy, and including it would size the click
        // target to a model the player can no longer see.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);

        if (renderers.Length == 0)
        {
            selectionCollider.center = new Vector3(0f, 1f, 0f);
            selectionCollider.size = new Vector3(1.5f, 2f, 1.5f);
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 scale = transform.lossyScale;
        selectionCollider.center = transform.InverseTransformPoint(bounds.center);
        selectionCollider.size = new Vector3(
            bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
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
