using UnityEngine;

/// <summary>
/// Exposes this object's child Transforms as an ordered movement route.
/// Hierarchy order is route order: SpawnPoint first, GoalPoint last.
/// Attach this to the `Path` GameObject created in Phase 1.
/// </summary>
public class WaypointRoute : MonoBehaviour
{
    /// <summary>Number of points in the route, including spawn and goal.</summary>
    public int Count => transform.childCount;

    /// <summary>World position of the point at <paramref name="index"/>.</summary>
    public Vector3 GetPoint(int index) => transform.GetChild(index).position;

    /// <summary>True when <paramref name="index"/> is the goal point.</summary>
    public bool IsGoal(int index) => index >= transform.childCount - 1;

    // Draws the route in the Scene view so the waypoint order is easy to verify.
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        for (int i = 0; i < transform.childCount; i++)
        {
            Vector3 point = transform.GetChild(i).position;
            Gizmos.DrawSphere(point, 0.3f);

            if (i > 0)
            {
                Gizmos.DrawLine(transform.GetChild(i - 1).position, point);
            }
        }
    }
}
