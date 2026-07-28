using UnityEngine;

/// <summary>
/// One spot on the map where a tower may be built. Nodes are placed by hand in
/// the Hierarchy, the same way waypoints were in Phase 1, which makes the
/// buildable layout a level-design decision rather than something derived from
/// the terrain.
///
/// This is also where the placement rules live, and there are only two of them:
/// a tower can go where a node is, and only if the node is empty. "You cannot
/// build on the path" needs no code at all — there is simply no node there.
///
/// Attach this to the BuildNode prefab, and keep the prefab on the `BuildNode`
/// layer so <see cref="TowerPlacer"/>'s ray can find it.
/// </summary>
public class BuildNode : MonoBehaviour
{
    [Tooltip("Color when the node is empty and nothing is hovering it.")]
    [SerializeField] Color availableColor = new Color(0.6f, 0.9f, 0.6f);

    [Tooltip("Color while the pointer is over this node.")]
    [SerializeField] Color hoverColor = Color.white;

    [Tooltip("Color once a tower stands here.")]
    [SerializeField] Color occupiedColor = new Color(0.35f, 0.35f, 0.35f);

    [Tooltip("Offset applied to the tower placed here, in the node's own orientation. " +
             "Raise Y when a tower model's pivot sits at its center and sinks into the pad.")]
    [SerializeField] Vector3 towerOffset;

    // The renderer whose color communicates the node's state. Cached in Awake
    // because Update-frequency GetComponent calls are a habit worth not forming.
    Renderer padRenderer;

    // Null means empty. This is a Tower reference rather than a bool because
    // Phase 8's upgrade and sell actions need to reach the tower standing here,
    // and storing the reference now costs nothing over storing a flag.
    Tower tower;

    bool isHovered;

    /// <summary>True when a tower already stands on this node.</summary>
    public bool IsOccupied => tower != null;

    /// <summary>The tower standing here, or null. Phase 8 upgrades and sells it.</summary>
    public Tower Tower => tower;

    void Awake()
    {
        padRenderer = GetComponentInChildren<Renderer>();

        if (padRenderer == null)
        {
            Debug.LogWarning($"{name} has no Renderer, so it cannot show its state. " +
                             "Placement still works, but the node will not highlight.", this);
        }

        // A node with no collider is invisible to the placement ray while still
        // looking perfectly correct in the Scene view, so clicks on it do
        // nothing and there is no clue as to why. Easy to cause by swapping in a
        // nicer model later; much easier to diagnose as a line in the Console.
        if (GetComponentInChildren<Collider>() == null)
        {
            Debug.LogError($"{name} has no Collider, so the placement ray cannot hit it. " +
                           "Add one and keep the node on the BuildNode layer.", this);
        }

        RefreshColor();
    }

    /// <summary>
    /// Called by <see cref="TowerPlacer"/> as the pointer moves on and off this
    /// node. The node does not look for the pointer itself — one placer asking
    /// "what is under the pointer" beats every node asking "is it me".
    /// </summary>
    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered)
        {
            return;
        }

        isHovered = hovered;
        RefreshColor();
    }

    /// <summary>
    /// Builds <paramref name="definition"/>'s tower on this node and returns it,
    /// or null if the node was already occupied. The caller is expected to check
    /// <see cref="IsOccupied"/> first; this re-checks anyway, because a rule
    /// enforced in one place is a rule, and a rule enforced only at the call
    /// site is a suggestion.
    ///
    /// Takes a definition rather than a prefab so that instantiating a tower and
    /// handing it its stats happen in the same place. Split across two call sites
    /// they eventually disagree, and a tower running on the wrong definition is
    /// not something you notice until the numbers look odd.
    /// </summary>
    public Tower Place(TowerDefinition definition)
    {
        if (definition == null || !definition.IsValid)
        {
            Debug.LogError($"{name} was asked to build a missing or incomplete tower definition.", this);
            return null;
        }

        Tower prefab = definition.Prefab;

        if (IsOccupied)
        {
            return null;
        }

        // Rotation without scale. The pad is scaled to something like
        // 0.9 x 0.05 x 0.9, so TransformPoint would squash the offset by the
        // same amounts and put the tower somewhere unintended.
        Vector3 position = transform.position + (transform.rotation * towerOffset);

        // Deliberately not parented to this node. A child inherits the pad's
        // flattened scale, which would leave every tower squashed into a tile.
        tower = Instantiate(prefab, position, transform.rotation);
        tower.name = $"{definition.DisplayName} ({name})";

        // Before the tower's Start runs, so it never sees itself without stats.
        tower.Initialize(definition);

        RefreshColor();
        return tower;
    }

    /// <summary>
    /// Removes the tower standing here and frees the node, returning true if
    /// there was one. The caller pays the refund — a node knows what stands on it
    /// and nothing about money, the same division that kept the cost check in
    /// <see cref="TowerPlacer"/> rather than here.
    /// </summary>
    public bool Clear()
    {
        if (tower == null)
        {
            return false;
        }

        // Destroy, not deactivate. Projectiles already in the air hold a
        // reference to this tower and check it for null on arrival, which is the
        // guard that turns a sold tower's shots into ones that quietly fizzle.
        Destroy(tower.gameObject);
        tower = null;

        RefreshColor();
        return true;
    }

    void RefreshColor()
    {
        if (padRenderer == null)
        {
            return;
        }

        Color target = IsOccupied ? occupiedColor : (isHovered ? hoverColor : availableColor);

        // `.material` returns a copy owned by this renderer, so each node colors
        // itself independently. `.sharedMaterial` would edit the material asset
        // instead — recoloring every node at once, and saving the change to the
        // project file when done in the editor.
        //
        // `Material.color` maps to whichever property the shader marks as its
        // main color, which is `_BaseColor` under URP rather than the `_Color`
        // of the old built-in shaders. Setting `.color` works either way.
        padRenderer.material.color = target;
    }

    // Marks where a tower placed here would actually stand, so Tower Offset can
    // be dialed in by eye at edit time instead of by building one and looking.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireCube(transform.position + (transform.rotation * towerOffset), Vector3.one * 0.35f);
    }
}
