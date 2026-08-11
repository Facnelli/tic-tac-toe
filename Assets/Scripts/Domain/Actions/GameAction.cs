using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Identifica a categoria lógica de uma ação.
    ///
    /// Este valor permite que relatórios, executores, logs e apresentação
    /// reconheçam a categoria sem depender do nome da classe concreta.
    /// </summary>
    public enum GameActionType
    {
        /// <summary>
        /// Protege contra o valor padrão de um enum.
        ///
        /// Nenhuma GameAction válida poderá possuir este tipo.
        /// </summary>
        None = 0,

        /// <summary>
        /// Coloca uma marca em uma casa do tabuleiro.
        /// </summary>
        PlaceMark = 1

        /*
         * Adições futuras:
         *
         * Novas categorias poderão ser adicionadas conforme os contratos
         * concretos surgirem, por exemplo:
         *
         * RemoveMark,
         * ClearRow,
         * ApplyCellModifier,
         * ActivateRune.
         *
         * Não devemos adicionar campos opcionais à GameAction para representar
         * todas essas possibilidades. Cada categoria deverá possuir sua própria
         * classe e seu próprio tipo de alvo.
         */
    }

    /// <summary>
    /// Informa qual sistema produziu a intenção.
    ///
    /// Origin não define quem executará a ação. Essa responsabilidade pertence
    /// a Actor.
    ///
    /// Por exemplo, uma política automática pode controlar Player, e um efeito
    /// de runa pertencente a Enemy pode produzir uma ação para Enemy.
    /// </summary>
    public enum GameActionOrigin
    {
        /// <summary>
        /// Protege contra o valor padrão do enum.
        /// </summary>
        None = 0,

        /// <summary>
        /// A intenção nasceu de uma interação humana, como um clique.
        /// </summary>
        PlayerInput = 1,

        /// <summary>
        /// A intenção foi escolhida por uma política de IA.
        /// </summary>
        AgentPolicy = 2,

        /// <summary>
        /// A intenção foi produzida por um efeito autorizado.
        ///
        /// No futuro, poderá representar runas, habilidades de inimigos ou
        /// outros efeitos que produzam uma GameAction.
        /// </summary>
        Effect = 3,

        /// <summary>
        /// A intenção foi produzida por uma regra do encontro.
        ///
        /// Alterações ambientais sem um ator participante deverão usar o futuro
        /// BoardCommand, e não fingir que Environment possui um Turno.
        /// </summary>
        EncounterRule = 4
    }

    /// <summary>
    /// Representa uma intenção imutável de executar algo no jogo.
    ///
    /// Uma GameAction não significa que a ação foi aceita ou aplicada.
    /// Ela informa apenas o que alguém pretende fazer.
    ///
    /// O futuro ActionExecutor será responsável por:
    ///
    /// 1. verificar se a ação pertence ao Turno atual;
    /// 2. verificar se o ator pode agir;
    /// 3. comparar a versão esperada com a versão atual do tabuleiro;
    /// 4. delegar a execução ao serviço autorizado;
    /// 5. produzir um ActionResult aplicado ou rejeitado;
    /// 6. consumir o orçamento do TurnContext somente quando for aplicada.
    ///
    /// Esta classe não modifica BoardState, não troca Turnos e não avalia reação.
    /// </summary>
    public abstract class GameAction
    {
        /// <summary>
        /// Participante que pretende executar a ação.
        ///
        /// Apenas Player e Enemy podem possuir ações de Turno.
        /// Alterações puramente ambientais deverão utilizar contratos próprios.
        /// </summary>
        public ScoreActor Actor { get; }

        /// <summary>
        /// Categoria lógica da ação.
        /// </summary>
        public GameActionType ActionType { get; }

        /// <summary>
        /// Sistema que produziu esta intenção.
        ///
        /// Este dado será útil para logs, replays, IA, depuração e apresentação,
        /// mas não deve substituir as regras do executor.
        /// </summary>
        public GameActionOrigin Origin { get; }

        /// <summary>
        /// Identidade do Turno para o qual esta ação foi criada.
        ///
        /// Isso impede que uma decisão atrasada seja aplicada em outro Turno,
        /// inclusive quando os dois Turnos pertencem ao mesmo participante.
        /// </summary>
        public long ExpectedTurnId { get; }

        /// <summary>
        /// Versão do tabuleiro observada quando a intenção foi criada.
        ///
        /// O futuro ActionExecutor comparará este número com BoardState.Version.
        /// Se o tabuleiro tiver mudado enquanto uma IA estava calculando, a ação
        /// antiga será rejeitada sem consumir orçamento.
        /// </summary>
        public long ExpectedBoardVersion { get; }

        /// <summary>
        /// Inicializa os dados compartilhados por todas as ações.
        ///
        /// O construtor é protected porque somente classes concretas, como
        /// PlaceMarkAction, devem criar uma GameAction.
        /// </summary>
        protected GameAction(
            ScoreActor actor,
            GameActionType actionType,
            GameActionOrigin origin,
            long expectedTurnId,
            long expectedBoardVersion)
        {
            ValidateActor(actor);
            ValidateActionType(actionType);
            ValidateOrigin(origin);

            if (expectedTurnId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedTurnId),
                    expectedTurnId,
                    "A ação precisa estar associada a um Turno maior que zero.");
            }

            if (expectedBoardVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedBoardVersion),
                    expectedBoardVersion,
                    "A versão esperada do tabuleiro não pode ser negativa.");
            }

            Actor = actor;
            ActionType = actionType;
            Origin = origin;
            ExpectedTurnId = expectedTurnId;
            ExpectedBoardVersion = expectedBoardVersion;
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
                    "Somente Player ou Enemy podem executar uma GameAction.");
            }
        }

        private static void ValidateActionType(
            GameActionType actionType)
        {
            if (!Enum.IsDefined(
                    typeof(GameActionType),
                    actionType) ||
                actionType == GameActionType.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionType),
                    actionType,
                    "A ação precisa possuir um tipo reconhecido.");
            }
        }

        private static void ValidateOrigin(
            GameActionOrigin origin)
        {
            if (!Enum.IsDefined(
                    typeof(GameActionOrigin),
                    origin) ||
                origin == GameActionOrigin.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(origin),
                    origin,
                    "A ação precisa possuir uma origem reconhecida.");
            }
        }

        /*
         * Adições futuras:
         *
         * 1. ActionExecutor comparará ExpectedTurnId com TurnContext.TurnId e
         *    ExpectedBoardVersion com BoardState.Version.
         *
         * 2. ActionResult informará se esta intenção foi aplicada ou rejeitada.
         *
         * 3. Uma ação aplicada poderá produzir um BoardChangeSet com todas as
         *    alterações realizadas.
         *
         * 4. Identificadores de eventos poderão ser adicionados quando existirem
         *    efeitos encadeados, logs autoritativos e replays.
         *
         * 5. GameAction não deverá receber referências a GameObject, Sprite,
         *    MonoBehaviour ou ScriptableObject.
         */
    }

    /// <summary>
    /// Base para ações que possuem um alvo de tipo conhecido.
    ///
    /// TTarget é substituído pelo tipo real do alvo. Em PlaceMarkAction, por
    /// exemplo, TTarget será BoardCoordinate.
    ///
    /// Isso impede o uso de object, strings ou vários campos opcionais para
    /// representar alvos completamente diferentes.
    /// </summary>
    /// <typeparam name="TTarget">
    /// Tipo imutável ou somente para leitura que identifica o alvo.
    /// </typeparam>
    public abstract class GameAction<TTarget> : GameAction
    {
        /// <summary>
        /// Alvo tipado desta ação.
        /// </summary>
        public TTarget Target { get; }

        protected GameAction(
            ScoreActor actor,
            GameActionType actionType,
            GameActionOrigin origin,
            long expectedTurnId,
            long expectedBoardVersion,
            TTarget target)
            : base(
                actor,
                actionType,
                origin,
                expectedTurnId,
                expectedBoardVersion)
        {
            /*
             * BoardCoordinate é um struct e nunca será nulo. Esta proteção também
             * prepara a classe para futuros alvos representados por classes.
             */
            if (ReferenceEquals(target, null))
            {
                throw new ArgumentNullException(nameof(target));
            }

            Target = target;
        }
    }
}