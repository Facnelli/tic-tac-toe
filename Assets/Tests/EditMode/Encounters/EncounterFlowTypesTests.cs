using System;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Encounters
{
    /// <summary>
    /// Testa os contratos puros extraídos para o fluxo do encontro.
    ///
    /// A suíte comprova principalmente que os DTOs não permitem contradizer os
    /// relatórios autoritativos produzidos por ReactionRule, ScorePipeline,
    /// ClashResolver e DamageResolver.
    /// </summary>
    public sealed class EncounterFlowTypesTests
    {
        [Test]
        public void EncounterSideRules_Player_MapsToPlayerAndX()
        {
            Assert.AreEqual(
                ScoreActor.Player,
                EncounterSideRules.ToActor(
                    EncounterSide.Player));

            Assert.AreEqual(
                CellMark.X,
                EncounterSideRules.GetNormalMoveMark(
                    EncounterSide.Player));
        }

        [Test]
        public void EncounterSideRules_Enemy_MapsToEnemyAndO()
        {
            Assert.AreEqual(
                ScoreActor.Enemy,
                EncounterSideRules.ToActor(
                    EncounterSide.Enemy));

            Assert.AreEqual(
                CellMark.O,
                EncounterSideRules.GetNormalMoveMark(
                    EncounterSide.Enemy));
        }

        [Test]
        public void EncounterSideRules_FromActor_RestoresBothSides()
        {
            Assert.AreEqual(
                EncounterSide.Player,
                EncounterSideRules.FromActor(
                    ScoreActor.Player));

            Assert.AreEqual(
                EncounterSide.Enemy,
                EncounterSideRules.FromActor(
                    ScoreActor.Enemy));
        }

        [Test]
        public void EncounterSideRules_GetOpposite_SwitchesBothSides()
        {
            Assert.AreEqual(
                EncounterSide.Enemy,
                EncounterSideRules.GetOpposite(
                    EncounterSide.Player));

            Assert.AreEqual(
                EncounterSide.Player,
                EncounterSideRules.GetOpposite(
                    EncounterSide.Enemy));
        }

        [Test]
        public void EncounterSideRules_InvalidValues_ThrowArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EncounterSideRules.ToActor(
                    (EncounterSide)100));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => EncounterSideRules.FromActor(
                    ScoreActor.None));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => EncounterSideRules.FromActor(
                    ScoreActor.Environment));
        }

        [Test]
        public void EncounterRules_ValidValues_ArePreserved()
        {
            EncounterRules rules =
                new EncounterRules(
                    requiredSequenceLength: 3,
                    minimumScoringSequenceLength: 2,
                    maximumScoringSequenceLength: 4,
                    victoryMultiplier: 1.5m);

            Assert.AreEqual(
                3,
                rules.RequiredSequenceLength);

            Assert.AreEqual(
                2,
                rules.MinimumScoringSequenceLength);

            Assert.AreEqual(
                4,
                rules.MaximumScoringSequenceLength);

            Assert.AreEqual(
                1.5m,
                rules.VictoryMultiplier);
        }

        [Test]
        public void EncounterRules_InvalidSequenceLengths_ThrowArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterRules(
                    requiredSequenceLength: 1,
                    minimumScoringSequenceLength: 2,
                    maximumScoringSequenceLength: 3,
                    victoryMultiplier: 1.5m));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterRules(
                    requiredSequenceLength: 3,
                    minimumScoringSequenceLength: 1,
                    maximumScoringSequenceLength: 3,
                    victoryMultiplier: 1.5m));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterRules(
                    requiredSequenceLength: 3,
                    minimumScoringSequenceLength: 3,
                    maximumScoringSequenceLength: 2,
                    victoryMultiplier: 1.5m));
        }

        [Test]
        public void EncounterRules_NegativeVictoryMultiplier_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterRules(
                    requiredSequenceLength: 3,
                    minimumScoringSequenceLength: 2,
                    maximumScoringSequenceLength: 3,
                    victoryMultiplier: -0.1m));
        }

        [Test]
        public void RoundResult_PlayerVictory_DerivesReactionFacts()
        {
            BoardState board =
                CreateBoardWithMarks(
                    CellMark.X,
                    CellMark.X);

            ReactionDecision decision =
                CreateTerminalDecision(
                    board.Version,
                    playerSequenceCount: 1,
                    enemySequenceCount: 0);

            RoundResult result =
                new RoundResult(
                    2,
                    board,
                    decision);

            Assert.AreEqual(
                2,
                result.RoundNumber);

            Assert.AreSame(
                board,
                result.Board);

            Assert.AreSame(
                decision,
                result.FinalDecision);

            Assert.AreEqual(
                RoundOutcome.PlayerVictory,
                result.Outcome);

            Assert.AreEqual(
                ScoreActor.Player,
                result.Winner);

            Assert.AreEqual(
                ReactionEndReason.NoAvailableActions,
                result.EndReason);

            Assert.AreEqual(
                ScoreActor.Player,
                result.VictoryMultiplierRecipient);

            Assert.IsFalse(
                result.IsDraw);
        }

        [Test]
        public void RoundResult_Draw_DoesNotGrantVictoryMultiplier()
        {
            BoardState board =
                CreateBoardWithMarks(
                    CellMark.X,
                    CellMark.O);

            ReactionDecision decision =
                CreateTerminalDecision(
                    board.Version,
                    playerSequenceCount: 0,
                    enemySequenceCount: 0);

            RoundResult result =
                new RoundResult(
                    1,
                    board,
                    decision);

            Assert.AreEqual(
                RoundOutcome.Draw,
                result.Outcome);

            Assert.AreEqual(
                ScoreActor.None,
                result.Winner);

            Assert.AreEqual(
                ScoreActor.None,
                result.VictoryMultiplierRecipient);

            Assert.IsTrue(
                result.IsDraw);
        }

        [Test]
        public void RoundResult_ContinuingDecision_ThrowsArgumentException()
        {
            BoardState board =
                CreateEmptyBoard(2, 1);

            ReactionState previousState =
                new ReactionState(
                    0,
                    0,
                    board.Version);

            ReactionState currentState =
                new ReactionState(
                    1,
                    0,
                    board.Version);

            ReactionDecision continuingDecision =
                new ReactionRule().Evaluate(
                    previousState,
                    currentState,
                    ScoreActor.Player,
                    hasAvailableActions: true);

            Assert.IsFalse(
                continuingDecision.EndsBoardMatch);

            Assert.Throws<ArgumentException>(
                () => new RoundResult(
                    1,
                    board,
                    continuingDecision));
        }

        [Test]
        public void RoundResult_DifferentBoardVersion_ThrowsArgumentException()
        {
            BoardState board =
                CreateEmptyBoard(1, 1);

            ReactionDecision decision =
                CreateTerminalDecision(
                    board.Version + 1,
                    playerSequenceCount: 1,
                    enemySequenceCount: 0);

            Assert.Throws<ArgumentException>(
                () => new RoundResult(
                    1,
                    board,
                    decision));
        }

        [Test]
        public void RoundResult_InvalidArguments_ThrowExpectedExceptions()
        {
            BoardState board =
                CreateEmptyBoard(1, 1);

            ReactionDecision decision =
                CreateTerminalDecision(
                    board.Version,
                    playerSequenceCount: 0,
                    enemySequenceCount: 0);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RoundResult(
                    0,
                    board,
                    decision));

            Assert.Throws<ArgumentNullException>(
                () => new RoundResult(
                    1,
                    null,
                    decision));

            Assert.Throws<ArgumentNullException>(
                () => new RoundResult(
                    1,
                    board,
                    null));
        }

        [Test]
        public void RoundResolution_ResolvedPipeline_PreservesAuthoritativeReports()
        {
            ResolutionFixture fixture =
                CreatePlayerVictoryResolution(
                    enemyHealth: 10);

            Assert.AreSame(
                fixture.Round,
                fixture.Resolution.Round);

            Assert.AreSame(
                fixture.PlayerScore,
                fixture.Resolution.PlayerScore);

            Assert.AreSame(
                fixture.EnemyScore,
                fixture.Resolution.EnemyScore);

            Assert.AreSame(
                fixture.Clash,
                fixture.Resolution.Clash);

            Assert.AreSame(
                fixture.Damage,
                fixture.Resolution.Damage);

            Assert.IsFalse(
                fixture.Resolution.CausedEncounterEnd);
        }

        [Test]
        public void RoundResolution_DamageDefeatsEnemy_ReportsEncounterEnd()
        {
            ResolutionFixture fixture =
                CreatePlayerVictoryResolution(
                    enemyHealth: 2);

            Assert.IsTrue(
                fixture.Resolution.EnemyAfter.IsDefeated);

            Assert.IsTrue(
                fixture.Resolution.CausedEncounterEnd);
        }

        [Test]
        public void RoundResolution_SwappedScores_ThrowsArgumentException()
        {
            ResolutionFixture fixture =
                CreatePlayerVictoryResolution(
                    enemyHealth: 10);

            Assert.Throws<ArgumentException>(
                () => new RoundResolution(
                    fixture.Round,
                    fixture.EnemyScore,
                    fixture.PlayerScore,
                    fixture.Clash,
                    fixture.Damage,
                    fixture.PlayerBefore,
                    fixture.PlayerAfter,
                    fixture.EnemyBefore,
                    fixture.EnemyAfter));
        }

        [Test]
        public void RoundResolution_SwappedSnapshotOwners_ThrowsArgumentException()
        {
            ResolutionFixture fixture =
                CreatePlayerVictoryResolution(
                    enemyHealth: 10);

            Assert.Throws<ArgumentException>(
                () => new RoundResolution(
                    fixture.Round,
                    fixture.PlayerScore,
                    fixture.EnemyScore,
                    fixture.Clash,
                    fixture.Damage,
                    fixture.EnemyBefore,
                    fixture.EnemyAfter,
                    fixture.PlayerBefore,
                    fixture.PlayerAfter));
        }

        [Test]
        public void EncounterResult_DefeatedEnemy_DerivesPlayerVictory()
        {
            ResolutionFixture fixture =
                CreatePlayerVictoryResolution(
                    enemyHealth: 2);

            EncounterResult result =
                new EncounterResult(
                    fixture.Resolution);

            Assert.AreEqual(
                EncounterOutcome.PlayerVictory,
                result.Outcome);

            Assert.AreSame(
                fixture.Resolution,
                result.FinalRound);

            Assert.AreSame(
                fixture.Resolution.PlayerAfter,
                result.Player);

            Assert.AreSame(
                fixture.Resolution.EnemyAfter,
                result.Enemy);
        }

        [Test]
        public void EncounterResult_BothCombatantsAlive_ThrowsArgumentException()
        {
            ResolutionFixture fixture =
                CreatePlayerVictoryResolution(
                    enemyHealth: 10);

            Assert.Throws<ArgumentException>(
                () => new EncounterResult(
                    fixture.Resolution));
        }

        [Test]
        public void EncounterResult_NullResolution_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new EncounterResult(null));
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

        private static BoardState CreateBoardWithMarks(
            CellMark firstMark,
            CellMark secondMark)
        {
            BoardDefinition definition =
                BoardDefinition.CreateRectangular(2, 1);

            return new BoardState(
                definition,
                new[]
                {
                    new CellState(
                        new BoardCoordinate(0, 0),
                        firstMark),

                    new CellState(
                        new BoardCoordinate(1, 0),
                        secondMark)
                });
        }

        private static ReactionDecision CreateTerminalDecision(
            long boardVersion,
            int playerSequenceCount,
            int enemySequenceCount)
        {
            ReactionState previousState =
                new ReactionState(
                    0,
                    0,
                    boardVersion);

            ReactionState currentState =
                new ReactionState(
                    playerSequenceCount,
                    enemySequenceCount,
                    boardVersion);

            return new ReactionRule().Evaluate(
                previousState,
                currentState,
                ScoreActor.Player,
                hasAvailableActions: false);
        }

        private static ResolutionFixture
            CreatePlayerVictoryResolution(
                int enemyHealth)
        {
            BoardState board =
                CreateBoardWithMarks(
                    CellMark.X,
                    CellMark.X);

            ReactionDecision decision =
                CreateTerminalDecision(
                    board.Version,
                    playerSequenceCount: 1,
                    enemySequenceCount: 0);

            RoundResult round =
                new RoundResult(
                    1,
                    board,
                    decision);

            ScorePipeline scorePipeline =
                new ScorePipeline();

            ScorePipelineResult playerScore =
                scorePipeline.Resolve(
                    new ScorePipelineRequest(
                        ScoreActor.Player,
                        CellMark.X,
                        board,
                        minimumSequenceLength: 2,
                        maximumSequenceLength: 2,
                        isWinner: true,
                        victoryMultiplier: 1m));

            ScorePipelineResult enemyScore =
                scorePipeline.Resolve(
                    new ScorePipelineRequest(
                        ScoreActor.Enemy,
                        CellMark.O,
                        board,
                        minimumSequenceLength: 2,
                        maximumSequenceLength: 2,
                        isWinner: false,
                        victoryMultiplier: 1m));

            ClashReport clash =
                new ClashResolver().Resolve(
                    playerScore,
                    enemyScore);

            CombatantState playerState =
                new CombatantState(
                    "player",
                    ScoreActor.Player,
                    maximumHealth: 10);

            CombatantState enemyState =
                new CombatantState(
                    "enemy",
                    ScoreActor.Enemy,
                    maximumHealth: enemyHealth);

            CombatantSnapshot playerBefore =
                CombatantSnapshot.Capture(
                    playerState);

            CombatantSnapshot enemyBefore =
                CombatantSnapshot.Capture(
                    enemyState);

            DamageReport damage =
                new DamageResolver().Resolve(
                    clash,
                    playerState,
                    enemyState);

            CombatantSnapshot playerAfter =
                CombatantSnapshot.Capture(
                    playerState);

            CombatantSnapshot enemyAfter =
                CombatantSnapshot.Capture(
                    enemyState);

            RoundResolution resolution =
                new RoundResolution(
                    round,
                    playerScore,
                    enemyScore,
                    clash,
                    damage,
                    playerBefore,
                    playerAfter,
                    enemyBefore,
                    enemyAfter);

            return new ResolutionFixture(
                round,
                playerScore,
                enemyScore,
                clash,
                damage,
                playerBefore,
                playerAfter,
                enemyBefore,
                enemyAfter,
                resolution);
        }

        private sealed class ResolutionFixture
        {
            public RoundResult Round { get; }
            public ScorePipelineResult PlayerScore { get; }
            public ScorePipelineResult EnemyScore { get; }
            public ClashReport Clash { get; }
            public DamageReport Damage { get; }
            public CombatantSnapshot PlayerBefore { get; }
            public CombatantSnapshot PlayerAfter { get; }
            public CombatantSnapshot EnemyBefore { get; }
            public CombatantSnapshot EnemyAfter { get; }
            public RoundResolution Resolution { get; }

            public ResolutionFixture(
                RoundResult round,
                ScorePipelineResult playerScore,
                ScorePipelineResult enemyScore,
                ClashReport clash,
                DamageReport damage,
                CombatantSnapshot playerBefore,
                CombatantSnapshot playerAfter,
                CombatantSnapshot enemyBefore,
                CombatantSnapshot enemyAfter,
                RoundResolution resolution)
            {
                Round = round;
                PlayerScore = playerScore;
                EnemyScore = enemyScore;
                Clash = clash;
                Damage = damage;
                PlayerBefore = playerBefore;
                PlayerAfter = playerAfter;
                EnemyBefore = enemyBefore;
                EnemyAfter = enemyAfter;
                Resolution = resolution;
            }
        }
    }
}