using UnityEngine;

/// <summary>
/// Walks a slime along a <see cref="WaypointRoute"/> from the spawn point to the
/// goal, then despawns. Phase 5 gave it health that towers can subtract from and
/// a death of its own; Phase 6 attached a price to both outcomes — killing one
/// pays the player, and letting one reach the goal costs a life.
/// </summary>
public class Slime : MonoBehaviour
{
    [SerializeField] float speed = 3f;
    [SerializeField] float health = 10f;

    [Tooltip("How close the slime must get to a waypoint before moving to the next one.")]
    [SerializeField] float arriveDistance = 0.15f;

    [Tooltip("Money paid to the player when this slime dies. An int because money is counted, " +
             "not measured: a fractional balance means CanAfford(50) says no while a HUD " +
             "rounding to whole numbers displays 50.")]
    [Min(0)]
    [SerializeField] int reward = 10;

    [Tooltip("Lives taken when this slime reaches the goal. A field rather than a hardcoded 1 " +
             "so Phase 8's boss slime, which costs five, is a number on a prefab instead of a " +
             "new code path.")]
    [Min(0)]
    [SerializeField] int lifeCost = 1;

    [Tooltip("Incoming damage is multiplied by this before it is subtracted. 0.5 is armour that " +
             "halves every hit; 1 takes damage as dealt. Phase 5 predicted this exact field when " +
             "it put damage interpretation on the slime rather than on the projectile — armour " +
             "arrives without a single line changing in Projectile or Tower.")]
    [Min(0f)]
    [SerializeField] float damageMultiplier = 1f;

    [Tooltip("Only towers whose definition allows it can shoot this slime. A flag rather than a " +
             "subclass: flying changes no behaviour here, it changes who is allowed to target it.")]
    [SerializeField] bool isFlying;

    [Tooltip("How far above the route a flying slime rides. Applied when the route is assigned; " +
             "movement keeps whatever height it starts at, so nothing else has to know about it.")]
    [Min(0f)]
    [SerializeField] float flightHeight;

    [Tooltip("Extra yaw applied after aiming down the path, in degrees. Unity's LookRotation " +
             "points a transform's +Z at the target, but the imported slime model faces -Z, " +
             "so it needs 180 here to walk face-first instead of backwards.")]
    [SerializeField] float modelYawOffset = -180f;

    WaypointRoute route;
    int targetIndex;
    SpriteRenderer aimRenderer;

    // Speed multiplier from frost towers, and when it expires. Applied as a
    // multiplier at movement time rather than by writing to `speed`, so there is
    // no original value to remember and restore — and nothing to get wrong when
    // Phase 8's pooling reuses a slime that was slowed when it died.
    float slowFactor = 1f;
    float slowUntil;

    // True once this slime has been counted onto the board, so it is counted off
    // exactly once no matter which way it leaves.
    bool registered;

    // Set the moment an outcome is decided. Destroy is deferred to the end of the
    // frame, so without this a second projectile landing in the same frame — two
    // towers, one slime, a routine occurrence — could pay the reward twice and
    // decrement the live count twice.
    bool despawning;

    /// <summary>
    /// World-space point projectiles should home toward. The slime's root stays
    /// on the path at ground level, while its animated sprite can stretch and
    /// jump above it. Renderer bounds follow the currently displayed frame, so
    /// their center keeps shots visually attached to the airborne slime.
    /// </summary>
    public Vector3 AimPosition => aimRenderer != null && aimRenderer.enabled
        ? aimRenderer.bounds.center
        : transform.position;

    /// <summary>
    /// Remaining health. Towers set to <see cref="TargetingMode.LowestHealth"/>
    /// compare it to decide what to finish off.
    /// </summary>
    public float Health => health;

    /// <summary>
    /// True when only towers that can hit flyers may target this slime.
    /// </summary>
    public bool IsFlying => isFlying;

    /// <summary>
    /// How far along the route this slime is, as its waypoint index minus a
    /// small fraction of the distance still to walk on the current leg. Towers
    /// compare this to shoot whatever is closest to the goal.
    ///
    /// The index alone is too coarse: every slime between the same pair of
    /// waypoints ties, and the tower ends up picking whichever the physics query
    /// happened to return first, which is not stable frame to frame. Subtracting
    /// the scaled remaining distance breaks the tie toward whichever is nearer
    /// the next waypoint, and keeps the ordering consistent so a tower does not
    /// flicker between two targets mid-reload.
    /// </summary>
    public float RouteProgress
    {
        get
        {
            if (route == null || route.Count == 0)
            {
                return 0f;
            }

            float remaining = Vector3.Distance(transform.position, route.GetPoint(targetIndex));
            return targetIndex - (remaining * 0.001f);
        }
    }

    /// <summary>
    /// Assigns the route and snaps the slime to its first point. The Phase 3
    /// spawner will call this right after instantiating a slime.
    /// </summary>
    public void SetRoute(WaypointRoute newRoute)
    {
        route = newRoute;
        targetIndex = 0;

        if (route != null && route.Count > 0)
        {
            // Height is applied once, here. Update moves in the horizontal plane
            // only — it copies the slime's own y onto its target every frame — so
            // a slime that starts above the path stays there for its whole life
            // without any of the movement code knowing that flying exists.
            transform.position = route.GetPoint(0) + (Vector3.up * flightHeight);
        }
    }

