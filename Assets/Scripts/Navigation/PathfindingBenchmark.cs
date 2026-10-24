using System;
using System.Diagnostics;

namespace SuperTowerDefense.Navigation
{
    [Serializable]
    public sealed class PathfindingBenchmarkSample
    {
        public double rebuildMs, lookupMs, observedFrameMs;
        public long allocatedBytes;
    }

    [Serializable]
    public sealed class PathfindingBenchmarkResult
    {
        public int size, fieldCount, warmupCount, sampleCount;
        public string layout;
        public long fieldArrayBytes, totalRebuildAllocatedBytes;
        public double rebuildMedianMs, rebuildP95Ms, rebuildMaxMs;
        public double lookupMedianMs, frameMedianMs, frameP95Ms, frameMaxMs;
        public PathfindingBenchmarkSample[] samples;

        public void Summarize()
        {
            var rebuild = new double[samples.Length];
            var lookup = new double[samples.Length];
            var frames = new double[samples.Length];
            totalRebuildAllocatedBytes = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                rebuild[i] = samples[i].rebuildMs;
                lookup[i] = samples[i].lookupMs;
                frames[i] = samples[i].observedFrameMs;
                totalRebuildAllocatedBytes += samples[i].allocatedBytes;
            }
            Array.Sort(rebuild); Array.Sort(lookup); Array.Sort(frames);
            rebuildMedianMs = Median(rebuild); rebuildP95Ms = P95(rebuild); rebuildMaxMs = rebuild[rebuild.Length - 1];
            lookupMedianMs = Median(lookup);
            frameMedianMs = Median(frames); frameP95Ms = P95(frames); frameMaxMs = frames[frames.Length - 1];
        }

        private static double Median(double[] values) => (values[(values.Length - 1) / 2] + values[values.Length / 2]) / 2;
        private static double P95(double[] values) => values[(int)Math.Ceiling(values.Length * 0.95) - 1];
    }

    // All setup/array allocation happens before the measured warm steady-state rebuild.
    public sealed class PathfindingBenchmarkCase
    {
        public const int LookupCount = 10000;
        private readonly int[] exits = new int[5], spawns = new int[5];
        private readonly ReverseBfsField[] fields;
        private readonly bool nearest;
        public long LookupChecksum { get; private set; }

        public PathfindingBenchmarkCase(int size, int fieldCount, bool maze)
        {
            if (size < 5) throw new ArgumentOutOfRangeException(nameof(size));
            if (fieldCount != 1 && fieldCount != 5) throw new ArgumentOutOfRangeException(nameof(fieldCount));
            var grid = new SquareNavigationGrid(size, size);
            if (maze)
                for (int x = 4; x < size - 1; x += 6)
                for (int y = 0; y < size; y++)
                    if (y != (x * 7) % size) grid.SetWalkable(grid.ToNode(x, y), false);
            for (int i = 0; i < 5; i++)
            {
                int y = i * (size - 1) / 4;
                exits[i] = grid.ToNode(size - 1, y);
                spawns[i] = grid.ToNode(0, y);
            }
            nearest = fieldCount == 1;
            fields = new ReverseBfsField[fieldCount];
            for (int i = 0; i < fieldCount; i++) fields[i] = new ReverseBfsField(grid);
            // Warm timing and allocation APIs too, outside measurement.
            Stopwatch.GetTimestamp(); GC.GetAllocatedBytesForCurrentThread();
        }

        public void Rebuild()
        {
            for (int i = 0; i < fields.Length; i++) fields[i].Rebuild(exits, nearest ? 31 : 1 << i);
        }

        public void Measure(PathfindingBenchmarkSample sample)
        {
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            Rebuild();
            long end = Stopwatch.GetTimestamp();
            sample.allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            sample.rebuildMs = (end - start) * 1000.0 / Stopwatch.Frequency;
            long checksum = 0;
            start = Stopwatch.GetTimestamp();
            for (int i = 0; i < LookupCount; i++)
            {
                var field = fields[i % fields.Length];
                int node = spawns[i % spawns.Length];
                checksum += field.Distance(node) + field.NextNode(node);
            }
            sample.lookupMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            LookupChecksum = checksum;
            if (checksum <= 0) throw new InvalidOperationException("Benchmark routes must be reachable.");
        }
    }
}
