using UnityEngine;

/// <summary>
/// A tower whose shots damage everything near the impact rather than only what
/// they were aimed at. Slower and dearer than the pebble tower and weaker per
/// hit, so it is worth building only against groups — which is what Phase 8's
/// wave groups introduce.
///
/// This is a subclass rather than a `splashRadius` field on <see cref="Tower"/>,
/// because it does something structurally different when a shot lands, not the
/// same thing with a different number. A pebble tower with `splashRadius = 0` is
/// a base class carrying a field that is meaningless for most of its instances.
/// </summary>
public class SplashTower : Tower
{
    [Tooltip("How far from the impact the damage reaches. Slimes outside it are untouched, " +
             "including the one the shot was aimed at if it moved.")]
    [Min(0.1f)]
    [SerializeField] float splashRadius = 1.5f;

    [Tooltip("Damage dealt to slimes caught by the splash but not directly hit, as a fraction " +
             "of full damage. Below 1 the tower still rewards aiming at the densest part of a " +
             "group rather than anywhere near it.")]
    [Range(0f, 1f)]
    [SerializeField] float splashFalloff = 0.6f;

    // Reused by every shot, for the same reason Tower's is: a splash query per
    // hit that allocated an array would produce garbage in proportion to how
    // well the tower is doing its job.
    readonly Collider[] caught = new Collider[64];

    /// <summary>
    /// Damages the intended target in full and everything else within
    /// <see cref="splashRadius"/> at a fraction.
    /// </summary>
    public override void OnProjectileHit(Slime target, Vector3 impactPoint)
    {
        // Deliberately not returning early when the target is gone. A shot whose
        // target died mid-flight still lands where it was going, and a splash
        // that vanished because one slime in the group died first would be a
        // strange rule to explain.
        if (target != null)
        {
            target.TakeDamage(Damage);
        }

        int count = Physics.OverlapSphereNonAlloc(impactPoint, splashRadius, caught, SlimeMask);
        float splashDamage = Damage * splashFalloff;

        for (int i = 0; i < count; i++)
        {
            Slime slime = caught[i].GetComponentInParent<Slime>();

            if (slime == null || slime == target)
            {
                // Skipping the original target is what keeps it from being hit
                // twice — once in full above and once as a bystander here.
                continue;
            }

            slime.TakeDamage(splashDamage);
        }
    }

    // Drawn at the tower rather than at an impact, because an impact only exists
    // for a frame. It shows the size of the splash next to the size of the
    // range, which is the comparison that matters when placing one.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}
