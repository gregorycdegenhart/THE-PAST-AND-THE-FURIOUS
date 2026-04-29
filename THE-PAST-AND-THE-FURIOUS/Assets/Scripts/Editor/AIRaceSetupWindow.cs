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
    const string PlayerPrefabPath = "Assets/Prefabs/Player_BMW_NEW.prefab";
    const string LegacyPlayerPrefabPath = "Assets/Prefabs/Player_BMW.prefab";
    const string RaceManagerPrefabPath = "Assets/Prefabs/RaceManager.prefab";
    const int DefaultAICount = 7;

    int aiCount = DefaultAICount;
    GameObject playerPrefabOverride;
    bool reuseExistingWaypointPath = true;
    bool spawnRaceManagerIfMissing = true;

    [MenuItem("Tools/AI Racers/Setup In Active Scene")]
    static void ShowWindow()
    {
        GetWindow<AIRaceSetupWindow>("AI Racer Setup");
    }

    [MenuItem("Tools/AI Racers/Quick Setup (7 racers, active scene)")]
    static void QuickSetup()
    {
        GameObject prefab = LoadPlayerPrefab();
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("AI Setup", $"Couldn't find player prefab at {PlayerPrefabPath} or {LegacyPlayerPrefabPath}.", "OK");
            return;
        }
        RunSetup(DefaultAICount, prefab, reuseExistingPath: true, spawnRaceManager: true);
    }

    static GameObject LoadPlayerPrefab()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (p == null) p = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPlayerPrefabPath);
        return p;
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
            new GUIContent("Car Prefab", "Defaults to Player_BMW_NEW. Assign a different prefab here to override."),
            playerPrefabOverride, typeof(GameObject), false);

        reuseExistingWaypointPath = EditorGUILayout.Toggle(
            new GUIContent("Reuse existing waypoint path",
                "If a hand-painted AIWaypointPath already exists in the scene, use it as-is instead of rebuilding from LapCheckpoints. Off = always rebuild from checkpoints (legacy behavior)."),
            reuseExistingWaypointPath);

        spawnRaceManagerIfMissing = EditorGUILayout.Toggle(
            new GUIContent("Spawn RaceManager if missing",
                "Drop in Assets/Prefabs/RaceManager.prefab when no RaceManager exists in the scene."),
            spawnRaceManagerIfMissing);

        GUILayout.Space(8);

        if (GUILayout.Button("Run Setup In Active Scene", GUILayout.Height(36)))
        {
            GameObject prefab = playerPrefabOverride != null ? playerPrefabOverride : LoadPlayerPrefab();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("AI Setup", $"No prefab assigned and {PlayerPrefabPath} not found.", "OK");
                return;
            }
            RunSetup(aiCount, prefab, reuseExistingWaypointPath, spawnRaceManagerIfMissing);
        }

        GUILayout.Space(6);

        if (GUILayout.Button("Remove AI Setup From Active Scene"))
        {
            RemoveSetup();
        }
    }

    static void RunSetup(int count, GameObject carPrefab, bool reuseExistingPath, bool spawnRaceManager)
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

        // --- Resolve waypoint path: reuse hand-painted path if asked and one exists, else build from checkpoints ---
        AIWaypointPath path = null;
        int waypointCount = 0;
        bool reusedPath = false;

        if (reuseExistingPath)
        {
            path = Object.FindFirstObjectByType<AIWaypointPath>();
            if (path != null && path.WaypointCount >= 2)
            {
                reusedPath = true;
                waypointCount = path.WaypointCount;
            }
            else
            {
                path = null;
            }
        }

        if (path == null)
        {
            var checkpoints = Object.FindObjectsByType<LapCheckpoint>(FindObjectsSortMode.None)
                .OrderBy(cp => cp.checkpointIndex)
                .ToList();

            if (checkpoints.Count < 2)
            {
                EditorUtility.DisplayDialog("AI Setup",
                    $"No reusable AIWaypointPath in the scene, and only {checkpoints.Count} LapCheckpoints found. " +
                    "Either paint waypoints with the AIWaypointPainterWindow, or place at least 2 LapCheckpoints with unique checkpointIndex values, then try again.",
                    "OK");
                return;
            }

            // Only blow away path if we're rebuilding it.
            RemoveOldPathsAndSpawners();

            GameObject pathGO = new GameObject("AIWaypointPath");
            Undo.RegisterCreatedObjectUndo(pathGO, "Create AIWaypointPath");
            path = pathGO.AddComponent<AIWaypointPath>();

            Transform[] waypoints = new Transform[checkpoints.Count];
            for (int i = 0; i < checkpoints.Count; i++)
            {
                GameObject wp = new GameObject($"WP_{i:00}");
                wp.transform.SetParent(pathGO.transform);
                wp.transform.position = checkpoints[i].transform.position;
                waypoints[i] = wp.transform;
            }
            path.waypoints = waypoints;
            waypointCount = waypoints.Length;
        }
        else
        {
            // Reusing the path — just clean out any prior spawner so the new one is wired correctly.
            RemoveSpawnersOnly();
        }

        // --- RaceManager: if scene doesn't have one and we're allowed, drop in the prefab ---
        var raceManager = Object.FindFirstObjectByType<RaceManager>();
        bool addedRaceManager = false;
        if (raceManager == null && spawnRaceManager)
        {
            GameObject rmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RaceManagerPrefabPath);
            if (rmPrefab != null)
            {
                GameObject rmInstance = (GameObject)PrefabUtility.InstantiatePrefab(rmPrefab);
                Undo.RegisterCreatedObjectUndo(rmInstance, "Create RaceManager");
                raceManager = rmInstance.GetComponent<RaceManager>();
                if (raceManager == null) raceManager = rmInstance.GetComponentInChildren<RaceManager>();
                addedRaceManager = true;
            }
        }

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
        if (raceManager != null)
        {
            spawner.raceType = raceManager.raceType;
            spawner.totalLaps = raceManager.totalLaps;

            // Wire the RaceManager's player references too if they're empty (newly spawned prefab).
            if (raceManager.carRigidbody == null) raceManager.carRigidbody = playerRb;
            if (raceManager.carTransform == null) raceManager.carTransform = player.transform;
            if (raceManager.carController == null && playerRb != null)
                raceManager.carController = playerRb.GetComponent<CarController>();
            if (raceManager.playerInput == null && playerRb != null)
                raceManager.playerInput = playerRb.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        }

        EditorSceneManager.MarkSceneDirty(scene);

        string pathDesc = reusedPath
            ? $"Reused existing AIWaypointPath ({waypointCount} waypoints)"
            : $"Created AIWaypointPath ({waypointCount} waypoints from LapCheckpoints)";
        string rmDesc = addedRaceManager ? "\n• Spawned RaceManager.prefab" : "";

        Debug.Log($"[AIRaceSetupWindow] {pathDesc} and AIRaceGridSpawner ({count} AI) in scene '{scene.name}'.{(addedRaceManager ? " RaceManager added." : "")}");
        EditorUtility.DisplayDialog("AI Setup",
            $"Done.\n\n• {pathDesc}\n• AIRaceGridSpawner spawning {count} AI{rmDesc}\n\nPress Play to test. Save the scene to persist.",
            "OK");
    }

    static void RemoveSetup()
    {
        RemoveOldPathsAndSpawners();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void RemoveOldPathsAndSpawners()
    {
        foreach (var p in Object.FindObjectsByType<AIWaypointPath>(FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(p.gameObject);
        }
        RemoveSpawnersOnly();
    }

    static void RemoveSpawnersOnly()
    {
        foreach (var s in Object.FindObjectsByType<AIRaceGridSpawner>(FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(s.gameObject);
        }
    }
}
