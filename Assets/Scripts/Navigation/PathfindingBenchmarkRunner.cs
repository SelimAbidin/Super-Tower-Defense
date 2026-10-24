using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SuperTowerDefense.Navigation
{
    [Serializable]
    public sealed class PathfindingBenchmarkReport
    {
        public string utc, environment, deviceModel, operatingSystem, processor, graphics, unityVersion, backend;
        public int processorCount, memoryMB, targetFrameRate, lookupCountPerSample;
        public bool isEditor, isMobile, developmentBuild;
        public float batteryLevel;
        public string notes;
        public PathfindingBenchmarkResult[] results;
    }

    public sealed class PathfindingBenchmarkRunner : MonoBehaviour
    {
        public const int WarmupCount = 10, SampleCount = 60;
        public PathfindingBenchmarkReport Report { get; private set; }
        public bool Complete { get; private set; }
        public string ReportPath { get; private set; }
        public string Progress { get; private set; } = "Starting";
        private string json;
        private bool measuring;

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            StartCoroutine(Run());
        }

        public IEnumerator Run()
        {
            measuring = true; Complete = false;
            Report = new PathfindingBenchmarkReport
            {
                utc = DateTime.UtcNow.ToString("O"),
                environment = Application.isEditor ? "EDITOR SMOKE ONLY - NOT MOBILE RESULTS" : Application.isMobilePlatform ? "MOBILE PLAYER - record whether physical device or emulator" : "DESKTOP PLAYER - NOT MOBILE RESULTS",
                deviceModel = SystemInfo.deviceModel, operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType, processorCount = SystemInfo.processorCount,
                memoryMB = SystemInfo.systemMemorySize, graphics = SystemInfo.graphicsDeviceName,
                unityVersion = Application.unityVersion, isEditor = Application.isEditor,
                isMobile = Application.isMobilePlatform, developmentBuild = Debug.isDebugBuild,
                targetFrameRate = Application.targetFrameRate, lookupCountPerSample = PathfindingBenchmarkCase.LookupCount,
                batteryLevel = SystemInfo.batteryLevel,
#if ENABLE_IL2CPP
                backend = "IL2CPP",
#else
                backend = "Mono",
#endif
                notes = "Main-thread warm full rebuilds. Setup/allocations excluded from rebuild timing. 5 spawns/5 exits. One field = nearest-any; five fields = per-exit. Observed frame interval includes one rebuild set, 10k distance+next reads, benchmark GUI, rendering and pacing. No production gameplay or sustained thermal conclusion."
            };
            var results = new List<PathfindingBenchmarkResult>();
            foreach (int size in new[] { 32, 64, 128, 256 })
            foreach (bool maze in new[] { false, true })
            foreach (int count in new[] { 1, 5 })
            {
                var workload = new PathfindingBenchmarkCase(size, count, maze);
                var result = new PathfindingBenchmarkResult
                {
                    size = size, fieldCount = count, layout = maze ? "maze" : "open",
                    warmupCount = WarmupCount, sampleCount = SampleCount,
                    fieldArrayBytes = 16L * size * size * count,
                    samples = new PathfindingBenchmarkSample[SampleCount]
                };
                for (int i = 0; i < SampleCount; i++) result.samples[i] = new PathfindingBenchmarkSample();
                for (int warmup = 0; warmup < WarmupCount; warmup++)
                {
                    Progress = size + "x" + size + " / " + result.layout + " / " + count + " fields: warmup " + (warmup + 1);
                    workload.Rebuild();
                    yield return null;
                }
                for (int i = 0; i < SampleCount; i++)
                {
                    Progress = size + "x" + size + " / " + result.layout + " / " + count + " fields: sample " + (i + 1) + "/" + SampleCount;
                    workload.Measure(result.samples[i]);
                    yield return null;
                    result.samples[i].observedFrameMs = Time.unscaledDeltaTime * 1000.0;
                }
                result.Summarize();
                results.Add(result);
                // Let previous case/setup leave the measured frame window.
                yield return null;
            }
            Report.results = results.ToArray();
            json = JsonUtility.ToJson(Report, true);
            ReportPath = Path.Combine(Application.persistentDataPath, "pathfinding-benchmark-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(ReportPath, json);
            Complete = true; measuring = false;
            Progress = "Complete: " + Report.environment;
            Debug.Log("Pathfinding benchmark report saved: " + ReportPath);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40), GUI.skin.box);
            GUILayout.Label("PATHFINDING BENCHMARK - MAIN THREAD");
            GUILayout.Label(Application.isEditor ? "Editor smoke run; these are not mobile performance results." : "Keep this app foregrounded. Record physical device, battery/temperature and power mode.");
            GUILayout.Label(Progress);
            GUILayout.Label("32 / 64 / 128 / 256 square grids; open + maze; 1 + 5 shared fields; 10 warmups + 60 samples each.");
            if (Complete)
            {
                GUILayout.Label("JSON: " + ReportPath);
                if (GUILayout.Button("Copy report JSON")) GUIUtility.systemCopyBuffer = json;
                foreach (var result in Report.results)
                    GUILayout.Label(result.size + " " + result.layout + " " + result.fieldCount + " fields | rebuild median " + result.rebuildMedianMs.ToString("F3") + " ms | p95 " + result.rebuildP95Ms.ToString("F3") + " ms | max " + result.rebuildMaxMs.ToString("F3") + " ms | alloc " + result.totalRebuildAllocatedBytes + " B");
                if (!measuring && GUILayout.Button("Run again")) StartCoroutine(Run());
            }
            GUILayout.EndArea();
        }
    }
}
