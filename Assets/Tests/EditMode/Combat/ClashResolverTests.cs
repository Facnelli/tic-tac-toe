using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Combat
{
    /// <summary>
    /// Fixa a anulação simétrica dos placares antes de qualquer efeito de combate.
    /// </summary>
    public sealed class ClashResolverTests
    {
        [Test]
        public void Resolve_PlayerHasHigherScore_CancelsEqualAmountOnBothSides()
        {
            BoardState board = CreateBoardWithScores(
                playerMarkCount: 3,
                enemyMarkCount: 2);

            ClashReport result = ResolveClash(board);

            Assert.That(result.PlayerScore, Is.EqualTo(7));
            Assert.That(result.EnemyScore, Is.EqualTo(2));
            Assert.That(result.CancelledScorePerSide, Is.EqualTo(2));
            Assert.That(result.TotalCancelledScore, Is.EqualTo(4L));
            Assert.That(result.PlayerRemainingScore, Is.EqualTo(5));
            Assert.That(result.EnemyRemainingScore, Is.Zero);
            Assert.That(result.Outcome, Is.EqualTo(ClashOutcome.PlayerAdvantage));
            Assert.That(result.AdvantageActor, Is.EqualTo(ScoreActor.Player));
            Assert.That(result.DisadvantageActor, Is.EqualTo(ScoreActor.Enemy));
            Assert.That(result.UnopposedScore, Is.EqualTo(5));
        }

        [Test]
        public void Resolve_EnemyHasHigherScore_ReportsMirroredAdvantage()
        {
            BoardState board = CreateBoardWithScores(
                playerMarkCount: 2,
                enemyMarkCount: 3);

            ClashReport result = ResolveClash(board);

            Assert.That(result.PlayerScore, Is.EqualTo(2));
            Assert.That(result.EnemyScore, Is.EqualTo(7));
            Assert.That(result.CancelledScorePerSide, Is.EqualTo(2));
            Assert.That(result.PlayerRemainingScore, Is.Zero);
            Assert.That(result.EnemyRemainingScore, Is.EqualTo(5));
            Assert.That(result.Outcome, Is.EqualTo(ClashOutcome.EnemyAdvantage));
            Assert.That(result.AdvantageActor, Is.EqualTo(ScoreActor.Enemy));
            Assert.That(result.UnopposedScore, Is.EqualTo(5));
        }

        [Test]
        public void Resolve_EqualScores_ProducesExplicitTie()
        {
            BoardState board = CreateBoardWithScores(
                playerMarkCount: 2,
                enemyMarkCount: 2);

            ClashReport result = ResolveClash(board);

            Assert.That(result.PlayerScore, Is.EqualTo(2));
            Assert.That(result.EnemyScore, Is.EqualTo(2));
            Assert.That(result.CancelledScorePerSide, Is.EqualTo(2));
            Assert.That(result.PlayerRemainingScore, Is.Zero);
            Assert.That(result.EnemyRemainingScore, Is.Zero);
            Assert.That(result.IsTie, Is.True);
            Assert.That(result.AdvantageActor, Is.EqualTo(ScoreActor.None));
            Assert.That(result.DisadvantageActor, Is.EqualTo(ScoreActor.None));
            Assert.That(result.UnopposedScore, Is.Zero);
        }

        private static ClashReport ResolveClash(BoardState board)
        {
            ScorePipeline pipeline = new ScorePipeline();

            ScorePipelineResult player = pipeline.Resolve(
                CreateScoreRequest(
                    ScoreActor.Player,
                    CellMark.X,
                    board));

            ScorePipelineResult enemy = pipeline.Resolve(
                CreateScoreRequest(
                    ScoreActor.Enemy,
                    CellMark.O,
                    board));

            return new ClashResolver().Resolve(player, enemy);
        }

        private static ScorePipelineRequest CreateScoreRequest(
            ScoreActor participant,
            CellMark mark,
            BoardState board)
        {
            return new ScorePipelineRequest(
                participant,
                mark,
                board,
                minimumSequenceLength: 2,
                maximumSequenceLength: 3,
                isWinner: false,
                victoryMultiplier: 1.5m);
        }

        private static BoardState CreateBoardWithScores(
            int playerMarkCount,
            int enemyMarkCount)
        {
            CellState[] initialCells =
                new CellState[playerMarkCount + enemyMarkCount];

            for (int x = 0; x < playerMarkCount; x++)
            {
                initialCells[x] = new CellState(
                    new BoardCoordinate(x, 0),
                    CellMark.X);
            }

            for (int x = 0; x < enemyMarkCount; x++)
            {
                initialCells[playerMarkCount + x] = new CellState(
                    new BoardCoordinate(x, 1),
                    CellMark.O);
            }

            return new BoardState(
                BoardDefinition.CreateRectangular(3, 2),
                initialCells);
        }
    }
}