    void Awake()
    {
        // Prefer the SpriteRenderer that owns the sprite Animator. This avoids
        // accidentally selecting an unused sprite child left in the prefab.
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer candidate in renderers)
        {
            if (candidate.enabled && candidate.GetComponent<Animator>() != null)
            {
                aimRenderer = candidate;
                break;
            }
        }

        if (aimRenderer == null)
        {
            foreach (SpriteRenderer candidate in renderers)
            {
                if (candidate.enabled)
                {
                    aimRenderer = candidate;
                    break;
                }
            }
        }
    }

    void Start()
    {
        // Lets a slime dragged into the scene by hand find the route on its own.
        if (route == null)
        {
            SetRoute(FindFirstObjectByType<WaypointRoute>());
        }

        if (route == null || route.Count < 2)
        {
            Debug.LogError($"{name} has no usable WaypointRoute. Add the WaypointRoute component to the Path object.", this);
            enabled = false;
            return;
        }

        // Counted only once the slime is actually going to walk. A slime that
        // failed the check above never moves and never despawns, and registering
        // it would leave a phantom on the board that victory waits forever on.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterSlime();
            registered = true;
        }
    }

    /// <summary>
    /// Slows this slime to <paramref name="factor"/> of its speed for
    /// <paramref name="duration"/> seconds. Called by <see cref="FrostTower"/>.
    ///
    /// Slows do not stack, they compete: the strongest one wins and any hit
    /// refreshes the timer. Stacking multiplicatively would make two frost towers
    /// four times better than one, which is the kind of interaction that makes a
    /// support tower either useless alone or mandatory in pairs.
    /// </summary>
    public void ApplySlow(float factor, float duration)
    {
        factor = Mathf.Clamp(factor, 0.05f, 1f);

        if (factor < slowFactor || Time.time >= slowUntil)
        {
            slowFactor = factor;
        }

        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }

    void Update()
    {
        if (Time.time >= slowUntil)
        {
            slowFactor = 1f;
        }

        Vector3 target = route.GetPoint(targetIndex);

        // Move in the horizontal plane only, so terrain height differences
        // between waypoints don't stop the slime from arriving.
        target.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, target, speed * slowFactor * Time.deltaTime);

        Vector3 toTarget = target - transform.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            // The offset is multiplied on the right so it spins the slime about
            // its own up axis after it has been aimed, rather than tilting the
            // aim itself. Whatever the prefab was rotated to in the Scene view is
            // discarded here — the path decides the facing every frame.
            transform.rotation = Quaternion.LookRotation(toTarget) * Quaternion.Euler(-90f,0f, 90f);
        }

        if (Vector3.Distance(transform.position, target) > arriveDistance)
        {
            return;
        }

        if (route.IsGoal(targetIndex))
        {
            ReachGoal();
        }
        else
        {
            targetIndex++;
        }
    }

    /// <summary>
    /// Applies damage and kills the slime at zero. Called by projectiles today;
    /// Phase 8's area-of-effect towers will call it too.
    ///
    /// The projectile subtracts nothing itself — it says "take three damage" and
    /// the slime decides what that means. That is what lets Phase 8 add an
    /// armored slime that halves incoming damage, or Phase 9 a hit flash,
    /// without editing the projectile at all.
    /// </summary>
    public void TakeDamage(float amount)
    {
        // The multiplier is applied here rather than at the source, which is the
        // whole point of damage being the slime's business to interpret: an
        // armoured variant is a number on a prefab, and neither Projectile nor
        // any tower type needed a line changed to support it.
        health -= amount * damageMultiplier;

        if (health <= 0f)
        {
            Die();
        }
    }

    // Phase 5 left these two doing the same thing and said the duplication was
    // deliberate because Phase 6 would split them. This is the split: one pays
    // the player, the other charges them.
    //
    // Neither belongs in OnDestroy, which is the tempting shortcut since every
    // outcome destroys the slime. OnDestroy also fires when the scene unloads and
    // when Play mode stops, so leaving Play mid-wave would pay out for every
    // slime still walking, into a manager that may already be gone. Death is a
    // game event; destruction is a memory event. They coincide today and stop
    // coinciding in Phase 8, when pooled slimes are returned rather than
    // destroyed.

    void Die()
    {
        if (despawning)
        {
            return;
        }

        despawning = true;
        Unregister();

        // The reward is on the slime rather than on whatever killed it, for the
        // same reason TakeDamage is: the killer should not need to know what its
        // victim is worth. Phase 8's area-of-effect tower kills six slimes and
        // pays out six rewards without a line of new economy code.
        //
        // An explicit null check rather than `?.`, because Instance is a
        // UnityEngine.Object and only the overloaded == knows about destroyed
        // ones. The manager is normally there; this covers the scene being torn
        // down around a slime mid-frame.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(reward);
        }

        // Phase 9 will spawn a death effect here.
        Destroy(gameObject);
    }

    void ReachGoal()
    {
        if (despawning)
        {
            return;
        }

        despawning = true;

        // The life is taken *before* this slime is counted off the board, and the
        // order is load-bearing. Unregistering first would run the victory check
        // while lives were still non-zero: the last slime of the last wave
        // reaching the goal would clear the board, declare victory, and only then
        // take the life that ends the run — announcing both outcomes. Losing the
        // life first means IsGameOver is already set when the victory check runs,
        // and defeat wins the tie.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoseLife(lifeCost);
        }

        Unregister();

        Destroy(gameObject);
    }

    void Unregister()
    {
        if (!registered)
        {
            return;
        }

        registered = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterSlime();
        }
    }
}
