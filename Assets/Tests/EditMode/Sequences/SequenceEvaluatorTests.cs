using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Sequences;

namespace TicTacToeRoguelike.Domain.EditModeTests.Sequences
{
    public sealed class SequenceEvaluatorTests
    {
        private SequenceEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new SequenceEvaluator();
        }

        [TestCase(SequenceDirection.Horizontal)]
        [TestCase(SequenceDirection.Vertical)]
        [TestCase(SequenceDirection.MainDiagonal)]
        [TestCase(SequenceDirection.SecondaryDiagonal)]
        public void EvaluateWinningSequences_FindsSequenceInEachDirection(
            SequenceDirection direction)
        {
            BoardCoordinate[] coordinates = GetCoordinates(direction);
            BoardState board = CreateBoardWithMarks(3, 3, CellMark.X, coordinates);

            IReadOnlyList<SequenceMatch> matches =
                _evaluator.EvaluateWinningSequences(board, CellMark.X, 3);

            SequenceMatch match = matches.Single(item => item.Direction == direction);
            Assert.That(match.Mark, Is.EqualTo(CellMark.X));
            Assert.That(match.Length, Is.EqualTo(3));
            CollectionAssert.AreEqual(coordinates, match.Cells);
        }

        [Test]
        public void EvaluateScoringSequences_LineOfFourProducesAllWindows()
        {
            BoardState board = CreateBoardWithMarks(
                4,
                1,
                CellMark.X,
                new[]
                {
                    new BoardCoordinate(0, 0),
                    new BoardCoordinate(1, 0),
                    new BoardCoordinate(2, 0),
                    new BoardCoordinate(3, 0)
                });

            IReadOnlyList<SequenceMatch> matches =
                _evaluator.EvaluateScoringSequences(board, CellMark.X, 4);

            Assert.That(matches.Count(item => item.Length == 2), Is.EqualTo(3));
            Assert.That(matches.Count(item => item.Length == 3), Is.EqualTo(2));
            Assert.That(matches.Count(item => item.Length == 4), Is.EqualTo(1));
        }

        [Test]
        public void EvaluateWinningSequences_LineOfFourContainsTwoWinningTrios()
        {
            BoardState board = CreateBoardWithMarks(
                4,
                1,
                CellMark.X,
                new[]
                {
                    new BoardCoordinate(0, 0),
                    new BoardCoordinate(1, 0),
                    new BoardCoordinate(2, 0),
                    new BoardCoordinate(3, 0)
                });

            IReadOnlyList<SequenceMatch> matches =
                _evaluator.EvaluateWinningSequences(board, CellMark.X, 3);

            Assert.That(matches.Count, Is.EqualTo(2));
            Assert.That(_evaluator.HasWinningSequence(board, CellMark.X, 3), Is.True);
        }

