using UnityEngine;

/// <summary>
/// Walks a slime along a <see cref="WaypointRoute"/> from the spawn point to the
/// goal, then despawns. Losing a life at the goal comes in Phase 6; taking
/// damage comes in Phase 5.
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
    [SerializeField] float modelYawOffset = 180f;

    WaypointRoute route;
    int targetIndex;

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
            transform.rotation = Quaternion.LookRotation(toTarget) * Quaternion.Euler(0f, modelYawOffset, 0f);
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

    void ReachGoal()
    {
        // Phase 6 will subtract a life here before the slime despawns.
        Destroy(gameObject);
    }
}
