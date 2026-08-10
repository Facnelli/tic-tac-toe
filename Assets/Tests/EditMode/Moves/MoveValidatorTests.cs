using System;
using System.Linq;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;

namespace TicTacToeRoguelike.Domain.EditModeTests.Moves
{
    public sealed class MoveValidatorTests
    {
        private MoveValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _validator = new MoveValidator();
        }

        [Test]
        public void ValidateNormalMove_OnEmptyCell_IsAllowedAndDoesNotChangeBoard()
        {
            BoardState board = CreateBoard();

            MoveValidationResult result = _validator.ValidateNormalMove(
                board,
                new BoardCoordinate(2, 0),
                CellMark.O);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.RejectionReason, Is.EqualTo(MoveRejectionReason.None));
            Assert.That(board.GetCell(2, 0).IsEmpty, Is.True);
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void ValidateNormalMove_OnOccupiedCommonCell_RejectsOverlap()
        {
            MoveValidationResult result = _validator.ValidateNormalMove(
                CreateBoard(),
                new BoardCoordinate(0, 0),
                CellMark.O);

            AssertRejected(
                result,
                MoveRejectionReason.OccupiedCellDoesNotAllowOverlap);
        }

        [Test]
        public void ValidateNormalMove_OnOccupiedGoldenCell_AllowsOtherMark()
        {
            MoveValidationResult result = _validator.ValidateNormalMove(
                CreateBoard(),
                new BoardCoordinate(1, 0),
                CellMark.O);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateNormalMove_WhenMarkAlreadyExists_RejectsEvenGoldenCell()
        {
            MoveValidationResult result = _validator.ValidateNormalMove(
                CreateBoard(),
                new BoardCoordinate(1, 0),
                CellMark.X);

            AssertRejected(result, MoveRejectionReason.MarkAlreadyPresent);
        }

        [Test]
        public void ValidateNormalMove_OnMissingCell_RejectsMove()
        {
            MoveValidationResult result = _validator.ValidateNormalMove(
                CreateBoard(),
                new BoardCoordinate(9, 9),
                CellMark.X);

            AssertRejected(result, MoveRejectionReason.CellDoesNotExist);
        }

        [TestCase(CellMark.None)]
        [TestCase(CellMark.X | CellMark.O)]
        public void ValidateNormalMove_WithInvalidMoveMark_Throws(CellMark mark)
        {
            Assert.Throws<ArgumentException>(() => _validator.ValidateNormalMove(
                CreateBoard(),
                new BoardCoordinate(2, 0),
                mark));
        }

        [Test]
        public void ValidateNormalMove_WithNullBoard_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateNormalMove(
                null,
                new BoardCoordinate(0, 0),
                CellMark.X));
        }

        [Test]
        public void GetLegalNormalMoves_ReturnsOnlyCoordinatesAllowedForMark()
        {
            BoardState board = CreateBoard();

            BoardCoordinate[] legalMoves =
                _validator.GetLegalNormalMoves(board, CellMark.O).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    new BoardCoordinate(1, 0),
                    new BoardCoordinate(2, 0)
                },
                legalMoves);
        }

        private static BoardState CreateBoard()
        {
            BoardDefinition definition = BoardDefinition.CreateRectangular(3, 1);

            return new BoardState(
                definition,
                new[]
                {
                    new CellState(new BoardCoordinate(0, 0), CellMark.X),
                    new CellState(
                        new BoardCoordinate(1, 0),
                        CellMark.X,
                        new[] { CellModifierId.Golden })
                });
        }

        private static void AssertRejected(
            MoveValidationResult result,
            MoveRejectionReason reason)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.RejectionReason, Is.EqualTo(reason));
        }
    }
}
