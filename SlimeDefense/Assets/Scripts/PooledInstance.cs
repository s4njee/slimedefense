using UnityEngine;

/// <summary>
/// A receipt stamped on every object <see cref="ObjectPool"/> creates, saying
/// which prefab it came from and whether it is currently parked.
///
/// Added at runtime rather than authored on the prefabs, so pooling an object
/// costs nothing on the asset side: any prefab can be spawned through the pool
/// and none of them have to be prepared for it first.
///
/// The alternative is a dictionary in the pool mapping instance to prefab, which
/// is the same information stored somewhere a despawn has to search for it. This
/// way the object being returned already knows where it belongs, which turns
/// <see cref="ObjectPool.Despawn"/> into a lookup-free operation and means an
/// object destroyed behind the pool's back takes its bookkeeping with it.
/// </summary>
public class PooledInstance : MonoBehaviour
{
    /// <summary>The prefab this object was copied from. Its key in the pool.</summary>
    public GameObject Origin { get; set; }

    /// <summary>
    /// True while this object is parked in the pool rather than in play. What
    /// makes a second <see cref="ObjectPool.Despawn"/> in the same frame
    /// harmless — two projectiles landing on one slime is a routine occurrence,
    /// and without this the slime would be pushed onto the idle stack twice and
    /// handed out to two callers at once.
    /// </summary>
    public bool IsIdle { get; set; }
}
