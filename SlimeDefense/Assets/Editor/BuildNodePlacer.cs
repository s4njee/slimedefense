using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lines build nodes along both shoulders of the route, evenly spaced, at a fixed
/// distance from the road.
///
/// Placing these by hand is the same problem the terrain brush had: a node is
/// meant to sit a specific distance from the path, and dragging two hundred of
/// them by eye produces a field that is neither even nor symmetric — and quietly
/// changes the balance, because a node nearer the road covers more of it.
///
/// The interesting part is the rejection test. Candidates are generated at the
/// offset from their own segment, then thrown away if they land closer than
/// <see cref="roadClearance"/> to *any* part of the route. That one check handles
/// both cases that ruin a generated layout: nodes flung wide on the outside of a
/// hairpin, and nodes from one leg landing on top of the leg beside it.
///
/// Open it from **Tools > SlimeDefense > Build Node Placer**.
/// </summary>
public class BuildNodePlacer : EditorWindow
{
    Transform route;
    BuildNode nodePrefab;
    Transform parent;
    Terrain terrain;

    float spacing = 9f;
    float offset = 5.5f;
    float roadClearance = 4.5f;
    float minSeparation = 5f;
    bool leftSide = true;
    bool rightSide = true;
    bool clearExisting = true;

    [MenuItem("Tools/SlimeDefense/Build Node Placer")]
    static void Open()
    {
        GetWindow<BuildNodePlacer>("Node Placer").minSize = new Vector2(340f, 320f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        route = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Route", "The Path object. Children are used in sibling order."),
            route, typeof(Transform), true);
        nodePrefab = (BuildNode)EditorGUILayout.ObjectField("Node prefab", nodePrefab, typeof(BuildNode), false);
        parent = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Parent", "Container the nodes are created under. Usually BuildNodes."),
            parent, typeof(Transform), true);
        terrain = (Terrain)EditorGUILayout.ObjectField(
            new GUIContent("Terrain", "Optional. Nodes are dropped onto its surface when set."),
            terrain, typeof(Terrain), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
        spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing", "Distance between nodes along the road."), spacing, 2f, 30f);
        offset = EditorGUILayout.Slider(
            new GUIContent("Offset", "Distance from the road centre line to a node."), offset, 1f, 25f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Sides");
            leftSide = GUILayout.Toggle(leftSide, "Left", EditorStyles.miniButtonLeft);
            rightSide = GUILayout.Toggle(rightSide, "Right", EditorStyles.miniButtonRight);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rejection", EditorStyles.boldLabel);
        roadClearance = EditorGUILayout.Slider(
            new GUIContent("Road clearance", "Drop any node closer than this to any part of the route. " +
                                             "Keeps nodes off the road at corners and out of the gap " +
                                             "between two legs running close together."),
            roadClearance, 0f, 25f);
        minSeparation = EditorGUILayout.Slider(
            new GUIContent("Min separation", "Drop any node this close to one already placed."),
            minSeparation, 0f, 25f);

        EditorGUILayout.Space();
        clearExisting = EditorGUILayout.Toggle(
            new GUIContent("Clear existing", "Delete the parent's current children first."), clearExisting);

        if (roadClearance > offset)
        {
            EditorGUILayout.HelpBox(
                "Road clearance is greater than the offset, so every node will be rejected. " +
                "Clearance should sit just under the offset.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!CanPlace()))
        {
            if (GUILayout.Button(clearExisting ? "Replace nodes along route" : "Add nodes along route",
                                 GUILayout.Height(30f)))
            {
                Place();
            }
        }

        if (!CanPlace())
        {
            EditorGUILayout.HelpBox("Assign a route with at least two children, a node prefab, and a parent.",
                                    MessageType.None);
        }
    }

    bool CanPlace()
    {
        return route != null && route.childCount >= 2 && nodePrefab != null && parent != null;
    }

    void Place()
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < route.childCount; i++)
        {
            points.Add(route.GetChild(i).position);
        }

        if (clearExisting)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        List<Vector3> placed = new List<Vector3>();
        int rejectedByRoad = 0;
        int rejectedByCrowding = 0;

        // Walked by arc length rather than per segment, so spacing stays even
        // across a corner instead of bunching up at every waypoint.
        float carry = 0f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];

            Vector2 flatA = new Vector2(a.x, a.z);
            Vector2 flatB = new Vector2(b.x, b.z);

            float length = Vector2.Distance(flatA, flatB);

            if (length < 0.001f)
            {
                continue;
            }

            Vector2 direction = (flatB - flatA) / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);

            for (float d = carry; d < length; d += spacing)
            {
                Vector2 centre = flatA + (direction * d);

                if (leftSide)
                {
                    TryPlace(centre + (normal * offset), points, placed,
                             ref rejectedByRoad, ref rejectedByCrowding);
                }

                if (rightSide)
                {
                    TryPlace(centre - (normal * offset), points, placed,
                             ref rejectedByRoad, ref rejectedByCrowding);
                }
            }

            // Whatever was left of the interval rolls into the next segment.
            carry = Mathf.Repeat(carry - length, spacing);
        }

        Debug.Log($"Placed {placed.Count} build nodes along {points.Count} waypoints. " +
                  $"Rejected {rejectedByRoad} for road clearance and {rejectedByCrowding} for crowding.",
                  parent);
    }

    void TryPlace(Vector2 flat, List<Vector3> route3D, List<Vector3> placed,
                  ref int rejectedByRoad, ref int rejectedByCrowding)
    {
        // The whole reason generated layouts usually look wrong: a node offset
        // from its own segment can still be sitting on a different one.
        if (DistanceToPolyline(flat, route3D) < roadClearance)
        {
            rejectedByRoad++;
            return;
        }

        foreach (Vector3 existing in placed)
        {
            if (Vector2.Distance(flat, new Vector2(existing.x, existing.z)) < minSeparation)
            {
                rejectedByCrowding++;
                return;
            }
        }

        Vector3 position = new Vector3(flat.x, 0f, flat.y);

        if (terrain != null)
        {
            // SampleHeight is relative to the terrain object, so its own y has to
            // be added back on — a terrain that is not at y = 0 otherwise buries
            // every node it generates.
            position.y = terrain.transform.position.y + terrain.SampleHeight(position);
        }
        else if (route3D.Count > 0)
        {
            position.y = route3D[0].y;
        }

        BuildNode node = (BuildNode)PrefabUtility.InstantiatePrefab(nodePrefab, parent);
        Undo.RegisterCreatedObjectUndo(node.gameObject, "Place Build Nodes");

        node.transform.position = position;
        node.name = $"BuildNode_{placed.Count:00}";

        placed.Add(position);
    }

    static float DistanceToPolyline(Vector2 point, List<Vector3> points)
    {
        float best = float.MaxValue;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = new Vector2(points[i].x, points[i].z);
            Vector2 b = new Vector2(points[i + 1].x, points[i + 1].z);
            float d = DistanceToSegment(point, a, b);

            if (d < best)
            {
                best = d;
            }
        }

        return best;
    }

    static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;

        if (lengthSquared < 0.000001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        return Vector2.Distance(point, a + (ab * t));
    }
}
