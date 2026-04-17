using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One-click setup for AI racers in the active scene.
///
/// - Builds an AIWaypointPath object from existing LapCheckpoints (sorted by checkpointIndex).
/// - Creates an AIRaceGridSpawner object pre-wired to the player, the waypoint path, and the player's car prefab.
///
/// Run via Tools > AI Racers > Setup In Active Scene.
/// </summary>
public class AIRaceSetupWindow : EditorWindow
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player_BMW.prefab";
    const int DefaultAICount = 7;

    int aiCount = DefaultAICount;
    GameObject playerPrefabOverride;

    [MenuItem("Tools/AI Racers/Setup In Active Scene")]
    static void ShowWindow()
    {
        GetWindow<AIRaceSetupWindow>("AI Racer Setup");
    }

    [MenuItem("Tools/AI Racers/Quick Setup (7 racers, active scene)")]
    static void QuickSetup()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("AI Setup", $"Couldn't find player prefab at {PlayerPrefabPath}.", "OK");
            return;
        }
        RunSetup(DefaultAICount, prefab);
    }

    void OnGUI()
    {
        GUILayout.Label("AI Racer Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This builds an AI waypoint path from your LapCheckpoints and spawns AI racers in a starting grid behind the player. " +
            "Make sure the active scene has: a player car tagged \"Player\", and LapCheckpoint components placed around the track.",
            MessageType.Info);

        aiCount = EditorGUILayout.IntSlider("AI Racers", aiCount, 1, 15);
        playerPrefabOverride = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Car Prefab", "Defaults to Player_BMW. Assign a different prefab here to override."),
            playerPrefabOverride, typeof(GameObject), false);

        GUILayout.Space(8);

        if (GUILayout.Button("Run Setup In Active Scene", GUILayout.Height(36)))
        {
            GameObject prefab = playerPrefabOverride != null
                ? playerPrefabOverride
                : AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("AI Setup", $"No prefab assigned and {PlayerPrefabPath} not found.", "OK");
                return;
            }

            RunSetup(aiCount, prefab);
        }

        GUILayout.Space(6);

        if (GUILayout.Button("Remove AI Setup From Active Scene"))
        {
            RemoveSetup();
        }
    }

    static void RunSetup(int count, GameObject carPrefab)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("AI Setup", "No active scene.", "OK");
            return;
        }

        // --- Find player ---
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("AI Setup",
                "No GameObject tagged \"Player\" in the active scene. Place the player car first.",
                "OK");
            return;
        }

        Rigidbody playerRb = player.GetComponentInParent<Rigidbody>();
        if (playerRb == null) playerRb = player.GetComponentInChildren<Rigidbody>();

        // --- Build waypoint path from LapCheckpoints ---
        var checkpoints = Object.FindObjectsByType<LapCheckpoint>(FindObjectsSortMode.None)
            .OrderBy(cp => cp.checkpointIndex)
            .ToList();

        if (checkpoints.Count < 2)
        {
            EditorUtility.DisplayDialog("AI Setup",
                $"Found {checkpoints.Count} LapCheckpoints. Need at least 2 to build a waypoint path.\n\n" +
                "Place LapCheckpoint triggers around the track (with unique checkpointIndex values) and try again.",
                "OK");
            return;
        }

        // Remove any prior path / spawner before recreating.
        RemoveSetup();

        GameObject pathGO = new GameObject("AIWaypointPath");
        Undo.RegisterCreatedObjectUndo(pathGO, "Create AIWaypointPath");
        AIWaypointPath path = pathGO.AddComponent<AIWaypointPath>();

        Transform[] waypoints = new Transform[checkpoints.Count];
        for (int i = 0; i < checkpoints.Count; i++)
        {
            GameObject wp = new GameObject($"WP_{i:00}");
            wp.transform.SetParent(pathGO.transform);
            wp.transform.position = checkpoints[i].transform.position;
            waypoints[i] = wp.transform;
        }
        path.waypoints = waypoints;

        // --- Create spawner ---
        GameObject spawnerGO = new GameObject("AIRaceGridSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerGO, "Create AIRaceGridSpawner");
        AIRaceGridSpawner spawner = spawnerGO.AddComponent<AIRaceGridSpawner>();

        spawner.carSourcePrefab = carPrefab;
        spawner.numberOfAI = count;
        spawner.playerTransform = player.transform;
        spawner.playerRigidbody = playerRb;
        spawner.waypointPath = path;

        // Forward the RaceManager settings if present so AI and player finish under the same rules.
        var raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager != null)
        {
            spawner.raceType = raceManager.raceType;
            spawner.totalLaps = raceManager.totalLaps;
        }

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[AIRaceSetupWindow] Created AIWaypointPath ({waypoints.Length} points) and AIRaceGridSpawner ({count} AI) in scene '{scene.name}'.");
        EditorUtility.DisplayDialog("AI Setup",
            $"Done.\n\nCreated:\n• AIWaypointPath with {waypoints.Length} waypoints (from LapCheckpoints)\n• AIRaceGridSpawner spawning {count} AI in a grid behind the player\n\nPress Play to test. Save the scene to persist.",
            "OK");
    }

    static void RemoveSetup()
    {
        var existingPaths = Object.FindObjectsByType<AIWaypointPath>(FindObjectsSortMode.None);
        foreach (var p in existingPaths)
        {
            Undo.DestroyObjectImmediate(p.gameObject);
        }
        var existingSpawners = Object.FindObjectsByType<AIRaceGridSpawner>(FindObjectsSortMode.None);
        foreach (var s in existingSpawners)
        {
            Undo.DestroyObjectImmediate(s.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
