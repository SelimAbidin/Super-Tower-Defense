using System;

namespace SuperTowerDefense.Navigation
{
    public sealed class SquareNavigationGrid : INavigationGrid
    {
        private readonly bool[] walkable;
        public int Width { get; }
        public int Height { get; }
        public int NodeCount => walkable.Length;
        public int NeighborSlotCount => 4;
        public int Revision { get; private set; }

        public SquareNavigationGrid(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            walkable = new bool[checked(width * height)];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = true;
        }

        public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public int ToNode(int x, int y)
        {
            if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Tile is outside the grid.");
            return x + y * Width;
        }

        public int GetX(int node) { ValidateNode(node); return node % Width; }
        public int GetY(int node) { ValidateNode(node); return node / Width; }
        public bool IsWalkable(int node) { ValidateNode(node); return walkable[node]; }

        public void SetWalkable(int node, bool value)
        {
            ValidateNode(node);
            if (walkable[node] == value) return;
            walkable[node] = value;
            Revision++;
        }

        // North, east, south, west. Geometry is independent of walkability.
        public int GetNeighbor(int node, int slot)
        {
            ValidateNode(node);
            int x = node % Width, y = node / Width;
            switch (slot)
            {
                case 0: return y + 1 < Height ? node + Width : -1;
                case 1: return x + 1 < Width ? node + 1 : -1;
                case 2: return y > 0 ? node - Width : -1;
                case 3: return x > 0 ? node - 1 : -1;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private void ValidateNode(int node)
        {
            if (node < 0 || node >= NodeCount) throw new ArgumentOutOfRangeException(nameof(node));
        }
    }
}
