using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.EditModeTests.Combat
{
    /// <summary>
    /// Protege a conversão do saldo do confronto em dano básico, sem efeitos.
    /// </summary>
    public sealed class DamageResolverTests
    {
        [Test]
        public void Resolve_WithoutModifiers_AppliesUnopposedScoreToEnemyHealth()
        {
            ClashReport clash = CreateClash(
                playerMarkCount: 3,
                enemyMarkCount: 2);

            CombatantState player = CreatePlayer(currentHealth: 10);
            CombatantState enemy = CreateEnemy(currentHealth: 10);

            DamageReport result = new DamageResolver().Resolve(
                clash,
                player,
                enemy);

            Assert.That(result.SourceActor, Is.EqualTo(ScoreActor.Player));
            Assert.That(result.TargetActor, Is.EqualTo(ScoreActor.Enemy));
            Assert.That(result.RawDamage, Is.EqualTo(5));
            Assert.That(result.IncreasedDamage, Is.Zero);
            Assert.That(result.PreventedDamage, Is.Zero);
            Assert.That(result.DamageAfterModifiers, Is.EqualTo(5));
            Assert.That(result.DamageAbsorbedByShield, Is.Zero);
            Assert.That(result.RequestedHealthDamage, Is.EqualTo(5));
            Assert.That(result.AppliedHealthDamage, Is.EqualTo(5));
            Assert.That(result.Overkill, Is.Zero);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(5));
            Assert.That(enemy.Version, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.EqualTo(10));
            Assert.That(player.Version, Is.Zero);
        }

        [Test]
        public void Resolve_WithShield_ConsumesShieldBeforeHealth()
        {
            ClashReport clash = CreateClash(
                playerMarkCount: 3,
                enemyMarkCount: 2);

            CombatantState player = CreatePlayer(currentHealth: 10);
            CombatantState enemy = CreateEnemy(
                currentHealth: 10,
                shield: 2);

            DamageReport result = new DamageResolver().Resolve(
                clash,
                player,
                enemy);

            Assert.That(result.RawDamage, Is.EqualTo(5));
            Assert.That(result.DamageAbsorbedByShield, Is.EqualTo(2));
            Assert.That(result.ShieldConsumed, Is.EqualTo(2));
            Assert.That(result.RequestedHealthDamage, Is.EqualTo(3));
            Assert.That(result.AppliedHealthDamage, Is.EqualTo(3));
            Assert.That(result.WasFullyAbsorbedByShield, Is.False);
            Assert.That(enemy.Shield, Is.Zero);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(7));
            Assert.That(enemy.Version, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_WhenDamageExceedsHealth_RecordsOverkillAndDefeat()
        {
            ClashReport clash = CreateClash(
                playerMarkCount: 3,
                enemyMarkCount: 2);

            CombatantState player = CreatePlayer(currentHealth: 10);
            CombatantState enemy = CreateEnemy(currentHealth: 3);

            DamageReport result = new DamageResolver().Resolve(
                clash,
                player,
                enemy);

            Assert.That(result.RequestedHealthDamage, Is.EqualTo(5));
            Assert.That(result.AppliedHealthDamage, Is.EqualTo(3));
            Assert.That(result.Overkill, Is.EqualTo(2));
            Assert.That(result.CausedDefeat, Is.True);
            Assert.That(result.TargetBefore.CurrentHealth, Is.EqualTo(3));
            Assert.That(result.TargetAfter.CurrentHealth, Is.Zero);
            Assert.That(enemy.IsDefeated, Is.True);
        }

        [Test]
        public void Resolve_TiedClash_ReturnsNoDamageWithoutChangingEitherState()
        {
            ClashReport clash = CreateClash(
                playerMarkCount: 2,
                enemyMarkCount: 2);

            CombatantState player = CreatePlayer(
                currentHealth: 8,
                shield: 1);
            CombatantState enemy = CreateEnemy(
                currentHealth: 7,
                shield: 2);

            DamageReport result = new DamageResolver().Resolve(
                clash,
                player,
                enemy);

            Assert.That(result.IsNoDamage, Is.True);
            Assert.That(result.HasTarget, Is.False);
            Assert.That(result.RawDamage, Is.Zero);
            Assert.That(result.ChangedTargetState, Is.False);
            Assert.That(player.CurrentHealth, Is.EqualTo(8));
            Assert.That(player.Shield, Is.EqualTo(1));
            Assert.That(player.Version, Is.Zero);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(7));
            Assert.That(enemy.Shield, Is.EqualTo(2));
            Assert.That(enemy.Version, Is.Zero);
        }

        private static CombatantState CreatePlayer(
            int currentHealth,
            int shield = 0)
        {
            return new CombatantState(
                "player:test",
                ScoreActor.Player,
                maximumHealth: 10,
                currentHealth: currentHealth,
                shield: shield);
        }

        private static CombatantState CreateEnemy(
            int currentHealth,
            int shield = 0)
        {
            return new CombatantState(
                "enemy:test",
                ScoreActor.Enemy,
                maximumHealth: 10,
                currentHealth: currentHealth,
                shield: shield);
        }

        private static ClashReport CreateClash(
            int playerMarkCount,
            int enemyMarkCount)
        {
            BoardState board = CreateBoardWithScores(
                playerMarkCount,
                enemyMarkCount);

            ScorePipeline pipeline = new ScorePipeline();

            ScorePipelineResult playerScore = pipeline.Resolve(
                CreateScoreRequest(
                    ScoreActor.Player,
                    CellMark.X,
                    board));

            ScorePipelineResult enemyScore = pipeline.Resolve(
                CreateScoreRequest(
                    ScoreActor.Enemy,
                    CellMark.O,
                    board));

            return new ClashResolver().Resolve(
                playerScore,
                enemyScore);
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