using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyFreshMushroom.Tests
{
    public sealed class ForestGridTests
    {
        [TestCase(1f, 0.5f)]
        [TestCase(2f, 1.5f)]
        public void PositiveAxesPointUpLeftAndUpRight(float tileWidth, float tileHeight)
        {
            var grid = new ForestGrid(16, new Vector2(tileWidth, tileHeight));
            Vector2 origin = grid.GridToWorld(new Vector2(7, 7));
            Assert.That(grid.GridToWorld(new Vector2(8, 7)) - origin,
                Is.EqualTo(new Vector2(-tileWidth * 0.5f, tileHeight * 0.5f)));
            Assert.That(grid.GridToWorld(new Vector2(7, 8)) - origin,
                Is.EqualTo(new Vector2(tileWidth * 0.5f, tileHeight * 0.5f)));
            Assert.That(grid.GridToWorld(Vector2.zero), Is.EqualTo(Vector2.zero));
            var largerGrid = new ForestGrid(32, new Vector2(tileWidth, tileHeight));
            Assert.That(largerGrid.GridToWorld(new Vector2(7, 7)), Is.EqualTo(origin));
        }

        [TestCase("Assets/Prefabs/Exploration/SmallForestPathMap.prefab")]
        [TestCase("Assets/Prefabs/Exploration/StumpForestMap.prefab")]
        public void ExplorationGridUsesTheSameAxisDirections(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            var grid = prefab.GetComponentInChildren<Grid>();
            Assert.That(grid, Is.Not.Null);
            var forest = new ForestGrid(16, new Vector2(grid.cellSize.x, grid.cellSize.y));
            Vector3 origin = grid.CellToWorld(Vector3Int.zero);
            Assert.That((Vector2)(grid.CellToWorld(Vector3Int.right) - origin),
                Is.EqualTo(forest.GridToWorld(Vector2.right) - forest.GridToWorld(Vector2.zero)));
            Assert.That((Vector2)(grid.CellToWorld(Vector3Int.up) - origin),
                Is.EqualTo(forest.GridToWorld(Vector2.up) - forest.GridToWorld(Vector2.zero)));
        }

        [Test]
        public void EveryCellCentreRoundTripsThroughProjection()
        {
            var grid = new ForestGrid(16, new Vector2(1, 0.5f));
            for (int x = grid.MinCellCoordinate; x < grid.MaxCellCoordinateExclusive; x++)
            for (int y = grid.MinCellCoordinate; y < grid.MaxCellCoordinateExclusive; y++)
            {
                var expected = new Vector2Int(x, y);
                Assert.That(grid.TryWorldToCell(grid.CellCenterToWorld(expected), out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
            }
            Assert.That(grid.GridToWorld(Vector2.zero), Is.EqualTo(Vector2.zero));
        }

        [TestCase(-8.01f, 0)]
        [TestCase(0, -8.01f)]
        [TestCase(8, 0)]
        [TestCase(0, 8)]
        public void OutsideAndExclusiveUpperEdgesAreRejected(float x, float y)
        {
            var grid = new ForestGrid(16, new Vector2(1, 0.5f));
            Assert.That(grid.TryWorldToCell(grid.GridToWorld(new Vector2(x, y)), out _), Is.False);
        }

        [Test]
        public void FootprintMustFitEntirelyAndDoesNotOverflow()
        {
            var grid = new ForestGrid(16, new Vector2(1, 0.5f));
            Assert.That(grid.ContainsFootprint(new Vector2Int(6, 6), new Vector2Int(2, 2)), Is.True);
            Assert.That(grid.ContainsFootprint(new Vector2Int(7, 6), new Vector2Int(2, 2)), Is.False);
            Assert.That(grid.ContainsFootprint(new Vector2Int(-8, -8), Vector2Int.one), Is.True);
            Assert.That(grid.ContainsFootprint(new Vector2Int(-9, 0), Vector2Int.one), Is.False);
            Assert.That(grid.ContainsFootprint(Vector2Int.zero, Vector2Int.zero), Is.False);
            Assert.That(grid.ContainsFootprint(Vector2Int.one, new Vector2Int(int.MaxValue, 1)), Is.False);
        }

        [Test]
        public void InvalidGeometryIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ForestGrid(0, Vector2.one));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ForestGrid(16, new Vector2(0, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ForestGrid(16, new Vector2(float.NaN, 1)));
            var grid = new ForestGrid(16, Vector2.one);
            Assert.That(grid.TryWorldToCell(new Vector2(float.PositiveInfinity, 0), out _), Is.False);
        }

        [Test]
        public void SelectionOnlyAcceptsOwnedFacilitiesAndNotifiesOnActualChange()
        {
            var data = AssetDatabase.LoadAssetAtPath<FacilityData>("Assets/Data/Facilities/OysterShelf.asset");
            Assert.That(data != null, Is.True);
            var shelf = new FacilityState("shelf", data, new Vector2Int(-1, -1));
            var other = new FacilityState("other", data, Vector2Int.zero);
            var selection = new FacilitySelection(new[] { shelf });
            int notifications = 0;
            selection.Changed += _ => notifications++;
            Assert.That(selection.Select(other), Is.False);
            Assert.That(selection.Select(shelf), Is.True);
            Assert.That(selection.Select(shelf), Is.False);
            Assert.That(selection.Selected, Is.SameAs(shelf));
            Assert.That(shelf.ContainsCell(Vector2Int.zero), Is.True);
            Assert.That(shelf.ContainsCell(new Vector2Int(1, 0)), Is.False);
            Assert.That(selection.Select(null), Is.True);
            Assert.That(selection.Selected, Is.Null);
            Assert.That(notifications, Is.EqualTo(2));
        }
    }
}
