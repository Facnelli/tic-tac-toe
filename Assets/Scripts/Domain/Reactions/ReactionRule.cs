using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Reactions
{
    /// <summary>
    /// Decide o resultado da fase de reação ao final de uma rodada de ações.
    ///
    /// Esta classe é uma regra de domínio pura:
    ///
    /// - não conhece MonoBehaviour, GameObject, textos ou animações;
    /// - não conta sequências diretamente;
    /// - não escolhe nem executa o próximo turno;
    /// - não altera ReactionState;
    /// - não aplica multiplicadores de vitória.
    ///
    /// Antes de chamar Evaluate, o controlador deverá aplicar todas as ações da
    /// rodada, recontar as sequências existentes no tabuleiro e criar o novo
    /// ReactionState. A regra compara essa fotografia com a anterior e devolve
    /// uma ReactionDecision autoritativa.
    ///
    /// IMPORTANTE SOBRE A PALAVRA "RODADA"
    ///
    /// Aqui, rodada de ações é a oportunidade completa de um participante. Uma
    /// runa poderá permitir duas ações dentro dessa mesma rodada; nesse caso,
    /// Evaluate deve ser chamado somente depois da segunda ação. Se uma runa
    /// conceder duas rodadas consecutivas, Evaluate deverá ser chamado ao final
    /// de cada uma delas.
    /// </summary>
    public sealed class ReactionRule
    {
        /// <summary>
        /// Avalia a transição ocorrida ao final de uma rodada de ações.
        /// </summary>
        /// <param name="previousState">
        /// Estado de reação válido antes do início da rodada que terminou.
        /// </param>
        /// <param name="currentState">
        /// Nova recontagem completa, produzida depois que todas as ações dessa
        /// rodada modificaram o tabuleiro.
        /// </param>
        /// <param name="completedRoundActor">
        /// Participante que realmente concluiu a rodada de ações.
        ///
        /// Um turno pulado não deve chamar este método. Pular o turno do
        /// participante que precisa reagir apenas adia sua oportunidade; não
        /// significa que ele tentou reagir e falhou.
        /// </param>
        /// <param name="hasAvailableActions">
        /// Informa se ainda existe pelo menos uma ação capaz de continuar a
        /// partida/tabuleiro depois da recontagem.
        ///
        /// Atualmente essa consulta poderá considerar somente jogadas normais.
        /// Futuramente também deverá considerar runas capazes de limpar, trocar
        /// ou modificar casas, inclusive em um tabuleiro completamente cheio.
        /// </param>
        /// <returns>
        /// Decisão imutável que informa se a disputa continua, se a reação mudou
        /// de estado ou se a partida no tabuleiro terminou.
        /// </returns>
        public ReactionDecision Evaluate(
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor,
            bool hasAvailableActions)
        {
            ValidateInputs(
                previousState,
                currentState,
                completedRoundActor);

            /*
             * A ausência de ações possui prioridade porque encerra a disputa
             * independentemente da transição que acabou de ocorrer no placar.
             *
             * Exemplos:
             * - tabuleiro termina cheio em 2 x 2: empate sem multiplicador;
             * - tabuleiro termina cheio em 2 x 1: vitória do líder;
             * - tabuleiro cheio, mas uma runa ainda pode alterar uma casa:
             *   hasAvailableActions será true e a disputa não termina por aqui.
             *
             * Escolher essa prioridade também conserva um motivo de encerramento
             * preciso no relatório: NoAvailableActions, em vez de obrigar o
             * EncounterController a deduzir novamente que o tabuleiro acabou.
             */
            if (!hasAvailableActions)
            {
                return CreateNoAvailableActionsDecision(
                    previousState,
                    currentState,
                    completedRoundActor);
            }

            /*
             * Se antes da rodada não havia líder, estávamos no estado normal.
             * A nova recontagem pode apenas:
             *
             * 1. continuar empatada; ou
             * 2. deixar alguém à frente e iniciar uma reação.
             */
            if (previousState.IsTied)
            {
                ReactionDecisionKind kind = currentState.IsTied
                    ? ReactionDecisionKind.ContinueNormal
                    : ReactionDecisionKind.ReactionStarted;

                return CreateContinuingDecision(
                    kind,
                    previousState,
                    currentState,
                    completedRoundActor);
            }

            /*
             * A partir daqui já existia uma reação pendente antes da rodada.
             * Empatar sempre resolve a reação e devolve a partida ao fluxo
             * normal, independentemente de quem causou a alteração. Esse cuidado
             * é útil para futuros efeitos que também possam remover sequências do
             * próprio participante.
             */
            if (currentState.IsTied)
            {
                return CreateContinuingDecision(
                    ReactionDecisionKind.ReactionResolvedByTie,
                    previousState,
                    currentState,
                    completedRoundActor);
            }

            bool responderCompletedRound =
                completedRoundActor == previousState.PendingResponder;

            bool previousLeaderRemainedAhead =
                currentState.Leader == previousState.Leader;

            /*
             * O participante obrigado a reagir gastou sua oportunidade padrão e
             * terminou ainda atrás. A partida no tabuleiro acaba imediatamente,
             * mesmo que ainda existam casas ou outras ações comuns disponíveis.
             *
             * Duas ações na MESMA rodada não chegam aqui entre uma ação e outra,
             * porque Evaluate só deve ser chamado quando a rodada inteira acabar.
             * Já uma segunda rodada consecutiva não salva o reagente: a primeira
             * rodada é avaliada e a derrota já foi decidida.
             */
            if (responderCompletedRound &&
                previousLeaderRemainedAhead)
            {
                return new ReactionDecision(
                    ReactionDecisionKind.Victory,
                    ReactionEndReason.ResponderRemainedBehind,
                    previousState,
                    currentState,
                    completedRoundActor,
                    hasAvailableActions);
            }

            /*
             * Uma liderança diferente da anterior transfere a reação. O antigo
             * líder agora está atrás e passa a ser o novo PendingResponder.
             *
             * O caso comum é o participante que estava reagindo virar o placar.
             * A condição também permanece correta para futuros efeitos capazes de
             * mudar os dois lados do tabuleiro durante a rodada do líder.
             */
            if (!previousLeaderRemainedAhead)
            {
                return CreateContinuingDecision(
                    ReactionDecisionKind.ReactionTransferred,
                    previousState,
                    currentState,
                    completedRoundActor);
            }

            /*
             * O mesmo líder continuou à frente, mas quem acabou de jogar não era
             * o participante obrigado a reagir. Isso representa principalmente
             * uma rodada extra do líder ou um turno adversário pulado.
             *
             * A vantagem pode aumentar, diminuir ou permanecer igual. Enquanto o
             * líder não mudar, o direito de reação do adversário continua intacto.
             */
            return CreateContinuingDecision(
                ReactionDecisionKind.ReactionMaintained,
                previousState,
                currentState,
                completedRoundActor);
        }

        /// <summary>
        /// Produz vitória ou empate terminal quando não há ações disponíveis.
        /// </summary>
        private static ReactionDecision CreateNoAvailableActionsDecision(
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor)
        {
            ReactionDecisionKind kind = currentState.IsTied
                ? ReactionDecisionKind.DrawNoActions
                : ReactionDecisionKind.Victory;

            return new ReactionDecision(
                kind,
                ReactionEndReason.NoAvailableActions,
                previousState,
                currentState,
                completedRoundActor,
                hasAvailableActions: false);
        }

        /// <summary>
        /// Centraliza a criação das decisões que permitem outra rodada.
        /// Todas elas usam EndReason.None e exigem ações disponíveis.
        /// </summary>
        private static ReactionDecision CreateContinuingDecision(
            ReactionDecisionKind kind,
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor)
        {
            return new ReactionDecision(
                kind,
                ReactionEndReason.None,
                previousState,
                currentState,
                completedRoundActor,
                hasAvailableActions: true);
        }

        /// <summary>
        /// Valida a fronteira pública da regra antes de interpretar os estados.
        /// </summary>
        private static void ValidateInputs(
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor)
        {
            if (previousState == null)
            {
                throw new ArgumentNullException(nameof(previousState));
            }

            if (currentState == null)
            {
                throw new ArgumentNullException(nameof(currentState));
            }

            if (completedRoundActor != ScoreActor.Player &&
                completedRoundActor != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedRoundActor),
                    completedRoundActor,
                    "A regra de reação aceita somente uma rodada concluída " +
                    "por Player ou Enemy.");
            }

            if (currentState.BoardVersion < previousState.BoardVersion)
            {
                throw new ArgumentException(
                    "O estado atual da reação não pode pertencer a uma versão " +
                    "do tabuleiro anterior à versão do estado precedente.",
                    nameof(currentState));
            }
        }

        /*
         * ADIÇÕES FUTURAS POSSÍVEIS
         *
         * 1. A consulta hasAvailableActions poderá ser substituída por um
         *    ActionAvailabilityResult. Além de true/false, esse relatório poderá
         *    explicar quais jogadas normais, runas ou habilidades ainda permitem
         *    modificar a disputa.
         *
         * 2. Se uma runa conceder mais de uma OPORTUNIDADE DE REAÇÃO, e não apenas
         *    mais ações dentro da mesma rodada, ReactionState poderá guardar o
         *    orçamento restante. A regra deverá consumi-lo antes de declarar
         *    ResponderRemainedBehind.
         *
         * 3. Efeitos que encerram diretamente a partida poderão acrescentar um
         *    ReactionEndReason próprio e entrar antes das regras comuns. Essa
         *    decisão deverá continuar no domínio, nunca na animação.
         *
         * 4. Para partidas com mais de dois participantes, a noção de Leader e
         *    PendingResponder precisará deixar de ser binária. A API atual foi
         *    intencionalmente mantida simples porque o encontro possui apenas
         *    Player e Enemy.
         */
    }
}