using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Turns
{
    /// <summary>
    /// Fotografia imutável de um Turno encerrado.
    ///
    /// Diferentemente de TurnContext, este objeto nunca muda. Depois de criado,
    /// ele pode ser entregue com segurança para:
    ///
    /// - o futuro EncounterEngine;
    /// - a avaliação de reação;
    /// - logs e replays;
    /// - IA;
    /// - apresentação.
    ///
    /// Como todos os dados necessários estão armazenados aqui, esses sistemas
    /// não precisam consultar um TurnContext que talvez já tenha sido
    /// substituído pelo Turno seguinte.
    /// </summary>
    public sealed class TurnCompletion
    {
        /// <summary>
        /// Identidade do Turno encerrado.
        ///
        /// Dois Turnos consecutivos do mesmo participante possuem TurnId
        /// diferentes e, portanto, continuam sendo oportunidades separadas.
        /// </summary>
        public long TurnId { get; }

        /// <summary>
        /// Participante que possuía o Turno.
        /// </summary>
        public ScoreActor Actor { get; }

        /// <summary>
        /// Quantidade máxima de ações permitidas no Turno.
        /// </summary>
        public int ActionBudget { get; }

        /// <summary>
        /// Quantidade de ações realmente aplicadas.
        ///
        /// Tentativas rejeitadas não aparecem nesta contagem porque não são
        /// registradas pelo TurnContext.
        /// </summary>
        public int ActionsConsumed { get; }

        /// <summary>
        /// Versão do tabuleiro quando o Turno foi aberto.
        /// </summary>
        public long InitialBoardVersion { get; }

        /// <summary>
        /// Versão conhecida quando o Turno foi encerrado.
        ///
        /// Ela pode ser igual à versão inicial, pois uma ação futura poderá
        /// modificar pontuação, runas ou outro estado sem alterar o tabuleiro.
        /// </summary>
        public long FinalBoardVersion { get; }

        /// <summary>
        /// Motivo pelo qual o Turno terminou.
        /// </summary>
        public TurnEndReason EndReason { get; }

        /// <summary>
        /// Informa se pelo menos uma ação foi realmente aplicada.
        /// </summary>
        public bool HasPlayedAction =>
            ActionsConsumed > 0;

        /// <summary>
        /// Informa que o Turno terminou porque consumiu todo o orçamento.
        /// </summary>
        public bool EndedByActionBudget =>
            EndReason == TurnEndReason.ActionBudgetConsumed;

        /// <summary>
        /// Informa que o participante foi obrigado a pular o Turno.
        /// </summary>
        public bool WasSkipped =>
            EndReason == TurnEndReason.Skipped;

        /// <summary>
        /// Informa que o Turno foi cancelado por invalidação do fluxo.
        /// </summary>
        public bool WasCancelled =>
            EndReason == TurnEndReason.Cancelled;

        /// <summary>
        /// Somente um Turno encerrado pelo consumo do orçamento e que executou
        /// uma ação real pode alimentar a ReactionRule.
        ///
        /// Essa propriedade impede que Turnos pulados ou cancelados sejam
        /// interpretados como tentativas fracassadas de reação.
        /// </summary>
        public bool CanEvaluateReaction =>
            EndedByActionBudget &&
            HasPlayedAction;

        private TurnCompletion(
            long turnId,
            ScoreActor actor,
            int actionBudget,
            int actionsConsumed,
            long initialBoardVersion,
            long finalBoardVersion,
            TurnEndReason endReason)
        {
            TurnId = turnId;
            Actor = actor;
            ActionBudget = actionBudget;
            ActionsConsumed = actionsConsumed;
            InitialBoardVersion = initialBoardVersion;
            FinalBoardVersion = finalBoardVersion;
            EndReason = endReason;
        }

        /// <summary>
        /// Cria uma fotografia imutável de um TurnContext encerrado.
        ///
        /// Um contexto ainda aberto não possui conclusão e será rejeitado.
        /// O método apenas lê o contexto: não altera seu estado e não escolhe
        /// o próximo participante.
        /// </summary>
        /// <param name="turnContext">
        /// Contexto encerrado que será transformado em relatório.
        /// </param>
        public static TurnCompletion CreateFrom(
            TurnContext turnContext)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(
                    nameof(turnContext));
            }

            if (!turnContext.IsClosed)
            {
                throw new InvalidOperationException(
                    $"O Turno {turnContext.TurnId} ainda está aberto e não " +
                    "possui uma conclusão.");
            }

            TurnEndReason endReason =
                GetEndReason(turnContext.Status);

            return new TurnCompletion(
                turnContext.TurnId,
                turnContext.Actor,
                turnContext.ActionBudget,
                turnContext.ActionsConsumed,
                turnContext.InitialBoardVersion,
                turnContext.CurrentBoardVersion,
                endReason);
        }

        private static TurnEndReason GetEndReason(
            TurnStatus status)
        {
            switch (status)
            {
                case TurnStatus.Completed:
                    return TurnEndReason.ActionBudgetConsumed;

                case TurnStatus.Skipped:
                    return TurnEndReason.Skipped;

                case TurnStatus.Cancelled:
                    return TurnEndReason.Cancelled;

                case TurnStatus.Open:
                    throw new InvalidOperationException(
                        "Um Turno aberto não possui motivo de encerramento.");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "O estado informado não representa um Turno conhecido.");
            }
        }

        /*
         * Adições futuras:
         *
         * 1. O relatório poderá armazenar identificadores das ações aplicadas
         *    quando GameAction e ActionResult existirem.
         *
         * 2. Logs e replays poderão guardar TurnCompletion sem depender do
         *    TurnContext mutável.
         *
         * 3. O futuro EncounterEngine deverá criar no máximo uma conclusão
         *    autoritativa para cada TurnId e avaliar a reação uma única vez.
         *
         * 4. Não devemos adicionar horário do sistema neste objeto. Replays e
         *    simulações precisam continuar determinísticos.
         */
    }
}