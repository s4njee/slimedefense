using UnityEngine;

/// <summary>
/// Exposes this object's child Transforms as an ordered movement route.
/// Hierarchy order is route order: SpawnPoint first, GoalPoint last.
/// Attach this to the `Path` GameObject created in Phase 1.
/// </summary>
public class WaypointRoute : MonoBehaviour
{
    [Tooltip("Radius of the Scene view route markers. Scale this to the terrain: " +
             "large terrains need a bigger value or the markers become sub-pixel.")]
    [SerializeField] float gizmoRadius = 8f;

    /// <summary>Number of points in the route, including spawn and goal.</summary>
    public int Count => transform.childCount;

    /// <summary>World position of the point at <paramref name="index"/>.</summary>
    public Vector3 GetPoint(int index) => transform.GetChild(index).position;

    /// <summary>True when <paramref name="index"/> is the goal point.</summary>
    public bool IsGoal(int index) => index >= transform.childCount - 1;

    /// <summary>
    /// Reports what the gizmo drawing code sees. Right-click the WaypointRoute
    /// header in the Inspector and pick "Log Route Info" to run it on demand.
    /// </summary>
    [ContextMenu("Log Route Info")]
    void LogRouteInfo()
    {
        if (transform.childCount == 0)
        {
            Debug.LogWarning($"{name}: WaypointRoute has NO child objects, so there is nothing to draw. " +
                             "The waypoints must be children of this object.", this);
            return;
        }

        Debug.Log($"{name}: {transform.childCount} points, gizmoRadius={gizmoRadius}, " +
                  $"first={transform.GetChild(0).position}, " +
                  $"last={transform.GetChild(transform.childCount - 1).position}, " +
                  $"activeInHierarchy={gameObject.activeInHierarchy}, enabled={enabled}", this);
    }

    // Draws the route in the Scene view so the waypoint order is easy to verify.
    // Spawn is green, the goal is red, and the points between them are cyan.
    void OnDrawGizmos()
    {
        int last = transform.childCount - 1;

        for (int i = 0; i <= last; i++)
        {
            Vector3 point = transform.GetChild(i).position;

            if (i == 0)
            {
                Gizmos.color = Color.green;
            }
            else if (i == last)
            {
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.cyan;
            }

            Gizmos.DrawSphere(point, gizmoRadius);

            if (i > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.GetChild(i - 1).position, point);
            }
        }
    }
}
