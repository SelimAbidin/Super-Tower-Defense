using System;
using System.Collections.Generic;
using SuperTowerDefense.Navigation;
using UnityEditor;
using UnityEngine;

public static class PathfindingValidation
{
    [MenuItem("Tools/Super Tower Defense/Validate Pathfinding")]
    public static void Run()
    {
        GridValidation.Run();
        ValidateAllSmallMazes();
        ValidatePoliciesAndFailures();
        ValidateTickReplay();
        Debug.Log("STD-6 PASS: all 512 small-maze masks against independent forward BFS; nearest/assigned/subset policies; tie order; blocked/invalid goals; stale fields; 12 scenario/policy tick replays with cardinal movement and allowed arrivals.");
    }

    // Independent forward search from each start, instead of repeating reverse-field logic.
    private static int ForwardDistance(SquareNavigationGrid grid, int start, int target)
    {
        if (!grid.IsWalkable(start) || !grid.IsWalkable(target)) return -1;
        var queue = new Queue<int>();
        var distance = new int[grid.NodeCount];
        for (int i = 0; i < distance.Length; i++) distance[i] = -1;
        distance[start] = 0; queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            if (node == target) return distance[node];
            int x = grid.GetX(node), y = grid.GetY(node);
            int[] dx = { -1, 0, 1, 0 }, dy = { 0, -1, 0, 1 };
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if (!grid.Contains(nx, ny)) continue;
                int next = grid.ToNode(nx, ny);
                if (!grid.IsWalkable(next) || distance[next] >= 0) continue;
                distance[next] = distance[node] + 1; queue.Enqueue(next);
            }
        }
        return -1;
    }

    private static void ValidateAllSmallMazes()
    {
        for (int mask = 0; mask < 512; mask++)
        {
            var grid = new SquareNavigationGrid(3, 3);
            for (int node = 0; node < 9; node++) grid.SetWalkable(node, (mask & (1 << node)) == 0);
            for (int exit = 0; exit < 9; exit++)
            {
                if (!grid.IsWalkable(exit)) continue;
                var field = new ReverseBfsField(grid);
                field.Rebuild(new[] { exit }, 1);
                for (int start = 0; start < 9; start++)
                {
                    int expected = ForwardDistance(grid, start, exit);
                    Check(field.Distance(start) == expected, "Independent shortest distance mismatch");
                    int node = start;
                    for (int step = expected; step > 0; step--)
                    {
                        int next = field.NextNode(node);
                        Cardinal(grid, node, next);
                        Check(field.Distance(next) == step - 1, "Distance must decrease");
                        node = next;
                    }
                    if (expected >= 0) Check(node == exit && field.NextNode(node) == node, "Reach exit and stop");
                    else Check(field.NextNode(start) == -1, "No invalid next step for blocked/disconnected tile");
                }
            }
        }
    }

    private static void ValidatePoliciesAndFailures()
    {
        var grid = new SquareNavigationGrid(7, 5);
        int[] exits = { grid.ToNode(0, 0), grid.ToNode(6, 4) };
        var field = new ReverseBfsField(grid);
        field.Rebuild(exits, 3);
        Check(field.Distance(grid.ToNode(1, 1)) == 2 && field.GoalId(grid.ToNode(1, 1)) == 0, "First spawn nearest A");
        Check(field.Distance(grid.ToNode(5, 3)) == 2 && field.GoalId(grid.ToNode(5, 3)) == 1, "Second spawn nearest B");
        Check(field.GoalId(grid.ToNode(3, 2)) == 0, "Stable lowest exit ID on ties");
        var assigned = new NavigationFields(grid, exits, DestinationPolicy.Assigned);
        assigned.TryRoute(grid.ToNode(1, 1), 2, out _, out int goal, out int distance);
        Check(goal == 1 && distance == 8, "Assigned farther destination");
        var subset = new NavigationFields(grid, exits, DestinationPolicy.Subset);
        subset.TryRoute(grid.ToNode(3, 2), 3, out _, out goal, out distance);
        Check(goal == 0 && distance == 5, "Subset tie");
        subset.TryRoute(grid.ToNode(1, 1), 2, out _, out goal, out distance);
        Check(goal == 1 && distance == 8, "Subset restriction");
        var nearest = new NavigationFields(grid, exits, DestinationPolicy.Nearest);
        Check(nearest.FieldCount == 1 && assigned.FieldCount == 2, "Fields shared across spawn positions");
        Expect<ArgumentException>(() => nearest.TryRoute(0, 1, out _, out _, out _));
        Expect<ArgumentException>(() => assigned.TryRoute(0, 3, out _, out _, out _));
        Expect<ArgumentOutOfRangeException>(() => subset.TryRoute(0, 0, out _, out _, out _));
        Expect<ArgumentOutOfRangeException>(() => subset.TryRoute(0, 4, out _, out _, out _));
        Expect<ArgumentException>(() => field.Rebuild(new int[0], 1));
        Expect<ArgumentException>(() => field.Rebuild(new int[6], 1));
        Expect<ArgumentException>(() => field.Rebuild(new[] { 0, 0 }, 3));
        Expect<ArgumentException>(() => field.Rebuild(new[] { 99 }, 1));
        grid.SetWalkable(1, false);
        Expect<InvalidOperationException>(() => field.Distance(2));
        Expect<ArgumentException>(() => field.Rebuild(new[] { 1 }, 1));
        field.Rebuild(exits, 3);
        Check(field.Distance(1) == -1 && field.IsCurrent, "Rebuild after edit");
        // Multiple-goal distances must match the minimum independent forward search.
        for (int node = 0; node < grid.NodeCount; node++)
        {
            int a = ForwardDistance(grid, node, exits[0]), b = ForwardDistance(grid, node, exits[1]);
            int expected = a < 0 ? b : b < 0 ? a : Math.Min(a, b);
            Check(field.Distance(node) == expected, "Multi-goal minimum");
        }
    }

    private static void ValidateTickReplay()
    {
        for (int scenario = 0; scenario < PathfindingSimulation.ScenarioCount; scenario++)
        foreach (DestinationPolicy policy in Enum.GetValues(typeof(DestinationPolicy)))
        {
            var first = new PathfindingSimulation(scenario, policy);
            var replay = new PathfindingSimulation(scenario, policy);
            Check(first.Fields.FieldCount == (policy == DestinationPolicy.Nearest ? 1 : first.ExitCount), "Field count");
            for (int tick = 0; tick < 100; tick++)
            {
                int[] previous = new int[first.AgentCount];
                for (int agent = 0; agent < first.AgentCount; agent++) previous[agent] = first.Position(agent);
                first.AdvanceTick(); replay.AdvanceTick();
                Check(first.Tick == tick + 1 && replay.Tick == first.Tick, "Integer ticks");
                for (int agent = 0; agent < first.AgentCount; agent++)
                {
                    int current = first.Position(agent);
                    Check(current == replay.Position(agent), "Identical inputs reproduce every tick");
                    if (current != previous[agent]) Cardinal(first.Grid, previous[agent], current);
                    bool found = first.Route(agent, out _, out int exit, out int remaining);
                    if (remaining == 0)
                        Check(current == first.Exit(exit) && (first.AllowedMask(agent) & (1 << exit)) != 0, "Allowed arrival");
                    if (scenario == 3) Check(!found && current == first.Spawn(agent), "Disconnected agents stay put");
                    else if (tick == 99) Check(found && remaining == 0, "Connected agents eventually arrive");
                }
            }
        }
    }

    private static void Cardinal(SquareNavigationGrid grid, int a, int b)
    {
        Check(grid.IsWalkable(b) && Math.Abs(grid.GetX(a) - grid.GetX(b)) + Math.Abs(grid.GetY(a) - grid.GetY(b)) == 1,
            "Move exactly one walkable orthogonal tile");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
