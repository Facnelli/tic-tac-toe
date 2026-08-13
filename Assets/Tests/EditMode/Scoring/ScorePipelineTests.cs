using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Scoring
{
    /// <summary>
    /// Protege a conversão das sequências atuais em pontos, sem efeitos externos.
    /// </summary>
    public sealed class ScorePipelineTests
    {
        [Test]
        public void Resolve_ThreeMarkWinningLine_ReproducesBaselineScore()
        {
            BoardState board = CreateHorizontalLine(CellMark.X, 3);
            long versionBefore = board.Version;

            ScorePipelineResult result = new ScorePipeline().Resolve(
                new ScorePipelineRequest(
                    ScoreActor.Player,
                    CellMark.X,
                    board,
                    minimumSequenceLength: 2,
                    maximumSequenceLength: 3,
                    isWinner: true,
                    victoryMultiplier: 1.5m));

            /*
             * XXX contém dois pares e uma trinca:
             * 2 + 2 + 3 = 7 pontos básicos.
             * A vitória multiplica por 1,5: 10,5, arredondado para 11.
             */
            Assert.That(result.ScoringSequences.Count, Is.EqualTo(3));
            Assert.That(result.Breakdown.BasePoints, Is.EqualTo(7m));
            Assert.That(
                result.Breakdown.VictoryMultiplierProduct,
                Is.EqualTo(1.5m));
            Assert.That(result.Breakdown.UnclampedScore, Is.EqualTo(10.5m));
            Assert.That(result.Breakdown.FinalScore, Is.EqualTo(11));
            Assert.That(result.Breakdown.Contributions.Count, Is.EqualTo(4));
            Assert.That(result.BoardVersion, Is.EqualTo(versionBefore));
            Assert.That(board.Version, Is.EqualTo(versionBefore));
        }

        [Test]
        public void Resolve_NonWinner_DoesNotApplyVictoryMultiplier()
        {
            BoardState board = CreateHorizontalLine(CellMark.O, 3);

            ScorePipelineResult result = new ScorePipeline().Resolve(
                new ScorePipelineRequest(
                    ScoreActor.Enemy,
                    CellMark.O,
                    board,
                    minimumSequenceLength: 2,
                    maximumSequenceLength: 3,
                    isWinner: false,
                    victoryMultiplier: 1.5m));

            Assert.That(result.ScoringSequences.Count, Is.EqualTo(3));
            Assert.That(result.Breakdown.BasePoints, Is.EqualTo(7m));
            Assert.That(
                result.Breakdown.VictoryMultiplierProduct,
                Is.EqualTo(1m));
            Assert.That(result.Breakdown.FinalScore, Is.EqualTo(7));
            Assert.That(result.Breakdown.Contributions.Count, Is.EqualTo(3));
        }

        [Test]
        public void Resolve_WithoutScoringSequence_ProducesZero()
        {
            BoardState board = CreateHorizontalLine(CellMark.X, 1);

            ScorePipelineResult result = new ScorePipeline().Resolve(
                new ScorePipelineRequest(
                    ScoreActor.Player,
                    CellMark.X,
                    board,
                    minimumSequenceLength: 2,
                    maximumSequenceLength: 3,
                    isWinner: false,
                    victoryMultiplier: 1.5m));

            Assert.That(result.ScoringSequences, Is.Empty);
            Assert.That(result.Breakdown.Contributions, Is.Empty);
            Assert.That(result.Breakdown.FinalScore, Is.Zero);
        }

        private static BoardState CreateHorizontalLine(
            CellMark mark,
            int markCount)
        {
            const int width = 3;
            CellState[] initialCells = new CellState[markCount];

            for (int x = 0; x < markCount; x++)
            {
                initialCells[x] = new CellState(
                    new BoardCoordinate(x, 0),
                    mark);
            }

            return new BoardState(
                BoardDefinition.CreateRectangular(width, 1),
                initialCells);
        }
    }
}