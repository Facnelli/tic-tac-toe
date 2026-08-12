using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.AI
{
    /// <summary>
    /// Fotografia completa e somente para leitura entregue a uma IAgentPolicy.
    ///
    /// O contexto separa dois mundos:
    ///
    /// - EncounterEngine conserva o estado autoritativo do encontro;
    /// - a política recebe cópias nas quais pode pesquisar e simular com segurança.
    ///
    /// BoardSnapshot e TurnSnapshot são cópias independentes. ReactionState e
    /// GameAction já são objetos imutáveis e, por isso, podem ser compartilhados
    /// com segurança. A coleção de ações é copiada e exposta somente para leitura.
    ///
    /// A geração, o TurnId e a BoardVersion formam três proteções complementares
    /// contra uma resposta atrasada. Mesmo assim, a decisão escolhida sempre deve
    /// passar por ActionExecutor: o contexto nunca autoriza aplicação direta.
    /// </summary>
    public sealed class AgentDecisionContext
    {
        private readonly ReadOnlyCollection<GameAction>
            _legalActions;

        /// <summary>
        /// Cópia independente do tabuleiro no instante da solicitação.
        /// Simulações da IA devem partir desta instância.
        /// </summary>
        public BoardState BoardSnapshot { get; }

        /// <summary>
        /// Cópia independente do TurnContext aberto.
        ///
        /// O snapshot conserva identidade, ator, orçamento, ações consumidas e
        /// versões. Alterar o TurnContext autoritativo depois da criação não muda
        /// estes dados.
        /// </summary>
        public TurnContext TurnSnapshot { get; }

        /// <summary>
        /// Estado de reação confirmado antes da oportunidade atual.
        ///
        /// Em um Turno com várias ações, BoardSnapshot pode estar numa versão
        /// posterior, enquanto ReactionState continua apontando para a versão do
        /// começo do Turno. A reação será recontada somente quando o orçamento
        /// inteiro terminar.
        /// </summary>
        public ReactionState ReactionState { get; }

        /// <summary>
        /// Tamanho necessário para as sequências que participam da reação.
        /// </summary>
        public int RequiredSequenceLength { get; }

        /// <summary>
        /// Geração lógica fornecida pelo adaptador que solicitou a decisão.
        /// Uma coroutine ou tarefa antiga poderá comparar esse número antes de
        /// tentar entregar seu resultado ao motor.
        /// </summary>
        public int EncounterGeneration { get; }

        /// <summary>
        /// Ações concretas disponíveis para escolha nesta fotografia.
        ///
        /// Hoje elas normalmente são PlaceMarkAction. No futuro, a mesma coleção
        /// poderá conter ações produzidas por runas e habilidades de boss, sem
        /// alterar IAgentPolicy.
        /// </summary>
        public IReadOnlyList<GameAction> LegalActions =>
            _legalActions;

        public ScoreActor Actor =>
            TurnSnapshot.Actor;

        public long TurnId =>
            TurnSnapshot.TurnId;

        public long BoardVersion =>
            BoardSnapshot.Version;

        public int RemainingActions =>
            TurnSnapshot.RemainingActions;

        public bool HasLegalActions =>
            _legalActions.Count > 0;

        public bool IsReactionActive =>
            ReactionState.IsReactionActive;

        public bool ActorMustReact =>
            ReactionState.PendingResponder == Actor;

        public bool ActorLeadsReaction =>
            ReactionState.Leader == Actor;

        public CellMark ControlledMark =>
            GetMark(Actor);

        public CellMark OpponentMark =>
            Actor == ScoreActor.Player
                ? CellMark.O
                : CellMark.X;

        /// <summary>
        /// Cria uma fotografia a partir dos objetos autoritativos atuais.
        ///
        /// legalActions deve conter intenções já materializadas para exatamente o
        /// ator, TurnId e BoardVersion informados. O construtor valida esses fatos,
        /// mas não aplica as ações nem repete a regra específica de cada categoria.
        /// </summary>
        public AgentDecisionContext(
            BoardState board,
            TurnContext turnContext,
            ReactionState reactionState,
            int requiredSequenceLength,
            IEnumerable<GameAction> legalActions,
            int encounterGeneration)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (!turnContext.IsOpen)
            {
                throw new ArgumentException(
                    "A decisão da IA exige um TurnContext aberto.",
                    nameof(turnContext));
            }

            if (reactionState == null)
            {
                throw new ArgumentNullException(nameof(reactionState));
            }

            if (requiredSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredSequenceLength),
                    requiredSequenceLength,
                    "A condição de sequência deve possuir tamanho mínimo 2.");
            }

            if (legalActions == null)
            {
                throw new ArgumentNullException(nameof(legalActions));
            }

            if (encounterGeneration < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(encounterGeneration),
                    encounterGeneration,
                    "A geração do encontro não pode ser negativa.");
            }

            ValidateSynchronizedState(
                board,
                turnContext,
                reactionState);

            List<GameAction> actionSnapshot =
                CopyAndValidateActions(
                    legalActions,
                    turnContext,
                    board.Version);

            BoardSnapshot = board.Clone();
            TurnSnapshot = CloneOpenTurn(turnContext);

            // ReactionState é completamente imutável. Criar uma segunda instância
            // não acrescentaria isolamento e perderia sua identidade nos relatórios.
            ReactionState = reactionState;
            RequiredSequenceLength = requiredSequenceLength;
            EncounterGeneration = encounterGeneration;

            _legalActions =
                new ReadOnlyCollection<GameAction>(
                    actionSnapshot);
        }

        /// <summary>
        /// Atalho para o caso atual em que as opções concretas são somente
        /// colocações normais informadas por ActionAvailabilityReport.
        ///
        /// Quando provedores de runas começarem a enumerar ações especiais, o
        /// composition root juntará todas as GameActions e usará o construtor
        /// principal diretamente.
        /// </summary>
        public static AgentDecisionContext CreateForNormalActions(
            BoardState board,
            TurnContext turnContext,
            ReactionState reactionState,
            int requiredSequenceLength,
            ActionAvailabilityReport availability,
            int encounterGeneration)
        {
            if (availability == null)
            {
                throw new ArgumentNullException(nameof(availability));
            }

            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (availability.Actor != turnContext.Actor)
            {
                throw new ArgumentException(
                    "A disponibilidade pertence a outro participante.",
                    nameof(availability));
            }

            if (availability.BoardVersion != board.Version)
            {
                throw new ArgumentException(
                    "A disponibilidade pertence a outra versão do tabuleiro.",
                    nameof(availability));
            }

            List<GameAction> normalActions =
                new List<GameAction>(
                    availability.LegalNormalMoveCount);

            CellMark mark =
                GetMark(turnContext.Actor);

            for (int index = 0;
                 index < availability.LegalNormalMoves.Count;
                 index++)
            {
                normalActions.Add(
                    new PlaceMarkAction(
                        turnContext.Actor,
                        GameActionOrigin.AgentPolicy,
                        turnContext.TurnId,
                        board.Version,
                        availability.LegalNormalMoves[index],
                        mark));
            }

            return new AgentDecisionContext(
                board,
                turnContext,
                reactionState,
                requiredSequenceLength,
                normalActions,
                encounterGeneration);
        }

        /// <summary>
        /// Confirma se uma política devolveu exatamente uma das instâncias que
        /// recebeu para escolher.
        /// </summary>
        public bool ContainsLegalAction(
            GameAction action)
        {
            if (action == null)
            {
                return false;
            }

            for (int index = 0;
                 index < _legalActions.Count;
                 index++)
            {
                if (ReferenceEquals(
                        _legalActions[index],
                        action))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verificação rápida usada quando uma decisão assíncrona retorna.
        ///
        /// True apenas significa que geração, Turno, ator e versão ainda são os
        /// mesmos. A ActionExecutor continuará sendo a validação autoritativa e
        /// poderá rejeitar a ação por qualquer regra que tenha mudado.
        /// </summary>
        public bool MatchesCurrentState(
            BoardState currentBoard,
            TurnContext currentTurn,
            int currentEncounterGeneration)
        {
            if (currentBoard == null ||
                currentTurn == null)
            {
                return false;
            }

            return currentEncounterGeneration ==
                       EncounterGeneration &&
                   currentTurn.IsOpen &&
                   currentTurn.TurnId == TurnId &&
                   currentTurn.Actor == Actor &&
                   currentTurn.CurrentBoardVersion ==
                       currentBoard.Version &&
                   currentBoard.Version == BoardVersion;
        }

        private static void ValidateSynchronizedState(
            BoardState board,
            TurnContext turnContext,
            ReactionState reactionState)
        {
            if (turnContext.CurrentBoardVersion != board.Version)
            {
                throw new ArgumentException(
                    "O TurnContext e o BoardState precisam estar na mesma versão.",
                    nameof(turnContext));
            }

            /*
             * ReactionState representa a recontagem confirmada antes deste Turno.
             * Em uma oportunidade com orçamento maior que um, ações intermediárias
             * avançam Board.Version, mas a reação continua ancorada na versão
             * inicial até o encerramento do orçamento.
             */
            if (reactionState.BoardVersion !=
                turnContext.InitialBoardVersion)
            {
                throw new ArgumentException(
                    "ReactionState precisa representar o início do Turno atual.",
                    nameof(reactionState));
            }
        }

        private static List<GameAction>
            CopyAndValidateActions(
                IEnumerable<GameAction> legalActions,
                TurnContext turnContext,
                long boardVersion)
        {
            List<GameAction> copy =
                new List<GameAction>();

            foreach (GameAction action in legalActions)
            {
                if (action == null)
                {
                    throw new ArgumentException(
                        "A coleção de ações legais não pode conter null.",
                        nameof(legalActions));
                }

                if (action.Actor != turnContext.Actor)
                {
                    throw new ArgumentException(
                        "Uma ação legal pertence a outro participante.",
                        nameof(legalActions));
                }

                if (action.ExpectedTurnId != turnContext.TurnId)
                {
                    throw new ArgumentException(
                        "Uma ação legal pertence a outro Turno.",
                        nameof(legalActions));
                }

                if (action.ExpectedBoardVersion != boardVersion)
                {
                    throw new ArgumentException(
                        "Uma ação legal pertence a outra versão do tabuleiro.",
                        nameof(legalActions));
                }

                copy.Add(action);
            }

            return copy;
        }

        private static TurnContext CloneOpenTurn(
            TurnContext source)
        {
            TurnContext clone = new TurnContext(
                source.Actor,
                source.TurnId,
                source.InitialBoardVersion,
                source.ActionBudget);

            /*
             * TurnContext permite registrar a mesma versão para ações que alterem
             * outros estados sem mudar o tabuleiro. Podemos usar essa propriedade
             * para reconstruir o contador do snapshot sem inventar as versões
             * intermediárias que não fazem parte do contrato público.
             */
            for (int index = 0;
                 index < source.ActionsConsumed;
                 index++)
            {
                clone.RegisterAppliedAction(
                    source.CurrentBoardVersion);
            }

            if (!clone.IsOpen)
            {
                throw new InvalidOperationException(
                    "O snapshot reconstruído deveria representar um Turno aberto.");
            }

            return clone;
        }

        private static CellMark GetMark(
            ScoreActor actor)
        {
            switch (actor)
            {
                case ScoreActor.Player:
                    return CellMark.X;

                case ScoreActor.Enemy:
                    return CellMark.O;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(actor),
                        actor,
                        "Somente Player ou Enemy possuem uma marca normal.");
            }
        }

        /*
         * Adições futuras:
         *
         * 1. Os estados puros de vida, runas e efeitos ativos poderão entrar como
         *    snapshots adicionais quando esses contratos existirem.
         *
         * 2. Provedores de ações especiais deverão enumerar GameActions concretas
         *    para a IA, além de responder apenas disponibilidade booleana.
         *
         * 3. A política poderá produzir um AgentDecisionReport com pontuação
         *    estimada, profundidade e motivo da escolha para debug e balanceamento.
         *
         * 4. Este arquivo nunca deverá receber MonoBehaviour, GameObject,
         *    Coroutine, Sprite, ScriptableObject ou qualquer tipo da Unity.
         */
    }
}