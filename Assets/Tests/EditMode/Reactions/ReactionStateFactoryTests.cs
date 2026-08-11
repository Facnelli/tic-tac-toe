using System;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Sequences;

namespace TicTacToeRoguelike.EditModeTests.Reactions
{
    /// <summary>
    /// Comprova que ReactionStateFactory sempre representa o BoardState atual,
    /// sem acumular contagens de snapshots anteriores.
    ///
    /// Os testes permanecem inteiramente no domínio:
    /// não utilizam GameObject, MonoBehaviour, frame ou coroutine.
    /// </summary>
    public sealed class ReactionStateFactoryTests
    {
        private ReactionStateFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new ReactionStateFactory();
        }

        [Test]
        public void Create_CountsWinningSequencesForBothParticipants()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(4, 2));

            /*
             * Uma linha de quatro símbolos contém duas janelas de tamanho 3:
             * posições 0-1-2 e posições 1-2-3.
             */
            ApplyMove(board, 0, 0, CellMark.X);
            ApplyMove(board, 1, 0, CellMark.X);
            ApplyMove(board, 2, 0, CellMark.X);
            ApplyMove(board, 3, 0, CellMark.X);

            ApplyMove(board, 0, 1, CellMark.O);
            ApplyMove(board, 1, 1, CellMark.O);
            ApplyMove(board, 2, 1, CellMark.O);
            ApplyMove(board, 3, 1, CellMark.O);

            long versionBeforeEvaluation = board.Version;

            ReactionState state = _factory.Create(
                board,
                playerMark: CellMark.X,
                enemyMark: CellMark.O,
                requiredSequenceLength: 3);

            Assert.That(
                state.PlayerSequenceCount,
                Is.EqualTo(2));

            Assert.That(
                state.EnemySequenceCount,
                Is.EqualTo(2));

            Assert.That(
                state.BoardVersion,
                Is.EqualTo(versionBeforeEvaluation));

            Assert.That(
                board.Version,
                Is.EqualTo(versionBeforeEvaluation));

            Assert.That(
                state.IsTied,
                Is.True);
        }

        [Test]
        public void Create_UsesParticipantMarkMappingProvidedByCaller()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(
                    3,
                    1),
                new[]
                {
                    CreateCell(0, 0, CellMark.X),
                    CreateCell(1, 0, CellMark.X),
                    CreateCell(2, 0, CellMark.X)
                });

            /*
             * Neste cenário o Player controla O e o Enemy controla X.
             * A fábrica não deve possuir a associação Player = X escondida.
             */
            ReactionState state = _factory.Create(
                board,
                playerMark: CellMark.O,
                enemyMark: CellMark.X,
                requiredSequenceLength: 3);

            Assert.That(
                state.PlayerSequenceCount,
                Is.Zero);

            Assert.That(
                state.EnemySequenceCount,
                Is.EqualTo(1));
        }

        [Test]
        public void Create_CellWithBothMarksCountsForBothParticipants()
        {
            CellMark bothMarks =
                CellMark.X |
                CellMark.O;

            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(
                    3,
                    1),
                new[]
                {
                    CreateCell(0, 0, bothMarks),
                    CreateCell(1, 0, bothMarks),
                    CreateCell(2, 0, bothMarks)
                });

            ReactionState state = _factory.Create(
                board,
                playerMark: CellMark.X,
                enemyMark: CellMark.O,
                requiredSequenceLength: 3);

            Assert.That(
                state.PlayerSequenceCount,
                Is.EqualTo(1));

            Assert.That(
                state.EnemySequenceCount,
                Is.EqualTo(1));
        }

        [Test]
        public void Create_AfterSimulatedCleaningAndOverwrite_RecountsBothSides()
        {
            BoardDefinition definition =
                BoardDefinition.CreateRectangular(
                    3,
                    2);

            BoardState boardBeforeEffects = new BoardState(
                definition,
                new[]
                {
                    CreateCell(0, 0, CellMark.X),
                    CreateCell(1, 0, CellMark.X),
                    CreateCell(2, 0, CellMark.X),

                    CreateCell(0, 1, CellMark.O),
                    CreateCell(1, 1, CellMark.O),
                    CreateCell(2, 1, CellMark.O)
                });

            ReactionState stateBeforeEffects = _factory.Create(
                boardBeforeEffects,
                playerMark: CellMark.X,
                enemyMark: CellMark.O,
                requiredSequenceLength: 3);

            /*
             * Representamos o resultado de dois futuros efeitos:
             *
             * 1. a última casa da sequência de X foi limpa;
             * 2. a última casa da sequência de O foi sobrescrita por X.
             *
             * BoardMutationService será criado no Marco 4. Por enquanto,
             * construímos diretamente o estado posterior para comprovar que
             * a fábrica não conserva contagens antigas.
             */
            BoardState boardAfterEffects = new BoardState(
                definition,
                new[]
                {
                    CreateCell(0, 0, CellMark.X),
                    CreateCell(1, 0, CellMark.X),

                    CreateCell(0, 1, CellMark.O),
                    CreateCell(1, 1, CellMark.O),
                    CreateCell(2, 1, CellMark.X)
                });

            ReactionState stateAfterEffects = _factory.Create(
                boardAfterEffects,
                playerMark: CellMark.X,
                enemyMark: CellMark.O,
                requiredSequenceLength: 3);

            Assert.That(
                stateBeforeEffects.PlayerSequenceCount,
                Is.EqualTo(1));

            Assert.That(
                stateBeforeEffects.EnemySequenceCount,
                Is.EqualTo(1));

            Assert.That(
                stateAfterEffects.PlayerSequenceCount,
                Is.Zero);

            Assert.That(
                stateAfterEffects.EnemySequenceCount,
                Is.Zero);

            Assert.That(
                stateAfterEffects.IsTied,
                Is.True);
        }

        [Test]
        public void Create_WithNullBoard_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => _factory.Create(
                    null,
                    CellMark.X,
                    CellMark.O,
                    requiredSequenceLength: 3));
        }

        [TestCase(CellMark.None, CellMark.O)]
        [TestCase(CellMark.X | CellMark.O, CellMark.O)]
        [TestCase(CellMark.X, CellMark.None)]
        [TestCase(CellMark.X, CellMark.X | CellMark.O)]
        public void Create_WithInvalidParticipantMark_Throws(
            CellMark playerMark,
            CellMark enemyMark)
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(
                    3,
                    3));

            Assert.Throws<ArgumentException>(
                () => _factory.Create(
                    board,
                    playerMark,
                    enemyMark,
                    requiredSequenceLength: 3));
        }

        [Test]
        public void Create_WithSameMarkForBothParticipants_Throws()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(
                    3,
                    3));

            ArgumentException exception =
                Assert.Throws<ArgumentException>(
                    () => _factory.Create(
                        board,
                        CellMark.X,
                        CellMark.X,
                        requiredSequenceLength: 3));

            Assert.That(
                exception.ParamName,
                Is.EqualTo("enemyMark"));
        }

        [Test]
        public void Create_WithRequiredLengthBelowTwo_Throws()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(
                    3,
                    3));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => _factory.Create(
                    board,
                    CellMark.X,
                    CellMark.O,
                    requiredSequenceLength: 1));
        }

        [Test]
        public void Constructor_WithNullSequenceEvaluator_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ReactionStateFactory(null));
        }

        private static CellState CreateCell(
            int x,
            int y,
            CellMark mark)
        {
            return new CellState(
                new BoardCoordinate(x, y),
                mark);
        }

        private static void ApplyMove(
            BoardState board,
            int x,
            int y,
            CellMark mark)
        {
            MoveValidationResult result =
                new MoveService().TryApplyNormalMove(
                    board,
                    x,
                    y,
                    mark);

            Assert.That(
                result.IsValid,
                Is.True,
                $"A preparação do teste não conseguiu aplicar {mark} " +
                $"em ({x}, {y}).");
        }
    }
}