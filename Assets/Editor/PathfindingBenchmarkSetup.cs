using System;
using System.IO;
using SuperTowerDefense.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PathfindingBenchmarkSetup
{
    public const string ScenePath = "Assets/Scenes/PathfindingBenchmark.unity";
    public const string AndroidId = "com.selimabidin.pathfindingbenchmark";

    [MenuItem("Tools/Super Tower Defense/Create Pathfinding Benchmark")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Benchmark Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
        new GameObject("Pathfinding Benchmark", typeof(PathfindingBenchmarkRunner));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        ValidateStatistics();
        Debug.Log("STD-8 SETUP PASS: benchmark scene saved; percentile/statistics checks passed.");
    }

    private static void ValidateStatistics()
    {
        var result = new PathfindingBenchmarkResult { samples = new PathfindingBenchmarkSample[20] };
        for (int i = 0; i < 20; i++) result.samples[i] = new PathfindingBenchmarkSample { rebuildMs = i, lookupMs = i, observedFrameMs = i, allocatedBytes = 2 };
        result.Summarize();
        if (result.rebuildMedianMs != 9.5 || result.rebuildP95Ms != 18 || result.rebuildMaxMs != 19 || result.totalRebuildAllocatedBytes != 40)
            throw new InvalidOperationException("Benchmark statistics are incorrect.");
    }

    public static void BuildAndroid()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Android build support is unavailable.");
        string previousId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string previousName = PlayerSettings.productName;
        bool previousBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            PlayerSettings.productName = "Pathfinding Benchmark";
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Android/PathfindingBenchmark.apk", BuildTarget.Android, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Benchmark Android build failed: " + report.summary.result);
            Debug.Log("STD-8 ANDROID BUILD PASS: release ARM64 IL2CPP benchmark APK; package " + AndroidId);
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousId);
            PlayerSettings.productName = previousName;
            EditorUserBuildSettings.buildAppBundle = previousBundle;
        }
    }
}
