using UnityEngine;

/// <summary>
/// A tower standing on a <see cref="BuildNode"/>. In Phase 4 it does nothing at
/// all: it holds two numbers and draws its reach in the Scene view. Phase 5 adds
/// the detection radius, target selection, and fire rate that make it a weapon.
///
/// The fields exist now so the values have a home before the systems that read
/// them do — the same reason the slime carried a Health field through Phase 2
/// without anything able to damage it.
/// </summary>
public class Tower : MonoBehaviour
{
    [Tooltip("What this tower costs to build. Unused until Phase 6 adds currency.")]
    [Min(0)]
    [SerializeField] int cost = 50;

    [Tooltip("How far this tower will reach once it can shoot. Unused until Phase 5, " +
             "but drawn as a gizmo now so node placement can be judged by eye.")]
    [Min(0f)]
    [SerializeField] float range = 6f;

    /// <summary>What this tower costs to build.</summary>
    public int Cost => cost;

    /// <summary>How far this tower reaches. Phase 5 targets slimes inside it.</summary>
    public float Range => range;

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
