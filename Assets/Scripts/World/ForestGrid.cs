using System;
using UnityEngine;

namespace MyFreshMushroom
{
    public sealed class ForestGrid
    {
        public int Size { get; }
        public Vector2 TileSize { get; }

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

        // Integer coordinates identify tile corners. The whole map is centred at (0, 0).
        public Vector2 GridToWorld(Vector2 position)
        {
            return new Vector2((position.x - position.y) * TileSize.x * 0.5f,
                (position.x + position.y - Size) * TileSize.y * 0.5f);
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
            float sum = position.y * 2 / TileSize.y + Size;
            float difference = position.x * 2 / TileSize.x;
            float x = (sum + difference) * 0.5f;
            float y = (sum - difference) * 0.5f;
            if (x < 0 || y < 0 || x >= Size || y >= Size)
                return false;
            cell = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
            return true;
        }

        public bool ContainsFootprint(Vector2Int origin, Vector2Int size)
        {
            return size.x > 0 && size.y > 0 && origin.x >= 0 && origin.y >= 0
                && size.x <= Size && size.y <= Size
                && origin.x <= Size - size.x && origin.y <= Size - size.y;
        }
    }
}
