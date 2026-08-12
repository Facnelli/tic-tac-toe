using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Actions
{
    /// <summary>
    /// Comprova que jogadas normais e futuras ações especiais participam da
    /// mesma consulta de disponibilidade.
    ///
    /// Estes testes não dependem da Unity, não utilizam Assert.Multiple e não
    /// acessam os métodos internos de mutação do BoardState.
    /// </summary>
    public sealed class ActionAvailabilityServiceTests
    {
        [Test]
        public void Evaluate_NullBoard_ThrowsArgumentNullException()
        {
            ActionAvailabilityService service =
                new ActionAvailabilityService();

            Assert.Throws<ArgumentNullException>(
                () => service.Evaluate(
                    null,
                    ScoreActor.Player));
        }

        [Test]
        public void Evaluate_NoneActor_ThrowsArgumentOutOfRangeException()
        {
            ActionAvailabilityService service =
                new ActionAvailabilityService();

            BoardState board =
                CreateEmptyBoard(1, 1);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.Evaluate(
                    board,
                    ScoreActor.None));
        }

        [Test]
        public void Evaluate_EnvironmentActor_ThrowsArgumentOutOfRangeException()
        {
            ActionAvailabilityService service =
                new ActionAvailabilityService();

            BoardState board =
                CreateEmptyBoard(1, 1);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.Evaluate(
                    board,
                    ScoreActor.Environment));
        }

        [Test]
        public void Constructor_NullMoveValidator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ActionAvailabilityService(
                    null,
                    Array.Empty<IActionAvailabilityProvider>()));
        }

        [Test]
        public void Constructor_NullProviderCollection_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ActionAvailabilityService(
                    new MoveValidator(),
                    null));
        }

        [Test]
        public void Constructor_NullProviderItem_ThrowsArgumentException()
        {
            IActionAvailabilityProvider[] providers =
            {
                null
            };

            Assert.Throws<ArgumentException>(
                () => new ActionAvailabilityService(
                    new MoveValidator(),
                    providers));
        }

        [Test]
        public void Evaluate_EmptyBoard_ReturnsEveryLegalNormalMove()
        {
            BoardState board =
                CreateEmptyBoard(2, 2);

            ActionAvailabilityService service =
                new ActionAvailabilityService();

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                4,
                report.LegalNormalMoveCount);

            Assert.IsTrue(
                report.HasAvailableNormalMoves);

            Assert.IsFalse(
                report.HasAvailableSpecialActions);

            Assert.IsTrue(
                report.HasAvailableActions);

            Assert.AreEqual(
                0,
                report.BoardVersion);

            Assert.AreEqual(
                ScoreActor.Player,
                report.Actor);
        }

        [Test]
        public void Evaluate_FullBoardWithoutSpecialAction_ReturnsUnavailable()
        {
            BoardState board =
                CreateFullBoard(2, 2);

            ActionAvailabilityService service =
                new ActionAvailabilityService();

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                0,
                report.LegalNormalMoveCount);

            Assert.AreEqual(
                0,
                report.AvailableSpecialActionProviderCount);

            Assert.IsFalse(
                report.HasAvailableActions);
        }

        [Test]
        public void Evaluate_FullBoardWithAvailableSpecialAction_ReturnsAvailable()
        {
            BoardState board =
                CreateFullBoard(2, 2);

            RecordingProvider availableProvider =
                new RecordingProvider(true);

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        availableProvider
                    });

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                0,
                report.LegalNormalMoveCount);

            Assert.AreEqual(
                1,
                report.AvailableSpecialActionProviderCount);

            Assert.IsTrue(
                report.HasAvailableSpecialActions);

            Assert.IsTrue(
                report.HasAvailableActions);
        }

        [Test]
        public void Evaluate_FullBoardWithUnavailableProvider_ReturnsUnavailable()
        {
            BoardState board =
                CreateFullBoard(1, 1);

            RecordingProvider unavailableProvider =
                new RecordingProvider(false);

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        unavailableProvider
                    });

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                0,
                report.AvailableSpecialActionProviderCount);

            Assert.IsFalse(
                report.HasAvailableActions);
        }

        [Test]
        public void Evaluate_MultipleProviders_CountsOnlyAvailableProviders()
        {
            RecordingProvider firstAvailable =
                new RecordingProvider(true);

            RecordingProvider unavailable =
                new RecordingProvider(false);

            RecordingProvider secondAvailable =
                new RecordingProvider(true);

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        firstAvailable,
                        unavailable,
                        secondAvailable
                    });

            ActionAvailabilityReport report =
                service.Evaluate(
                    CreateFullBoard(1, 1),
                    ScoreActor.Enemy);

            Assert.AreEqual(
                2,
                report.AvailableSpecialActionProviderCount);

            Assert.AreEqual(
                1,
                firstAvailable.CallCount);

            Assert.AreEqual(
                1,
                unavailable.CallCount);

            Assert.AreEqual(
                1,
                secondAvailable.CallCount);
        }

        [Test]
        public void Evaluate_NormalMoveExists_StillConsultsSpecialProviders()
        {
            RecordingProvider provider =
                new RecordingProvider(false);

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        provider
                    });

            ActionAvailabilityReport report =
                service.Evaluate(
                    CreateEmptyBoard(1, 1),
                    ScoreActor.Player);

            Assert.IsTrue(
                report.HasAvailableNormalMoves);

            Assert.AreEqual(
                1,
                provider.CallCount);
        }

        [Test]
        public void Evaluate_UsesPlayerXAndEnemyOForNormalMoves()
        {
            BoardCoordinate coordinate =
                new BoardCoordinate(0, 0);

            BoardDefinition definition =
                BoardDefinition.CreateRectangular(1, 1);

            CellState goldenCellWithX =
                new CellState(
                    coordinate,
                    CellMark.X,
                    new[]
                    {
                        CellModifierId.Golden
                    });

            BoardState board =
                new BoardState(
                    definition,
                    new[]
                    {
                        goldenCellWithX
                    });

            ActionAvailabilityService service =
                new ActionAvailabilityService();

            ActionAvailabilityReport playerReport =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            ActionAvailabilityReport enemyReport =
                service.Evaluate(
                    board,
                    ScoreActor.Enemy);

            Assert.AreEqual(
                0,
                playerReport.LegalNormalMoveCount);

            Assert.AreEqual(
                1,
                enemyReport.LegalNormalMoveCount);

            Assert.AreEqual(
                coordinate,
                enemyReport.LegalNormalMoves[0]);
        }

        [Test]
        public void Evaluate_ProviderReceivesExpectedContext()
        {
            BoardState board =
                CreateEmptyBoard(1, 1);

            RecordingProvider provider =
                new RecordingProvider(true);

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        provider
                    });

            service.Evaluate(
                board,
                ScoreActor.Enemy);

            Assert.IsNotNull(
                provider.LastContext);

            Assert.AreSame(
                board,
                provider.LastContext.Board);

            Assert.AreEqual(
                ScoreActor.Enemy,
                provider.LastContext.Actor);

            Assert.AreEqual(
                CellMark.O,
                provider.LastContext.NormalMoveMark);

            Assert.AreEqual(
                board.Version,
                provider.LastContext.BoardVersion);
        }

        [Test]
        public void Evaluate_ProviderChangesBoard_ThrowsInvalidOperationException()
        {
            BoardState board =
                CreateEmptyBoard(1, 1);

            MutatingProvider provider =
                new MutatingProvider();

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        provider
                    });

            Assert.Throws<InvalidOperationException>(
                () => service.Evaluate(
                    board,
                    ScoreActor.Player));
        }

        [Test]
        public void Evaluate_IrregularBoard_ReturnsOnlyExistingCoordinates()
        {
            BoardCoordinate blockedCoordinate =
                new BoardCoordinate(1, 0);

            BoardDefinition definition =
                BoardDefinition.CreateWithBlockedCells(
                    2,
                    2,
                    new[]
                    {
                        blockedCoordinate
                    });

            BoardState board =
                new BoardState(definition);

            ActionAvailabilityService service =
                new ActionAvailabilityService();

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                3,
                report.LegalNormalMoveCount);

            Assert.IsFalse(
                ContainsCoordinate(
                    report.LegalNormalMoves,
                    blockedCoordinate));
        }

        [Test]
        public void Evaluate_DoesNotChangeBoardVersion()
        {
            BoardState board =
                CreateEmptyBoard(2, 2);

            long versionBefore =
                board.Version;

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        new RecordingProvider(true)
                    });

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                versionBefore,
                board.Version);

            Assert.AreEqual(
                versionBefore,
                report.BoardVersion);
        }

        [Test]
        public void HasAvailableActions_ReturnsSameReducedAnswerAsReport()
        {
            BoardState board =
                CreateFullBoard(1, 1);

            ActionAvailabilityService service =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        new RecordingProvider(true)
                    });

            ActionAvailabilityReport report =
                service.Evaluate(
                    board,
                    ScoreActor.Player);

            bool reducedAnswer =
                service.HasAvailableActions(
                    board,
                    ScoreActor.Player);

            Assert.AreEqual(
                report.HasAvailableActions,
                reducedAnswer);
        }

        [Test]
        public void Report_CopiesLegalNormalMoveCollection()
        {
            List<BoardCoordinate> source =
                new List<BoardCoordinate>
                {
                    new BoardCoordinate(0, 0)
                };

            ActionAvailabilityReport report =
                new ActionAvailabilityReport(
                    ScoreActor.Player,
                    0,
                    source,
                    0);

            source.Add(
                new BoardCoordinate(1, 0));

            Assert.AreEqual(
                1,
                report.LegalNormalMoveCount);
        }

        [Test]
        public void Report_DuplicateNormalCoordinate_ThrowsArgumentException()
        {
            BoardCoordinate coordinate =
                new BoardCoordinate(0, 0);

            Assert.Throws<ArgumentException>(
                () => new ActionAvailabilityReport(
                    ScoreActor.Player,
                    0,
                    new[]
                    {
                        coordinate,
                        coordinate
                    },
                    0));
        }

        private static BoardState CreateEmptyBoard(
            int width,
            int height)
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(
                    width,
                    height));
        }

        private static BoardState CreateFullBoard(
            int width,
            int height)
        {
            BoardState board =
                CreateEmptyBoard(
                    width,
                    height);

            MoveService moveService =
                new MoveService();

            foreach (BoardCoordinate coordinate
                     in board.Definition.Cells)
            {
                MoveValidationResult result =
                    moveService.TryApplyNormalMove(
                        board,
                        coordinate,
                        CellMark.X);

                if (!result.IsValid)
                {
                    throw new InvalidOperationException(
                        $"A preparação do teste não conseguiu ocupar a casa {coordinate}.");
                }
            }

            return board;
        }

        private static bool ContainsCoordinate(
            IReadOnlyList<BoardCoordinate> coordinates,
            BoardCoordinate expected)
        {
            for (int index = 0;
                 index < coordinates.Count;
                 index++)
            {
                if (coordinates[index] == expected)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RecordingProvider :
            IActionAvailabilityProvider
        {
            private readonly bool _hasAvailableAction;

            public int CallCount { get; private set; }

            public ActionAvailabilityContext LastContext
            {
                get;
                private set;
            }

            public RecordingProvider(
                bool hasAvailableAction)
            {
                _hasAvailableAction = hasAvailableAction;
            }

            public bool HasAvailableAction(
                ActionAvailabilityContext context)
            {
                CallCount++;
                LastContext = context;
                return _hasAvailableAction;
            }
        }

        /// <summary>
        /// Provedor propositalmente incorreto usado para comprovar a proteção do
        /// serviço contra consultas que alteram o estado.
        ///
        /// Ele usa MoveService, que é uma API pública autorizada para aplicar uma
        /// jogada. A alteração é válida como jogada, mas inválida durante uma
        /// consulta de disponibilidade.
        /// </summary>
        private sealed class MutatingProvider :
            IActionAvailabilityProvider
        {
            public bool HasAvailableAction(
                ActionAvailabilityContext context)
            {
                MoveService moveService =
                    new MoveService();

                BoardCoordinate coordinate =
                    context.Board.Definition.Cells[0];

                MoveValidationResult result =
                    moveService.TryApplyNormalMove(
                        context.Board,
                        coordinate,
                        context.NormalMoveMark);

                return result.IsValid;
            }
        }
    }
}