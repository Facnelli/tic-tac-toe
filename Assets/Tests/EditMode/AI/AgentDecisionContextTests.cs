using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Application.AI;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.EditModeTests.AI
{
    /// <summary>
    /// Testes do contrato criado na sessão 6.1.
    ///
    /// Esta suíte ainda não testa a estratégia da BasicTicTacToePolicy. Ela
    /// comprova somente que qualquer política futura recebe uma fotografia segura
    /// e devolve GameAction pelo contrato comum.
    /// </summary>
    public sealed class AgentDecisionContextTests
    {
        [Test]
        public void Constructor_CreatesIndependentBoardAndTurnSnapshots()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board,
                actionBudget: 2);

            ReactionState reaction = CreateTiedReaction(board);
            GameAction action = CreateAction(
                turn,
                board,
                new BoardCoordinate(0, 0));

            AgentDecisionContext context =
                new AgentDecisionContext(
                    board,
                    turn,
                    reaction,
                    requiredSequenceLength: 3,
                    legalActions: new[] { action },
                    encounterGeneration: 7);

            Assert.That(
                context.BoardSnapshot,
                Is.Not.SameAs(board));

            Assert.That(
                context.TurnSnapshot,
                Is.Not.SameAs(turn));

            Assert.That(
                context.BoardVersion,
                Is.EqualTo(board.Version));

            Assert.That(
                context.TurnId,
                Is.EqualTo(turn.TurnId));

            turn.RegisterAppliedAction(board.Version);

            Assert.That(
                context.TurnSnapshot.ActionsConsumed,
                Is.Zero);

            Assert.That(
                context.TurnSnapshot.RemainingActions,
                Is.EqualTo(2));
        }

        [Test]
        public void Constructor_CopiesLegalActionCollection()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board);

            List<GameAction> originalActions =
                new List<GameAction>
                {
                    CreateAction(
                        turn,
                        board,
                        new BoardCoordinate(0, 0))
                };

            AgentDecisionContext context =
                CreateContext(
                    board,
                    turn,
                    originalActions);

            originalActions.Clear();

            Assert.That(
                context.LegalActions.Count,
                Is.EqualTo(1));

            Assert.That(
                context.HasLegalActions,
                Is.True);

            IList<GameAction> listView =
                (IList<GameAction>)context.LegalActions;

            Assert.Throws<NotSupportedException>(
                () => listView.Add(
                    CreateAction(
                        turn,
                        board,
                        new BoardCoordinate(1, 0))));
        }

        [Test]
        public void Constructor_DerivesActorMarksAndReactionFacts()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board);

            ReactionState reaction = new ReactionState(
                playerSequenceCount: 2,
                enemySequenceCount: 1,
                boardVersion: board.Version);

            AgentDecisionContext context =
                new AgentDecisionContext(
                    board,
                    turn,
                    reaction,
                    requiredSequenceLength: 3,
                    legalActions: new GameAction[0],
                    encounterGeneration: 4);

            Assert.That(
                context.Actor,
                Is.EqualTo(ScoreActor.Enemy));

            Assert.That(
                context.ControlledMark,
                Is.EqualTo(CellMark.O));

            Assert.That(
                context.OpponentMark,
                Is.EqualTo(CellMark.X));

            Assert.That(
                context.IsReactionActive,
                Is.True);

            Assert.That(
                context.ActorMustReact,
                Is.True);

            Assert.That(
                context.ActorLeadsReaction,
                Is.False);
        }

        [Test]
        public void Constructor_WithPartiallyConsumedTurn_PreservesSnapshot()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board,
                actionBudget: 3);

            turn.RegisterAppliedAction(board.Version);

            AgentDecisionContext context =
                CreateContext(
                    board,
                    turn,
                    new GameAction[0]);

            Assert.That(
                context.TurnSnapshot.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                context.RemainingActions,
                Is.EqualTo(2));

            Assert.That(
                context.TurnSnapshot.IsOpen,
                Is.True);
        }

        [Test]
        public void Constructor_WhenLegalActionBelongsToOtherTurn_Throws()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext currentTurn = CreateTurn(
                ScoreActor.Enemy,
                board);

            PlaceMarkAction oldAction =
                new PlaceMarkAction(
                    ScoreActor.Enemy,
                    GameActionOrigin.AgentPolicy,
                    expectedTurnId: currentTurn.TurnId + 1,
                    expectedBoardVersion: board.Version,
                    target: new BoardCoordinate(0, 0),
                    mark: CellMark.O);

            Assert.Throws<ArgumentException>(
                () => CreateContext(
                    board,
                    currentTurn,
                    new GameAction[] { oldAction }));
        }

        [Test]
        public void Constructor_WhenLegalActionHasOldBoardVersion_Throws()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board);

            PlaceMarkAction oldAction =
                new PlaceMarkAction(
                    ScoreActor.Enemy,
                    GameActionOrigin.AgentPolicy,
                    turn.TurnId,
                    expectedBoardVersion: board.Version + 1,
                    target: new BoardCoordinate(0, 0),
                    mark: CellMark.O);

            Assert.Throws<ArgumentException>(
                () => CreateContext(
                    board,
                    turn,
                    new GameAction[] { oldAction }));
        }

        [Test]
        public void CreateForNormalActions_MaterializesAgentPlaceMarkActions()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board);

            ActionAvailabilityReport availability =
                new ActionAvailabilityService().Evaluate(
                    board,
                    ScoreActor.Enemy);

            AgentDecisionContext context =
                AgentDecisionContext.CreateForNormalActions(
                    board,
                    turn,
                    CreateTiedReaction(board),
                    requiredSequenceLength: 3,
                    availability: availability,
                    encounterGeneration: 12);

            Assert.That(
                context.LegalActions.Count,
                Is.EqualTo(9));

            PlaceMarkAction firstAction =
                context.LegalActions[0] as PlaceMarkAction;

            Assert.That(firstAction, Is.Not.Null);

            Assert.That(
                firstAction.Actor,
                Is.EqualTo(ScoreActor.Enemy));

            Assert.That(
                firstAction.Origin,
                Is.EqualTo(GameActionOrigin.AgentPolicy));

            Assert.That(
                firstAction.Mark,
                Is.EqualTo(CellMark.O));

            Assert.That(
                firstAction.ExpectedTurnId,
                Is.EqualTo(turn.TurnId));
        }

        [Test]
        public void ContainsLegalAction_UsesReceivedInstance()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board);

            GameAction listedAction = CreateAction(
                turn,
                board,
                new BoardCoordinate(0, 0));

            AgentDecisionContext context =
                CreateContext(
                    board,
                    turn,
                    new[] { listedAction });

            GameAction equivalentButNew = CreateAction(
                turn,
                board,
                new BoardCoordinate(0, 0));

            Assert.That(
                context.ContainsLegalAction(listedAction),
                Is.True);

            Assert.That(
                context.ContainsLegalAction(equivalentButNew),
                Is.False);

            Assert.That(
                context.ContainsLegalAction(null),
                Is.False);
        }

        [Test]
        public void MatchesCurrentState_DetectsGenerationAndBoardChanges()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board,
                actionBudget: 2);

            AgentDecisionContext context =
                new AgentDecisionContext(
                    board,
                    turn,
                    CreateTiedReaction(board),
                    requiredSequenceLength: 3,
                    legalActions: new GameAction[0],
                    encounterGeneration: 5);

            Assert.That(
                context.MatchesCurrentState(
                    board,
                    turn,
                    currentEncounterGeneration: 5),
                Is.True);

            Assert.That(
                context.MatchesCurrentState(
                    board,
                    turn,
                    currentEncounterGeneration: 6),
                Is.False);

            MoveValidationResult moveResult =
                new MoveService().TryApplyNormalMove(
                    board,
                    new BoardCoordinate(0, 0),
                    CellMark.O);

            Assert.That(moveResult.IsValid, Is.True);

            turn.RegisterAppliedAction(board.Version);

            Assert.That(
                context.MatchesCurrentState(
                    board,
                    turn,
                    currentEncounterGeneration: 5),
                Is.False);

            Assert.That(
                context.BoardSnapshot.GetCell(0, 0).IsEmpty,
                Is.True);
        }

        [Test]
        public void IAgentPolicy_ReturnsActionFromCommonContract()
        {
            BoardState board = CreateEmptyBoard();
            TurnContext turn = CreateTurn(
                ScoreActor.Enemy,
                board);

            GameAction legalAction = CreateAction(
                turn,
                board,
                new BoardCoordinate(0, 0));

            AgentDecisionContext context =
                CreateContext(
                    board,
                    turn,
                    new[] { legalAction });

            IAgentPolicy policy =
                new FirstLegalActionPolicy();

            bool chose = policy.TryChooseAction(
                context,
                out GameAction chosenAction);

            Assert.That(chose, Is.True);

            Assert.That(
                chosenAction,
                Is.SameAs(legalAction));

            Assert.That(
                context.ContainsLegalAction(chosenAction),
                Is.True);
        }

        private static AgentDecisionContext CreateContext(
            BoardState board,
            TurnContext turn,
            IEnumerable<GameAction> legalActions)
        {
            return new AgentDecisionContext(
                board,
                turn,
                new ReactionState(
                    playerSequenceCount: 0,
                    enemySequenceCount: 0,
                    boardVersion: turn.InitialBoardVersion),
                requiredSequenceLength: 3,
                legalActions: legalActions,
                encounterGeneration: 1);
        }

        private static BoardState CreateEmptyBoard()
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(3, 3));
        }

        private static TurnContext CreateTurn(
            ScoreActor actor,
            BoardState board,
            int actionBudget = 1)
        {
            return new TurnContext(
                actor,
                turnId: 21,
                initialBoardVersion: board.Version,
                actionBudget: actionBudget);
        }

        private static ReactionState CreateTiedReaction(
            BoardState board)
        {
            return new ReactionState(
                playerSequenceCount: 0,
                enemySequenceCount: 0,
                boardVersion: board.Version);
        }

        private static PlaceMarkAction CreateAction(
            TurnContext turn,
            BoardState board,
            BoardCoordinate coordinate)
        {
            CellMark mark = turn.Actor == ScoreActor.Player
                ? CellMark.X
                : CellMark.O;

            return new PlaceMarkAction(
                turn.Actor,
                GameActionOrigin.AgentPolicy,
                turn.TurnId,
                board.Version,
                coordinate,
                mark);
        }

        private sealed class FirstLegalActionPolicy :
            IAgentPolicy
        {
            public bool TryChooseAction(
                AgentDecisionContext context,
                out GameAction action)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                if (!context.HasLegalActions)
                {
                    action = null;
                    return false;
                }

                action = context.LegalActions[0];
                return true;
            }
        }
    }
}