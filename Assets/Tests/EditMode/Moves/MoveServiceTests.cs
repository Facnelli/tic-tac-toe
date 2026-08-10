using System;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;

namespace TicTacToeRoguelike.Domain.EditModeTests.Moves
{
    public sealed class MoveServiceTests
    {
        private MoveService _moves;

        [SetUp]
        public void SetUp()
        {
            _moves = new MoveService();
        }

        [Test]
        public void TryApplyNormalMove_WhenAllowed_AddsMarkAndIncrementsVersion()
        {
            BoardState board = CreateEmptyBoard();

            MoveValidationResult result = _moves.TryApplyNormalMove(
                board,
                new BoardCoordinate(0, 0),
                CellMark.X);

            Assert.That(result.IsValid, Is.True);
            Assert.That(board.GetCell(0, 0).HasMark(CellMark.X), Is.True);
            Assert.That(board.Version, Is.EqualTo(1));
        }

        [Test]
        public void TryApplyNormalMove_WhenRejected_DoesNotChangeCellOrVersion()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(1, 1),
                new[]
                {
                    new CellState(new BoardCoordinate(0, 0), CellMark.X)
                });

            MoveValidationResult result = _moves.TryApplyNormalMove(
                board,
                0,
                0,
                CellMark.O);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.RejectionReason,
                Is.EqualTo(MoveRejectionReason.OccupiedCellDoesNotAllowOverlap));
            Assert.That(board.GetCell(0, 0).Marks, Is.EqualTo(CellMark.X));
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void TryApplyNormalMove_OnGoldenCell_AddsSecondMark()
        {
            BoardState board = CreateGoldenBoard(CellMark.X);

            MoveValidationResult result = _moves.TryApplyNormalMove(
                board,
                0,
                0,
                CellMark.O);

            Assert.That(result.IsValid, Is.True);
            Assert.That(board.GetCell(0, 0).ContainsBothMarks, Is.True);
            Assert.That(board.Version, Is.EqualTo(1));
        }

        [Test]
        public void TryApplyNormalMove_WhenSameMarkIsRepeated_DoesNotIncrementVersion()
        {
            BoardState board = CreateGoldenBoard(CellMark.X);

            MoveValidationResult result = _moves.TryApplyNormalMove(
                board,
                0,
                0,
                CellMark.X);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.RejectionReason,
                Is.EqualTo(MoveRejectionReason.MarkAlreadyPresent));
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void CoordinateOverload_AndIntegerOverload_ProduceSameResult()
        {
            BoardState firstBoard = CreateEmptyBoard();
            BoardState secondBoard = CreateEmptyBoard();

            _moves.TryApplyNormalMove(
                firstBoard,
                new BoardCoordinate(1, 1),
                CellMark.O);
            _moves.TryApplyNormalMove(secondBoard, 1, 1, CellMark.O);

            Assert.That(
                firstBoard.GetCell(1, 1).Marks,
                Is.EqualTo(secondBoard.GetCell(1, 1).Marks));
            Assert.That(firstBoard.Version, Is.EqualTo(secondBoard.Version));
        }

        [Test]
        public void Constructor_WithNullValidator_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MoveService(null));
        }

        private static BoardState CreateEmptyBoard()
        {
            return new BoardState(BoardDefinition.CreateRectangular(2, 2));
        }

        private static BoardState CreateGoldenBoard(CellMark mark)
        {
            BoardCoordinate coordinate = new BoardCoordinate(0, 0);

            return new BoardState(
                BoardDefinition.CreateRectangular(1, 1),
                new[]
                {
                    new CellState(
                        coordinate,
                        mark,
                        new[] { CellModifierId.Golden })
                });
        }
    }
}
