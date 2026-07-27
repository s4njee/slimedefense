using UnityEngine;

/// <summary>
/// How a tower chooses which slime to shoot. Phase 5 hardcoded first-in-line and
/// said this would become a per-tower choice once there were tower types worth
/// differentiating; this is that choice.
/// </summary>
public enum TargetingMode
{
    /// <summary>The slime furthest along the route. The most urgent threat.</summary>
    FirstInLine,

    /// <summary>The closest slime. Keeps a short-range tower busy.</summary>
    Nearest,

    /// <summary>The weakest slime. Finishes things off rather than starting them.</summary>
    LowestHealth,
}

/// <summary>
/// One tower type's stats, stored as an asset rather than as numbers on a prefab.
/// Create with **Create &gt; SlimeDefense &gt; Tower** and keep them in
/// `Assets/Towers`.
///
/// Same reasoning as <see cref="WaveDefinition"/> in Phase 3: this is data, not
/// behavior, so it lives in the Project window where it can be edited, compared
/// side by side, and duplicated to try a variant without touching a prefab.
///
/// What is *not* here is anything only one tower type cares about. A splash
/// radius lives on <see cref="SplashTower"/> and a slow factor on
/// <see cref="FrostTower"/>, because a shared definition carrying every
/// type's parameters would be mostly meaningless fields for any given asset —
/// and a field that is meaningless three times out of four is one somebody will
/// eventually set by mistake.
/// </summary>
[CreateAssetMenu(fileName = "Tower_", menuName = "SlimeDefense/Tower", order = 1)]
public class TowerDefinition : ScriptableObject
{
    [Tooltip("Name shown on the selection panel.")]
    [SerializeField] string displayName = "Tower";

    [Tooltip("The prefab built when this tower is chosen. Its Tower component decides how the " +
             "tower behaves; this asset decides the numbers it behaves with.")]
    [SerializeField] Tower prefab;

    [Tooltip("What this tower costs to build. TowerPlacer reads it from here, so a new type " +
             "arrives with its price attached instead of needing an entry in a table.")]
    [Min(0)]
    [SerializeField] int cost = 50;

    [Tooltip("Detection radius. Measured to the slime's collider, so an oversized slime " +
             "collider quietly extends this.")]
    [Min(0f)]
    [SerializeField] float range = 6f;

    [Tooltip("Damage dealt per shot that lands. Zero is legitimate — a support tower that also " +
             "deals damage is not a choice, it is an upgrade.")]
    [Min(0f)]
    [SerializeField] float damage = 3f;

    [Tooltip("Shots per second.")]
    [Min(0.01f)]
    [SerializeField] float fireRate = 1.5f;

    [Tooltip("The projectile fired at targets. One prefab can serve several tower types; what " +
             "happens on arrival is decided by the tower that fired it.")]
    [SerializeField] Projectile projectilePrefab;

    [Tooltip("Which slime this tower shoots at when several are in range.")]
    [SerializeField] TargetingMode targeting = TargetingMode.FirstInLine;

    /// <summary>Name shown on the selection panel.</summary>
    public string DisplayName => displayName;

    /// <summary>The prefab this definition builds.</summary>
    public Tower Prefab => prefab;

    /// <summary>What this tower costs to build.</summary>
    public int Cost => cost;

    /// <summary>How far this tower reaches.</summary>
    public float Range => range;

    /// <summary>Damage dealt per shot that lands.</summary>
    public float Damage => damage;

    /// <summary>Shots per second.</summary>
    public float FireRate => fireRate;

    /// <summary>The projectile this tower fires.</summary>
    public Projectile ProjectilePrefab => projectilePrefab;

    /// <summary>Which slime this tower prefers.</summary>
    public TargetingMode Targeting => targeting;

    // Read-only properties over private serialized fields, exactly as on
    // WaveDefinition and for the same reason: an asset is shared by every tower
    // built from it, so a script that could write to `damage` would be editing
    // the saved asset — and in the editor, saving that change into the project.

    /// <summary>
    /// True when this definition can actually build something. Checked once by
    /// the placer rather than discovered as a NullReferenceException at the
    /// moment a player clicks.
    /// </summary>
    public bool IsValid => prefab != null;
}
