using System;

namespace SuperTowerDefense.Navigation
{
    // Unit-cost, reciprocal edges only. Directed graphs need predecessor enumeration.
    public sealed class ReverseBfsField
    {
        private readonly INavigationGrid grid;
        private readonly int[] distances, nextNodes, goalIds, queue;
        private int builtRevision = -1;
        public bool IsCurrent => builtRevision == grid.Revision;

        public ReverseBfsField(INavigationGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            distances = new int[grid.NodeCount];
            nextNodes = new int[grid.NodeCount];
            goalIds = new int[grid.NodeCount];
            queue = new int[grid.NodeCount];
        }

        // Array positions are stable exit IDs (0..4). A mask selects permitted exits.
        public void Rebuild(int[] exits, int allowedMask)
        {
            if (exits == null || exits.Length < 1 || exits.Length > 5)
                throw new ArgumentException("Supply one to five exits.", nameof(exits));
            int all = (1 << exits.Length) - 1;
            if (allowedMask <= 0 || (allowedMask & ~all) != 0)
                throw new ArgumentOutOfRangeException(nameof(allowedMask));
            for (int i = 0; i < exits.Length; i++)
            {
                if (exits[i] < 0 || exits[i] >= grid.NodeCount || !grid.IsWalkable(exits[i]))
                    throw new ArgumentException("Exits must be valid walkable nodes.", nameof(exits));
                for (int j = 0; j < i; j++)
                    if (exits[i] == exits[j]) throw new ArgumentException("Exit nodes must be distinct.", nameof(exits));
            }
            builtRevision = -1;
            for (int i = 0; i < grid.NodeCount; i++)
                distances[i] = nextNodes[i] = goalIds[i] = -1;
            int read = 0, write = 0;
            // Stable ID ordering resolves equal-distance goals consistently.
            for (int id = 0; id < exits.Length; id++)
            {
                if ((allowedMask & (1 << id)) == 0) continue;
                int node = exits[id];
                distances[node] = 0;
                nextNodes[node] = node;
                goalIds[node] = id;
                queue[write++] = node;
            }
            while (read < write)
            {
                int node = queue[read++];
                for (int slot = 0; slot < grid.NeighborSlotCount; slot++)
                {
                    int neighbor = grid.GetNeighbor(node, slot);
                    if (neighbor < 0 || !grid.IsWalkable(neighbor) || distances[neighbor] >= 0) continue;
                    distances[neighbor] = distances[node] + 1;
                    nextNodes[neighbor] = node;
                    goalIds[neighbor] = goalIds[node];
                    queue[write++] = neighbor;
                }
            }
            builtRevision = grid.Revision;
        }

        public int Distance(int node) { Validate(node); return distances[node]; }
        public int NextNode(int node) { Validate(node); return nextNodes[node]; }
        public int GoalId(int node) { Validate(node); return goalIds[node]; }

        private void Validate(int node)
        {
            if (!IsCurrent) throw new InvalidOperationException("Rebuild the field after navigation changes.");
            if (node < 0 || node >= grid.NodeCount) throw new ArgumentOutOfRangeException(nameof(node));
        }
    }
}
