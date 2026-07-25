using UnityEngine;

/// <summary>
/// One wave of slimes, stored as an asset in the project rather than as numbers
/// inside the spawner. Create waves with **Create &gt; SlimeDefense &gt; Wave**
/// and assign them to the <see cref="WaveSpawner"/> in the scene.
///
/// A ScriptableObject is a Unity object that lives in the Project window
/// instead of in a scene. It has no Transform and no Update, so it is a
/// container for data, not behavior. Editing a wave asset changes the game
/// without touching a line of code — which is the point of the phase.
/// </summary>
// fileName is the default name of a newly created asset; menuName is where it
// appears under the Create menu. The `order` keeps the entry near the top of
// the menu rather than buried under Unity's built-in asset types.
[CreateAssetMenu(fileName = "Wave_00", menuName = "SlimeDefense/Wave", order = 0)]
public class WaveDefinition : ScriptableObject
{
    [Tooltip("Which slime prefab this wave sends. Typed as Slime rather than " +
             "GameObject so the Inspector only accepts prefabs that can actually walk a route.")]
    [SerializeField] Slime slimePrefab;

    [Tooltip("How many slimes this wave sends.")]
    [Min(1)]
    [SerializeField] int count = 5;

    [Tooltip("Seconds between one slime and the next. Smaller means a denser wave.")]
    [Min(0f)]
    [SerializeField] float spacing = 1f;

    [Tooltip("Extra pause before this wave's first slime, on top of the spawner's " +
             "Time Between Waves. Useful for a breather before a hard wave.")]
    [Min(0f)]
    [SerializeField] float delayBeforeWave;

    /// <summary>The slime prefab this wave instantiates.</summary>
    public Slime SlimePrefab => slimePrefab;

    /// <summary>How many slimes this wave sends.</summary>
    public int Count => count;

    /// <summary>Seconds between consecutive slimes in this wave.</summary>
    public float Spacing => spacing;

    /// <summary>Seconds to wait before this wave's first slime.</summary>
    public float DelayBeforeWave => delayBeforeWave;

    // The fields are [SerializeField] private and the accessors are read-only
    // properties, exactly as on Slime. An asset is shared by everything that
    // references it, so a script that could write to `count` would be editing
    // the saved asset for every other user of it at the same time.

    /// <summary>
    /// True when this wave is safe to spawn. The spawner calls this so a wave
    /// with an empty prefab slot is reported once instead of throwing a
    /// NullReferenceException on every slime it tries to create.
    /// </summary>
    public bool IsValid => slimePrefab != null && count > 0;

    // Phase 8 adds enemy variety, which turns the prefab/count/spacing trio into
    // a list of groups so one wave can mix fast slimes with tanky ones. The
    // spawner's loop survives that change largely intact.
}
