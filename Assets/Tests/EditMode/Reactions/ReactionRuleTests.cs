using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Reactions
{
    /// <summary>
    /// Comprova a matriz completa da regra de reação.
    ///
    /// Cada cenário informa:
    /// - o placar anterior;
    /// - o placar depois do Turno;
    /// - quem concluiu o Turno;
    /// - se ainda existem ações;
    /// - qual decisão deve ser produzida.
    ///
    /// Esses testes não usam Unity, GameObject, frame ou coroutine.
    /// </summary>
    public sealed class ReactionRuleTests
    {
        private readonly ReactionRule _rule = new ReactionRule();

        [TestCaseSource(nameof(GetTransitionCases))]
        public void Evaluate_ReturnsExpectedDecision(
            ReactionScenario scenario)
        {
            ReactionState previousState = new ReactionState(
                scenario.PreviousPlayerCount,
                scenario.PreviousEnemyCount,
                boardVersion: 10);

            ReactionState currentState = new ReactionState(
                scenario.CurrentPlayerCount,
                scenario.CurrentEnemyCount,
                boardVersion: 11);

            ReactionDecision decision = _rule.Evaluate(
                previousState,
                currentState,
                scenario.CompletedTurnActor,
                scenario.HasAvailableActions);

            /*
             * Não usamos Assert.Multiple porque a versão de NUnit atualmente
             * utilizada pelo projeto não oferece esse método.
             */
            Assert.That(
                decision.Kind,
                Is.EqualTo(scenario.ExpectedKind));

            Assert.That(
                decision.EndReason,
                Is.EqualTo(scenario.ExpectedEndReason));

            Assert.That(
                decision.Winner,
                Is.EqualTo(scenario.ExpectedWinner));

            Assert.That(
                decision.PendingResponder,
                Is.EqualTo(scenario.ExpectedPendingResponder));

            Assert.That(
                decision.ShouldContinue,
                Is.EqualTo(scenario.ShouldContinue));

            Assert.That(
                decision.EndsBoardMatch,
                Is.EqualTo(!scenario.ShouldContinue));

            Assert.That(
                decision.HasAvailableActions,
                Is.EqualTo(scenario.HasAvailableActions));

            Assert.That(
                decision.HasActiveReaction,
                Is.EqualTo(
                    scenario.ShouldContinue &&
                    scenario.ExpectedPendingResponder != ScoreActor.None));

            Assert.That(
                decision.IsDraw,
                Is.EqualTo(
                    scenario.ExpectedKind ==
                    ReactionDecisionKind.DrawNoActions));

            Assert.That(
                decision.VictoryMultiplierRecipient,
                Is.EqualTo(scenario.ExpectedWinner));

            Assert.That(
                decision.GrantsVictoryMultiplier,
                Is.EqualTo(
                    scenario.ExpectedWinner != ScoreActor.None));

            Assert.That(
                decision.PreviousState,
                Is.SameAs(previousState));

            Assert.That(
                decision.CurrentState,
                Is.SameAs(currentState));
        }

        [Test]
        public void Evaluate_AcceptsSameBoardVersion()
        {
            /*
             * Nem todo Turno precisa modificar o tabuleiro.
             * Portanto, versões iguais são permitidas.
             */
            ReactionState previousState =
                new ReactionState(0, 0, boardVersion: 7);

            ReactionState currentState =
                new ReactionState(1, 0, boardVersion: 7);

            ReactionDecision decision = _rule.Evaluate(
                previousState,
                currentState,
                ScoreActor.Player,
                hasAvailableActions: true);

            Assert.That(
                decision.Kind,
                Is.EqualTo(ReactionDecisionKind.ReactionStarted));
        }

        [Test]
        public void Evaluate_RejectsOlderCurrentBoardVersion()
        {
            ReactionState previousState =
                new ReactionState(0, 0, boardVersion: 8);

            ReactionState staleCurrentState =
                new ReactionState(1, 0, boardVersion: 7);

            ArgumentException exception =
                Assert.Throws<ArgumentException>(
                    () => _rule.Evaluate(
                        previousState,
                        staleCurrentState,
                        ScoreActor.Player,
                        hasAvailableActions: true));

            Assert.That(
                exception.ParamName,
                Is.EqualTo("currentState"));
        }

        [Test]
        public void Evaluate_RejectsNullPreviousState()
        {
            ReactionState currentState =
                new ReactionState(0, 0, boardVersion: 0);

            Assert.Throws<ArgumentNullException>(
                () => _rule.Evaluate(
                    null,
                    currentState,
                    ScoreActor.Player,
                    hasAvailableActions: true));
        }

        [Test]
        public void Evaluate_RejectsNullCurrentState()
        {
            ReactionState previousState =
                new ReactionState(0, 0, boardVersion: 0);

            Assert.Throws<ArgumentNullException>(
                () => _rule.Evaluate(
                    previousState,
                    null,
                    ScoreActor.Player,
                    hasAvailableActions: true));
        }

        [TestCase(ScoreActor.None)]
        [TestCase(ScoreActor.Environment)]
        public void Evaluate_RejectsActorThatCannotCompleteTurn(
            ScoreActor invalidActor)
        {
            ReactionState previousState =
                new ReactionState(0, 0, boardVersion: 0);

            ReactionState currentState =
                new ReactionState(0, 0, boardVersion: 0);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => _rule.Evaluate(
                    previousState,
                    currentState,
                    invalidActor,
                    hasAvailableActions: true));
        }

        private static IEnumerable<TestCaseData> GetTransitionCases()
        {
            yield return CreateCase(
                "Tied_zero_to_zero_continues_normally",
                previousPlayer: 0,
                previousEnemy: 0,
                currentPlayer: 0,
                currentEnemy: 0,
                completedActor: ScoreActor.Player,
                hasActions: true,
                expectedKind: ReactionDecisionKind.ContinueNormal,
                expectedEndReason: ReactionEndReason.None,
                expectedWinner: ScoreActor.None,
                expectedResponder: ScoreActor.None,
                shouldContinue: true);

            yield return CreateCase(
                "Player_lead_starts_reaction_for_Enemy",
                0, 0,
                1, 0,
                ScoreActor.Player,
                true,
                ReactionDecisionKind.ReactionStarted,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.Enemy,
                true);

            yield return CreateCase(
                "Enemy_lead_starts_reaction_for_Player",
                0, 0,
                0, 1,
                ScoreActor.Enemy,
                true,
                ReactionDecisionKind.ReactionStarted,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.Player,
                true);

            yield return CreateCase(
                "Enemy_ties_and_resolves_reaction",
                1, 0,
                1, 1,
                ScoreActor.Enemy,
                true,
                ReactionDecisionKind.ReactionResolvedByTie,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.None,
                true);

            yield return CreateCase(
                "Player_ties_and_resolves_reaction",
                0, 1,
                1, 1,
                ScoreActor.Player,
                true,
                ReactionDecisionKind.ReactionResolvedByTie,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.None,
                true);

            yield return CreateCase(
                "Enemy_takes_lead_and_transfers_reaction",
                1, 0,
                1, 2,
                ScoreActor.Enemy,
                true,
                ReactionDecisionKind.ReactionTransferred,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.Player,
                true);

            yield return CreateCase(
                "Player_takes_lead_and_transfers_reaction",
                0, 1,
                2, 1,
                ScoreActor.Player,
                true,
                ReactionDecisionKind.ReactionTransferred,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.Enemy,
                true);

            yield return CreateCase(
                "Extra_turn_of_Player_maintains_reaction",
                1, 0,
                2, 0,
                ScoreActor.Player,
                true,
                ReactionDecisionKind.ReactionMaintained,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.Enemy,
                true);

            yield return CreateCase(
                "Extra_turn_of_Enemy_maintains_reaction",
                0, 1,
                0, 2,
                ScoreActor.Enemy,
                true,
                ReactionDecisionKind.ReactionMaintained,
                ReactionEndReason.None,
                ScoreActor.None,
                ScoreActor.Player,
                true);

            yield return CreateCase(
                "Enemy_responder_remains_behind_and_loses",
                1, 0,
                2, 0,
                ScoreActor.Enemy,
                true,
                ReactionDecisionKind.Victory,
                ReactionEndReason.ResponderRemainedBehind,
                ScoreActor.Player,
                ScoreActor.None,
                false);

            yield return CreateCase(
                "Player_responder_remains_behind_and_loses",
                0, 1,
                0, 2,
                ScoreActor.Player,
                true,
                ReactionDecisionKind.Victory,
                ReactionEndReason.ResponderRemainedBehind,
                ScoreActor.Enemy,
                ScoreActor.None,
                false);

            yield return CreateCase(
                "No_actions_with_tie_ends_in_draw",
                0, 0,
                0, 0,
                ScoreActor.Player,
                false,
                ReactionDecisionKind.DrawNoActions,
                ReactionEndReason.NoAvailableActions,
                ScoreActor.None,
                ScoreActor.None,
                false);

            yield return CreateCase(
                "No_actions_with_Player_leading_gives_Player_victory",
                0, 0,
                1, 0,
                ScoreActor.Player,
                false,
                ReactionDecisionKind.Victory,
                ReactionEndReason.NoAvailableActions,
                ScoreActor.Player,
                ScoreActor.None,
                false);

            yield return CreateCase(
                "No_actions_with_Enemy_leading_gives_Enemy_victory",
                0, 0,
                0, 1,
                ScoreActor.Enemy,
                false,
                ReactionDecisionKind.Victory,
                ReactionEndReason.NoAvailableActions,
                ScoreActor.Enemy,
                ScoreActor.None,
                false);

            /*
             * Este cenário demonstra explicitamente a prioridade da ausência
             * de ações. Embora o Enemy tenha virado o placar, a decisão não é
             * ReactionTransferred porque a disputa não pode continuar.
             */
            yield return CreateCase(
                "No_actions_has_priority_over_reaction_transfer",
                1, 0,
                1, 2,
                ScoreActor.Enemy,
                false,
                ReactionDecisionKind.Victory,
                ReactionEndReason.NoAvailableActions,
                ScoreActor.Enemy,
                ScoreActor.None,
                false);

            /*
             * Aqui também haveria falha do reagente, mas NoAvailableActions
             * precisa conservar a prioridade e o motivo terminal correto.
             */
            yield return CreateCase(
                "No_actions_has_priority_over_responder_failure",
                1, 0,
                2, 0,
                ScoreActor.Enemy,
                false,
                ReactionDecisionKind.Victory,
                ReactionEndReason.NoAvailableActions,
                ScoreActor.Player,
                ScoreActor.None,
                false);
        }

        private static TestCaseData CreateCase(
            string name,
            int previousPlayer,
            int previousEnemy,
            int currentPlayer,
            int currentEnemy,
            ScoreActor completedActor,
            bool hasActions,
            ReactionDecisionKind expectedKind,
            ReactionEndReason expectedEndReason,
            ScoreActor expectedWinner,
            ScoreActor expectedResponder,
            bool shouldContinue)
        {
            ReactionScenario scenario = new ReactionScenario(
                previousPlayer,
                previousEnemy,
                currentPlayer,
                currentEnemy,
                completedActor,
                hasActions,
                expectedKind,
                expectedEndReason,
                expectedWinner,
                expectedResponder,
                shouldContinue);

            return new TestCaseData(scenario).SetName(name);
        }

        /// <summary>
        /// Dados imutáveis de uma linha da matriz de testes.
        /// </summary>
        public sealed class ReactionScenario
        {
            public int PreviousPlayerCount { get; }
            public int PreviousEnemyCount { get; }
            public int CurrentPlayerCount { get; }
            public int CurrentEnemyCount { get; }

            public ScoreActor CompletedTurnActor { get; }
            public bool HasAvailableActions { get; }

            public ReactionDecisionKind ExpectedKind { get; }
            public ReactionEndReason ExpectedEndReason { get; }
            public ScoreActor ExpectedWinner { get; }
            public ScoreActor ExpectedPendingResponder { get; }
            public bool ShouldContinue { get; }

            public ReactionScenario(
                int previousPlayerCount,
                int previousEnemyCount,
                int currentPlayerCount,
                int currentEnemyCount,
                ScoreActor completedTurnActor,
                bool hasAvailableActions,
                ReactionDecisionKind expectedKind,
                ReactionEndReason expectedEndReason,
                ScoreActor expectedWinner,
                ScoreActor expectedPendingResponder,
                bool shouldContinue)
            {
                PreviousPlayerCount = previousPlayerCount;
                PreviousEnemyCount = previousEnemyCount;
                CurrentPlayerCount = currentPlayerCount;
                CurrentEnemyCount = currentEnemyCount;
                CompletedTurnActor = completedTurnActor;
                HasAvailableActions = hasAvailableActions;
                ExpectedKind = expectedKind;
                ExpectedEndReason = expectedEndReason;
                ExpectedWinner = expectedWinner;
                ExpectedPendingResponder =
                    expectedPendingResponder;
                ShouldContinue = shouldContinue;
            }
        }
    }
}