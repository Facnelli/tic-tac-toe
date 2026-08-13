using NUnit.Framework;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Scoring
{
    /// <summary>
    /// Congela a fórmula pura de pontuação antes da introdução de runas.
    ///
    /// Estes testes não procuram sequências. Eles entregam contribuições prontas e
    /// verificam a ordem das fases, os totais intermediários e o arredondamento.
    /// </summary>
    public sealed class ScoreBreakdownTests
    {
        [Test]
        public void Resolve_WithAllPhases_ReproducesCurrentFormula()
        {
            ScoreContribution[] unorderedContributions =
            {
                ScoreContribution.CreateFinalFlatPoints(
                    "test:final",
                    "Ajuste final",
                    ScoreActor.Player,
                    ScoreActor.Player,
                    -2m),
                ScoreContribution.CreateVictoryMultiplier(
                    "test:victory",
                    "Vitória",
                    ScoreActor.Environment,
                    ScoreActor.Player,
                    1.5m),
                ScoreContribution.CreateIndependentMultiplier(
                    "test:independent",
                    "Multiplicador independente",
                    ScoreActor.Player,
                    ScoreActor.Player,
                    2m),
                ScoreContribution.CreateAdditiveMultiplier(
                    "test:additive",
                    "Multiplicador aditivo",
                    ScoreActor.Player,
                    ScoreActor.Player,
                    0.5m),
                ScoreContribution.CreateFlatPoints(
                    "test:flat",
                    "Pontos planos",
                    ScoreActor.Player,
                    ScoreActor.Player,
                    1m),
                ScoreContribution.CreateBasePoints(
                    "test:base",
                    "Pontos básicos",
                    ScoreActor.Player,
                    ScoreActor.Player,
                    4m)
            };

            ScoreBreakdown result = ScoreBreakdown.Resolve(
                ScoreActor.Player,
                unorderedContributions);

            /*
             * ((4 + 1) * (1 + 0,5) * 2 * 1,5) - 2 = 20,5.
             * A regra AwayFromZero transforma 20,5 em 21.
             */
            Assert.That(result.BasePoints, Is.EqualTo(4m));
            Assert.That(result.FlatPoints, Is.EqualTo(1m));
            Assert.That(result.AdditiveMultiplier, Is.EqualTo(0.5m));
            Assert.That(result.AdditiveMultiplierFactor, Is.EqualTo(1.5m));
            Assert.That(result.IndependentMultiplierProduct, Is.EqualTo(2m));
            Assert.That(result.VictoryMultiplierProduct, Is.EqualTo(1.5m));
            Assert.That(result.FinalFlatPoints, Is.EqualTo(-2m));
            Assert.That(result.TotalMultiplier, Is.EqualTo(4.5m));
            Assert.That(result.UnclampedScore, Is.EqualTo(20.5m));
            Assert.That(result.NonNegativeScore, Is.EqualTo(20.5m));
            Assert.That(result.RoundedScore, Is.EqualTo(21m));
            Assert.That(result.FinalScore, Is.EqualTo(21));
            Assert.That(result.WasClampedToIntegerRange, Is.False);

            Assert.That(
                result.Contributions[0].Phase,
                Is.EqualTo(ScorePhase.BasePoints));
            Assert.That(
                result.Contributions[5].Phase,
                Is.EqualTo(ScorePhase.FinalFlatPoints));
            Assert.That(result.Steps.Count, Is.EqualTo(6));
        }

        [Test]
        public void Resolve_WithHalfPoint_RoundsAwayFromZero()
        {
            ScoreBreakdown result = ScoreBreakdown.Resolve(
                ScoreActor.Player,
                new[]
                {
                    ScoreContribution.CreateBasePoints(
                        "test:base",
                        "Pontos básicos",
                        ScoreActor.Player,
                        ScoreActor.Player,
                        5m),
                    ScoreContribution.CreateIndependentMultiplier(
                        "test:half",
                        "Metade",
                        ScoreActor.Player,
                        ScoreActor.Player,
                        0.5m)
                });

            Assert.That(result.UnclampedScore, Is.EqualTo(2.5m));
            Assert.That(result.FinalScore, Is.EqualTo(3));
        }

        [Test]
        public void Resolve_WithNegativeFinalResult_ClampsScoreToZero()
        {
            ScoreBreakdown result = ScoreBreakdown.Resolve(
                ScoreActor.Enemy,
                new[]
                {
                    ScoreContribution.CreateBasePoints(
                        "test:base",
                        "Pontos básicos",
                        ScoreActor.Enemy,
                        ScoreActor.Enemy,
                        2m),
                    ScoreContribution.CreateFinalFlatPoints(
                        "test:penalty",
                        "Penalidade final",
                        ScoreActor.Player,
                        ScoreActor.Enemy,
                        -5m)
                });

            Assert.That(result.UnclampedScore, Is.EqualTo(-3m));
            Assert.That(result.NonNegativeScore, Is.Zero);
            Assert.That(result.FinalScore, Is.Zero);
        }
    }
}