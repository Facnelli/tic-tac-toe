using TicTacToeRoguelike.Domain.Actions;

namespace TicTacToeRoguelike.Application.AI
{
    /// <summary>
    /// Contrato comum para qualquer sistema que escolha ações automaticamente.
    ///
    /// Uma política pode representar:
    ///
    /// - a IA básica de um inimigo comum;
    /// - uma estratégia própria de boss;
    /// - um jogador automático usado em testes e simulações;
    /// - uma política futura que considere runas e efeitos.
    ///
    /// A política apenas escolhe uma intenção que já existe em LegalActions.
    /// Ela não modifica o BoardState autoritativo, não consome TurnContext, não
    /// troca Turnos e não decide o resultado da reação. A ação escolhida ainda
    /// deverá passar pelo mesmo ActionExecutor usado pelo clique do Player.
    /// </summary>
    public interface IAgentPolicy
    {
        /// <summary>
        /// Tenta escolher uma das ações legais do contexto recebido.
        ///
        /// Retorna true e preenche action quando existe uma escolha. Retorna
        /// false e deixa action como null quando a política não possui ação para
        /// oferecer. A ausência de escolha não deve ser representada por uma ação
        /// inventada nem por uma alteração direta do tabuleiro.
        ///
        /// Quando retornar true, a implementação deve devolver a própria instância
        /// presente em context.LegalActions. Essa regra permite confirmar a origem
        /// da escolha por referência, sem criar uma igualdade genérica frágil para
        /// todas as futuras categorias de GameAction.
        /// </summary>
        bool TryChooseAction(
            AgentDecisionContext context,
            out GameAction action);
    }
}