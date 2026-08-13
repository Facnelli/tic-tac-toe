using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.EditModeTests.Boards
{
    /// <summary>
    /// Fixa o contrato público do aplicador de mutações autorizadas.
    ///
    /// O serviço pode receber vários comandos de uma futura runa, mas precisa
    /// apresentar o resultado como uma única operação lógica: validar tudo antes,
    /// consolidar cada casa e alterar somente o estado final necessário.
    /// </summary>
    public sealed class BoardMutationServiceTests
    {
        private BoardMutationService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new BoardMutationService();
        }

        [Test]
        public void ApplyAuthorized_WhenLateCoordinateIsInvalid_IsAtomic()
        {
            BoardState board = CreateEmptyBoard(2, 1);
            BoardCoordinate valid = new BoardCoordinate(0, 0);
            BoardCoordinate invalid = new BoardCoordinate(4, 4);

            BoardMutationCommand[] commands =
            {
                BoardMutationCommand.AddMark(valid, CellMark.X),
                BoardMutationCommand.AddMark(invalid, CellMark.O)
            };

            /*
             * O primeiro comando é válido, mas não pode ser aplicado antes que o
             * segundo também seja validado. Caso contrário, uma runa composta
             * poderia deixar metade de seu efeito no tabuleiro.
             */
            Assert.Throws<ArgumentException>(
                () => _service.ApplyAuthorized(board, commands));

            Assert.That(board.GetCell(valid).IsEmpty, Is.True);
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void ApplyAuthorized_OnMultipleCells_ReportsDeterministicChanges()
        {
            BoardState board = CreateEmptyBoard(2, 1);
            BoardCoordinate first = new BoardCoordinate(0, 0);
            BoardCoordinate second = new BoardCoordinate(1, 0);

            BoardChangeSet result = _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.AddMark(first, CellMark.X),
                    BoardMutationCommand.AddMark(second, CellMark.O)
                });

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Coordinate, Is.EqualTo(first));
            Assert.That(result[1].Coordinate, Is.EqualTo(second));
            Assert.That(result[0].Before.IsEmpty, Is.True);
            Assert.That(result[0].After.HasMark(CellMark.X), Is.True);
            Assert.That(result[1].Before.IsEmpty, Is.True);
            Assert.That(result[1].After.HasMark(CellMark.O), Is.True);
            Assert.That(board.Version, Is.EqualTo(2));
        }

        [Test]
        public void ApplyAuthorized_WithSeveralCommandsOnSameCell_ConsolidatesReport()
        {
            BoardState board = CreateEmptyBoard(1, 1);
            BoardCoordinate coordinate = new BoardCoordinate(0, 0);

            BoardChangeSet result = _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.AddMark(coordinate, CellMark.X),
                    BoardMutationCommand.AddModifier(
                        coordinate,
                        CellModifierId.Golden),
                    BoardMutationCommand.AddMark(coordinate, CellMark.O)
                });

            /*
             * Três mudanças reais chegam ao BoardState, mas a apresentação recebe
             * somente um BoardChange com o antes da ação e o resultado final.
             */
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Coordinate, Is.EqualTo(coordinate));
            Assert.That(result[0].Before.IsEmpty, Is.True);
            Assert.That(result[0].After.ContainsBothMarks, Is.True);
            Assert.That(
                result[0].After.HasModifier(CellModifierId.Golden),
                Is.True);
            Assert.That(result[0].MarksChanged, Is.True);
            Assert.That(result[0].ModifiersChanged, Is.True);
            Assert.That(board.Version, Is.EqualTo(3));
        }

        [Test]
        public void ApplyAuthorized_WhenEveryCommandIsNoOp_ReturnsEmptyResult()
        {
            BoardCoordinate coordinate = new BoardCoordinate(0, 0);
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(1, 1),
                new[]
                {
                    new CellState(
                        coordinate,
                        CellMark.X,
                        new[] { CellModifierId.Golden })
                });

            BoardChangeSet result = _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.AddMark(coordinate, CellMark.X),
                    BoardMutationCommand.RemoveMark(coordinate, CellMark.O),
                    BoardMutationCommand.AddModifier(
                        coordinate,
                        CellModifierId.Golden)
                });

            Assert.That(result, Is.SameAs(BoardChangeSet.Empty));
            Assert.That(board.GetCell(coordinate).Marks, Is.EqualTo(CellMark.X));
            Assert.That(
                board.GetCell(coordinate).HasModifier(CellModifierId.Golden),
                Is.True);
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void ApplyAuthorized_WhenCommandsCancelEachOther_DoesNotChangeBoard()
        {
            BoardState board = CreateEmptyBoard(1, 1);
            BoardCoordinate coordinate = new BoardCoordinate(0, 0);

            BoardChangeSet result = _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.AddMark(coordinate, CellMark.X),
                    BoardMutationCommand.RemoveMark(coordinate, CellMark.X),
                    BoardMutationCommand.AddModifier(
                        coordinate,
                        CellModifierId.Golden),
                    BoardMutationCommand.RemoveModifier(
                        coordinate,
                        CellModifierId.Golden)
                });

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(board.GetCell(coordinate).IsEmpty, Is.True);
            Assert.That(board.Version, Is.Zero);
        }

        [Test]
        public void ApplyAuthorized_AcrossBatches_EvolvesVersionOnlyForFinalDifferences()
        {
            BoardState board = CreateEmptyBoard(1, 1);
            BoardCoordinate coordinate = new BoardCoordinate(0, 0);

            _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.AddMark(coordinate, CellMark.X)
                });

            Assert.That(board.Version, Is.EqualTo(1));

            _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.RemoveMark(coordinate, CellMark.X),
                    BoardMutationCommand.AddMark(coordinate, CellMark.X)
                });

            /*
             * O segundo lote possui estados intermediários, porém termina igual ao
             * início. A versão autoritativa precisa continuar em 1.
             */
            Assert.That(board.Version, Is.EqualTo(1));

            _service.ApplyAuthorized(
                board,
                new[]
                {
                    BoardMutationCommand.AddMark(coordinate, CellMark.O),
                    BoardMutationCommand.AddModifier(
                        coordinate,
                        CellModifierId.Golden)
                });

            Assert.That(board.Version, Is.EqualTo(3));
            Assert.That(board.GetCell(coordinate).ContainsBothMarks, Is.True);
            Assert.That(
                board.GetCell(coordinate).HasModifier(CellModifierId.Golden),
                Is.True);
        }

        private static BoardState CreateEmptyBoard(int width, int height)
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(width, height));
        }
    }
}