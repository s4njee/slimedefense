using System;
using UnityEngine;

/// <summary>
/// One run of a single slime type inside a wave. A wave is a list of these, so
/// one wave can open with runners and close with something heavy.
/// </summary>
[Serializable]
public class WaveGroup
{
    [Tooltip("Which slime prefab this group sends. Typed as Slime rather than GameObject so the " +
             "Inspector only accepts prefabs that can actually walk a route.")]
    public Slime SlimePrefab;

    [Tooltip("How many slimes this group sends.")]
    [Min(1)]
    public int Count = 5;

    [Tooltip("Seconds between one slime and the next within this group. Smaller means denser.")]
    [Min(0f)]
    public float Spacing = 1f;

    [Tooltip("Extra pause before this group's first slime, on top of whatever the previous " +
             "group's spacing left. A breather between a swarm and the thing behind it.")]
    [Min(0f)]
    public float DelayBeforeGroup;

    /// <summary>True when this group is safe to spawn.</summary>
    public bool IsValid => SlimePrefab != null && Count > 0;
}

/// <summary>
/// One wave of slimes, stored as an asset in the project rather than as numbers
/// inside the spawner. Create waves with **Create &gt; SlimeDefense &gt; Wave**
/// and assign them to the <see cref="WaveSpawner"/> in the scene.
///
/// A ScriptableObject is a Unity object that lives in the Project window instead
/// of in a scene. It has no Transform and no Update, so it is a container for
/// data, not behavior. Editing a wave asset changes the game without touching a
/// line of code — which was the point of Phase 3.
///
/// Phase 8 Part C turned the single prefab/count/spacing trio into a list of
/// <see cref="WaveGroup"/>s, which is exactly the change Phase 3 said would come
/// and said the spawner's loop would survive "largely intact". It did: RunWave
/// gained an outer loop and the inner per-slime loop is untouched. That is the
/// payoff of the wave data having been an asset from the start rather than
/// fields on the spawner.
/// </summary>
// fileName is the default name of a newly created asset; menuName is where it
// appears under the Create menu. The `order` keeps the entry near the top of
// the menu rather than buried under Unity's built-in asset types.
[CreateAssetMenu(fileName = "Wave_00", menuName = "SlimeDefense/Wave", order = 0)]
public class WaveDefinition : ScriptableObject
{
    [Tooltip("The groups this wave sends, in order. One group is the Phase 3 behaviour; several " +
             "let a wave mix slime types.")]
    [SerializeField] WaveGroup[] groups = new WaveGroup[1];

    [Tooltip("Extra pause before this wave's first group, on top of the spawner's Time Between " +
             "Waves. Useful for a breather before a hard wave.")]
    [Min(0f)]
    [SerializeField] float delayBeforeWave;

    /// <summary>The groups this wave sends, in order.</summary>
    public WaveGroup[] Groups => groups;

    /// <summary>Seconds to wait before this wave's first group.</summary>
    public float DelayBeforeWave => delayBeforeWave;

    /// <summary>
    /// How many slimes this wave sends in total. Useful for balancing against the
    /// reward economy, which is why it is worth having rather than counting by
    /// hand across groups.
    /// </summary>
    public int TotalCount
    {
        get
        {
            if (groups == null)
            {
                return 0;
            }

            int total = 0;

            foreach (WaveGroup group in groups)
            {
                if (group != null && group.IsValid)
                {
                    total += group.Count;
                }
            }

            return total;
        }
    }

    // The field is [SerializeField] private and the accessors are read-only
    // properties, exactly as on Slime. An asset is shared by everything that
    // references it, so a script that could write to a group would be editing
    // the saved asset for every other user of it at the same time.

    /// <summary>
    /// True when this wave has at least one group worth spawning. The spawner
    /// checks this so a wave with an empty prefab slot is reported once instead
    /// of throwing a NullReferenceException on every slime it tries to create.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (groups == null)
            {
                return false;
            }

            foreach (WaveGroup group in groups)
            {
                if (group != null && group.IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
