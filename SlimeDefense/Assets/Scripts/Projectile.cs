using UnityEngine;

/// <summary>
/// A single shot fired by a <see cref="Tower"/>. It remembers the slime it was
/// aimed at, flies toward it, and deals damage on arrival.
///
/// Deliberately dumb. It knows nothing about towers, targeting, or what damage
/// means — it carries a number to a slime and lets the slime decide what to do
/// with it. That is what lets Phase 8 add an armored slime, or Phase 9 a hit
/// flash, without this file changing at all.
///
/// The tower could just as easily subtract health the instant it fires. The shot
/// exists because it is the only feedback the combat loop has until Phases 7 and
/// 9 add UI and effects: without something visibly leaving the tower and
/// arriving at a slime, combat reads as slimes dying at random.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Tooltip("Travel speed in units per second. Fast enough to feel like a shot, " +
             "slow enough that the flight is visible — that visibility is the point.")]
    [Min(0.1f)]
    [SerializeField] float speed = 20f;

    [Tooltip("Seconds before the projectile gives up and despawns. A safety net, " +
             "not a tuning knob: a shot that never arrives has to clean itself up " +
             "or it flies forever and leaks.")]
    [Min(0.1f)]
    [SerializeField] float lifetime = 3f;

    [Tooltip("How close to the target counts as a hit. Without this the projectile " +
             "can step past its target between frames and never register an arrival.")]
    [Min(0.01f)]
    [SerializeField] float arriveDistance = 0.2f;

    Slime target;
    float damage;

    /// <summary>
    /// Aims this projectile. Called by <see cref="Tower"/> immediately after
    /// instantiating it, before the first Update runs.
    ///
    /// Damage is passed in rather than stored on the prefab so a single
    /// projectile prefab can serve every tower Phase 8 adds — the tower owns how
    /// hard it hits, the projectile only owns how it travels.
    /// </summary>
    public void Launch(Slime newTarget, float newDamage)
    {
        target = newTarget;
        damage = newDamage;
    }

    void Start()
    {
        // Scheduled once rather than counted down in Update. Nothing else needs
        // the remaining time, and Destroy's delay overload already does exactly
        // this job.
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // The target can die mid-flight — commonly, once two towers' ranges
        // overlap and both shoot the same slime. A destroyed GameObject in Unity
        // is not a null reference: it is a live C# object whose == operator
        // reports null while any member access throws MissingReferenceException.
        // This check is what turns that crash into the projectile quietly giving
        // up, and it is the single most important line in the file.
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Read every frame rather than captured at launch, so the shot homes.
        // Ballistic shots that can miss a moving target are a Phase 9 decision
        // about feel, not something to inherit by accident here.
        Vector3 destination = target.transform.position;

        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, destination) > arriveDistance)
        {
            return;
        }

        target.TakeDamage(damage);
        Destroy(gameObject);
    }
}
