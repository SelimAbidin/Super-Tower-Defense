using System;

namespace SuperTowerDefense.Navigation
{
    public enum DestinationPolicy { Nearest, Assigned, Subset }

    public sealed class NavigationFields
    {
        private readonly ReverseBfsField[] fields;
        public DestinationPolicy Policy { get; }
        public int ExitCount { get; }
        public int FieldCount => fields.Length;

        public NavigationFields(INavigationGrid grid, int[] exits, DestinationPolicy policy)
        {
            if (exits == null || exits.Length < 1 || exits.Length > 5)
                throw new ArgumentException("Supply one to five exits.", nameof(exits));
            if (!Enum.IsDefined(typeof(DestinationPolicy), policy)) throw new ArgumentOutOfRangeException(nameof(policy));
            Policy = policy;
            ExitCount = exits.Length;
            fields = new ReverseBfsField[policy == DestinationPolicy.Nearest ? 1 : exits.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = new ReverseBfsField(grid);
                fields[i].Rebuild(exits, policy == DestinationPolicy.Nearest ? (1 << exits.Length) - 1 : 1 << i);
            }
        }

        public bool TryRoute(int node, int allowedMask, out int next, out int exitId, out int distance)
        {
            int all = (1 << ExitCount) - 1;
            if (allowedMask <= 0 || (allowedMask & ~all) != 0) throw new ArgumentOutOfRangeException(nameof(allowedMask));
            if (Policy == DestinationPolicy.Nearest && allowedMask != all)
                throw new ArgumentException("Nearest field requires all enabled exits; use subset fields for restricted targets.");
            if (Policy == DestinationPolicy.Assigned && (allowedMask & (allowedMask - 1)) != 0)
                throw new ArgumentException("Assigned policy requires exactly one exit.");
            next = exitId = distance = -1;
            for (int i = 0; i < fields.Length; i++)
            {
                if (Policy != DestinationPolicy.Nearest && (allowedMask & (1 << i)) == 0) continue;
                int candidate = fields[i].Distance(node);
                // First exit ID wins ties. Never select unreachable (-1).
                if (candidate < 0 || (distance >= 0 && candidate >= distance)) continue;
                distance = candidate;
                next = fields[i].NextNode(node);
                exitId = fields[i].GoalId(node);
            }
            return next >= 0;
        }
    }
}
