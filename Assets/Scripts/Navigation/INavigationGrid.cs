namespace SuperTowerDefense.Navigation
{
    // Search works with node IDs; geometry and world positions belong to the caller.
    public interface INavigationGrid
    {
        int NodeCount { get; }
        int NeighborSlotCount { get; }
        int Revision { get; }
        bool IsWalkable(int node);
        // Returns -1 for a missing edge. Slots must have a stable order.
        int GetNeighbor(int node, int slot);
    }
}
