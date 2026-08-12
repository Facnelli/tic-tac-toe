using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Descreve, de forma imutável, quais famílias de ação ainda estão
    /// disponíveis para um participante.
    ///
    /// Este relatório mantém duas informações separadas:
    ///
    /// - as coordenadas que aceitam uma jogada normal;
    /// - quantos provedores de ações especiais informaram possuir ao menos
    ///   uma ação disponível.
    ///
    /// A separação será útil para IA, interface, logs e testes. A ReactionRule,
    /// porém, não precisa conhecer nenhuma dessas categorias: ela receberá
    /// somente HasAvailableActions.
    ///
    /// O relatório é uma fotografia. Alterar posteriormente a lista usada para
    /// criá-lo não modifica LegalNormalMoves.
    /// </summary>
    public sealed class ActionAvailabilityReport
    {
        private readonly ReadOnlyCollection<BoardCoordinate>
            _legalNormalMoves;

        /// <summary>
        /// Participante para o qual a disponibilidade foi calculada.
        /// </summary>
        public ScoreActor Actor { get; }

        /// <summary>
        /// Versão do BoardState observada durante toda a consulta.
        ///
        /// O serviço de disponibilidade garante que nenhum provedor alterou o
        /// tabuleiro enquanto o relatório era produzido.
        /// </summary>
        public long BoardVersion { get; }

        /// <summary>
        /// Coordenadas nas quais o ator pode realizar uma jogada normal.
        ///
        /// A ordem é a mesma ordem determinística de BoardDefinition.Cells.
        /// </summary>
        public IReadOnlyList<BoardCoordinate> LegalNormalMoves =>
            _legalNormalMoves;

        /// <summary>
        /// Quantidade de provedores especiais que possuem ao menos uma ação.
        ///
        /// Este número não é a quantidade total de alvos ou ativações. Um único
        /// provedor pode futuramente oferecer várias ações concretas.
        /// </summary>
        public int AvailableSpecialActionProviderCount { get; }

        public int LegalNormalMoveCount =>
            _legalNormalMoves.Count;

        public bool HasAvailableNormalMoves =>
            LegalNormalMoveCount > 0;

        public bool HasAvailableSpecialActions =>
            AvailableSpecialActionProviderCount > 0;

        /// <summary>
        /// Resposta reduzida que o fluxo do encontro entregará à ReactionRule.
        ///
        /// Assim, a regra de reação não conhece MoveValidator, runas, inventário,
        /// habilidades de boss ou qualquer categoria concreta de GameAction.
        /// </summary>
        public bool HasAvailableActions =>
            HasAvailableNormalMoves ||
            HasAvailableSpecialActions;

        public ActionAvailabilityReport(
            ScoreActor actor,
            long boardVersion,
            IEnumerable<BoardCoordinate> legalNormalMoves,
            int availableSpecialActionProviderCount)
        {
            ValidateActor(actor);

            if (boardVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boardVersion),
                    boardVersion,
                    "A versão do tabuleiro não pode ser negativa.");
            }

            if (legalNormalMoves == null)
            {
                throw new ArgumentNullException(
                    nameof(legalNormalMoves));
            }

            if (availableSpecialActionProviderCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableSpecialActionProviderCount),
                    availableSpecialActionProviderCount,
                    "A quantidade de provedores disponíveis não pode ser negativa.");
            }

            List<BoardCoordinate> normalMoveSnapshot =
                new List<BoardCoordinate>();

            HashSet<BoardCoordinate> uniqueCoordinates =
                new HashSet<BoardCoordinate>();

            foreach (BoardCoordinate coordinate in legalNormalMoves)
            {
                /*
                 * Uma coordenada duplicada inflaria LegalNormalMoveCount e faria
                 * o relatório deixar de representar corretamente as opções reais.
                 * MoveValidator já não produz duplicatas, mas esta validação também
                 * protege usos diretos do construtor.
                 */
                if (!uniqueCoordinates.Add(coordinate))
                {
                    throw new ArgumentException(
                        $"A coordenada {coordinate} foi informada mais de uma vez.",
                        nameof(legalNormalMoves));
                }

                normalMoveSnapshot.Add(coordinate);
            }

            Actor = actor;
            BoardVersion = boardVersion;
            AvailableSpecialActionProviderCount =
                availableSpecialActionProviderCount;

            _legalNormalMoves =
                new ReadOnlyCollection<BoardCoordinate>(
                    normalMoveSnapshot);
        }

        private static void ValidateActor(
            ScoreActor actor)
        {
            if (actor != ScoreActor.Player &&
                actor != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actor),
                    actor,
                    "A disponibilidade somente pode ser calculada para Player ou Enemy.");
            }
        }

        /*
         * Adições futuras:
         *
         * 1. O relatório poderá receber identificadores estáveis das categorias
         *    disponíveis quando logs, replays ou HUD precisarem explicá-las.
         *
         * 2. Não devemos adicionar referências a runas concretas neste tipo.
         *    Informações próprias de uma ação continuarão pertencendo ao seu
         *    provedor e à futura GameAction concreta.
         *
         * 3. Este tipo não deve receber GameObject, Sprite, ScriptableObject ou
         *    qualquer outro tipo pertencente à Unity.
         */
    }
}