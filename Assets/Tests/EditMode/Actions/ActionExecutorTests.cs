using System;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.EditModeTests.Actions
{
    /// <summary>
    /// Comprova que ActionExecutor é a fronteira autoritativa comum entre
    /// GameAction, MoveService, BoardState e TurnContext.
    ///
    /// A suíte cobre quatro responsabilidades principais:
    ///
    /// 1. rejeitar intenções incompatíveis com a fase ou com o Turno atual;
    /// 2. impedir que uma intenção atrasada altere outra versão do tabuleiro;
    /// 3. preservar as regras de jogada normal já existentes no MoveService;
    /// 4. consumir orçamento somente depois de uma ação realmente aplicada.
    ///
    /// Nenhum teste depende de GameObject, MonoBehaviour, cena, frame ou
    /// coroutine. Toda a execução ocorre como teste EditMode puro.
    /// </summary>
    public sealed class ActionExecutorTests
    {
        private const long DefaultTurnId = 1;

        private ActionExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            _executor = new ActionExecutor();
        }

        [Test]
        public void Constructor_WithNullMoveService_Throws()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(
                    () => new ActionExecutor(null));

            Assert.That(
                exception.ParamName,
                Is.EqualTo("moveService"));
        }

        [Test]
        public void Execute_WithNullAction_Throws()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(
                    () => _executor.Execute(
                        null,
                        board,
                        turn,
                        encounterAcceptsActions: true));

            Assert.That(
                exception.ParamName,
                Is.EqualTo("action"));
        }

        [Test]
        public void Execute_WithNullBoard_Throws()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(0, 0),
                CellMark.X);

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(
                    () => _executor.Execute(
                        action,
                        null,
                        turn,
                        encounterAcceptsActions: true));

            Assert.That(
                exception.ParamName,
                Is.EqualTo("board"));
        }

        [Test]
        public void Execute_WhenEncounterDoesNotAcceptActions_RejectsWithoutChangingState()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(0, 0),
                CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: false);

            AssertRejected(
                result,
                ActionRejectionReason.EncounterDoesNotAcceptActions);

            AssertBoardAndTurnWereNotChanged(
                board,
                turn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_WithoutOpenTurn_RejectsWithoutChangingBoard()
        {
            BoardState board = CreateEmptyBoard();

            /*
             * A ausência de TurnContext é uma situação normal durante transições
             * entre fases. Ela produz rejeição, e não exceção.
             */
            PlaceMarkAction action = new PlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                expectedTurnId: DefaultTurnId,
                expectedBoardVersion: board.Version,
                target: new BoardCoordinate(0, 0),
                mark: CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                board,
                turnContext: null,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.TurnIsNotOpen);

            Assert.That(
                board.Version,
                Is.Zero);

            Assert.That(
                board.GetCell(0, 0).Marks,
                Is.EqualTo(CellMark.None));
        }

        [Test]
        public void Execute_WithClosedTurn_RejectsWithoutConsumingAgain()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            turn.Cancel(board.Version);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(0, 0),
                CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.TurnIsNotOpen);

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Cancelled));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                board.Version,
                Is.Zero);
        }

        [Test]
        public void Execute_WithDifferentTurnId_RejectsOldIntention()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext currentTurn = CreateTurn(
                ScoreActor.Player,
                turnId: 2,
                board: board);

            /*
             * A intenção foi criada para o Turno 1, mas o encontro já está no
             * Turno 2. Mesmo pertencendo ao mesmo ator e à mesma versão, ela não
             * pode ser reaproveitada.
             */
            PlaceMarkAction oldAction = new PlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                expectedTurnId: 1,
                expectedBoardVersion: board.Version,
                target: new BoardCoordinate(0, 0),
                mark: CellMark.X);

            ActionResult result = _executor.Execute(
                oldAction,
                board,
                currentTurn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.TurnIdDoesNotMatch);

            AssertBoardAndTurnWereNotChanged(
                board,
                currentTurn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_WhenActorDoesNotOwnTurn_Rejects()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext playerTurn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction enemyAction = CreatePlaceMarkAction(
                ScoreActor.Enemy,
                GameActionOrigin.AgentPolicy,
                playerTurn,
                board,
                new BoardCoordinate(0, 0),
                CellMark.O);

            ActionResult result = _executor.Execute(
                enemyAction,
                board,
                playerTurn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.ActorDoesNotOwnTurn);

            AssertBoardAndTurnWereNotChanged(
                board,
                playerTurn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_WithOldExpectedBoardVersion_RejectsDelayedIntention()
        {
            BoardState board = CreateEmptyBoard();

            /*
             * Esta alteração representa outra ação autoritativa ocorrida depois
             * que a intenção antiga foi criada.
             */
            PlaceMarkAction delayedAction = new PlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                expectedTurnId: DefaultTurnId,
                expectedBoardVersion: board.Version,
                target: new BoardCoordinate(1, 0),
                mark: CellMark.X);

            ApplyNormalMove(
                board,
                new BoardCoordinate(0, 0),
                CellMark.O);

            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                DefaultTurnId,
                initialBoardVersion: board.Version);

            ActionResult result = _executor.Execute(
                delayedAction,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.BoardVersionDoesNotMatch);

            Assert.That(
                board.Version,
                Is.EqualTo(1));

            Assert.That(
                board.GetCell(1, 0).Marks,
                Is.EqualTo(CellMark.None));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);
        }

        [Test]
        public void Execute_WhenTurnAndBoardVersionsAreOutOfSync_Throws()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                DefaultTurnId,
                initialBoardVersion: board.Version);

            /*
             * O BoardState é alterado fora do executor. A ação conhece a nova
             * versão, mas o TurnContext continua na antiga. Esse estado não é uma
             * rejeição esperada do jogador: é uma inconsistência de programação.
             */
            ApplyNormalMove(
                board,
                new BoardCoordinate(0, 0),
                CellMark.O);

            PlaceMarkAction action = new PlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                expectedTurnId: turn.TurnId,
                expectedBoardVersion: board.Version,
                target: new BoardCoordinate(1, 0),
                mark: CellMark.X);

            Assert.Throws<InvalidOperationException>(
                () => _executor.Execute(
                    action,
                    board,
                    turn,
                    encounterAcceptsActions: true));

            Assert.That(
                board.Version,
                Is.EqualTo(1));

            Assert.That(
                board.GetCell(1, 0).Marks,
                Is.EqualTo(CellMark.None));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);
        }

        [Test]
        public void Execute_WithUnsupportedConcreteAction_Rejects()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            GameAction unsupportedAction = new UnsupportedTestAction(
                ScoreActor.Player,
                GameActionOrigin.Effect,
                turn.TurnId,
                board.Version,
                new BoardCoordinate(0, 0));

            ActionResult result = _executor.Execute(
                unsupportedAction,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.ActionTypeNotSupported);

            AssertBoardAndTurnWereNotChanged(
                board,
                turn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_WhenMarkDoesNotBelongToActor_Rejects()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(0, 0),
                CellMark.O);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.MarkDoesNotBelongToActor);

            AssertBoardAndTurnWereNotChanged(
                board,
                turn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_WhenCellDoesNotExist_ConvertsMoveServiceRejection()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(99, 99),
                CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.CellDoesNotExist);

            AssertBoardAndTurnWereNotChanged(
                board,
                turn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_OnOccupiedNormalCell_ConvertsMoveServiceRejection()
        {
            BoardCoordinate coordinate =
                new BoardCoordinate(0, 0);

            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(1, 1),
                new[]
                {
                    new CellState(
                        coordinate,
                        CellMark.O)
                });

            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                coordinate,
                CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.OccupiedCellDoesNotAllowOverlap);

            Assert.That(
                board.GetCell(coordinate).Marks,
                Is.EqualTo(CellMark.O));

            AssertBoardAndTurnWereNotChanged(
                board,
                turn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void Execute_WhenSameMarkAlreadyExists_ConvertsMoveServiceRejection()
        {
            BoardCoordinate coordinate =
                new BoardCoordinate(0, 0);

            BoardState board = CreateGoldenBoard(
                coordinate,
                CellMark.X);

            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                coordinate,
                CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            AssertRejected(
                result,
                ActionRejectionReason.MarkAlreadyPresent);

            Assert.That(
                board.GetCell(coordinate).Marks,
                Is.EqualTo(CellMark.X));

            AssertBoardAndTurnWereNotChanged(
                board,
                turn,
                expectedBoardVersion: 0);
        }

        [Test]
        public void ExecutePlaceMark_WhenValid_MatchesMoveServiceAndCompletesTurn()
        {
            BoardCoordinate coordinate =
                new BoardCoordinate(1, 1);

            BoardState executorBoard = CreateEmptyBoard();
            BoardState directMoveBoard = CreateEmptyBoard();

            TurnContext turn = CreateTurn(
                ScoreActor.Player,
                DefaultTurnId,
                executorBoard);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                executorBoard,
                coordinate,
                CellMark.X);

            ActionResult result = _executor.Execute(
                action,
                executorBoard,
                turn,
                encounterAcceptsActions: true);

            MoveValidationResult directMoveResult =
                new MoveService().TryApplyNormalMove(
                    directMoveBoard,
                    coordinate,
                    CellMark.X);

            Assert.That(
                result.WasApplied,
                Is.True);

            Assert.That(
                result.WasRejected,
                Is.False);

            Assert.That(
                result.RejectionReason,
                Is.EqualTo(ActionRejectionReason.None));

            Assert.That(
                result.BoardChanges.Count,
                Is.EqualTo(1));

            Assert.That(
                result.BoardChanges.IsEmpty,
                Is.False);

            Assert.That(
                directMoveResult.IsValid,
                Is.True);

            Assert.That(
                executorBoard.GetCell(coordinate).Marks,
                Is.EqualTo(directMoveBoard.GetCell(coordinate).Marks));

            Assert.That(
                executorBoard.Version,
                Is.EqualTo(directMoveBoard.Version));

            Assert.That(
                result.BoardVersionAfter,
                Is.EqualTo(executorBoard.Version));

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(executorBoard.Version));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));

            Assert.That(
                turn.CanEvaluateReaction,
                Is.True);
        }

        [Test]
        public void ExecutePlaceMark_FromEnemyPolicy_UsesSameExecutorAndAppliesO()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                DefaultTurnId,
                board);

            BoardCoordinate coordinate =
                new BoardCoordinate(0, 1);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Enemy,
                GameActionOrigin.AgentPolicy,
                turn,
                board,
                coordinate,
                CellMark.O);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            Assert.That(
                result.WasApplied,
                Is.True);

            Assert.That(
                board.GetCell(coordinate).HasMark(CellMark.O),
                Is.True);

            Assert.That(
                result.BoardChanges.Count,
                Is.EqualTo(1));

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));
        }

        [Test]
        public void ExecutePlaceMark_WithBudgetTwo_ConsumesOneActionAtATime()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                DefaultTurnId,
                initialBoardVersion: board.Version,
                actionBudget: 2);

            PlaceMarkAction firstAction = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(0, 0),
                CellMark.X);

            ActionResult firstResult = _executor.Execute(
                firstAction,
                board,
                turn,
                encounterAcceptsActions: true);

            Assert.That(
                firstResult.WasApplied,
                Is.True);

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.RemainingActions,
                Is.EqualTo(1));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));

            /*
             * A segunda intenção precisa ser criada depois da primeira, usando a
             * nova versão conhecida pelo mesmo TurnContext.
             */
            PlaceMarkAction secondAction = CreatePlaceMarkAction(
                ScoreActor.Player,
                GameActionOrigin.PlayerInput,
                turn,
                board,
                new BoardCoordinate(1, 0),
                CellMark.X);

            ActionResult secondResult = _executor.Execute(
                secondAction,
                board,
                turn,
                encounterAcceptsActions: true);

            Assert.That(
                secondResult.WasApplied,
                Is.True);

            Assert.That(
                board.Version,
                Is.EqualTo(2));

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(2));

            Assert.That(
                turn.RemainingActions,
                Is.Zero);

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(board.Version));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));
        }

        [Test]
        public void ExecutePlaceMark_OnGoldenCell_AppliesAuthorizedOverlap()
        {
            BoardCoordinate coordinate =
                new BoardCoordinate(0, 0);

            BoardState board = CreateGoldenBoard(
                coordinate,
                CellMark.X);

            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                DefaultTurnId,
                board);

            PlaceMarkAction action = CreatePlaceMarkAction(
                ScoreActor.Enemy,
                GameActionOrigin.AgentPolicy,
                turn,
                board,
                coordinate,
                CellMark.O);

            ActionResult result = _executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            Assert.That(
                result.WasApplied,
                Is.True);

            Assert.That(
                board.GetCell(coordinate).ContainsBothMarks,
                Is.True);

            Assert.That(
                board.GetCell(coordinate).HasModifier(
                    CellModifierId.Golden),
                Is.True);

            Assert.That(
                result.BoardChanges.Count,
                Is.EqualTo(1));

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));
        }

        /// <summary>
        /// Cria uma ação usando exatamente a identidade e a versão conhecidas no
        /// instante da chamada.
        ///
        /// Este auxiliar evita que um teste válido construa acidentalmente uma
        /// intenção atrasada. Os testes de incompatibilidade criam suas ações
        /// diretamente para controlar os valores divergentes.
        /// </summary>
        private static PlaceMarkAction CreatePlaceMarkAction(
            ScoreActor actor,
            GameActionOrigin origin,
            TurnContext turn,
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark)
        {
            return new PlaceMarkAction(
                actor,
                origin,
                turn.TurnId,
                board.Version,
                coordinate,
                mark);
        }

        private static TurnContext CreateTurn(
            ScoreActor actor,
            long turnId,
            BoardState board,
            int actionBudget = 1)
        {
            return new TurnContext(
                actor,
                turnId,
                board.Version,
                actionBudget);
        }

        private static BoardState CreateEmptyBoard()
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(2, 2));
        }

        private static BoardState CreateGoldenBoard(
            BoardCoordinate coordinate,
            CellMark initialMark)
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(1, 1),
                new[]
                {
                    new CellState(
                        coordinate,
                        initialMark,
                        new[]
                        {
                            CellModifierId.Golden
                        })
                });
        }

        /// <summary>
        /// Prepara um estado posterior do tabuleiro usando a API pública de jogadas.
        ///
        /// BoardState mantém seus métodos de mutação como internal para impedir que
        /// outras assemblies alterem o tabuleiro sem passar por uma regra autorizada.
        /// Nestes dois cenários, precisamos apenas simular uma jogada anterior para
        /// avançar BoardState.Version. Por isso utilizamos MoveService e confirmamos
        /// explicitamente que a preparação foi aplicada.
        /// </summary>
        private static void ApplyNormalMove(
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark)
        {
            MoveValidationResult result =
                new MoveService().TryApplyNormalMove(
                    board,
                    coordinate,
                    mark);

            Assert.That(
                result.IsValid,
                Is.True,
                "A jogada usada para preparar o teste deveria ser válida.");
        }

        /// <summary>
        /// Verifica o contrato comum de toda rejeição esperada.
        ///
        /// Uma rejeição precisa possuir motivo e nunca pode relatar mudanças no
        /// tabuleiro. As condições específicas do BoardState e do TurnContext são
        /// verificadas separadamente em cada cenário.
        /// </summary>
        private static void AssertRejected(
            ActionResult result,
            ActionRejectionReason expectedReason)
        {
            Assert.That(
                result.WasApplied,
                Is.False);

            Assert.That(
                result.WasRejected,
                Is.True);

            Assert.That(
                result.RejectionReason,
                Is.EqualTo(expectedReason));

            Assert.That(
                result.BoardChanges.IsEmpty,
                Is.True);

            Assert.That(
                result.BoardChanges.Count,
                Is.Zero);
        }

        private static void AssertBoardAndTurnWereNotChanged(
            BoardState board,
            TurnContext turn,
            long expectedBoardVersion)
        {
            Assert.That(
                board.Version,
                Is.EqualTo(expectedBoardVersion));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(expectedBoardVersion));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));
        }

        /// <summary>
        /// Tipo sintético usado apenas para comprovar que o executor não tenta
        /// interpretar qualquer GameAction como PlaceMarkAction.
        ///
        /// O enum ainda possui somente PlaceMark nesta etapa. Por isso este tipo
        /// utiliza a categoria reconhecida, mas continua sendo uma classe concreta
        /// diferente. Quando novas categorias forem criadas, este teste poderá ser
        /// adaptado para usar uma delas.
        /// </summary>
        private sealed class UnsupportedTestAction :
            GameAction<BoardCoordinate>
        {
            public UnsupportedTestAction(
                ScoreActor actor,
                GameActionOrigin origin,
                long expectedTurnId,
                long expectedBoardVersion,
                BoardCoordinate target)
                : base(
                    actor,
                    GameActionType.PlaceMark,
                    origin,
                    expectedTurnId,
                    expectedBoardVersion,
                    target)
            {
            }
        }
    }
}