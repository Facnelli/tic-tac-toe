using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.EditModeTests.Boards
{
    public sealed class CellStateTests
    {
        private static readonly BoardCoordinate Coordinate =
            new BoardCoordinate(1, 2);

        [Test]
        public void Constructor_WithoutInitialData_CreatesEmptyCell()
        {
            CellState cell = new CellState(Coordinate);

            Assert.That(cell.Coordinate, Is.EqualTo(Coordinate));
            Assert.That(cell.Marks, Is.EqualTo(CellMark.None));
            Assert.That(cell.IsEmpty, Is.True);
            Assert.That(cell.ContainsBothMarks, Is.False);
            Assert.That(cell.Modifiers, Is.Empty);
        }

        [Test]
        public void Constructor_WithSingleMark_StoresMark()
        {
            CellState cell = new CellState(Coordinate, CellMark.X);

            Assert.That(cell.HasMark(CellMark.X), Is.True);
            Assert.That(cell.IsEmpty, Is.False);
            Assert.That(cell.ContainsBothMarks, Is.False);
        }

        [Test]
        public void Constructor_WithBothMarks_RecognizesEachMark()
        {
            CellState cell = new CellState(
                Coordinate,
                CellMark.X | CellMark.O);

            Assert.That(cell.HasMark(CellMark.X), Is.True);
            Assert.That(cell.HasMark(CellMark.O), Is.True);
            Assert.That(cell.ContainsBothMarks, Is.True);
        }

        [Test]
        public void Constructor_WithGoldenModifier_StoresModifierSeparately()
        {
            CellState cell = new CellState(
                Coordinate,
                CellMark.None,
                new[] { CellModifierId.Golden });

            Assert.That(cell.HasModifier(CellModifierId.Golden), Is.True);
            Assert.That(cell.IsEmpty, Is.True);
        }

        [Test]
        public void Constructor_WithRepeatedModifier_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CellState(
                Coordinate,
                CellMark.None,
                new[] { CellModifierId.Golden, CellModifierId.Golden }));
        }

        [Test]
        public void Constructor_WithUnknownMark_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new CellState(Coordinate, (CellMark)8));
        }

        [TestCase(CellMark.None)]
        [TestCase(CellMark.X | CellMark.O)]
        public void HasMark_WithAnythingOtherThanOneMark_Throws(CellMark mark)
        {
            CellState cell = new CellState(Coordinate);

            Assert.Throws<ArgumentException>(() => cell.HasMark(mark));
        }

        [Test]
        public void Modifiers_CannotBeChangedThroughPublicCollection()
        {
            CellState cell = new CellState(
                Coordinate,
                CellMark.None,
                new[] { CellModifierId.Golden });

            IList<CellModifierId> modifiers =
                (IList<CellModifierId>)cell.Modifiers;

            Assert.Throws<NotSupportedException>(
                () => modifiers.Add(CellModifierId.Golden));
        }
    }
}
