using System;
using System.IO;
using SuperTowerDefense.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PathfindingBenchmarkEditorValidation
{
    private const string ActiveKey = "STD8.Validation.Active", PassedKey = "STD8.Validation.Passed";
    private static double deadline, captureReadyAt;
    private static bool captured;

    static PathfindingBenchmarkEditorValidation() => EditorApplication.playModeStateChanged += Changed;

    public static void Begin()
    {
        EditorSceneManager.OpenScene(PathfindingBenchmarkSetup.ScenePath);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(PassedKey, false);
        EditorApplication.isPlaying = true;
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 300;
            captured = false;
            EditorApplication.update += Poll;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(SessionState.GetBool(PassedKey, false) ? 0 : 1);
        }
    }

    private static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Benchmark did not finish.");
            var runner = UnityEngine.Object.FindFirstObjectByType<PathfindingBenchmarkRunner>();
            if (runner == null || !runner.Complete) return;
            if (!captured)
            {
                var report = runner.Report;
                if (!report.isEditor || report.isMobile || report.results.Length != 16)
                    throw new InvalidOperationException("Smoke metadata/case count mismatch.");
                foreach (var result in report.results)
                {
                    if (result.samples.Length != 60 || result.rebuildMedianMs < 0 || result.rebuildP95Ms < result.rebuildMedianMs || result.rebuildMaxMs < result.rebuildP95Ms)
                        throw new InvalidOperationException("Invalid timing report.");
                    if (result.totalRebuildAllocatedBytes != 0) throw new InvalidOperationException("Steady-state rebuild allocated managed memory.");
                    foreach (var sample in result.samples)
                        if (sample.observedFrameMs <= 0 || sample.lookupMs < 0) throw new InvalidOperationException("Invalid frame/query sample.");
                }
                Directory.CreateDirectory("Documentation");
                File.WriteAllText("Documentation/pathfinding-benchmark-editor.json", JsonUtility.ToJson(report, true));
                ScreenCapture.CaptureScreenshot("Documentation/pathfinding-benchmark-editor.png");
                captured = true;
                captureReadyAt = EditorApplication.timeSinceStartup + 1;
                return;
            }
            if (EditorApplication.timeSinceStartup < captureReadyAt) return;
            Debug.Log("STD-8 EDITOR SMOKE PASS: 16 cases x 60 samples; valid timing/statistics/frame data; zero managed bytes in all measured rebuilds. Desktop editor evidence only, not mobile results.");
            SessionState.SetBool(PassedKey, true);
        }
        catch (Exception error) { Debug.LogException(error); }
        EditorApplication.update -= Poll;
        EditorApplication.isPlaying = false;
    }
}
