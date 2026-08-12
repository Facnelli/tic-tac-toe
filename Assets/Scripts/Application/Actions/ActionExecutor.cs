using System;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.Actions
{
    /// <summary>
    /// Coordena a execução autoritativa de uma GameAction.
    ///
    /// O executor representa a fronteira entre:
    ///
    /// - a intenção de realizar uma ação;
    /// - as regras específicas que conseguem aplicá-la;
    /// - o TurnContext que controla o orçamento do Turno;
    /// - o ActionResult que descreve o resultado.
    ///
    /// Nenhuma origem de ação deve alterar o BoardState diretamente.
    ///
    /// No futuro, tanto o clique do jogador quanto a decisão da IA deverão criar
    /// uma PlaceMarkAction e entregá-la a este mesmo executor.
    ///
    /// Esta classe pertence à camada Application porque coordena vários objetos
    /// do domínio. Ela não contém regras visuais e não depende de UnityEngine.
    /// </summary>
    public sealed class ActionExecutor
    {
        private const CellMark PlayerMark =
            CellMark.X;

        private const CellMark EnemyMark =
            CellMark.O;

        private readonly MoveService _moveService;

        /// <summary>
        /// Cria o executor com um MoveService padrão.
        ///
        /// Este construtor é conveniente enquanto o projeto ainda não possui um
        /// ponto central responsável pela composição dos serviços.
        /// </summary>
        public ActionExecutor()
            : this(new MoveService())
        {
        }

        /// <summary>
        /// Cria o executor com um MoveService fornecido externamente.
        ///
        /// A injeção pelo construtor permite utilizar exatamente a mesma instância
        /// de MoveValidator e MoveService configurada para o encontro.
        /// </summary>
        public ActionExecutor(
            MoveService moveService)
        {
            _moveService = moveService ??
                throw new ArgumentNullException(
                    nameof(moveService));
        }

        /// <summary>
        /// Tenta executar uma ação no estado autoritativo do encontro.
        ///
        /// Rejeições previstas durante o jogo produzem ActionResult.Rejected.
        /// Inconsistências de programação continuam produzindo exceções.
        ///
        /// A validação acontece na seguinte ordem:
        ///
        /// 1. o encontro aceita ações;
        /// 2. existe um Turno aberto;
        /// 3. a ação pertence ao Turno atual;
        /// 4. o ator possui o Turno;
        /// 5. a versão esperada ainda corresponde ao BoardState;
        /// 6. a categoria concreta da ação é reconhecida;
        /// 7. as regras específicas da ação permitem sua aplicação.
        ///
        /// O TurnContext somente será modificado depois que a ação tiver sido
        /// efetivamente aplicada.
        /// </summary>
        /// <param name="action">
        /// Intenção imutável que deverá ser processada.
        /// </param>
        /// <param name="board">
        /// Tabuleiro autoritativo do encontro.
        /// </param>
        /// <param name="turnContext">
        /// Turno atualmente aberto.
        ///
        /// Null é permitido e produz TurnIsNotOpen, pois a ausência de um Turno
        /// pode acontecer normalmente durante transições do encontro.
        /// </param>
        /// <param name="encounterAcceptsActions">
        /// Informa se a fase atual aceita ações.
        ///
        /// O futuro adaptador da apresentação calculará este valor a partir da
        /// fase do encontro. O executor não recebe EncounterController.EncounterPhase
        /// porque Application não pode depender de Presentation.
        /// </param>
        public ActionResult Execute(
            GameAction action,
            BoardState board,
            TurnContext turnContext,
            bool encounterAcceptsActions)
        {
            if (action == null)
            {
                throw new ArgumentNullException(
                    nameof(action));
            }

            if (board == null)
            {
                throw new ArgumentNullException(
                    nameof(board));
            }

            /*
             * A fase é verificada antes dos dados do Turno.
             *
             * Durante resolução, apresentação ou encerramento, nenhuma ação deve
             * alcançar o MoveService, mesmo que um TurnContext antigo ainda esteja
             * armazenado temporariamente pelo fluxo.
             */
            if (!encounterAcceptsActions)
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason
                        .EncounterDoesNotAcceptActions,
                    board.Version);
            }

            if (turnContext == null ||
                !turnContext.IsOpen ||
                !turnContext.HasRemainingActions)
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason.TurnIsNotOpen,
                    board.Version);
            }

            /*
             * ExpectedTurnId impede que uma intenção criada para uma oportunidade
             * anterior seja utilizada em outro Turno do mesmo participante.
             *
             * O nome precisa corresponder ao contrato real de GameAction. TurnId é
             * propriedade de TurnContext; a intenção armazena o valor esperado.
             */
            if (action.ExpectedTurnId != turnContext.TurnId)
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason.TurnIdDoesNotMatch,
                    board.Version);
            }

            if (action.Actor != turnContext.Actor)
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason.ActorDoesNotOwnTurn,
                    board.Version);
            }

            /*
             * Uma decisão da IA ou um clique podem permanecer em uma fila enquanto
             * outra ação altera o tabuleiro.
             *
             * ExpectedBoardVersion transforma essa intenção atrasada em rejeição
             * normal, sem tentar aplicá-la sobre um estado diferente.
             */
            if (action.ExpectedBoardVersion != board.Version)
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason
                        .BoardVersionDoesNotMatch,
                    board.Version);
            }

            EnsureTurnAndBoardAreSynchronized(
                turnContext,
                board);

            ActionResult result;

            /*
             * O tipo concreto oferece o alvo tipado da ação.
             *
             * Futuras categorias serão adicionadas como novos casos, cada uma
             * delegando a aplicação ao serviço apropriado.
             */
            PlaceMarkAction placeMarkAction =
                action as PlaceMarkAction;

            if (placeMarkAction == null)
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason.ActionTypeNotSupported,
                    board.Version);
            }

            result = ExecutePlaceMark(
                placeMarkAction,
                board);

            /*
             * Tentativas rejeitadas não consomem orçamento.
             *
             * Essa condição é central: clicar em uma casa inválida ou receber uma
             * decisão atrasada da IA nunca pode encerrar o Turno.
             */
            if (result.WasApplied)
            {
                turnContext.RegisterAppliedAction(
                    result.BoardVersionAfter);
            }

            return result;
        }

        /// <summary>
        /// Executa a primeira categoria concreta suportada pelo sistema.
        ///
        /// Toda regra própria de uma jogada normal continua pertencendo ao
        /// MoveService e ao MoveValidator.
        /// </summary>
        private ActionResult ExecutePlaceMark(
            PlaceMarkAction action,
            BoardState board)
        {
            long boardVersionBefore =
                board.Version;

            /*
             * PlaceMarkAction herda GameAction&lt;BoardCoordinate&gt; e, portanto,
             * expõe a coordenada pela propriedade Target.
             *
             * Guardamos o alvo em uma variável local para que todas as etapas da
             * execução usem exatamente a mesma coordenada e não dependam de uma
             * propriedade Coordinate que não existe nesse contrato.
             */
            BoardCoordinate coordinate =
                action.Target;

            /*
             * A associação entre participante e símbolo pertence ao encontro atual:
             *
             * Player -> X
             * Enemy  -> O
             *
             * A ação carrega a marca solicitada para que o relatório seja completo,
             * mas não pode usar a marca do adversário.
             */
            if (!DoesMarkBelongToActor(
                    action.Actor,
                    action.Mark))
            {
                return ActionResult.Rejected(
                    action,
                    ActionRejectionReason
                        .MarkDoesNotBelongToActor,
                    boardVersionBefore);
            }

            /*
             * Precisamos copiar o estado anterior antes de chamar MoveService.
             *
             * BoardState.GetCell devolve a casa autoritativa. Se guardássemos apenas
             * essa referência, a aplicação da jogada também alteraria o objeto usado
             * como estado anterior.
             */
            CellState cellBefore =
                TryCreateCellSnapshot(
                    board,
                    coordinate);

            MoveValidationResult moveResult =
                _moveService.TryApplyNormalMove(
                    board,
                    coordinate,
                    action.Mark);

            if (!moveResult.IsValid)
            {
                EnsureRejectedMoveDidNotChangeBoard(
                    board,
                    boardVersionBefore);

                return ActionResult.Rejected(
                    action,
                    ConvertRejectionReason(
                        moveResult.RejectionReason),
                    boardVersionBefore);
            }

            /*
             * Uma jogada aprovada só pode ocorrer sobre uma casa existente.
             * Portanto, a ausência do snapshot neste ponto representa uma
             * inconsistência entre MoveService e BoardState.
             */
            if (cellBefore == null)
            {
                throw new InvalidOperationException(
                    $"A jogada na casa {coordinate} foi aplicada, " +
                    "mas não existia um estado anterior para produzir " +
                    "BoardChange.");
            }

            long boardVersionAfter =
                board.Version;

            if (boardVersionAfter <= boardVersionBefore)
            {
                throw new InvalidOperationException(
                    "MoveService informou que a jogada foi aplicada, mas a " +
                    "versão do tabuleiro não aumentou.");
            }

            CellState cellAfter =
                CreateCellSnapshot(
                    board.GetCell(
                        coordinate));

            BoardChangeSet boardChanges =
                BoardChangeSet.CreateSingle(
                    new BoardChange(
                        cellBefore,
                        cellAfter));

            return ActionResult.Applied(
                action,
                boardVersionBefore,
                boardVersionAfter,
                boardChanges);
        }

        /// <summary>
        /// Verifica uma invariável do fluxo autoritativo.
        ///
        /// Se a ação espera a versão real do BoardState, mas o TurnContext conhece
        /// outra versão, alguma alteração ocorreu fora do executor ou deixou de ser
        /// registrada.
        ///
        /// Isso representa erro de programação e não uma rejeição comum do jogador.
        /// </summary>
        private static void EnsureTurnAndBoardAreSynchronized(
            TurnContext turnContext,
            BoardState board)
        {
            if (turnContext.CurrentBoardVersion ==
                board.Version)
            {
                return;
            }

            throw new InvalidOperationException(
                $"O Turno {turnContext.TurnId} conhece a versão " +
                $"{turnContext.CurrentBoardVersion}, mas o BoardState está " +
                $"na versão {board.Version}. O tabuleiro foi alterado fora do " +
                "fluxo autoritativo de ações.");
        }

        private static bool DoesMarkBelongToActor(
            ScoreActor actor,
            CellMark mark)
        {
            switch (actor)
            {
                case ScoreActor.Player:
                    return mark == PlayerMark;

                case ScoreActor.Enemy:
                    return mark == EnemyMark;

                /*
                 * Environment e None não possuem um Turno normal e não podem
                 * colocar uma marca por PlaceMarkAction.
                 */
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converte a linguagem específica das jogadas normais para a linguagem
        /// comum de todas as GameActions.
        /// </summary>
        private static ActionRejectionReason
            ConvertRejectionReason(
                MoveRejectionReason reason)
        {
            switch (reason)
            {
                case MoveRejectionReason.CellDoesNotExist:
                    return ActionRejectionReason.CellDoesNotExist;

                case MoveRejectionReason.MarkAlreadyPresent:
                    return ActionRejectionReason.MarkAlreadyPresent;

                case MoveRejectionReason
                    .OccupiedCellDoesNotAllowOverlap:
                    return ActionRejectionReason
                        .OccupiedCellDoesNotAllowOverlap;

                case MoveRejectionReason.None:
                    throw new InvalidOperationException(
                        "Uma jogada rejeitada não pode possuir o motivo None.");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(reason),
                        reason,
                        "O motivo retornado pelo MoveService não possui " +
                        "conversão para ActionRejectionReason.");
            }
        }

        /// <summary>
        /// Tenta criar uma fotografia de uma casa.
        ///
        /// Retorna null quando a coordenada não existe. Essa possibilidade é
        /// necessária porque CellDoesNotExist é uma rejeição normal.
        /// </summary>
        private static CellState TryCreateCellSnapshot(
            BoardState board,
            BoardCoordinate coordinate)
        {
            if (!board.TryGetCell(
                    coordinate,
                    out CellState cell))
            {
                return null;
            }

            return CreateCellSnapshot(cell);
        }

        private static CellState CreateCellSnapshot(
            CellState source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source));
            }

            return new CellState(
                source.Coordinate,
                source.Marks,
                source.Modifiers);
        }

        private static void EnsureRejectedMoveDidNotChangeBoard(
            BoardState board,
            long boardVersionBefore)
        {
            if (board.Version == boardVersionBefore)
            {
                return;
            }

            throw new InvalidOperationException(
                "MoveService rejeitou a jogada, mas a versão do tabuleiro " +
                "foi modificada.");
        }

        /*
         * Integrações futuras:
         *
         * 1. ActionAvailabilityService será criado na sessão 4.6. Ele informará
         *    quais intenções podem ser construídas, mas não aplicará nenhuma ação.
         *
         * 2. UseRuneAction poderá ser reconhecida por outro caso em Execute.
         *    Sua aplicação poderá produzir comandos para BoardMutationService.
         *
         * 3. BoardMutationService não substitui MoveService. PlaceMarkAction
         *    continuará obedecendo às regras de uma jogada normal.
         *
         * 4. Na adaptação futura do EncounterController:
         *
         *    - o clique criará PlaceMarkAction;
         *    - a IA também produzirá PlaceMarkAction;
         *    - ambas chamarão este mesmo executor;
         *    - BoardView consumirá o BoardChangeSet retornado.
         *
         * 5. O executor não decide:
         *
         *    - quem recebe o próximo Turno;
         *    - se uma reação começou;
         *    - se a Rodada terminou;
         *    - pontuação ou dano;
         *    - animações ou mensagens.
         *
         * 6. Uma futura estrutura ActionExecutionContext poderá substituir o
         *    parâmetro encounterAcceptsActions quando EncounterState puro existir.
         *
         * 7. Esta classe não deve receber MonoBehaviour, GameObject, Coroutine,
         *    Sprite ou outros tipos pertencentes à Unity.
         */
    }
}