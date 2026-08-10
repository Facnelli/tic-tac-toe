using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;

namespace TicTacToeRoguelike.Domain.EditModeTests.Boards
{
    public sealed class BoardStateTests
    {
        [Test]
        public void Constructor_CreatesOneEmptyCellPerDefinitionCell()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(3, 2));

            Assert.That(board.CellCount, Is.EqualTo(6));
            Assert.That(board.Version, Is.Zero);

            foreach (CellState cell in board.Cells.Values)
            {
                Assert.That(cell.IsEmpty, Is.True);
            }
        }

        [Test]
        public void Constructor_PreservesInitialStateWithoutCountingAChange()
        {
            BoardCoordinate coordinate = new BoardCoordinate(1, 1);
            CellState initialCell = new CellState(
                coordinate,
                CellMark.X,
                new[] { CellModifierId.Golden });

            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(2, 2),
                new[] { initialCell });

            CellState storedCell = board.GetCell(coordinate);
            Assert.That(storedCell.HasMark(CellMark.X), Is.True);
            Assert.That(storedCell.HasModifier(CellModifierId.Golden), Is.True);
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void SpecialBoard_DoesNotCreateCellAtBlockedCoordinate()
        {
            BoardCoordinate blocked = new BoardCoordinate(1, 1);
            BoardState board = new BoardState(
                BoardDefinition.CreateWithBlockedCells(
                    3,
                    3,
                    new[] { blocked }));

            Assert.That(board.ContainsCell(blocked), Is.False);
            Assert.That(board.TryGetCell(blocked, out CellState cell), Is.False);
            Assert.That(cell, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => board.GetCell(blocked));
        }

        [Test]
        public void Constructor_WithInitialCellOutsideDefinition_Throws()
        {
            BoardDefinition definition = BoardDefinition.CreateRectangular(2, 2);
            CellState outsideCell = new CellState(new BoardCoordinate(3, 3));

            Assert.Throws<ArgumentException>(
                () => new BoardState(definition, new[] { outsideCell }));
        }

        [Test]
        public void AppliedMove_ChangesOnlyTargetCellAndIncrementsVersion()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(2, 1));

            new MoveService().TryApplyNormalMove(board, 0, 0, CellMark.X);

            Assert.That(board.GetCell(0, 0).HasMark(CellMark.X), Is.True);
            Assert.That(board.GetCell(1, 0).IsEmpty, Is.True);
            Assert.That(board.Version, Is.EqualTo(1));
        }

        [Test]
        public void Clone_CanChangeWithoutChangingOriginalBoard()
        {
            BoardState original = new BoardState(
                BoardDefinition.CreateRectangular(2, 1));
            MoveService moves = new MoveService();
            moves.TryApplyNormalMove(original, 0, 0, CellMark.X);

            BoardState clone = original.Clone();
            moves.TryApplyNormalMove(clone, 1, 0, CellMark.O);

            Assert.That(original.GetCell(1, 0).IsEmpty, Is.True);
            Assert.That(original.Version, Is.EqualTo(1));
            Assert.That(clone.GetCell(1, 0).HasMark(CellMark.O), Is.True);
            Assert.That(clone.Version, Is.EqualTo(2));
        }
    }
}
