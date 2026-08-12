using System;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.Encounters
{
    /// <summary>
    /// Descreve a próxima oportunidade escolhida para o encontro.
    ///
    /// Ator e orçamento aparecem juntos porque um efeito futuro poderá conceder
    /// outra oportunidade ao mesmo participante ou mais de uma ação no Turno.
    /// A estrutura não executa a troca; ela apenas entrega a decisão ao motor.
    /// </summary>
    public struct EncounterTurnPlan
    {
        public ScoreActor Actor { get; }
        public int ActionBudget { get; }

        public EncounterTurnPlan(
            ScoreActor actor,
            int actionBudget)
        {
            ValidateActor(actor);

            if (actionBudget < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionBudget),
                    actionBudget,
                    "O orçamento da oportunidade deve ser maior que zero.");
            }

            Actor = actor;
            ActionBudget = actionBudget;
        }

        private static void ValidateActor(
            ScoreActor actor)
        {
            if (actor != ScoreActor.Player &&
                actor != ScoreActor.Enemy)
            {
                throw new ArgumentException(
                    "Uma oportunidade pertence a Player ou Enemy.",
                    nameof(actor));
            }
        }
    }

    /// <summary>
    /// Ponto de extensão responsável somente por escolher a próxima oportunidade.
    ///
    /// O contrato fica fora do EncounterEngine para que efeitos futuros possam
    /// conservar o Turno, inverter a ordem ou alterar o orçamento sem ensinar
    /// runas concretas ao núcleo do encontro.
    /// </summary>
    public interface IEncounterTurnScheduler
    {
        EncounterTurnPlan CreateNextTurn(
            EncounterState state,
            TurnCompletion completedTurn,
            ReactionState currentReactionState);
    }

    /// <summary>
    /// Política padrão: uma ação por oportunidade e alternância entre os lados.
    /// </summary>
    public sealed class AlternatingEncounterTurnScheduler :
        IEncounterTurnScheduler
    {
        public EncounterTurnPlan CreateNextTurn(
            EncounterState state,
            TurnCompletion completedTurn,
            ReactionState currentReactionState)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (completedTurn == null)
            {
                throw new ArgumentNullException(nameof(completedTurn));
            }

            if (currentReactionState == null)
            {
                throw new ArgumentNullException(
                    nameof(currentReactionState));
            }

            ScoreActor nextActor =
                completedTurn.Actor == ScoreActor.Player
                    ? ScoreActor.Enemy
                    : ScoreActor.Player;

            return new EncounterTurnPlan(
                nextActor,
                actionBudget: 1);
        }
    }

    /// <summary>
    /// Orquestra o fluxo puro e autoritativo de um Confronto.
    ///
    /// Responsabilidades principais:
    ///
    /// 1. abrir Rodadas e oportunidades;
    /// 2. encaminhar toda GameAction ao ActionExecutor;
    /// 3. recontar as duas marcas depois de um Turno concluído;
    /// 4. entregar fatos à ReactionRule, sem duplicar sua lógica;
    /// 5. resolver ScorePipeline, ClashResolver e DamageResolver uma única vez;
    /// 6. publicar relatórios imutáveis para a futura apresentação.
    ///
    /// O motor não conhece MonoBehaviour, GameObject, input, IA, animações ou
    /// coroutines. Por isso, seus testes EditMode permanecem rápidos e
    /// determinísticos.
    /// </summary>
    public sealed class EncounterEngine
    {
        private readonly ActionExecutor _actionExecutor;
        private readonly ActionAvailabilityService
            _actionAvailabilityService;
        private readonly ReactionStateFactory _reactionStateFactory;
        private readonly ReactionRule _reactionRule;
        private readonly ScorePipeline _scorePipeline;
        private readonly ClashResolver _clashResolver;
        private readonly DamageResolver _damageResolver;
        private readonly IEncounterTurnScheduler _turnScheduler;

        public EncounterState State { get; }

        /// <summary>
        /// Composição padrão, suficiente para o jogo atual.
        /// </summary>
        public EncounterEngine(
            CombatantState playerState,
            CombatantState enemyState,
            EncounterRules rules)
            : this(
                playerState,
                enemyState,
                rules,
                new ActionExecutor(),
                new ActionAvailabilityService(),
                new ReactionStateFactory(),
                new ReactionRule(),
                new ScorePipeline(),
                new ClashResolver(),
                new DamageResolver(),
                new AlternatingEncounterTurnScheduler())
        {
        }

        /// <summary>
        /// Composição explícita usada por testes e pelo futuro composition root.
        /// </summary>
        public EncounterEngine(
            CombatantState playerState,
            CombatantState enemyState,
            EncounterRules rules,
            ActionExecutor actionExecutor,
            ActionAvailabilityService actionAvailabilityService,
            ReactionStateFactory reactionStateFactory,
            ReactionRule reactionRule,
            ScorePipeline scorePipeline,
            ClashResolver clashResolver,
            DamageResolver damageResolver,
            IEncounterTurnScheduler turnScheduler)
        {
            State = new EncounterState(
                playerState,
                enemyState,
                rules);

            _actionExecutor = actionExecutor ??
                throw new ArgumentNullException(nameof(actionExecutor));

            _actionAvailabilityService =
                actionAvailabilityService ??
                throw new ArgumentNullException(
                    nameof(actionAvailabilityService));

            _reactionStateFactory = reactionStateFactory ??
                throw new ArgumentNullException(
                    nameof(reactionStateFactory));

            _reactionRule = reactionRule ??
                throw new ArgumentNullException(nameof(reactionRule));

            _scorePipeline = scorePipeline ??
                throw new ArgumentNullException(nameof(scorePipeline));

            _clashResolver = clashResolver ??
                throw new ArgumentNullException(nameof(clashResolver));

            _damageResolver = damageResolver ??
                throw new ArgumentNullException(nameof(damageResolver));

            _turnScheduler = turnScheduler ??
                throw new ArgumentNullException(nameof(turnScheduler));
        }

        /// <summary>
        /// Inicia o Confronto e abre a primeira oportunidade.
        /// </summary>
        public void StartEncounter(
            BoardState board,
            EncounterSide startingSide,
            int actionBudget = 1)
        {
            if (State.Phase != EncounterPhase.NotStarted)
            {
                throw new InvalidOperationException(
                    "O Confronto já foi iniciado.");
            }

            EnsureBothCombatantsAreAlive();
            StartRound(board, startingSide, actionBudget);
        }

        /// <summary>
        /// Abre uma nova Rodada depois que a apresentação confirmou a anterior.
        /// </summary>
        public void StartNextRound(
            BoardState board,
            EncounterSide startingSide,
            int actionBudget = 1)
        {
            if (State.Phase !=
                EncounterPhase.WaitingForNextRound)
            {
                throw new InvalidOperationException(
                    "Uma nova Rodada só pode começar depois da confirmação " +
                    "da resolução anterior.");
            }

            EnsureBothCombatantsAreAlive();
            StartRound(board, startingSide, actionBudget);
        }

        /// <summary>
        /// Executa uma intenção pelo único caminho autoritativo de ações.
        ///
        /// Uma rejeição permanece disponível em LastActionResult e não consome o
        /// orçamento. Uma aplicação que fecha o TurnContext avança o fluxo
        /// imediatamente até a próxima espera ou até RoundResolved.
        /// </summary>
        public ActionResult ExecuteAction(
            GameAction action)
        {
            if (State.Board == null)
            {
                throw new InvalidOperationException(
                    "Não existe uma Rodada com tabuleiro ativo.");
            }

            TurnContext executingTurn = State.CurrentTurn;

            ActionResult result = _actionExecutor.Execute(
                action,
                State.Board,
                executingTurn,
                State.AcceptsActions);

            State.RecordActionResult(result);

            if (result.WasApplied &&
                executingTurn != null &&
                executingTurn.Status == TurnStatus.Completed)
            {
                TurnCompletion completion =
                    TurnCompletion.CreateFrom(executingTurn);

                State.BeginTurnResolution(completion);
                ResolveCompletedTurn(completion);
            }

            return result;
        }

        /// <summary>
        /// Encerra uma oportunidade sem ação.
        ///
        /// Skip é uma decisão explícita de fluxo: ele não conta como jogada e não
        /// chama ReactionRule. A regra de reação somente observa Turnos que
        /// realmente aplicaram ao menos uma ação.
        /// </summary>
        public TurnCompletion SkipCurrentTurn()
        {
            EnsureActionPhase();

            TurnContext skippedTurn = State.CurrentTurn;
            skippedTurn.Skip(State.Board.Version);

            TurnCompletion completion =
                TurnCompletion.CreateFrom(skippedTurn);

            State.BeginTurnResolution(completion);

            EncounterTurnPlan nextTurn =
                CreateNextTurnPlan(
                    completion,
                    State.ReactionState);

            State.OpenTurn(
                nextTurn.Actor,
                nextTurn.ActionBudget);

            return completion;
        }

        /// <summary>
        /// Libera o fluxo depois que consumidores terminaram de apresentar a
        /// resolução. Vida e dano já estavam resolvidos antes desta confirmação.
        /// </summary>
        public void AcknowledgeRoundResolution()
        {
            State.AcknowledgeRoundResolution();
        }

        private void StartRound(
            BoardState board,
            EncounterSide startingSide,
            int actionBudget)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (actionBudget < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionBudget),
                    actionBudget,
                    "O orçamento inicial deve ser maior que zero.");
            }

            ScoreActor startingActor =
                EncounterSideRules.ToActor(startingSide);

            ReactionState initialReactionState =
                CreateReactionState(board);

            State.BeginRound(
                board,
                initialReactionState);

            bool hasAvailableActions =
                _actionAvailabilityService.HasAvailableActions(
                    board,
                    startingActor);

            if (hasAvailableActions)
            {
                State.OpenTurn(
                    startingActor,
                    actionBudget);

                return;
            }

            /*
             * Não existe um Turno artificial aqui. A ReactionRule recebe os dois
             * snapshots iguais somente para aplicar sua prioridade terminal de
             * NoAvailableActions sobre o estado inicial já recontado.
             */
            ReactionDecision noActionsDecision =
                _reactionRule.Evaluate(
                    initialReactionState,
                    initialReactionState,
                    startingActor,
                    hasAvailableActions: false);

            State.RecordReactionDecision(
                initialReactionState,
                noActionsDecision);

            ResolveRound(noActionsDecision);
        }

        private void ResolveCompletedTurn(
            TurnCompletion completion)
        {
            if (!completion.CanEvaluateReaction)
            {
                throw new InvalidOperationException(
                    "Somente um Turno concluído por ações pode avaliar reação.");
            }

            if (completion.FinalBoardVersion !=
                State.Board.Version)
            {
                throw new InvalidOperationException(
                    "A conclusão do Turno não pertence à versão atual do tabuleiro.");
            }

            ReactionState currentReactionState =
                CreateReactionState(State.Board);

            EncounterTurnPlan nextTurn =
                CreateNextTurnPlan(
                    completion,
                    currentReactionState);

            bool nextActorHasActions =
                _actionAvailabilityService.HasAvailableActions(
                    State.Board,
                    nextTurn.Actor);

            ReactionDecision decision =
                _reactionRule.Evaluate(
                    State.ReactionState,
                    currentReactionState,
                    completion.Actor,
                    nextActorHasActions);

            State.RecordReactionDecision(
                currentReactionState,
                decision);

            if (decision.EndsBoardMatch)
            {
                ResolveRound(decision);
                return;
            }

            State.OpenTurn(
                nextTurn.Actor,
                nextTurn.ActionBudget);
        }

        private EncounterTurnPlan CreateNextTurnPlan(
            TurnCompletion completion,
            ReactionState currentReactionState)
        {
            EncounterTurnPlan plan =
                _turnScheduler.CreateNextTurn(
                    State,
                    completion,
                    currentReactionState);

            // O construtor do plano valida ator e orçamento. A verificação abaixo
            // também detecta o valor default(EncounterTurnPlan), que ignora o
            // construtor e carregaria Actor.None com orçamento zero.
            if ((plan.Actor != ScoreActor.Player &&
                 plan.Actor != ScoreActor.Enemy) ||
                plan.ActionBudget < 1)
            {
                throw new InvalidOperationException(
                    "O agendador produziu um plano de Turno inválido.");
            }

            return plan;
        }

        private ReactionState CreateReactionState(
            BoardState board)
        {
            return _reactionStateFactory.Create(
                board,
                EncounterSideRules.GetNormalMoveMark(
                    EncounterSide.Player),
                EncounterSideRules.GetNormalMoveMark(
                    EncounterSide.Enemy),
                State.Rules.RequiredSequenceLength);
        }

        private void ResolveRound(
            ReactionDecision finalDecision)
        {
            State.BeginRoundResolution();

            RoundResult roundResult = new RoundResult(
                State.RoundNumber,
                State.Board,
                finalDecision);

            CombatantSnapshot playerBefore =
                CombatantSnapshot.Capture(State.PlayerState);

            CombatantSnapshot enemyBefore =
                CombatantSnapshot.Capture(State.EnemyState);

            ScorePipelineResult playerScore =
                ResolveScore(
                    ScoreActor.Player,
                    CellMark.X,
                    finalDecision.VictoryMultiplierRecipient ==
                        ScoreActor.Player);

            ScorePipelineResult enemyScore =
                ResolveScore(
                    ScoreActor.Enemy,
                    CellMark.O,
                    finalDecision.VictoryMultiplierRecipient ==
                        ScoreActor.Enemy);

            ClashReport clash = _clashResolver.Resolve(
                playerScore,
                enemyScore);

            /*
             * DamageResolver aplica vida e escudo dentro desta única chamada.
             * Nenhum acknowledge, evento ou animação chamará dano novamente.
             */
            DamageReport damage = _damageResolver.Resolve(
                clash,
                State.PlayerState,
                State.EnemyState);

            CombatantSnapshot playerAfter =
                CombatantSnapshot.Capture(State.PlayerState);

            CombatantSnapshot enemyAfter =
                CombatantSnapshot.Capture(State.EnemyState);

            RoundResolution roundResolution =
                new RoundResolution(
                    roundResult,
                    playerScore,
                    enemyScore,
                    clash,
                    damage,
                    playerBefore,
                    playerAfter,
                    enemyBefore,
                    enemyAfter);

            EncounterResult encounterResult =
                roundResolution.CausedEncounterEnd
                    ? new EncounterResult(roundResolution)
                    : null;

            State.RecordRoundResolution(
                roundResult,
                roundResolution,
                encounterResult);
        }

        private ScorePipelineResult ResolveScore(
            ScoreActor participant,
            CellMark mark,
            bool receivesVictoryMultiplier)
        {
            ScorePipelineRequest request =
                new ScorePipelineRequest(
                    participant,
                    mark,
                    State.Board,
                    State.Rules.MinimumScoringSequenceLength,
                    State.Rules.MaximumScoringSequenceLength,
                    receivesVictoryMultiplier,
                    State.Rules.VictoryMultiplier);

            return _scorePipeline.Resolve(request);
        }

        private void EnsureActionPhase()
        {
            if (!State.AcceptsActions)
            {
                throw new InvalidOperationException(
                    "O Confronto não possui uma oportunidade aberta.");
            }
        }

        private void EnsureBothCombatantsAreAlive()
        {
            if (State.PlayerState.IsDefeated ||
                State.EnemyState.IsDefeated)
            {
                throw new InvalidOperationException(
                    "Uma Rodada não pode começar com combatente derrotado.");
            }
        }

        /*
         * Integrações futuras:
         *
         * 1. IA escolherá GameAction, mas chamará ExecuteAction como o Player.
         *
         * 2. Runes/Effects poderão compor IActionAvailabilityProvider,
         *    IEncounterTurnScheduler e contribuições do ScorePipeline.
         *
         * 3. EncounterController observará Phase e os relatórios. Delays,
         *    coroutines e animações permanecerão inteiramente em Presentation.
         *
         * 4. Falhas técnicas poderão ser encapsuladas por um adaptador que chame
         *    EncounterState.MarkFaulted. Regras normais continuam usando retornos
         *    explícitos, como ActionResult.Rejected.
         */
    }
}