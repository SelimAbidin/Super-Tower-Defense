using System;

namespace SuperTowerDefense.Navigation
{
    // Small algorithm sandbox, not a wave, collision or production placement system.
    // No clock, Unity objects, floats, random numbers or background work in simulation state.
    public sealed class PathfindingSimulation
    {
        public const int ScenarioCount = 4;
        public static readonly string[] ScenarioNames = { "1 spawn / 3 exits", "2 spawns / 1 exit", "5 spawns / 5 exits", "Disconnected route" };
        public SquareNavigationGrid Grid { get; }
        public NavigationFields Fields { get; }
        public DestinationPolicy Policy { get; }
        public int Tick { get; private set; }
        private readonly int[] spawns, exits, positions, masks;
        public int AgentCount => positions.Length;
        public int ExitCount => exits.Length;
        public int Spawn(int id) => spawns[id];
        public int Exit(int id) => exits[id];
        public int Position(int id) => positions[id];
        public int AllowedMask(int id) => masks[id];

        public PathfindingSimulation(int scenario, DestinationPolicy policy)
        {
            if (scenario < 0 || scenario >= ScenarioCount) throw new ArgumentOutOfRangeException(nameof(scenario));
            Policy = policy;
            Grid = new SquareNavigationGrid(17, 11);
            for (int y = 0; y < Grid.Height; y++)
            {
                if (y != 3 && y != 7) Grid.SetWalkable(Grid.ToNode(6, y), false);
                if (y != 1 && y != 5 && y != 9) Grid.SetWalkable(Grid.ToNode(10, y), false);
                if (scenario == 3) Grid.SetWalkable(Grid.ToNode(8, y), false);
            }
            if (scenario == 0)
            {
                spawns = new[] { Grid.ToNode(1, 5) };
                exits = new[] { Grid.ToNode(15, 2), Grid.ToNode(15, 5), Grid.ToNode(15, 8) };
            }
            else if (scenario == 1)
            {
                spawns = new[] { Grid.ToNode(1, 2), Grid.ToNode(1, 8) };
                exits = new[] { Grid.ToNode(15, 5) };
            }
            else
            {
                int count = scenario == 2 ? 5 : 1;
                spawns = new int[count]; exits = new int[count];
                for (int i = 0; i < count; i++)
                {
                    spawns[i] = Grid.ToNode(1, count == 1 ? 5 : 1 + i * 2);
                    exits[i] = Grid.ToNode(15, count == 1 ? 5 : 1 + i * 2);
                }
            }
            positions = (int[])spawns.Clone();
            masks = new int[spawns.Length];
            for (int i = 0; i < masks.Length; i++)
                masks[i] = policy == DestinationPolicy.Nearest ? (1 << exits.Length) - 1 :
                    policy == DestinationPolicy.Assigned ? 1 << ((i + 1) % exits.Length) :
                    (1 << (i % exits.Length)) | (1 << ((i + 2) % exits.Length));
            Fields = new NavigationFields(Grid, exits, policy);
        }

        public bool Route(int agent, out int next, out int exit, out int distance) =>
            Fields.TryRoute(positions[agent], masks[agent], out next, out exit, out distance);

        public void AdvanceTick()
        {
            // Every agent reads the same immutable navigation version for the whole tick.
            for (int i = 0; i < positions.Length; i++)
                if (Route(i, out int next, out _, out _)) positions[i] = next;
            Tick++;
        }
    }
}
