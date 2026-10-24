using System;
using SuperTowerDefense.Navigation;
using UnityEditor;
using UnityEngine;

public static class GridValidation
{
    [MenuItem("Tools/Super Tower Defense/Validate Navigation Grid")]
    public static void Run()
    {
        var grid = new SquareNavigationGrid(3, 2);
        Check(grid.NodeCount == 6 && grid.Revision == 0, "Initial state");
        for (int node = 0; node < grid.NodeCount; node++)
        {
            Check(grid.ToNode(grid.GetX(node), grid.GetY(node)) == node, "Coordinate roundtrip");
            Check(grid.IsWalkable(node), "Default walkability");
            for (int slot = 0; slot < 4; slot++)
            {
                int next = grid.GetNeighbor(node, slot);
                if (next < 0) continue;
                Check(Math.Abs(grid.GetX(next) - grid.GetX(node)) + Math.Abs(grid.GetY(next) - grid.GetY(node)) == 1,
                    "Every edge is cardinal without row wrapping");
                Check(grid.GetNeighbor(next, (slot + 2) % 4) == node, "Reciprocal edges");
            }
        }
        Check(grid.GetNeighbor(0, 0) == 3 && grid.GetNeighbor(0, 1) == 1 &&
              grid.GetNeighbor(0, 2) == -1 && grid.GetNeighbor(0, 3) == -1, "N/E/S/W ordering");
        Check(grid.GetNeighbor(2, 1) == -1 && grid.GetNeighbor(3, 3) == -1, "Row boundaries");
        grid.SetWalkable(1, false);
        Check(!grid.IsWalkable(1) && grid.Revision == 1, "Walkability revision");
        grid.SetWalkable(1, false);
        Check(grid.Revision == 1, "No-op does not change revision");
        grid.SetWalkable(1, true);
        Check(grid.Revision == 2, "Restoring tile changes revision");
        var single = new SquareNavigationGrid(1, 1);
        for (int i = 0; i < 4; i++) Check(single.GetNeighbor(0, i) == -1, "Single tile has no neighbors");
        ExpectRange(() => new SquareNavigationGrid(0, 2));
        ExpectRange(() => new SquareNavigationGrid(2, -1));
        ExpectRange(() => grid.ToNode(3, 0));
        ExpectRange(() => grid.IsWalkable(-1));
        ExpectRange(() => grid.GetNeighbor(6, 0));
        ExpectRange(() => grid.GetNeighbor(0, 4));
        Debug.Log("STD-5 PASS: grid coordinates, cardinal ordering, bounds, reciprocity, walkability and revisions.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ExpectRange(Action action)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException("Expected bounds validation.");
    }
}