        [Test]
        public void EvaluateSequences_DifferentMarkInterruptsLine()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(3, 1),
                new[]
                {
                    new CellState(new BoardCoordinate(0, 0), CellMark.X),
                    new CellState(new BoardCoordinate(1, 0), CellMark.O),
                    new CellState(new BoardCoordinate(2, 0), CellMark.X)
                });

            IReadOnlyList<SequenceMatch> matches =
                _evaluator.EvaluateWinningSequences(board, CellMark.X, 2);

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void EvaluateSequences_BlockedCellInterruptsLine()
        {
            BoardDefinition definition = BoardDefinition.CreateWithBlockedCells(
                3,
                1,
                new[] { new BoardCoordinate(1, 0) });
            BoardState board = new BoardState(
                definition,
                new[]
                {
                    new CellState(new BoardCoordinate(0, 0), CellMark.X),
                    new CellState(new BoardCoordinate(2, 0), CellMark.X)
                });

            Assert.That(
                _evaluator.HasWinningSequence(board, CellMark.X, 2),
                Is.False);
        }

        [Test]
        public void EvaluateSequences_CellWithBothMarksParticipatesForBothPlayers()
        {
            BoardCoordinate[] coordinates =
            {
                new BoardCoordinate(0, 0),
                new BoardCoordinate(1, 0),
                new BoardCoordinate(2, 0)
            };
            CellState[] cells = coordinates
                .Select(coordinate => new CellState(
                    coordinate,
                    CellMark.X | CellMark.O))
                .ToArray();
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(3, 1),
                cells);

            Assert.That(
                _evaluator.HasWinningSequence(board, CellMark.X, 3),
                Is.True);
            Assert.That(
                _evaluator.HasWinningSequence(board, CellMark.O, 3),
                Is.True);
        }

        [Test]
        public void SequenceMatch_ExposesCoordinatesAndContainment()
        {
            BoardCoordinate[] coordinates = GetCoordinates(
                SequenceDirection.MainDiagonal);
            BoardState board = CreateBoardWithMarks(3, 3, CellMark.O, coordinates);

            SequenceMatch match = _evaluator
                .EvaluateWinningSequences(board, CellMark.O, 3)
                .Single(item => item.Direction == SequenceDirection.MainDiagonal);

            Assert.That(match.Start, Is.EqualTo(new BoardCoordinate(0, 0)));
            Assert.That(match.End, Is.EqualTo(new BoardCoordinate(2, 2)));
            Assert.That(match.Contains(new BoardCoordinate(1, 1)), Is.True);
            Assert.That(match.Contains(new BoardCoordinate(2, 1)), Is.False);
        }

        [Test]
        public void Evaluation_DoesNotChangeBoardVersion()
        {
            BoardState board = CreateBoardWithMarks(
                2,
                1,
                CellMark.X,
                new[]
                {
                    new BoardCoordinate(0, 0),
                    new BoardCoordinate(1, 0)
                });

            _evaluator.EvaluateScoringSequences(board, CellMark.X, 2);

            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void EvaluateSequences_UsesOnlyDirectionsAllowedByDefinition()
        {
            BoardDefinition definition = new BoardDefinition(
                1,
                3,
                new[]
                {
                    new BoardCoordinate(0, 0),
                    new BoardCoordinate(0, 1),
                    new BoardCoordinate(0, 2)
                },
                new[] { BoardDirection.Horizontal });
            BoardState board = new BoardState(
                definition,
                definition.Cells.Select(coordinate =>
                    new CellState(coordinate, CellMark.X)));

            IReadOnlyList<SequenceMatch> matches =
                _evaluator.EvaluateWinningSequences(board, CellMark.X, 3);

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void EvaluateSequences_WithNullBoard_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _evaluator.EvaluateSequences(null, CellMark.X, 2, 3));
        }

        [TestCase(CellMark.None)]
        [TestCase(CellMark.X | CellMark.O)]
        public void EvaluateSequences_WithInvalidMark_Throws(CellMark mark)
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(2, 2));

            Assert.Throws<ArgumentException>(() =>
                _evaluator.EvaluateSequences(board, mark, 2, 3));
        }

        [Test]
        public void EvaluateSequences_WithMinimumBelowTwo_Throws()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(2, 2));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _evaluator.EvaluateSequences(board, CellMark.X, 1, 3));
        }

        [Test]
        public void EvaluateSequences_WithMaximumBelowMinimum_Throws()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(2, 2));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _evaluator.EvaluateSequences(board, CellMark.X, 3, 2));
        }

        private static BoardCoordinate[] GetCoordinates(
            SequenceDirection direction)
        {
            switch (direction)
            {
                case SequenceDirection.Horizontal:
                    return new[]
                    {
                        new BoardCoordinate(0, 0),
                        new BoardCoordinate(1, 0),
                        new BoardCoordinate(2, 0)
                    };

                case SequenceDirection.Vertical:
                    return new[]
                    {
                        new BoardCoordinate(0, 0),
                        new BoardCoordinate(0, 1),
                        new BoardCoordinate(0, 2)
                    };

                case SequenceDirection.MainDiagonal:
                    return new[]
                    {
                        new BoardCoordinate(0, 0),
                        new BoardCoordinate(1, 1),
                        new BoardCoordinate(2, 2)
                    };

                case SequenceDirection.SecondaryDiagonal:
                    return new[]
                    {
                        new BoardCoordinate(2, 0),
                        new BoardCoordinate(1, 1),
                        new BoardCoordinate(0, 2)
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static BoardState CreateBoardWithMarks(
            int width,
            int height,
            CellMark mark,
            IEnumerable<BoardCoordinate> coordinates)
        {
            IEnumerable<CellState> cells = coordinates.Select(
                coordinate => new CellState(coordinate, mark));

            return new BoardState(
                BoardDefinition.CreateRectangular(width, height),
                cells);
        }
    }
}
