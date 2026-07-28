using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paints a road along a waypoint route directly into the terrain's splatmap,
/// instead of dragging a round brush along it by hand.
///
/// The terrain brush is the wrong tool for this job: it is round, it is soft, and
/// it cannot hold a straight line, so a path painted by hand wobbles either side
/// of the waypoints the slimes actually walk. This computes, for every splatmap
/// texel, its distance to the route's polyline and writes the road weight from
/// that. The road is therefore exactly as straight as the route is, exactly as
/// wide as you asked, and — because it reads the same waypoints the spawner does
/// — it cannot drift away from where the slimes go.
///
/// Open it from **Tools > SlimeDefense > Terrain Path Painter**.
/// </summary>
public class TerrainPathPainter : EditorWindow
{
    Terrain terrain;
    Transform route;
    int roadLayer = 1;
    int baseLayer;
    float width = 6f;
    float feather = 2f;
    bool closeLoop;

    [MenuItem("Tools/SlimeDefense/Terrain Path Painter")]
    static void Open()
    {
        GetWindow<TerrainPathPainter>("Path Painter").minSize = new Vector2(320f, 260f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        route = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Route", "The Path object. Its children are used in sibling order, " +
                                    "which is the same order WaypointRoute walks them."),
            route, typeof(Transform), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);

        if (terrain != null && terrain.terrainData != null)
        {
            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            string[] labels = new string[layers.Length];

            for (int i = 0; i < layers.Length; i++)
            {
                labels[i] = layers[i] != null ? $"{i}: {layers[i].name}" : $"{i}: (empty)";
            }

            if (labels.Length > 0)
            {
                roadLayer = EditorGUILayout.Popup("Road layer", Mathf.Clamp(roadLayer, 0, labels.Length - 1), labels);
                baseLayer = EditorGUILayout.Popup(
                    new GUIContent("Base layer", "Used only where a texel had no other layer to give weight back to."),
                    Mathf.Clamp(baseLayer, 0, labels.Length - 1), labels);
            }
            else
            {
                EditorGUILayout.HelpBox("This terrain has no layers. Add a grass and a dirt layer first.", MessageType.Warning);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
        width = EditorGUILayout.Slider(
            new GUIContent("Road width", "World units, edge to edge, fully road."), width, 1f, 30f);
        feather = EditorGUILayout.Slider(
            new GUIContent("Feather", "World units of fade beyond the road edge. 0 gives a hard border."),
            feather, 0f, 15f);
        closeLoop = EditorGUILayout.Toggle(
            new GUIContent("Close loop", "Join the last waypoint back to the first."), closeLoop);

        EditorGUILayout.Space();

        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData data = terrain.terrainData;
            float unitsPerTexel = data.size.x / Mathf.Max(1, data.alphamapWidth - 1);
            EditorGUILayout.HelpBox(
                $"Control texture resolution {data.alphamapWidth} over {data.size.x:0} units " +
                $"= {unitsPerTexel:0.00} units per texel.\n" +
                $"A {width:0.#}-unit road is about {width / unitsPerTexel:0} texels wide." +
                (width / unitsPerTexel < 6f
                    ? "\n\nThat is coarse — raise Terrain Settings > Control Texture Resolution for cleaner edges."
                    : string.Empty),
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!CanPaint()))
        {
            if (GUILayout.Button("Paint road along route", GUILayout.Height(30f)))
            {
                Paint();
            }
        }

        if (!CanPaint())
        {
            EditorGUILayout.HelpBox("Assign a terrain and a route with at least two children.", MessageType.None);
        }
    }

    bool CanPaint()
    {
        return terrain != null
               && terrain.terrainData != null
               && terrain.terrainData.terrainLayers.Length > 0
               && route != null
               && route.childCount >= 2;
    }

    void Paint()
    {
        TerrainData data = terrain.terrainData;

        List<Vector2> points = new List<Vector2>();

        // Sibling order, which is the order WaypointRoute walks them — so the
        // road cannot disagree with the path the slimes take.
        for (int i = 0; i < route.childCount; i++)
        {
            Vector3 p = route.GetChild(i).position;
            points.Add(new Vector2(p.x, p.z));
        }

        if (closeLoop)
        {
            points.Add(points[0]);
        }

        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layerCount = data.alphamapLayers;

        float[,,] maps = data.GetAlphamaps(0, 0, w, h);

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        float half = width * 0.5f;
        float outer = half + feather;
        int touched = 0;

        // Registered before the write so a bad width is one Ctrl+Z away. Terrain
        // data is an asset rather than a scene object, so without this the edit
        // is permanent the moment it lands.
        Undo.RegisterCompleteObjectUndo(data, "Paint Terrain Path");

        for (int y = 0; y < h; y++)
        {
            // Alphamap y runs along world Z and x along world X.
            float wz = origin.z + (y / (float)(h - 1)) * size.z;

            for (int x = 0; x < w; x++)
            {
                float wx = origin.x + (x / (float)(w - 1)) * size.x;

                float distance = DistanceToPolyline(new Vector2(wx, wz), points);

                if (distance > outer)
                {
                    continue;
                }

                // 1 inside the road, falling to 0 across the feather.
                float road = feather > 0.0001f
                    ? 1f - Mathf.InverseLerp(half, outer, distance)
                    : 1f;

                if (road <= 0.0001f)
                {
                    continue;
                }

                // Splat weights must sum to 1. Whatever was here keeps its
                // relative mix and is scaled into what the road leaves over,
                // which is what stops a painted road erasing the blend between
                // two grasses at its edge.
                float remaining = 1f - road;
                float others = 0f;

                for (int l = 0; l < layerCount; l++)
                {
                    if (l != roadLayer)
                    {
                        others += maps[y, x, l];
                    }
                }

                if (others > 0.0001f)
                {
                    float scale = remaining / others;

                    for (int l = 0; l < layerCount; l++)
                    {
                        if (l != roadLayer)
                        {
                            maps[y, x, l] *= scale;
                        }
                    }
                }
                else if (baseLayer != roadLayer && baseLayer < layerCount)
                {
                    maps[y, x, baseLayer] = remaining;
                }

                maps[y, x, roadLayer] = road;
                touched++;
            }
        }

        data.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(data);

        Debug.Log($"Painted {touched} splatmap texels along {points.Count} waypoints " +
                  $"at width {width} (+{feather} feather).", terrain);
    }

    // Shortest distance from a point to the polyline, measured in the XZ plane.
    // Flat distance on purpose: the road is a texture on a heightmap, and a slope
    // should not make it narrower than the route it marks.
    static float DistanceToPolyline(Vector2 point, List<Vector2> points)
    {
        float best = float.MaxValue;

        for (int i = 0; i < points.Count - 1; i++)
        {
            float d = DistanceToSegment(point, points[i], points[i + 1]);

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

        // A zero-length segment — two waypoints on top of each other — would
        // divide by zero here, and duplicated waypoints are common enough to be
        // worth the branch.
        if (lengthSquared < 0.000001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        return Vector2.Distance(point, a + (ab * t));
    }
}
