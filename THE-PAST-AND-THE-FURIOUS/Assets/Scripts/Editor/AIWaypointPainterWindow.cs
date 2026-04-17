using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Scene-view click-to-place waypoint painter.
///
/// Workflow:
///   1. Tools > AI Racers > Paint Waypoints.
///   2. Assign or auto-create an AIWaypointPath in the active scene.
///   3. Click "Start Painting" and left-click along the dirt road in the Scene view.
///   4. Click "Stop Painting" and save the scene.
///
/// The painter raycasts against any collider (Terrain, mesh, etc.), so make sure the track
/// surface has a collider (Unity's Terrain does by default).
/// </summary>
public class AIWaypointPainterWindow : EditorWindow
{
    AIWaypointPath targetPath;
    bool painting;
    float minSpacing = 3f;
    float waypointYOffset = 0.1f;
    bool appendMode = true;

    [MenuItem("Tools/AI Racers/Paint Waypoints")]
    static void Show()
    {
        var w = GetWindow<AIWaypointPainterWindow>("Waypoint Painter");
        w.minSize = new Vector2(320, 260);
    }

    void OnGUI()
    {
        GUILayout.Label("AI Waypoint Painter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Click 'Find or Create AIWaypointPath' (or drag one into the Target field).\n" +
            "2. 'Clear All Waypoints' if you want to start fresh.\n" +
            "3. Click 'Start Painting'. In the Scene view, left-click along the dirt path.\n" +
            "4. 'Stop Painting' when done. Ctrl+S to save the scene.",
            MessageType.Info);

        targetPath = (AIWaypointPath)EditorGUILayout.ObjectField(
            "Target Path", targetPath, typeof(AIWaypointPath), true);

        if (targetPath == null)
        {
            if (GUILayout.Button("Find or Create AIWaypointPath In Active Scene"))
            {
                FindOrCreatePath();
            }
            return;
        }

        EditorGUILayout.Space();
        minSpacing = Mathf.Max(0.1f, EditorGUILayout.FloatField(
            new GUIContent("Min Spacing", "Waypoints closer than this to the previous one are skipped."),
            minSpacing));
        waypointYOffset = EditorGUILayout.FloatField(
            new GUIContent("Y Offset", "Added to the raycast hit Y so waypoints sit slightly above ground."),
            waypointYOffset);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(painting))
        {
            if (GUILayout.Button("Start Painting", GUILayout.Height(32)))
            {
                painting = true;
                SceneView.duringSceneGui -= OnScene;
                SceneView.duringSceneGui += OnScene;
                SceneView.RepaintAll();
            }
        }
        using (new EditorGUI.DisabledScope(!painting))
        {
            if (GUILayout.Button("Stop Painting", GUILayout.Height(32)))
            {
                StopPainting();
            }
        }

        EditorGUILayout.Space();

        int count = targetPath.waypoints != null ? targetPath.waypoints.Length : 0;
        GUILayout.Label($"Current waypoints: {count}");

        if (GUILayout.Button("Clear All Waypoints"))
        {
            if (EditorUtility.DisplayDialog("Clear Waypoints",
                $"Remove all {count} waypoint child objects from '{targetPath.name}'?",
                "Clear", "Cancel"))
            {
                ClearWaypoints();
            }
        }

        if (painting)
        {
            EditorGUILayout.HelpBox(
                "Painting is ON. Left-click in the Scene view to drop a waypoint. " +
                "Click 'Stop Painting' or close this window to end.",
                MessageType.Warning);
        }
    }

    void FindOrCreatePath()
    {
        targetPath = Object.FindFirstObjectByType<AIWaypointPath>();
        if (targetPath != null) return;

        GameObject go = new GameObject("AIWaypointPath");
        targetPath = go.AddComponent<AIWaypointPath>();
        targetPath.waypoints = new Transform[0];
        Undo.RegisterCreatedObjectUndo(go, "Create AIWaypointPath");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    void OnDisable()
    {
        // Closing the window ends the session; don't leave scene-view click handling active.
        StopPainting();
    }

    void StopPainting()
    {
        painting = false;
        SceneView.duringSceneGui -= OnScene;
        SceneView.RepaintAll();
    }

    void OnScene(SceneView sv)
    {
        if (!painting || targetPath == null) return;

        Event e = Event.current;

        // Claim priority so our left-click isn't consumed by object selection.
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        // Hover preview — a green disc where the next waypoint will land.
        if (Physics.Raycast(ray, out RaycastHit hoverHit, 10000f))
        {
            Handles.color = new Color(0.2f, 1f, 0.3f, 0.7f);
            Handles.DrawSolidDisc(hoverHit.point + Vector3.up * 0.02f, Vector3.up, 1.5f);
            Handles.color = new Color(0.2f, 1f, 0.3f, 1f);
            Handles.DrawWireDisc(hoverHit.point + Vector3.up * 0.02f, Vector3.up, 1.5f);

            // Draw a guideline from the last waypoint to the hover point.
            if (targetPath.waypoints != null && targetPath.waypoints.Length > 0)
            {
                Transform last = targetPath.waypoints[targetPath.waypoints.Length - 1];
                if (last != null)
                {
                    Handles.color = Color.green;
                    Handles.DrawLine(last.position, hoverHit.point);
                }
            }
        }

        // Left-click places a waypoint. Ignore alt/ctrl drags so scene-view navigation still works.
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.control && !e.shift)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                AddWaypoint(hit.point + Vector3.up * waypointYOffset);
                e.Use();
            }
        }

        // Continuous repaint so the hover preview follows the mouse smoothly.
        sv.Repaint();
    }

    void AddWaypoint(Vector3 pos)
    {
        List<Transform> list = new List<Transform>(targetPath.waypoints ?? new Transform[0]);

        if (!appendMode)
        {
            // Reserved for future "insert at cursor" mode. Currently we always append.
        }

        // Skip if too close to the previous waypoint to avoid accidental double-clicks.
        if (list.Count > 0 && list[list.Count - 1] != null)
        {
            if (Vector3.Distance(list[list.Count - 1].position, pos) < minSpacing) return;
        }

        GameObject go = new GameObject($"WP_{list.Count:00}");
        Undo.RegisterCreatedObjectUndo(go, "Add Waypoint");
        go.transform.SetParent(targetPath.transform);
        go.transform.position = pos;

        list.Add(go.transform);
        Undo.RecordObject(targetPath, "Add Waypoint");
        targetPath.waypoints = list.ToArray();

        EditorUtility.SetDirty(targetPath);
        EditorSceneManager.MarkSceneDirty(targetPath.gameObject.scene);
        Repaint();
    }

    void ClearWaypoints()
    {
        if (targetPath == null || targetPath.waypoints == null) return;

        foreach (var wp in targetPath.waypoints)
        {
            if (wp != null) Undo.DestroyObjectImmediate(wp.gameObject);
        }

        Undo.RecordObject(targetPath, "Clear Waypoints");
        targetPath.waypoints = new Transform[0];

        EditorUtility.SetDirty(targetPath);
        EditorSceneManager.MarkSceneDirty(targetPath.gameObject.scene);
    }
}
