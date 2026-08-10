using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.EditModeTests.Boards
{
    public sealed class BoardDefinitionTests
    {
        [Test]
        public void CreateRectangular_CreatesAllCellsInDeterministicOrder()
        {
            BoardDefinition definition = BoardDefinition.CreateRectangular(3, 2);

            Assert.That(definition.Width, Is.EqualTo(3));
            Assert.That(definition.Height, Is.EqualTo(2));
            Assert.That(definition.CellCount, Is.EqualTo(6));
            Assert.That(definition.Cells[0], Is.EqualTo(new BoardCoordinate(0, 0)));
            Assert.That(definition.Cells[5], Is.EqualTo(new BoardCoordinate(2, 1)));
            Assert.That(definition.SequenceDirections.Count, Is.EqualTo(4));
        }

        [Test]
        public void CreateWithBlockedCells_RemovesOnlyBlockedCoordinates()
        {
            BoardDefinition definition = BoardDefinition.CreateWithBlockedCells(
                3,
                3,
                new[] { new BoardCoordinate(1, 1) });

            Assert.That(definition.CellCount, Is.EqualTo(8));
            Assert.That(definition.IsInsideBounds(new BoardCoordinate(1, 1)), Is.True);
            Assert.That(definition.ContainsCell(1, 1), Is.False);
            Assert.That(definition.ContainsCell(0, 0), Is.True);
        }

        [Test]
        public void TryGetNextCell_WhenNextPositionIsBlocked_ReturnsFalse()
        {
            BoardDefinition definition = BoardDefinition.CreateWithBlockedCells(
                3,
                1,
                new[] { new BoardCoordinate(1, 0) });

            bool found = definition.TryGetNextCell(
                new BoardCoordinate(0, 0),
                BoardDirection.Horizontal,
                out BoardCoordinate next);

            Assert.That(found, Is.False);
            Assert.That(next, Is.EqualTo(new BoardCoordinate(1, 0)));
        }

        [Test]
        public void Constructor_SortsCoordinates()
        {
            BoardDefinition definition = new BoardDefinition(
                2,
                2,
                new[]
                {
                    new BoardCoordinate(1, 1),
                    new BoardCoordinate(0, 0),
                    new BoardCoordinate(1, 0)
                },
                new[] { BoardDirection.Horizontal });

            CollectionAssert.AreEqual(
                new[]
                {
                    new BoardCoordinate(0, 0),
                    new BoardCoordinate(1, 0),
                    new BoardCoordinate(1, 1)
                },
                definition.Cells);
        }

        [Test]
        public void Constructor_WithRepeatedCell_ThrowsArgumentException()
        {
            BoardCoordinate coordinate = new BoardCoordinate(0, 0);

            Assert.Throws<ArgumentException>(() => new BoardDefinition(
                1,
                1,
                new[] { coordinate, coordinate },
                new[] { BoardDirection.Horizontal }));
        }

        [TestCase(0, 3)]
        [TestCase(3, 0)]
        [TestCase(-1, 3)]
        public void CreateRectangular_WithInvalidDimension_Throws(
            int width,
            int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BoardDefinition.CreateRectangular(width, height));
        }

        [Test]
        public void BoardDirection_NormalizesOppositeDirections()
        {
            Assert.That(
                new BoardDirection(-1, 0),
                Is.EqualTo(BoardDirection.Horizontal));

            Assert.That(
                new BoardDirection(1, -1),
                Is.EqualTo(BoardDirection.SecondaryDiagonal));
        }

        [Test]
        public void BoardDirection_WithoutMovement_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new BoardDirection(0, 0));
        }
    }
}
