using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyFreshMushroom.Tests
{
    public sealed class ForestGridTests
    {
        [Test]
        public void EveryCellCentreRoundTripsThroughProjection()
        {
            var grid = new ForestGrid(16, new Vector2(1, 0.5f));
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
            {
                var expected = new Vector2Int(x, y);
                Assert.That(grid.TryWorldToCell(grid.CellCenterToWorld(expected), out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
            }
            Assert.That(grid.GridToWorld(new Vector2(8, 8)), Is.EqualTo(Vector2.zero));
        }

        [TestCase(-0.01f, 8)]
        [TestCase(8, -0.01f)]
        [TestCase(16, 8)]
        [TestCase(8, 16)]
        public void OutsideAndExclusiveUpperEdgesAreRejected(float x, float y)
        {
            var grid = new ForestGrid(16, new Vector2(1, 0.5f));
            Assert.That(grid.TryWorldToCell(grid.GridToWorld(new Vector2(x, y)), out _), Is.False);
        }

        [Test]
        public void FootprintMustFitEntirelyAndDoesNotOverflow()
        {
            var grid = new ForestGrid(16, new Vector2(1, 0.5f));
            Assert.That(grid.ContainsFootprint(new Vector2Int(14, 14), new Vector2Int(2, 2)), Is.True);
            Assert.That(grid.ContainsFootprint(new Vector2Int(15, 14), new Vector2Int(2, 2)), Is.False);
            Assert.That(grid.ContainsFootprint(new Vector2Int(-1, 0), Vector2Int.one), Is.False);
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
            var shelf = new FacilityState("shelf", data, new Vector2Int(7, 7));
            var other = new FacilityState("other", data, Vector2Int.zero);
            var selection = new FacilitySelection(new[] { shelf });
            int notifications = 0;
            selection.Changed += _ => notifications++;
            Assert.That(selection.Select(other), Is.False);
            Assert.That(selection.Select(shelf), Is.True);
            Assert.That(selection.Select(shelf), Is.False);
            Assert.That(selection.Selected, Is.SameAs(shelf));
            Assert.That(shelf.ContainsCell(new Vector2Int(8, 8)), Is.True);
            Assert.That(shelf.ContainsCell(new Vector2Int(9, 8)), Is.False);
            Assert.That(selection.Select(null), Is.True);
            Assert.That(selection.Selected, Is.Null);
            Assert.That(notifications, Is.EqualTo(2));
        }
    }
}
