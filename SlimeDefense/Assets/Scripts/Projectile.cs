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

    [Tooltip("Extra rotation applied after aiming down the flight path, in degrees. Unity's " +
             "LookRotation points a transform's +Z at the target, so a model whose length runs " +
             "along a different axis needs correcting here — an arrow built along +X wants " +
             "(0, -90, 0), and one built along -X wants (0, 90, 0). A sphere wants nothing, " +
             "which is why this is zero by default.")]
    [SerializeField] Vector3 modelRotationOffset;

    Slime target;
    Tower source;

    // When this shot gives up, as a deadline rather than a countdown.
    float despawnAt;

    /// <summary>
    /// Aims this projectile. Called by <see cref="Tower"/> immediately after
    /// instantiating it, before the first Update runs.
    ///
    /// The firing tower is remembered rather than a damage number, because Phase
    /// 8 made what-happens-on-arrival a per-tower decision: a splash tower hits
    /// everything near the impact and a frost tower slows instead of hurting.
    /// Handing the arrival back to the tower keeps every tower type's behavior
    /// in the tower type, and leaves exactly one projectile class that knows how
    /// to travel and nothing else.
    /// </summary>
    public void Launch(Slime newTarget, Tower newSource)
    {
        target = newTarget;
        source = newSource;

        // Aimed here as well as in Update, because the tower instantiates with
        // Quaternion.identity and Update does not run until the next frame. One
        // frame of an arrow pointing down the world's +Z axis before it snaps to
        // its flight path is exactly the kind of flicker that reads as a glitch.
        if (target != null)
        {
            FaceTravelDirection(target.AimPosition);
        }
    }

    // OnEnable rather than Start, and a deadline rather than Destroy's delay
    // overload. Part D pools projectiles, so Start runs once for a shot that
    // goes on to be fired hundreds of times, and a scheduled Destroy would take
    // the object out of the pool's hands three seconds into its first flight —
    // most likely while it was already in the air on its fifth.
    void OnEnable()
    {
        despawnAt = Time.time + lifetime;
    }

    // Dropped so a parked projectile is not the only thing keeping a dead
    // slime's C# object alive. Launch sets both again before the next flight.
    void OnDisable()
    {
        target = null;
        source = null;
    }

    void Update()
    {
        // The safety net that used to be a delayed Destroy. A shot that never
        // arrives has to clean itself up or it flies forever and, now that it is
        // pooled, never returns to be reused either.
        if (Time.time >= despawnAt)
        {
            ObjectPool.Despawn(gameObject);
            return;
        }

        // The target can die mid-flight — commonly, once two towers' ranges
        // overlap and both shoot the same slime. A destroyed GameObject in Unity
        // is not a null reference: it is a live C# object whose == operator
        // reports null while any member access throws MissingReferenceException.
        // This check is what turns that crash into the projectile quietly giving
        // up, and it is the single most important line in the file.
        // Part D added the second half of that check, and it is not optional. A
        // pooled slime that dies is deactivated rather than destroyed, so `==
        // null` reports it as perfectly alive — and once it is handed out again
        // this shot would home in on a completely different slime that happens
        // to be wearing the same object, and damage it. A dead target and a
        // recycled target both mean the same thing here: there is nothing left
        // to hit.
        if (target == null || !target.IsInPlay)
        {
            ObjectPool.Despawn(gameObject);
            return;
        }

        // Read every frame rather than captured at launch, so the shot homes.
        // Ballistic shots that can miss a moving target are a Phase 9 decision
        // about feel, not something to inherit by accident here.
        Vector3 destination = target.AimPosition;

        // Before moving, so the heading is the direction about to be travelled
        // rather than the one just finished. Re-aimed every frame because the
        // shot homes: a target that sidesteps should be followed by the arrow's
        // point, not just by its position.
        FaceTravelDirection(destination);

        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, destination) > arriveDistance)
        {
            return;
        }

        // The firing tower can be gone too — Phase 8's sell action destroys a
        // tower whose shots are still in the air. Same fake-null rule as the
        // target above: the reference looks fine and every member access throws.
        // A shot from a sold tower simply fizzles.
        if (source != null)
        {
            source.OnProjectileHit(target, transform.position);
        }

        ObjectPool.Despawn(gameObject);
    }

    // Points the projectile down its flight path. The offset is multiplied on
    // the right so it spins the model about its own axes after it has been
    // aimed, rather than tilting the aim itself — the same shape as the fix
    // Slime uses for a model that faces the wrong way.
    void FaceTravelDirection(Vector3 destination)
    {
        Vector3 heading = destination - transform.position;

        // A zero-length heading has no direction to look down, and
        // LookRotation logs a warning for every frame it is handed one.
        if (heading.sqrMagnitude < 0.000001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(modelRotationOffset);
    }
}
