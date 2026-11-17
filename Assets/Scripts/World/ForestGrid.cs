using System;
using UnityEngine;

namespace MyFreshMushroom
{
    public sealed class ForestGrid
    {
        public int Size { get; }
        public Vector2 TileSize { get; }
        public int MinCellCoordinate => -(Size / 2);
        public int MaxCellCoordinateExclusive => MinCellCoordinate + Size;

        public ForestGrid(int size, Vector2 tileSize)
        {
            if (size <= 0 || size > 128)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (!float.IsFinite(tileSize.x) || !float.IsFinite(tileSize.y)
                || tileSize.x <= 0 || tileSize.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(tileSize));
            Size = size;
            TileSize = tileSize;
        }

        // +X points up-left, +Y up-right; integer coordinates identify bottom corners.
        // The origin is fixed independently of map size; even-sized maps are centred on it.
        public Vector2 GridToWorld(Vector2 position)
        {
            return new Vector2((position.y - position.x) * TileSize.x * 0.5f,
                (position.x + position.y) * TileSize.y * 0.5f);
        }

        public Vector2 CellCenterToWorld(Vector2Int cell)
        {
            return GridToWorld((Vector2)cell + Vector2.one * 0.5f);
        }

        public bool TryWorldToCell(Vector2 position, out Vector2Int cell)
        {
            cell = default;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
                return false;
            float sum = position.y * 2 / TileSize.y;
            float difference = position.x * 2 / TileSize.x;
            float x = (sum - difference) * 0.5f;
            float y = (sum + difference) * 0.5f;
            if (x < MinCellCoordinate || y < MinCellCoordinate
                || x >= MaxCellCoordinateExclusive || y >= MaxCellCoordinateExclusive)
                return false;
            cell = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
            return true;
        }

        public bool ContainsFootprint(Vector2Int origin, Vector2Int size)
        {
            return size.x > 0 && size.y > 0
                && origin.x >= MinCellCoordinate && origin.y >= MinCellCoordinate
                && size.x <= Size && size.y <= Size
                && origin.x <= MaxCellCoordinateExclusive - size.x
                && origin.y <= MaxCellCoordinateExclusive - size.y;
        }
    }
}
