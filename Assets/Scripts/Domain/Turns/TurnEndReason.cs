namespace TicTacToeRoguelike.Domain.Turns
{
    /// <summary>
    /// Explica por que um Turno chegou ao fim.
    ///
    /// O motivo é armazenado no domínio para impedir que controladores,
    /// interface ou IA tentem deduzi-lo novamente observando apenas o
    /// tabuleiro.
    /// </summary>
    public enum TurnEndReason
    {
        /// <summary>
        /// Não existe um encerramento válido.
        ///
        /// Este valor protege contra o valor padrão do enum. Um
        /// TurnCompletion nunca será criado com este motivo.
        /// </summary>
        None = 0,

        /// <summary>
        /// Todas as ações disponíveis no orçamento foram aplicadas.
        ///
        /// Este é o único encerramento que representa um Turno realmente
        /// jogado e que poderá alimentar a avaliação da ReactionRule.
        /// </summary>
        ActionBudgetConsumed = 1,

        /// <summary>
        /// Uma regra obrigou o participante a perder sua oportunidade antes
        /// de executar qualquer ação.
        ///
        /// Um Turno pulado não representa uma tentativa fracassada de reação.
        /// </summary>
        Skipped = 2,

        /// <summary>
        /// O Turno foi abandonado antes de executar qualquer ação autoritativa,
        /// por exemplo durante reinício ou invalidação do fluxo.
        /// </summary>
        Cancelled = 3
    }
}