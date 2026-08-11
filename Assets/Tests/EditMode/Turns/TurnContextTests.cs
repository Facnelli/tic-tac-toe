using System;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.EditModeTests.Turns
{
    /// <summary>
    /// Comprova o ciclo de vida completo de um Turno.
    ///
    /// Estes testes distinguem explicitamente:
    ///
    /// - duas ações dentro do mesmo Turno;
    /// - dois Turnos consecutivos;
    /// - uma tentativa rejeitada;
    /// - um Turno realmente concluído;
    /// - um Turno pulado;
    /// - um Turno cancelado.
    ///
    /// Nenhum teste depende de GameObject, MonoBehaviour, frame ou coroutine.
    /// </summary>
    public sealed class TurnContextTests
    {
        [Test]
        public void Constructor_WithDefaultBudget_StartsOpen()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 1,
                initialBoardVersion: 7);

            Assert.That(
                turn.TurnId,
                Is.EqualTo(1));

            Assert.That(
                turn.Actor,
                Is.EqualTo(ScoreActor.Player));

            Assert.That(
                turn.ActionBudget,
                Is.EqualTo(1));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                turn.RemainingActions,
                Is.EqualTo(1));

            Assert.That(
                turn.InitialBoardVersion,
                Is.EqualTo(7));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(7));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));

            Assert.That(
                turn.IsOpen,
                Is.True);

            Assert.That(
                turn.IsClosed,
                Is.False);

            Assert.That(
                turn.HasPlayedAction,
                Is.False);

            Assert.That(
                turn.HasRemainingActions,
                Is.True);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.False);
        }

        [Test]
        public void RegisterAppliedAction_WithBudgetOne_CompletesTurn()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 1,
                initialBoardVersion: 10);

            bool completed =
                turn.RegisterAppliedAction(
                    boardVersionAfterAction: 11);

            Assert.That(
                completed,
                Is.True);

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.RemainingActions,
                Is.Zero);

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(11));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));

            Assert.That(
                turn.IsOpen,
                Is.False);

            Assert.That(
                turn.IsClosed,
                Is.True);

            Assert.That(
                turn.HasPlayedAction,
                Is.True);

            Assert.That(
                turn.HasRemainingActions,
                Is.False);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.True);
        }

        [Test]
        public void RegisterAppliedAction_WithBudgetTwo_OnlySecondActionCompletesTurn()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Enemy,
                turnId: 2,
                initialBoardVersion: 20,
                actionBudget: 2);

            bool completedAfterFirstAction =
                turn.RegisterAppliedAction(
                    boardVersionAfterAction: 21);

            Assert.That(
                completedAfterFirstAction,
                Is.False);

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.RemainingActions,
                Is.EqualTo(1));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(21));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));

            Assert.That(
                turn.IsOpen,
                Is.True);

            Assert.That(
                turn.HasPlayedAction,
                Is.True);

            Assert.That(
                turn.HasRemainingActions,
                Is.True);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.False);

            bool completedAfterSecondAction =
                turn.RegisterAppliedAction(
                    boardVersionAfterAction: 22);

            Assert.That(
                completedAfterSecondAction,
                Is.True);

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(2));

            Assert.That(
                turn.RemainingActions,
                Is.Zero);

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(22));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));

            Assert.That(
                turn.IsClosed,
                Is.True);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.True);
        }

        [Test]
        public void RejectedAction_DoesNotConsumeBudget()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 3,
                initialBoardVersion: 30);

            /*
             * TurnContext não recebe tentativas rejeitadas.
             *
             * O futuro ActionExecutor somente chamará
             * RegisterAppliedAction quando ActionResult informar que a ação
             * realmente foi aplicada.
             *
             * Por enquanto, este auxiliar representa essa fronteira.
             */
            ReportActionAttempt(
                turn,
                wasApplied: false,
                boardVersionAfterAttempt: 30);

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                turn.RemainingActions,
                Is.EqualTo(1));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(30));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));

            Assert.That(
                turn.HasPlayedAction,
                Is.False);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.False);

            /*
             * Depois da tentativa rejeitada, uma ação válida ainda deve poder
             * consumir o orçamento normalmente.
             */
            ReportActionAttempt(
                turn,
                wasApplied: true,
                boardVersionAfterAttempt: 31);

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));

            Assert.That(
                turn.CanEvaluateReaction,
                Is.True);
        }

        [Test]
        public void RegisterAppliedAction_WithUnchangedBoardVersion_IsAllowed()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 4,
                initialBoardVersion: 40);

            /*
             * Uma ação futura poderá modificar pontuação, runas ou outro estado
             * sem alterar o BoardState. Por isso, a versão pode permanecer igual.
             */
            bool completed =
                turn.RegisterAppliedAction(
                    boardVersionAfterAction: 40);

            Assert.That(
                completed,
                Is.True);

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(40));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));
        }

        [Test]
        public void RegisterAppliedAction_WithOlderBoardVersion_ThrowsWithoutChangingTurn()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 5,
                initialBoardVersion: 50,
                actionBudget: 2);

            turn.RegisterAppliedAction(
                boardVersionAfterAction: 52);

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => turn.RegisterAppliedAction(
                        boardVersionAfterAction: 51));

            Assert.That(
                exception.ParamName,
                Is.EqualTo("boardVersionAfterAction"));

            /*
             * A tentativa inválida precisa falhar antes de consumir orçamento.
             */
            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.RemainingActions,
                Is.EqualTo(1));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(52));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));
        }

        [Test]
        public void RegisterAppliedAction_AfterCompletion_Throws()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 6,
                initialBoardVersion: 60);

            turn.RegisterAppliedAction(
                boardVersionAfterAction: 61);

            Assert.Throws<InvalidOperationException>(
                () => turn.RegisterAppliedAction(
                    boardVersionAfterAction: 62));

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(61));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Completed));
        }

        [Test]
        public void Skip_BeforeAnyAction_CreatesNonReactiveCompletion()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Enemy,
                turnId: 7,
                initialBoardVersion: 70);

            turn.Skip(finalBoardVersion: 70);

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Skipped));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                turn.IsClosed,
                Is.True);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.False);

            TurnCompletion completion =
                TurnCompletion.CreateFrom(turn);

            Assert.That(
                completion.EndReason,
                Is.EqualTo(TurnEndReason.Skipped));

            Assert.That(
                completion.WasSkipped,
                Is.True);

            Assert.That(
                completion.WasCancelled,
                Is.False);

            Assert.That(
                completion.HasPlayedAction,
                Is.False);

            Assert.That(
                completion.CanEvaluateReaction,
                Is.False);
        }

        [Test]
        public void Cancel_BeforeAnyAction_CreatesNonReactiveCompletion()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 8,
                initialBoardVersion: 80);

            turn.Cancel(finalBoardVersion: 81);

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Cancelled));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(81));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);

            Assert.That(
                turn.CanEvaluateReaction,
                Is.False);

            TurnCompletion completion =
                TurnCompletion.CreateFrom(turn);

            Assert.That(
                completion.EndReason,
                Is.EqualTo(TurnEndReason.Cancelled));

            Assert.That(
                completion.WasCancelled,
                Is.True);

            Assert.That(
                completion.WasSkipped,
                Is.False);

            Assert.That(
                completion.HasPlayedAction,
                Is.False);

            Assert.That(
                completion.CanEvaluateReaction,
                Is.False);
        }

        [TestCase(TurnStatus.Skipped)]
        [TestCase(TurnStatus.Cancelled)]
        public void SkipOrCancel_AfterAppliedAction_Throws(
            TurnStatus attemptedStatus)
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 9,
                initialBoardVersion: 90,
                actionBudget: 2);

            turn.RegisterAppliedAction(
                boardVersionAfterAction: 91);

            if (attemptedStatus == TurnStatus.Skipped)
            {
                Assert.Throws<InvalidOperationException>(
                    () => turn.Skip(
                        finalBoardVersion: 91));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(
                    () => turn.Cancel(
                        finalBoardVersion: 91));
            }

            /*
             * A tentativa de encerrar incorretamente não pode esconder a ação
             * que já ocorreu. O Turno continua aberto aguardando a segunda ação.
             */
            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));

            Assert.That(
                turn.ActionsConsumed,
                Is.EqualTo(1));

            Assert.That(
                turn.RemainingActions,
                Is.EqualTo(1));

            Assert.That(
                turn.CurrentBoardVersion,
                Is.EqualTo(91));
        }

        [Test]
        public void CreateFrom_CompletedTurn_CopiesFinalSnapshot()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Enemy,
                turnId: 10,
                initialBoardVersion: 100,
                actionBudget: 2);

            turn.RegisterAppliedAction(
                boardVersionAfterAction: 101);

            turn.RegisterAppliedAction(
                boardVersionAfterAction: 103);

            TurnCompletion completion =
                TurnCompletion.CreateFrom(turn);

            Assert.That(
                completion.TurnId,
                Is.EqualTo(10));

            Assert.That(
                completion.Actor,
                Is.EqualTo(ScoreActor.Enemy));

            Assert.That(
                completion.ActionBudget,
                Is.EqualTo(2));

            Assert.That(
                completion.ActionsConsumed,
                Is.EqualTo(2));

            Assert.That(
                completion.InitialBoardVersion,
                Is.EqualTo(100));

            Assert.That(
                completion.FinalBoardVersion,
                Is.EqualTo(103));

            Assert.That(
                completion.EndReason,
                Is.EqualTo(TurnEndReason.ActionBudgetConsumed));

            Assert.That(
                completion.HasPlayedAction,
                Is.True);

            Assert.That(
                completion.EndedByActionBudget,
                Is.True);

            Assert.That(
                completion.WasSkipped,
                Is.False);

            Assert.That(
                completion.WasCancelled,
                Is.False);

            Assert.That(
                completion.CanEvaluateReaction,
                Is.True);
        }

        [Test]
        public void CreateFrom_OpenTurn_Throws()
        {
            TurnContext turn = new TurnContext(
                ScoreActor.Player,
                turnId: 11,
                initialBoardVersion: 110);

            Assert.Throws<InvalidOperationException>(
                () => TurnCompletion.CreateFrom(turn));

            Assert.That(
                turn.Status,
                Is.EqualTo(TurnStatus.Open));

            Assert.That(
                turn.ActionsConsumed,
                Is.Zero);
        }

        [Test]
        public void CreateFrom_NullTurn_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => TurnCompletion.CreateFrom(null));
        }

        [Test]
        public void ConsecutiveTurns_HaveDistinctIdentities()
        {
            /*
             * Dois Turnos consecutivos podem pertencer ao mesmo participante,
             * por exemplo devido a uma runa de Turno extra.
             *
             * Eles continuam sendo oportunidades diferentes porque possuem
             * identificadores diferentes.
             */
            TurnContext firstTurn = new TurnContext(
                ScoreActor.Player,
                turnId: 12,
                initialBoardVersion: 120);

            TurnContext secondTurn = new TurnContext(
                ScoreActor.Player,
                turnId: 13,
                initialBoardVersion: 120);

            Assert.That(
                firstTurn.Actor,
                Is.EqualTo(secondTurn.Actor));

            Assert.That(
                firstTurn.TurnId,
                Is.Not.EqualTo(secondTurn.TurnId));

            Assert.That(
                firstTurn.TurnId,
                Is.EqualTo(12));

            Assert.That(
                secondTurn.TurnId,
                Is.EqualTo(13));
        }

        [TestCase(ScoreActor.None)]
        [TestCase(ScoreActor.Environment)]
        public void Constructor_WithActorThatCannotPlay_Throws(
            ScoreActor actor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TurnContext(
                    actor,
                    turnId: 1,
                    initialBoardVersion: 0));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void Constructor_WithNonPositiveTurnId_Throws(
            long turnId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TurnContext(
                    ScoreActor.Player,
                    turnId,
                    initialBoardVersion: 0));
        }

        [Test]
        public void Constructor_WithNegativeBoardVersion_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TurnContext(
                    ScoreActor.Player,
                    turnId: 1,
                    initialBoardVersion: -1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WithNonPositiveActionBudget_Throws(
            int actionBudget)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TurnContext(
                    ScoreActor.Player,
                    turnId: 1,
                    initialBoardVersion: 0,
                    actionBudget));
        }

        /// <summary>
        /// Simula a fronteira que será implementada pelo ActionExecutor no
        /// Marco 4.
        ///
        /// Tentativas rejeitadas não são registradas no TurnContext. Somente
        /// resultados efetivamente aplicados consomem o orçamento do Turno.
        /// </summary>
        private static void ReportActionAttempt(
            TurnContext turn,
            bool wasApplied,
            long boardVersionAfterAttempt)
        {
            if (!wasApplied)
            {
                return;
            }

            turn.RegisterAppliedAction(
                boardVersionAfterAttempt);
        }
    }
}