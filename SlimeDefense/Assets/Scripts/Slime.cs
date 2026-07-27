using UnityEngine;

/// <summary>
/// Walks a slime along a <see cref="WaypointRoute"/> from the spawn point to the
/// goal, then despawns. Phase 5 gave it health that towers can subtract from and
/// a death of its own; losing a life at the goal still comes in Phase 6.
/// </summary>
public class Slime : MonoBehaviour
{
    [SerializeField] float speed = 3f;
    [SerializeField] float health = 10f;

    [Tooltip("How close the slime must get to a waypoint before moving to the next one.")]
    [SerializeField] float arriveDistance = 0.15f;

    [Tooltip("Extra yaw applied after aiming down the path, in degrees. Unity's LookRotation " +
             "points a transform's +Z at the target, but the imported slime model faces -Z, " +
             "so it needs 180 here to walk face-first instead of backwards.")]
    [SerializeField] float modelYawOffset = -180f;

    WaypointRoute route;
    int targetIndex;

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
            transform.position = route.GetPoint(0);
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
        }
    }

    void Update()
    {
        Vector3 target = route.GetPoint(targetIndex);

        // Move in the horizontal plane only, so terrain height differences
        // between waypoints don't stop the slime from arriving.
        target.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

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
        health -= amount;

        if (health <= 0f)
        {
            Die();
        }
    }

    // Die and ReachGoal do the same thing today, and that duplication is
    // deliberate. In Phase 6 one of them awards money and the other costs a
    // life. Collapsing them now because they look identical means splitting them
    // again one phase later.

    void Die()
    {
        // Phase 6 will pay the player here before the slime despawns.
        // Phase 9 will spawn a death effect.
        Destroy(gameObject);
    }

    void ReachGoal()
    {
        // Phase 6 will subtract a life here before the slime despawns.
        Destroy(gameObject);
    }
}
