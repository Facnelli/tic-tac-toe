using System;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Intenção imutável de colocar uma única marca em uma casa.
    ///
    /// Esta classe não verifica se:
    ///
    /// - a coordenada existe no tabuleiro;
    /// - a casa está vazia;
    /// - a casa dourada permite sobreposição;
    /// - a marca pertence ao ator;
    /// - o encontro está na fase correta.
    ///
    /// Essas verificações dependem do estado atual do encontro. No futuro,
    /// ActionExecutor validará o contexto e delegará a regra do lance normal
    /// ao MoveService.
    ///
    /// Manter esta classe como uma intenção permite que Player e IA enviem
    /// exatamente o mesmo contrato ao executor.
    /// </summary>
    public sealed class PlaceMarkAction :
        GameAction<BoardCoordinate>
    {
        /// <summary>
        /// Marca que deverá ser colocada.
        ///
        /// Somente X ou O são aceitos. None e a combinação X | O não representam
        /// um lance normal de colocação.
        /// </summary>
        public CellMark Mark { get; }

        /// <summary>
        /// Cria uma intenção de colocar uma marca.
        /// </summary>
        /// <param name="actor">
        /// Participante que pretende executar a ação.
        /// </param>
        /// <param name="origin">
        /// Sistema que produziu a intenção.
        /// </param>
        /// <param name="expectedTurnId">
        /// Turno para o qual a intenção foi criada.
        /// </param>
        /// <param name="expectedBoardVersion">
        /// Versão observada quando a intenção foi criada.
        /// </param>
        /// <param name="target">
        /// Coordenada que receberá a marca.
        ///
        /// Ela pode não existir no tabuleiro atual. Essa situação será uma
        /// rejeição normal do MoveService, e não um erro de construção da ação.
        /// </param>
        /// <param name="mark">
        /// Única marca que se pretende colocar.
        /// </param>
        public PlaceMarkAction(
            ScoreActor actor,
            GameActionOrigin origin,
            long expectedTurnId,
            long expectedBoardVersion,
            BoardCoordinate target,
            CellMark mark)
            : base(
                actor,
                GameActionType.PlaceMark,
                origin,
                expectedTurnId,
                expectedBoardVersion,
                target)
        {
            ValidateSingleMark(mark);
            Mark = mark;
        }

        private static void ValidateSingleMark(
            CellMark mark)
        {
            /*
             * CellMark utiliza Flags, portanto X | O é um valor válido para o
             * estado de uma casa. Porém, uma jogada normal coloca apenas uma
             * marca por vez.
             */
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mark),
                    mark,
                    "PlaceMarkAction precisa colocar exatamente X ou O.");
            }
        }

        /*
         * Exemplo futuro de criação pelo jogador:
         *
         * new PlaceMarkAction(
         *     ScoreActor.Player,
         *     GameActionOrigin.PlayerInput,
         *     currentTurn.TurnId,
         *     board.Version,
         *     clickedCoordinate,
         *     CellMark.X);
         *
         * Exemplo futuro de criação pela IA:
         *
         * new PlaceMarkAction(
         *     ScoreActor.Enemy,
         *     GameActionOrigin.AgentPolicy,
         *     currentTurn.TurnId,
         *     board.Version,
         *     chosenCoordinate,
         *     CellMark.O);
         *
         * Os dois objetos serão enviados ao mesmo ActionExecutor.
         */
    }
}