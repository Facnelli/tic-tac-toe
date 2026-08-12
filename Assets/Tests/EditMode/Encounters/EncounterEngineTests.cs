using NUnit.Framework;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;
using EncounterEngine = TicTacToeRoguelike.Application.Encounters.EncounterEngine;

namespace TicTacToeRoguelike.EditModeTests.Encounters
{
    /// <summary>
    /// Testes públicos das sessões 5.2 e 5.3.
    ///
    /// A suíte usa somente o contrato externo do EncounterEngine. Ela não chama
    /// os métodos internal de EncounterState e, portanto, comprova que o estado
    /// não precisa expor setters apenas para facilitar testes.
    /// </summary>
    public sealed class EncounterEngineTests
    {
        [Test]
        public void StartEncounter_OpensFirstTurnAndInitializesState()
        {
            EncounterEngine engine = CreateEngine();
            BoardState board = CreateEmptyBoard(3, 3);

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.WaitingForAction));

            Assert.That(
                engine.State.RoundNumber,
                Is.EqualTo(1));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Player));

            Assert.That(
                engine.State.CurrentTurn.TurnId,
                Is.EqualTo(1));

            Assert.That(
                engine.State.ReactionState.IsTied,
                Is.True);

            Assert.That(
                engine.State.LastReactionDecision,
                Is.Null);
        }

        [Test]
        public void ExecuteAction_WhenRejected_DoesNotConsumeOrAdvanceTurn()
        {
            EncounterEngine engine = CreateEngine();
            BoardState board = CreateEmptyBoard(3, 3);

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            TurnContext originalTurn =
                engine.State.CurrentTurn;

            PlaceMarkAction wrongActorAction =
                new PlaceMarkAction(
                    ScoreActor.Enemy,
                    GameActionOrigin.AgentPolicy,
                    originalTurn.TurnId,
                    board.Version,
                    new BoardCoordinate(0, 0),
                    CellMark.O);

            ActionResult result =
                engine.ExecuteAction(wrongActorAction);

            Assert.That(result.WasRejected, Is.True);

            Assert.That(
                result.RejectionReason,
                Is.EqualTo(
                    ActionRejectionReason.ActorDoesNotOwnTurn));

            Assert.That(
                engine.State.CurrentTurn,
                Is.SameAs(originalTurn));

            Assert.That(
                originalTurn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                engine.State.LastReactionDecision,
                Is.Null);
        }

        [Test]
        public void ExecuteAction_WithBudgetTwo_EvaluatesOnlyAfterSecondAction()
        {
            EncounterEngine engine = CreateEngine();
            BoardState board = CreateEmptyBoard(3, 3);

            engine.StartEncounter(
                board,
                EncounterSide.Player,
                actionBudget: 2);

            ActionResult firstResult = ExecuteCurrentMove(
                engine,
                new BoardCoordinate(0, 0));

            Assert.That(firstResult.WasApplied, Is.True);

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Player));

            Assert.That(
                engine.State.CurrentTurn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                engine.State.LastReactionDecision,
                Is.Null);

            ActionResult secondResult = ExecuteCurrentMove(
                engine,
                new BoardCoordinate(1, 0));

            Assert.That(secondResult.WasApplied, Is.True);

            Assert.That(
                engine.State.LastTurnCompletion.ActionsConsumed,
                Is.EqualTo(2));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ContinueNormal));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Enemy));
        }

        [Test]
        public void ExecuteAction_WhenFirstSequenceAppears_StartsReaction()
        {
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X));

            EncounterEngine engine = CreateEngine();

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(2, 0));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ReactionStarted));

            Assert.That(
                engine.State.ReactionState.Leader,
                Is.EqualTo(ScoreActor.Player));

            Assert.That(
                engine.State.ReactionState.PendingResponder,
                Is.EqualTo(ScoreActor.Enemy));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Enemy));
        }

        [Test]
        public void ExecuteAction_WhenResponderTies_ResolvesReactionAndContinues()
        {
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X),
                Cell(2, 0, CellMark.X),
                Cell(0, 1, CellMark.O),
                Cell(1, 1, CellMark.O));

            EncounterEngine engine = CreateEngine();

            engine.StartEncounter(
                board,
                EncounterSide.Enemy);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(2, 1));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ReactionResolvedByTie));

            Assert.That(
                engine.State.ReactionState.IsTied,
                Is.True);

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.WaitingForAction));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Player));
        }

        [Test]
        public void ExecuteAction_WhenResponderTakesLead_TransfersReaction()
        {
            /*
             * X começa com uma linha. O centro superior contém X e O; isso é
             * permitido pelo domínio e prepara duas linhas de O que serão
             * completadas pela mesma jogada no centro do tabuleiro.
             */
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X | CellMark.O),
                Cell(2, 0, CellMark.X),
                Cell(0, 1, CellMark.O),
                Cell(2, 1, CellMark.O),
                Cell(1, 2, CellMark.O));

            EncounterEngine engine = CreateEngine();

            engine.StartEncounter(
                board,
                EncounterSide.Enemy);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(1, 1));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ReactionTransferred));

            Assert.That(
                engine.State.ReactionState.Leader,
                Is.EqualTo(ScoreActor.Enemy));

            Assert.That(
                engine.State.ReactionState.PendingResponder,
                Is.EqualTo(ScoreActor.Player));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Player));
        }

        [Test]
        public void ExecuteAction_WhenResponderRemainsBehind_ResolvesDamageOnce()
        {
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X),
                Cell(2, 0, CellMark.X));

            EncounterEngine engine = CreateEngine();

            engine.StartEncounter(
                board,
                EncounterSide.Enemy);

            // Uma sequência inicial abre a reação, mas não encerra a Rodada antes
            // que o Enemy receba e conclua sua oportunidade.
            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.WaitingForAction));

            Assert.That(
                engine.State.LastRoundResolution,
                Is.Null);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(0, 2));

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.RoundResolved));

            Assert.That(
                engine.State.LastRoundResult.Outcome,
                Is.EqualTo(RoundOutcome.PlayerVictory));

            Assert.That(
                engine.State.LastRoundResult.EndReason,
                Is.EqualTo(
                    ReactionEndReason.ResponderRemainedBehind));

            Assert.That(
                engine.State.LastRoundResolution.PlayerScore.IsWinner,
                Is.True);

            Assert.That(
                engine.State.LastRoundResolution.EnemyScore.IsWinner,
                Is.False);

            int healthAfterResolution =
                engine.State.EnemyState.CurrentHealth;

            Assert.That(
                healthAfterResolution,
                Is.LessThan(
                    engine.State.EnemyState.MaximumHealth));

            engine.AcknowledgeRoundResolution();

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(
                    EncounterPhase.WaitingForNextRound));

            Assert.That(
                engine.State.EnemyState.CurrentHealth,
                Is.EqualTo(healthAfterResolution));
        }

        [Test]
        public void ExecuteAction_WhenDamageDefeatsCombatant_EndsAfterAcknowledge()
        {
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X),
                Cell(2, 0, CellMark.X));

            EncounterEngine engine = CreateEngine(
                playerHealth: 100,
                enemyHealth: 1);

            engine.StartEncounter(
                board,
                EncounterSide.Enemy);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(0, 2));

            Assert.That(
                engine.State.EnemyState.IsDefeated,
                Is.True);

            Assert.That(
                engine.State.LastEncounterResult.Outcome,
                Is.EqualTo(
                    EncounterOutcome.PlayerVictory));

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.RoundResolved));

            engine.AcknowledgeRoundResolution();

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.EncounterEnded));
        }

        [Test]
        public void ExecuteAction_WhenBoardFillsWithoutSequence_EndsAsDraw()
        {
            EncounterEngine engine = CreateEngine();
            BoardState board = CreateEmptyBoard(1, 1);

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(0, 0));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.DrawNoActions));

            Assert.That(
                engine.State.LastRoundResult.IsDraw,
                Is.True);

            Assert.That(
                engine.State.LastRoundResolution.PlayerScore.IsWinner,
                Is.False);

            Assert.That(
                engine.State.LastRoundResolution.EnemyScore.IsWinner,
                Is.False);
        }

        [Test]
        public void ExecuteAction_WhenSpecialActionExists_FullBoardContinues()
        {
            ActionAvailabilityService availability =
                new ActionAvailabilityService(
                    new IActionAvailabilityProvider[]
                    {
                        new AlwaysAvailableProvider()
                    });

            EncounterEngine engine = CreateEngine(
                availability,
                new AlternatingEncounterTurnScheduler());

            BoardState board = CreateEmptyBoard(1, 1);

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(0, 0));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ContinueNormal));

            Assert.That(
                engine.State.Phase,
                Is.EqualTo(EncounterPhase.WaitingForAction));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Enemy));
        }

        [Test]
        public void SkipCurrentTurn_DoesNotEvaluateReaction()
        {
            EncounterEngine engine = CreateEngine();
            BoardState board = CreateEmptyBoard(3, 3);

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            TurnCompletion completion =
                engine.SkipCurrentTurn();

            Assert.That(
                completion.CanEvaluateReaction,
                Is.False);

            Assert.That(
                engine.State.LastReactionDecision,
                Is.Null);

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Enemy));

            Assert.That(
                engine.State.CurrentTurn.TurnId,
                Is.EqualTo(2));
        }

        [Test]
        public void ConsecutiveLeaderTurns_MaintainReactionUntilResponderActs()
        {
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X));

            EncounterEngine engine = CreateEngine(
                new ActionAvailabilityService(),
                new SameActorTurnScheduler());

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(2, 0));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ReactionStarted));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Player));

            ExecuteCurrentMove(
                engine,
                new BoardCoordinate(0, 1));

            Assert.That(
                engine.State.LastReactionDecision.Kind,
                Is.EqualTo(
                    ReactionDecisionKind.ReactionMaintained));

            Assert.That(
                engine.State.CurrentActor,
                Is.EqualTo(ScoreActor.Player));
        }

        private static EncounterEngine CreateEngine(
            int playerHealth = 100,
            int enemyHealth = 100)
        {
            return new EncounterEngine(
                new CombatantState(
                    "player",
                    ScoreActor.Player,
                    playerHealth),
                new CombatantState(
                    "enemy",
                    ScoreActor.Enemy,
                    enemyHealth),
                CreateRules());
        }

        private static EncounterEngine CreateEngine(
            ActionAvailabilityService availability,
            IEncounterTurnScheduler turnScheduler)
        {
            return new EncounterEngine(
                new CombatantState(
                    "player",
                    ScoreActor.Player,
                    100),
                new CombatantState(
                    "enemy",
                    ScoreActor.Enemy,
                    100),
                CreateRules(),
                new ActionExecutor(),
                availability,
                new ReactionStateFactory(),
                new ReactionRule(),
                new ScorePipeline(),
                new ClashResolver(),
                new DamageResolver(),
                turnScheduler);
        }

        private static EncounterRules CreateRules()
        {
            return new EncounterRules(
                requiredSequenceLength: 3,
                minimumScoringSequenceLength: 2,
                maximumScoringSequenceLength: 3,
                victoryMultiplier: 2m);
        }

        private static ActionResult ExecuteCurrentMove(
            EncounterEngine engine,
            BoardCoordinate coordinate)
        {
            TurnContext turn = engine.State.CurrentTurn;
            ScoreActor actor = turn.Actor;

            PlaceMarkAction action = new PlaceMarkAction(
                actor,
                actor == ScoreActor.Player
                    ? GameActionOrigin.PlayerInput
                    : GameActionOrigin.AgentPolicy,
                turn.TurnId,
                engine.State.Board.Version,
                coordinate,
                actor == ScoreActor.Player
                    ? CellMark.X
                    : CellMark.O);

            return engine.ExecuteAction(action);
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

        private static BoardState CreateBoard(
            int width,
            int height,
            params CellState[] initialCells)
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(
                    width,
                    height),
                initialCells);
        }

        private static CellState Cell(
            int x,
            int y,
            CellMark marks)
        {
            return new CellState(
                new BoardCoordinate(x, y),
                marks);
        }

        private sealed class AlwaysAvailableProvider :
            IActionAvailabilityProvider
        {
            public bool HasAvailableAction(
                ActionAvailabilityContext context)
            {
                return true;
            }
        }

        private sealed class SameActorTurnScheduler :
            IEncounterTurnScheduler
        {
            public EncounterTurnPlan CreateNextTurn(
                EncounterState state,
                TurnCompletion completedTurn,
                ReactionState currentReactionState)
            {
                return new EncounterTurnPlan(
                    completedTurn.Actor,
                    actionBudget: 1);
            }
        }
    }
}