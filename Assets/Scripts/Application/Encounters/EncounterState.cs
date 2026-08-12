using System;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.Encounters
{
    /// <summary>
    /// Reúne o estado autoritativo e puro de um Confronto.
    ///
    /// Esta classe guarda referências para as fontes de verdade já existentes:
    ///
    /// - BoardState conserva o conteúdo do tabuleiro;
    /// - CombatantState conserva vida e escudo;
    /// - TurnContext conserva a oportunidade atual;
    /// - ReactionState conserva a última recontagem confirmada;
    /// - os relatórios imutáveis explicam as transições já resolvidas.
    ///
    /// EncounterState não executa regras. Seus métodos de alteração são internal
    /// e somente o EncounterEngine, pertencente à mesma assembly Application,
    /// deve utilizá-los. Presentation, IA e testes externos apenas consultam o
    /// estado resultante.
    /// </summary>
    public sealed class EncounterState
    {
        private long _nextTurnId;

        public EncounterRules Rules { get; }

        public CombatantState PlayerState { get; }
        public CombatantState EnemyState { get; }

        public EncounterPhase Phase { get; private set; }

        public int RoundNumber { get; private set; }

        public BoardState Board { get; private set; }

        public ReactionState ReactionState { get; private set; }

        public TurnContext CurrentTurn { get; private set; }

        public TurnCompletion LastTurnCompletion { get; private set; }

        public ActionResult LastActionResult { get; private set; }

        public ReactionDecision LastReactionDecision { get; private set; }

        public RoundResult LastRoundResult { get; private set; }

        public RoundResolution LastRoundResolution { get; private set; }

        public EncounterResult LastEncounterResult { get; private set; }

        public bool HasStarted =>
            Phase != EncounterPhase.NotStarted;

        public bool AcceptsActions =>
            Phase == EncounterPhase.WaitingForAction &&
            CurrentTurn != null &&
            CurrentTurn.IsOpen;

        public bool HasActiveRound =>
            Board != null &&
            ReactionState != null &&
            Phase != EncounterPhase.NotStarted &&
            Phase != EncounterPhase.WaitingForNextRound &&
            Phase != EncounterPhase.EncounterEnded &&
            Phase != EncounterPhase.Faulted;

        public bool IsEncounterOver =>
            Phase == EncounterPhase.EncounterEnded;

        public ScoreActor CurrentActor =>
            CurrentTurn != null && CurrentTurn.IsOpen
                ? CurrentTurn.Actor
                : ScoreActor.None;

        internal EncounterState(
            CombatantState playerState,
            CombatantState enemyState,
            EncounterRules rules)
        {
            PlayerState = playerState ??
                throw new ArgumentNullException(nameof(playerState));

            EnemyState = enemyState ??
                throw new ArgumentNullException(nameof(enemyState));

            Rules = rules ??
                throw new ArgumentNullException(nameof(rules));

            ValidateCombatantOwner(
                PlayerState,
                ScoreActor.Player,
                nameof(playerState));

            ValidateCombatantOwner(
                EnemyState,
                ScoreActor.Enemy,
                nameof(enemyState));

            if (string.Equals(
                    PlayerState.CombatantId,
                    EnemyState.CombatantId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Player e Enemy precisam possuir CombatantId diferentes.");
            }

            Phase = EncounterPhase.NotStarted;
            RoundNumber = 0;
            _nextTurnId = 1;
        }

        internal void BeginRound(
            BoardState board,
            ReactionState reactionState)
        {
            Board = board ??
                throw new ArgumentNullException(nameof(board));

            ReactionState = reactionState ??
                throw new ArgumentNullException(nameof(reactionState));

            if (ReactionState.BoardVersion != Board.Version)
            {
                throw new ArgumentException(
                    "O ReactionState inicial precisa pertencer à versão atual do tabuleiro.",
                    nameof(reactionState));
            }

            RoundNumber++;

            CurrentTurn = null;
            LastTurnCompletion = null;
            LastActionResult = null;
            LastReactionDecision = null;
            LastRoundResult = null;
            LastRoundResolution = null;

            Phase = EncounterPhase.ResolvingTurn;
        }

        internal TurnContext OpenTurn(
            ScoreActor actor,
            int actionBudget)
        {
            if (Board == null)
            {
                throw new InvalidOperationException(
                    "Não é possível abrir um Turno sem uma Rodada ativa.");
            }

            CurrentTurn = new TurnContext(
                actor,
                _nextTurnId,
                Board.Version,
                actionBudget);

            _nextTurnId++;
            Phase = EncounterPhase.WaitingForAction;

            return CurrentTurn;
        }

        internal void RecordActionResult(
            ActionResult result)
        {
            LastActionResult = result ??
                throw new ArgumentNullException(nameof(result));
        }

        internal void BeginTurnResolution(
            TurnCompletion completion)
        {
            LastTurnCompletion = completion ??
                throw new ArgumentNullException(nameof(completion));

            CurrentTurn = null;
            LastReactionDecision = null;
            Phase = EncounterPhase.ResolvingTurn;
        }

        internal void RecordReactionDecision(
            ReactionState reactionState,
            ReactionDecision decision)
        {
            ReactionState = reactionState ??
                throw new ArgumentNullException(nameof(reactionState));

            LastReactionDecision = decision ??
                throw new ArgumentNullException(nameof(decision));

            if (!ReferenceEquals(
                    LastReactionDecision.CurrentState,
                    ReactionState))
            {
                throw new ArgumentException(
                    "A decisão precisa conservar o ReactionState registrado.",
                    nameof(decision));
            }
        }

        internal void BeginRoundResolution()
        {
            if (LastReactionDecision == null ||
                !LastReactionDecision.EndsBoardMatch)
            {
                throw new InvalidOperationException(
                    "A resolução da Rodada exige uma decisão terminal registrada.");
            }

            CurrentTurn = null;
            Phase = EncounterPhase.ResolvingRound;
        }

        internal void RecordRoundResolution(
            RoundResult roundResult,
            RoundResolution roundResolution,
            EncounterResult encounterResult)
        {
            LastRoundResult = roundResult ??
                throw new ArgumentNullException(nameof(roundResult));

            LastRoundResolution = roundResolution ??
                throw new ArgumentNullException(nameof(roundResolution));

            if (!ReferenceEquals(
                    LastRoundResolution.Round,
                    LastRoundResult))
            {
                throw new ArgumentException(
                    "A resolução precisa pertencer ao RoundResult registrado.",
                    nameof(roundResolution));
            }

            if (encounterResult != null &&
                !ReferenceEquals(
                    encounterResult.FinalRound,
                    LastRoundResolution))
            {
                throw new ArgumentException(
                    "O EncounterResult precisa pertencer à resolução registrada.",
                    nameof(encounterResult));
            }

            LastEncounterResult = encounterResult;
            CurrentTurn = null;
            Phase = EncounterPhase.RoundResolved;
        }

        internal void AcknowledgeRoundResolution()
        {
            if (Phase != EncounterPhase.RoundResolved ||
                LastRoundResolution == null)
            {
                throw new InvalidOperationException(
                    "Não existe uma resolução de Rodada aguardando confirmação.");
            }

            Phase = LastEncounterResult != null
                ? EncounterPhase.EncounterEnded
                : EncounterPhase.WaitingForNextRound;
        }

        internal void MarkFaulted()
        {
            if (CurrentTurn != null &&
                CurrentTurn.IsOpen &&
                !CurrentTurn.HasPlayedAction)
            {
                CurrentTurn.Cancel(
                    Board != null
                        ? Board.Version
                        : CurrentTurn.CurrentBoardVersion);
            }

            CurrentTurn = null;
            Phase = EncounterPhase.Faulted;
        }

        private static void ValidateCombatantOwner(
            CombatantState state,
            ScoreActor expectedActor,
            string parameterName)
        {
            if (state.Actor != expectedActor)
            {
                throw new ArgumentException(
                    $"O combatente informado em {parameterName} precisa " +
                    $"pertencer a {expectedActor}.",
                    parameterName);
            }
        }

        /*
         * Integrações futuras:
         *
         * 1. EncounterEngine é a única classe autorizada a chamar os métodos
         *    internal de transição deste estado.
         *
         * 2. IA e Presentation receberão esta instância somente para leitura.
         *    Nenhuma delas decidirá vencedor, reação, dano ou próximo Turno.
         *
         * 3. Filas de ações, histórico completo e replay poderão ser adicionados
         *    como relatórios imutáveis sem duplicar BoardState ou CombatantState.
         *
         * 4. Este arquivo não deve depender de MonoBehaviour, GameObject,
         *    Coroutine, Sprite, ScriptableObject ou outros tipos da Unity.
         */
    }
}