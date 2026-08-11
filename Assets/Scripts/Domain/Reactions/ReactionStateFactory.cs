using System;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Sequences;

namespace TicTacToeRoguelike.Domain.Reactions
{
    /// <summary>
    /// Produz um ReactionState a partir de uma recontagem completa do tabuleiro.
    ///
    /// Esta fábrica centraliza uma regra importante:
    /// as quantidades de sequências não são acumuladas a partir do snapshot
    /// anterior. Elas sempre são descobertas novamente no BoardState atual.
    ///
    /// Isso permite que futuras runas:
    /// - removam símbolos;
    /// - sobrescrevam casas;
    /// - criem novas sequências;
    /// - destruam sequências dos dois participantes simultaneamente.
    ///
    /// O futuro coordenador do encontro não deverá chamar o
    /// SequenceEvaluator separadamente para Player e Enemy. Ele solicitará
    /// apenas um novo snapshot a esta fábrica.
    /// </summary>
    public sealed class ReactionStateFactory
    {
        private readonly SequenceEvaluator _sequenceEvaluator;

        /// <summary>
        /// Cria a fábrica com o avaliador padrão.
        ///
        /// Este construtor é conveniente enquanto o projeto ainda não possui
        /// um ponto central para montar todos os serviços do domínio.
        /// </summary>
        public ReactionStateFactory()
            : this(new SequenceEvaluator())
        {
        }

        /// <summary>
        /// Permite fornecer o avaliador externamente.
        ///
        /// Essa forma deixa explícita a dependência usada pela fábrica e
        /// facilitará a composição dos serviços no futuro EncounterEngine.
        /// </summary>
        public ReactionStateFactory(
            SequenceEvaluator sequenceEvaluator)
        {
            _sequenceEvaluator = sequenceEvaluator ??
                throw new ArgumentNullException(
                    nameof(sequenceEvaluator));
        }

        /// <summary>
        /// Reconta as sequências de vitória dos dois participantes e cria
        /// uma fotografia correspondente à versão atual do tabuleiro.
        ///
        /// playerMark e enemyMark são recebidos explicitamente porque a
        /// arquitetura não deve presumir para sempre que Player será X e
        /// Enemy será O. Um encontro futuro poderá inverter essa associação.
        /// </summary>
        /// <param name="board">
        /// Tabuleiro atual que será avaliado.
        /// </param>
        /// <param name="playerMark">
        /// Símbolo controlado pelo Player neste encontro.
        /// </param>
        /// <param name="enemyMark">
        /// Símbolo controlado pelo Enemy neste encontro.
        /// </param>
        /// <param name="requiredSequenceLength">
        /// Tamanho exato exigido para uma condição de vitória.
        /// </param>
        public ReactionState Create(
            BoardState board,
            CellMark playerMark,
            CellMark enemyMark,
            int requiredSequenceLength)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            ValidateParticipantMark(
                playerMark,
                nameof(playerMark));

            ValidateParticipantMark(
                enemyMark,
                nameof(enemyMark));

            if (playerMark == enemyMark)
            {
                throw new ArgumentException(
                    "Player e Enemy precisam utilizar símbolos diferentes.",
                    nameof(enemyMark));
            }

            if (requiredSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredSequenceLength),
                    requiredSequenceLength,
                    "Uma sequência de vitória precisa possuir pelo menos " +
                    "duas casas.");
            }

            /*
             * Esta é uma única recontagem lógica do estado de reação.
             *
             * O SequenceEvaluator atual recebe um símbolo por chamada, então
             * precisamos consultá-lo uma vez para cada participante. Essas duas
             * consultas permanecem reunidas aqui para que nenhum controlador
             * duplique ou esqueça parte da operação.
             *
             * Não utilizamos o ReactionState anterior. Portanto, sequências
             * destruídas desaparecem naturalmente do novo snapshot.
             */
            int playerSequenceCount =
                _sequenceEvaluator
                    .EvaluateWinningSequences(
                        board,
                        playerMark,
                        requiredSequenceLength)
                    .Count;

            int enemySequenceCount =
                _sequenceEvaluator
                    .EvaluateWinningSequences(
                        board,
                        enemyMark,
                        requiredSequenceLength)
                    .Count;

            /*
             * SequenceEvaluator é somente leitura e não modifica BoardState.
             * Assim, Version identifica exatamente o tabuleiro usado para
             * produzir estas duas contagens.
             */
            return new ReactionState(
                playerSequenceCount,
                enemySequenceCount,
                board.Version);
        }

        private static void ValidateParticipantMark(
            CellMark mark,
            string parameterName)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentException(
                    "Um participante deve utilizar exatamente X ou O.",
                    parameterName);
            }
        }

        /*
         * Adições futuras:
         *
         * 1. Se SequenceEvaluator passar a produzir um resultado contendo
         *    simultaneamente as sequências de X e O, esta fábrica poderá
         *    reutilizar esse resultado sem mudar o contrato de ReactionState.
         *
         * 2. Regras de encontro poderão fornecer condições diferentes para
         *    cada participante. Nesse caso, a configuração deverá ser recebida
         *    explicitamente, sem consultar componentes da interface.
         *
         * 3. Um cache poderá reutilizar uma recontagem enquanto BoardVersion
         *    não mudar. O cache deverá pertencer ao domínio ou à aplicação,
         *    nunca à apresentação.
         *
         * 4. Padrões de vitória não lineares poderão ser fornecidos por outro
         *    avaliador, mantendo esta fábrica como ponto único de criação do
         *    snapshot de reação.
         */
    }
}