using System;
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
/// One rung of a tower's upgrade ladder: what it costs to reach, and what the
/// tower is once it gets there.
///
/// A whole row of stats rather than a multiplier. `damage *= 1.25f` per level is
/// fewer numbers to author and worse in every other way: compounding makes level
/// five an accident rather than a decision, balancing means solving an
/// exponential instead of reading a table, and there is no way to offer a level
/// that trades range for fire rate. Explicit rows are more typing and stay
/// legible at level six.
/// </summary>
[Serializable]
public class TowerLevel
{
    [Tooltip("What reaching this level costs. On level 0 this is the build price.")]
    [Min(0)]
    public int Cost = 50;

    [Tooltip("Detection radius at this level.")]
    [Min(0f)]
    public float Range = 6f;

    [Tooltip("Damage per shot at this level. Zero is legitimate for a support tower.")]
    [Min(0f)]
    public float Damage = 3f;

    [Tooltip("Shots per second at this level.")]
    [Min(0.01f)]
    public float FireRate = 1.5f;

    [Tooltip("The model shown at this level, as a prefab rather than a raw FBX. Leave empty to " +
             "keep whatever the previous level was showing, so only the levels that actually " +
             "change appearance need one. The prefab's own local position is preserved when it " +
             "is instantiated, which is where each mesh's base offset lives.")]
    public GameObject Model;
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
/// Part B turned the flat stat block into <see cref="TowerLevel"/> rows. Level 0
/// is the tower as built, so the array *is* the tower's stats — there is no
/// separate base block to keep in step with the upgrades.
///
/// What is *not* here is anything only one tower type cares about. A splash
/// radius lives on <see cref="SplashTower"/> and a slow factor on
/// <see cref="FrostTower"/>, because a shared definition carrying every type's
/// parameters would be mostly meaningless fields for any given asset — and a
/// field that is meaningless three times out of four is one somebody will
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

    [Tooltip("The upgrade ladder, cheapest first. Element 0 is the tower as built and must " +
             "exist; every element after it is an upgrade the player can buy.")]
    [SerializeField] TowerLevel[] levels = new TowerLevel[1];

    [Tooltip("Fraction of everything spent on a tower that selling gives back. At 1 selling is " +
             "free undo and the optimal play is rebuilding the board every wave; below about " +
             "0.5 nobody ever sells and the feature is decoration.")]
    [Range(0f, 1f)]
    [SerializeField] float sellRefund = 0.7f;

    [Tooltip("The projectile fired at targets. One prefab can serve several tower types; what " +
             "happens on arrival is decided by the tower that fired it.")]
    [SerializeField] Projectile projectilePrefab;

    [Tooltip("Which slime this tower shoots at when several are in range.")]
    [SerializeField] TargetingMode targeting = TargetingMode.FirstInLine;

    [Tooltip("Whether this tower can shoot flying slimes. On by default, so adding a flying " +
             "variant changes nothing until a tower type is deliberately made ground-only — " +
             "which is the interesting case, and one worth turning on rather than discovering.")]
    [SerializeField] bool canHitFlying = true;

    /// <summary>Name shown on the selection panel.</summary>
    public string DisplayName => displayName;

    /// <summary>The prefab this definition builds.</summary>
    public Tower Prefab => prefab;

    /// <summary>What this tower costs to build — the price of its first level.</summary>
    public int Cost => levels != null && levels.Length > 0 ? levels[0].Cost : 0;

    /// <summary>How many levels this tower has, including the one it is built at.</summary>
    public int LevelCount => levels != null ? levels.Length : 0;

    /// <summary>The highest level index this tower can reach.</summary>
    public int MaxLevel => Mathf.Max(0, LevelCount - 1);

    /// <summary>Fraction of total spend returned when selling.</summary>
    public float SellRefund => sellRefund;

    /// <summary>The projectile this tower fires.</summary>
    public Projectile ProjectilePrefab => projectilePrefab;

    /// <summary>Which slime this tower prefers.</summary>
    public TargetingMode Targeting => targeting;

    /// <summary>Whether this tower may target flying slimes.</summary>
    public bool CanHitFlying => canHitFlying;

    /// <summary>
    /// The stats at <paramref name="level"/>, clamped to the ladder rather than
    /// throwing. A tower asking for a level that does not exist is a bug worth
    /// seeing on screen as stats that stopped improving, not one worth taking the
    /// frame down for.
    /// </summary>
    public TowerLevel GetLevel(int level)
    {
        if (levels == null || levels.Length == 0)
        {
            return null;
        }

        return levels[Mathf.Clamp(level, 0, levels.Length - 1)];
    }

    /// <summary>
    /// What upgrading from <paramref name="currentLevel"/> costs, or 0 when the
    /// tower is already at the top of its ladder.
    /// </summary>
    public int UpgradeCost(int currentLevel)
    {
        int next = currentLevel + 1;
        return next < LevelCount ? levels[next].Cost : 0;
    }

    // Read-only properties over private serialized fields, exactly as on
    // WaveDefinition and for the same reason: an asset is shared by every tower
    // built from it, so a script that could write to a level would be editing the
    // saved asset — and in the editor, saving that change into the project.

    /// <summary>
    /// True when this definition can actually build something. Checked once by
    /// the placer rather than discovered as a NullReferenceException at the
    /// moment a player clicks.
    /// </summary>
    public bool IsValid => prefab != null && LevelCount > 0;
}
