using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Application.Actions
{
    /// <summary>
    /// Contexto somente para leitura entregue a um provedor de ações especiais.
    ///
    /// O provedor pode consultar o tabuleiro e os dados do ator para descobrir se
    /// possui alguma ação capaz de alterar o estado. Ele não pode modificar o
    /// BoardState durante esta consulta.
    ///
    /// Uma futura implementação poderá representar:
    ///
    /// - ações oferecidas pelas runas do Player;
    /// - ações oferecidas pelas runas do Enemy;
    /// - habilidades próprias de um boss;
    /// - regras especiais de determinado encontro.
    /// </summary>
    public sealed class ActionAvailabilityContext
    {
        public BoardState Board { get; }
        public ScoreActor Actor { get; }

        /// <summary>
        /// Marca usada pelo ator em jogadas normais.
        ///
        /// Player utiliza X e Enemy utiliza O no encontro atual.
        /// </summary>
        public CellMark NormalMoveMark { get; }

        /// <summary>
        /// Versão observada antes de consultar os provedores.
        /// </summary>
        public long BoardVersion { get; }

        internal ActionAvailabilityContext(
            BoardState board,
            ScoreActor actor,
            CellMark normalMoveMark)
        {
            Board = board ??
                throw new ArgumentNullException(nameof(board));

            Actor = actor;
            NormalMoveMark = normalMoveMark;
            BoardVersion = board.Version;
        }
    }

    /// <summary>
    /// Contrato de extensão para qualquer sistema que possa oferecer uma ação
    /// diferente de uma jogada normal.
    ///
    /// A implementação responde somente se possui ao menos uma ação disponível.
    /// Ela não executa a ação, não consome o TurnContext e não altera o tabuleiro.
    ///
    /// O nome não menciona runas de propósito: runas serão a primeira grande
    /// origem de ações especiais, mas bosses e regras de encontro também poderão
    /// utilizar o mesmo contrato.
    /// </summary>
    public interface IActionAvailabilityProvider
    {
        bool HasAvailableAction(
            ActionAvailabilityContext context);
    }

    /// <summary>
    /// Unifica a pergunta sobre disponibilidade de ações.
    ///
    /// O serviço combina:
    ///
    /// 1. jogadas normais encontradas pelo MoveValidator;
    /// 2. provedores extensíveis de ações especiais.
    ///
    /// O fluxo do encontro consultará este serviço em vez de decidir que um
    /// tabuleiro cheio sempre encerra a Rodada. Se uma runa ainda puder limpar,
    /// inverter ou sobrescrever casas, HasAvailableActions continuará verdadeiro.
    ///
    /// Esta classe pertence a Application porque coordena uma regra existente do
    /// domínio com provedores que futuramente dependerão da composição do encontro.
    /// </summary>
    public sealed class ActionAvailabilityService
    {
        private const CellMark PlayerMark =
            CellMark.X;

        private const CellMark EnemyMark =
            CellMark.O;

        private readonly MoveValidator _moveValidator;

        private readonly ReadOnlyCollection<IActionAvailabilityProvider>
            _specialActionProviders;

        /// <summary>
        /// Cria o serviço padrão sem ações especiais registradas.
        ///
        /// Enquanto o núcleo de runas ainda não existe, a disponibilidade será
        /// determinada somente pelas jogadas normais.
        /// </summary>
        public ActionAvailabilityService()
            : this(
                new MoveValidator(),
                Array.Empty<IActionAvailabilityProvider>())
        {
        }

        /// <summary>
        /// Cria o serviço com os provedores especiais do encontro.
        /// </summary>
        public ActionAvailabilityService(
            IEnumerable<IActionAvailabilityProvider>
                specialActionProviders)
            : this(
                new MoveValidator(),
                specialActionProviders)
        {
        }

        /// <summary>
        /// Cria o serviço com todas as dependências fornecidas externamente.
        ///
        /// Os provedores são copiados para uma coleção somente para leitura. Isso
        /// impede que outra parte do programa altere silenciosamente o conjunto de
        /// regras depois que o serviço foi composto para um encontro.
        /// </summary>
        public ActionAvailabilityService(
            MoveValidator moveValidator,
            IEnumerable<IActionAvailabilityProvider>
                specialActionProviders)
        {
            _moveValidator = moveValidator ??
                throw new ArgumentNullException(
                    nameof(moveValidator));

            if (specialActionProviders == null)
            {
                throw new ArgumentNullException(
                    nameof(specialActionProviders));
            }

            List<IActionAvailabilityProvider> providerSnapshot =
                new List<IActionAvailabilityProvider>();

            foreach (IActionAvailabilityProvider provider
                     in specialActionProviders)
            {
                if (provider == null)
                {
                    throw new ArgumentException(
                        "A coleção de provedores não pode conter um item nulo.",
                        nameof(specialActionProviders));
                }

                providerSnapshot.Add(provider);
            }

            _specialActionProviders =
                new ReadOnlyCollection<IActionAvailabilityProvider>(
                    providerSnapshot);
        }

        /// <summary>
        /// Produz uma fotografia completa da disponibilidade do ator.
        ///
        /// A consulta não exige um TurnContext aberto. Ela será usada justamente
        /// entre oportunidades, quando o fluxo precisar decidir se abrirá o próximo
        /// Turno ou encerrará a Rodada.
        /// </summary>
        public ActionAvailabilityReport Evaluate(
            BoardState board,
            ScoreActor actor)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            CellMark normalMoveMark =
                ResolveNormalMoveMark(actor);

            long boardVersionBefore =
                board.Version;

            IReadOnlyList<BoardCoordinate> legalNormalMoves =
                _moveValidator.GetLegalNormalMoves(
                    board,
                    normalMoveMark);

            EnsureBoardWasNotChanged(
                board,
                boardVersionBefore,
                "MoveValidator");

            ActionAvailabilityContext context =
                new ActionAvailabilityContext(
                    board,
                    actor,
                    normalMoveMark);

            int availableSpecialProviderCount = 0;

            for (int index = 0;
                 index < _specialActionProviders.Count;
                 index++)
            {
                IActionAvailabilityProvider provider =
                    _specialActionProviders[index];

                bool hasAvailableAction =
                    provider.HasAvailableAction(context);

                /*
                 * Disponibilidade é uma consulta. Um provedor que altere o estado
                 * tornaria as coordenadas normais já calculadas obsoletas e poderia
                 * causar resultados diferentes apenas pela ordem de enumeração.
                 */
                EnsureBoardWasNotChanged(
                    board,
                    boardVersionBefore,
                    provider.GetType().FullName);

                if (hasAvailableAction)
                {
                    availableSpecialProviderCount++;
                }
            }

            return new ActionAvailabilityReport(
                actor,
                boardVersionBefore,
                legalNormalMoves,
                availableSpecialProviderCount);
        }

        /// <summary>
        /// Atalho para chamadores que precisam somente da resposta reduzida.
        ///
        /// O futuro EncounterEngine usará este valor ao chamar ReactionRule.
        /// IA e apresentação poderão continuar utilizando Evaluate quando
        /// precisarem conhecer as coordenadas normais.
        /// </summary>
        public bool HasAvailableActions(
            BoardState board,
            ScoreActor actor)
        {
            return Evaluate(
                board,
                actor).HasAvailableActions;
        }

        private static CellMark ResolveNormalMoveMark(
            ScoreActor actor)
        {
            switch (actor)
            {
                case ScoreActor.Player:
                    return PlayerMark;

                case ScoreActor.Enemy:
                    return EnemyMark;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(actor),
                        actor,
                        "A disponibilidade somente pode ser calculada para Player ou Enemy.");
            }
        }

        private static void EnsureBoardWasNotChanged(
            BoardState board,
            long expectedVersion,
            string consultedComponent)
        {
            if (board.Version == expectedVersion)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{consultedComponent} alterou o BoardState durante uma " +
                "consulta de disponibilidade. Provedores devem somente " +
                "consultar o estado.");
        }

        /*
         * Integrações futuras:
         *
         * 1. Um provedor de runas poderá guardar uma coleção somente para leitura
         *    das runas pertencentes ao ator e responder se alguma consegue criar
         *    uma ação concreta no contexto atual.
         *
         * 2. IActionAvailabilityProvider não aplica GameAction. A aplicação
         *    continuará passando pelo ActionExecutor e pelo serviço autorizado.
         *
         * 3. No Marco 5, EncounterEngine produzirá o relatório para o participante
         *    que receberia a próxima oportunidade e entregará apenas
         *    HasAvailableActions à ReactionRule.
         *
         * 4. EncounterController e UI não devem somar provedores nem consultar
         *    GetLegalNormalMoves para decidir o encerramento da Rodada.
         *
         * 5. No Marco 8, novos provedores serão adicionados por composição. Não
         *    será necessário adicionar condicionais por tipo de runa neste serviço.
         *
         * 6. Esta classe não deve receber MonoBehaviour, GameObject, Coroutine,
         *    ScriptableObject ou outros tipos pertencentes à Unity.
         */
    }
}