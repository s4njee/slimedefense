using UnityEngine;

/// <summary>
/// A support tower. Its shots slow a slime instead of hurting it, buying every
/// other tower on the map more time inside its own range.
///
/// Its definition's `Damage` is meant to be zero, and that is the point. A
/// support tower that also deals respectable damage is not a choice the player
/// makes, it is a strictly better pebble tower — so the interesting decision
/// ("is a slower wave worth a build node I could have put damage on?") never
/// gets asked.
/// </summary>
public class FrostTower : Tower
{
    [Tooltip("Speed multiplier applied to a slime that gets hit. 0.5 halves its speed; 1 does " +
             "nothing at all.")]
    [Range(0.05f, 1f)]
    [SerializeField] float slowFactor = 0.5f;

    [Tooltip("Seconds the slow lasts, refreshed by each new hit. Comfortably longer than this " +
             "tower's reload, or a slime spends half its time back at full speed and the tower " +
             "reads as broken rather than weak.")]
    [Min(0.1f)]
    [SerializeField] float slowDuration = 2f;

    /// <summary>
    /// Slows the target, then applies whatever damage the definition carries —
    /// normally none.
    /// </summary>
    public override void OnProjectileHit(Slime target, Vector3 impactPoint)
    {
        if (target == null)
        {
            return;
        }

        target.ApplySlow(slowFactor, slowDuration);

        // Still routed through TakeDamage rather than skipped. At zero damage
        // this does nothing today, and it means a frost tower that Phase 8's
        // balancing decides should chip for 1 needs a number changed on an asset
        // rather than a line added here.
        if (Damage > 0f)
        {
            target.TakeDamage(Damage);
        }
    }
}
