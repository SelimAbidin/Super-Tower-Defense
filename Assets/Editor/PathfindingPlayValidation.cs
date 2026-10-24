using System;
using SuperTowerDefense.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with -batchmode -executeMethod PathfindingPlayValidation.Begin (without -quit).
[InitializeOnLoad]
public static class PathfindingPlayValidation
{
    private const string ActiveKey = "STD6.PlayValidation.Active";
    private const string PassedKey = "STD6.PlayValidation.Passed";
    private static int stage;
    private static double readyAt;

    static PathfindingPlayValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Begin()
    {
        EditorSceneManager.OpenScene(PathfindingSandboxSetup.ScenePath);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(PassedKey, false);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            readyAt = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update += Validate;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool passed = SessionState.GetBool(PassedKey, false);
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static void Validate()
    {
        if (EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            var demo = UnityEngine.Object.FindFirstObjectByType<PathfindingSandbox>();
            if (demo == null) throw new InvalidOperationException("Sandbox missing in Play mode.");
            if (stage == 0)
            {
                if (demo.Simulation.Tick != 0) throw new InvalidOperationException("Sandbox must start paused at tick zero.");
                var reference = new PathfindingSimulation(2, DestinationPolicy.Nearest);
                for (int tick = 0; tick < 80; tick++)
                {
                    demo.AdvanceTick(); reference.AdvanceTick();
                    for (int i = 0; i < reference.AgentCount; i++)
                    {
                        if (demo.Simulation.Position(i) != reference.Position(i)) throw new InvalidOperationException("Play-mode tick mismatch.");
                        var marker = demo.transform.Find("Board/Agent " + (i + 1));
                        if (marker == null || marker.position != demo.World(reference.Position(i), 0.45f))
                            throw new InvalidOperationException("Rendered marker does not follow simulation.");
                    }
                }
                demo.ResetSandbox();
                stage = 1;
                readyAt = EditorApplication.timeSinceStartup + 0.2;
                return;
            }
            if (demo.Simulation.Tick != 0) throw new InvalidOperationException("Reset must restore tick zero.");
            for (int i = 0; i < demo.Simulation.AgentCount; i++)
            {
                var marker = demo.transform.Find("Board/Agent " + (i + 1));
                if (marker == null || marker.position != demo.World(demo.Simulation.Spawn(i), 0.45f))
                    throw new InvalidOperationException("Reset must restore visible spawn positions.");
            }
            Debug.Log("STD-6 PLAY PASS: Awake starts paused; 80 explicit ticks match reference; all five rendered markers follow integer state; reset restores tick zero/spawns.");
            SessionState.SetBool(PassedKey, true);
        }
        catch (Exception error) { Debug.LogException(error); }
        EditorApplication.update -= Validate;
        EditorApplication.isPlaying = false;
    }
}
