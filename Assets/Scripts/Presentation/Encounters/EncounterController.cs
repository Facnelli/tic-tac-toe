using System;
using System.Collections;
using System.Collections.Generic;
using TicTacToeRoguelike.Application.AI;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Sequences;
using TicTacToeRoguelike.Presentation.Board;
using TicTacToeRoguelike.Presentation.Combat;
using UnityEngine;

namespace TicTacToeRoguelike.Presentation.Encounters
{

    /// <summary>
    /// Orquestra um encontro completo entre jogador e inimigo.
    ///
    /// "Orquestrar" significa chamar cada serviço na ordem certa. Este componente
    /// não implementa novamente as regras desses serviços:
    ///
    /// - MoveService valida e aplica jogadas;
    /// - BasicTicTacToePolicy escolhe a intenção da IA;
    /// - SequenceEvaluator detecta o encerramento do tabuleiro;
    /// - ScorePipeline resolve os dois placares;
    /// - ClashResolver compara os placares;
    /// - DamageResolver altera a vida e o escudo do alvo correto;
    /// - CombatReportAnimator apenas apresenta os relatórios prontos.
    ///
    /// A ordem mais importante da Fase 2 é "domínio primeiro, animação depois".
    /// Quando a animação começa, o resultado e o dano já são autoritativos.
    /// Portanto, jogar sem animador ou interromper uma animação nunca cria
    /// uma fórmula alternativa.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterController : MonoBehaviour
    {
        /// <summary>
        /// Lado lógico que realiza uma jogada.
        ///
        /// Lado e símbolo continuam separados. Hoje Player usa X e Enemy usa O,
        /// mas bosses ou regras futuras poderão alterar essa associação.
        /// </summary>
        public enum EncounterSide
        {
            Player,
            Enemy
        }

        /// <summary>
        /// Estado atual do fluxo do encontro.
        ///
        /// A disponibilidade do input é determinada por esta fase. Assim, não
        /// existe outro bool capaz de contradizer o estado real do encontro.
        /// </summary>
        public enum EncounterPhase
        {
            NotStarted,
            WaitingForPlayer,
            WaitingForEnemy,
            ResolvingMove,
            ResolvingRound,
            PresentingRoundResolution,
            WaitingForNextRound,
            EncounterEnded,
            Faulted
        }

        public enum RoundOutcome
        {
            PlayerVictory,
            EnemyVictory,
            Draw
        }

        public enum RoundEndReason
        {
            WinningSequence,
            NoLegalMove,
            InitialBoardWasAlreadyTerminal
        }

        public enum EncounterOutcome
        {
            PlayerVictory,
            EnemyVictory,
            Draw
        }

        /// <summary>
        /// Registro imutável de uma jogada aplicada com sucesso.
        /// </summary>
        public readonly struct AppliedMove
        {
            public EncounterSide Side { get; }
            public CellMark Mark { get; }
            public BoardCoordinate Coordinate { get; }
            public long BoardVersion { get; }
            public IReadOnlyList<SequenceMatch> WinningSequences { get; }

            internal AppliedMove(
                EncounterSide side,
                CellMark mark,
                BoardCoordinate coordinate,
                long boardVersion,
                IReadOnlyList<SequenceMatch> winningSequences)
            {
                Side = side;
                Mark = mark;
                Coordinate = coordinate;
                BoardVersion = boardVersion;
                WinningSequences = winningSequences ??
                                   throw new ArgumentNullException(nameof(winningSequences));
            }
        }

        /// <summary>
        /// Registro de uma tentativa de jogada rejeitada pelo domínio.
        /// </summary>
        public readonly struct RejectedMove
        {
            public EncounterSide Side { get; }
            public CellMark Mark { get; }
            public BoardCoordinate Coordinate { get; }
            public MoveRejectionReason RejectionReason { get; }

            internal RejectedMove(
                EncounterSide side,
                CellMark mark,
                BoardCoordinate coordinate,
                MoveRejectionReason rejectionReason)
            {
                Side = side;
                Mark = mark;
                Coordinate = coordinate;
                RejectionReason = rejectionReason;
            }
        }

        /// <summary>
        /// Fatos encontrados ao encerrar o tabuleiro de uma rodada.
        ///
        /// Este objeto ainda não é o resultado completo do combate. Ele informa
        /// por que as jogadas terminaram e qual lado venceu a rodada.
        ///
        /// Pontuação, confronto e dano aparecem no RoundResolution.
        /// </summary>
        public sealed class RoundResult
        {
            public int RoundNumber { get; }
            public RoundOutcome Outcome { get; }
            public RoundEndReason EndReason { get; }
            public BoardState Board { get; }
            public BoardCoordinate? LastMove { get; }

            public IReadOnlyList<SequenceMatch>
                PlayerWinningSequences { get; }

            public IReadOnlyList<SequenceMatch>
                EnemyWinningSequences { get; }

            internal RoundResult(
                int roundNumber,
                RoundOutcome outcome,
                RoundEndReason endReason,
                BoardState board,
                BoardCoordinate? lastMove,
                IReadOnlyList<SequenceMatch> playerWinningSequences,
                IReadOnlyList<SequenceMatch> enemyWinningSequences)
            {
                if (roundNumber < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(roundNumber));
                }

                RoundNumber = roundNumber;
                Outcome = outcome;
                EndReason = endReason;
                Board = board ??
                        throw new ArgumentNullException(nameof(board));

                LastMove = lastMove;

                PlayerWinningSequences =
                    playerWinningSequences ??
                    throw new ArgumentNullException(
                        nameof(playerWinningSequences));

                EnemyWinningSequences =
                    enemyWinningSequences ??
                    throw new ArgumentNullException(
                        nameof(enemyWinningSequences));
            }
        }

        /// <summary>
        /// Pacote imutável com toda a resolução autoritativa de uma rodada.
        ///
        /// Os snapshots dos dois combatentes facilitam interface, logs e replay.
        /// O DamageReport já contém o antes/depois do alvo, mas conservar ambos
        /// os lados aqui evita que um observador precise consultar estados
        /// mutáveis mais tarde.
        /// </summary>
        public sealed class RoundResolution
        {
            public RoundResult Round { get; }
            public ScorePipelineResult PlayerScore { get; }
            public ScorePipelineResult EnemyScore { get; }
            public ClashReport Clash { get; }
            public DamageReport Damage { get; }

            public CombatantSnapshot PlayerBefore { get; }
            public CombatantSnapshot PlayerAfter { get; }
            public CombatantSnapshot EnemyBefore { get; }
            public CombatantSnapshot EnemyAfter { get; }

            public bool CausedEncounterEnd =>
                PlayerAfter.IsDefeated || EnemyAfter.IsDefeated;

            internal RoundResolution(
                RoundResult round,
                ScorePipelineResult playerScore,
                ScorePipelineResult enemyScore,
                ClashReport clash,
                DamageReport damage,
                CombatantSnapshot playerBefore,
                CombatantSnapshot playerAfter,
                CombatantSnapshot enemyBefore,
                CombatantSnapshot enemyAfter)
            {
                Round = round ??
                        throw new ArgumentNullException(nameof(round));

                PlayerScore = playerScore ??
                              throw new ArgumentNullException(nameof(playerScore));

                EnemyScore = enemyScore ??
                             throw new ArgumentNullException(nameof(enemyScore));

                Clash = clash ??
                        throw new ArgumentNullException(nameof(clash));

                Damage = damage ??
                         throw new ArgumentNullException(nameof(damage));

                PlayerBefore = playerBefore ??
                               throw new ArgumentNullException(nameof(playerBefore));

                PlayerAfter = playerAfter ??
                              throw new ArgumentNullException(nameof(playerAfter));

                EnemyBefore = enemyBefore ??
                              throw new ArgumentNullException(nameof(enemyBefore));

                EnemyAfter = enemyAfter ??
                             throw new ArgumentNullException(nameof(enemyAfter));

                ValidateContracts();
            }

            private void ValidateContracts()
            {
                if (PlayerScore.Participant != ScoreActor.Player ||
                    EnemyScore.Participant != ScoreActor.Enemy)
                {
                    throw new ArgumentException(
                        "Os placares do RoundResolution pertencem aos lados errados.");
                }

                if (Round.Board.Version != PlayerScore.BoardVersion ||
                    Round.Board.Version != EnemyScore.BoardVersion)
                {
                    throw new ArgumentException(
                        "A rodada e os placares precisam pertencer à mesma " +
                        "versão do tabuleiro.");
                }

                if (!ReferenceEquals(
                        Clash.PlayerScoreResult,
                        PlayerScore) ||
                    !ReferenceEquals(
                        Clash.EnemyScoreResult,
                        EnemyScore))
                {
                    throw new ArgumentException(
                        "O confronto precisa ter sido criado a partir dos " +
                        "placares armazenados no RoundResolution.");
                }

                if (!ReferenceEquals(Damage.Clash, Clash))
                {
                    throw new ArgumentException(
                        "O dano precisa ter sido originado pelo confronto " +
                        "armazenado no RoundResolution.");
                }

                ValidateSnapshotPair(
                    PlayerBefore,
                    PlayerAfter,
                    ScoreActor.Player,
                    "Player");

                ValidateSnapshotPair(
                    EnemyBefore,
                    EnemyAfter,
                    ScoreActor.Enemy,
                    "Enemy");
            }

            private static void ValidateSnapshotPair(
                CombatantSnapshot before,
                CombatantSnapshot after,
                ScoreActor expectedActor,
                string sideName)
            {
                if (before.Actor != expectedActor ||
                    after.Actor != expectedActor)
                {
                    throw new ArgumentException(
                        "Os snapshots de " + sideName +
                        " pertencem ao participante errado.");
                }

                if (!string.Equals(
                        before.CombatantId,
                        after.CombatantId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Os snapshots anterior e posterior de " +
                        sideName +
                        " precisam representar o mesmo combatente.");
                }
            }
        }

        /// <summary>
        /// Relatório final do encontro.
        ///
        /// Ele é emitido somente depois da apresentação da última rodada ou
        /// imediatamente quando não existe um CombatReportAnimator.
        /// </summary>
        public sealed class EncounterResult
        {
            public EncounterOutcome Outcome { get; }
            public RoundResolution FinalRound { get; }
            public CombatantSnapshot Player { get; }
            public CombatantSnapshot Enemy { get; }

            internal EncounterResult(
                EncounterOutcome outcome,
                RoundResolution finalRound,
                CombatantSnapshot player,
                CombatantSnapshot enemy)
            {
                Outcome = outcome;

                FinalRound = finalRound ??
                             throw new ArgumentNullException(nameof(finalRound));

                Player = player ??
                         throw new ArgumentNullException(nameof(player));

                Enemy = enemy ??
                        throw new ArgumentNullException(nameof(enemy));
            }
        }

        private const CellMark PlayerMark = CellMark.X;
        private const CellMark EnemyMark = CellMark.O;

        // ==========================================================
        // REFERÊNCIAS DA CENA
        // ==========================================================

        [Header("Referências")]
        [Tooltip(
            "View que cria as casas, exibe o BoardState e comunica cliques.")]
        [SerializeField]
        private BoardView _boardView;

        [Tooltip(
            "Apresentador opcional. Se ficar vazio, toda a regra continuará " +
            "sendo resolvida e a rodada será finalizada imediatamente.")]
        [SerializeField]
        private CombatReportAnimator _combatReportAnimator;

        // ==========================================================
        // CONFIGURAÇÃO DO ENCONTRO PADRÃO
        // ==========================================================

        [Header("Encontro padrão para desenvolvimento")] [SerializeField]
        private bool _startDefaultEncounterOnStart = true;

        [Min(1)] [SerializeField] private int _defaultPlayerMaximumHealth = 100;

        [Min(1)] [SerializeField] private int _defaultEnemyMaximumHealth = 100;

        // ==========================================================
        // CONFIGURAÇÃO DO TABULEIRO PADRÃO
        // ==========================================================

        [Header("Tabuleiro padrão")] [Min(1)] [SerializeField]
        private int _defaultBoardWidth = 3;

        [Min(1)] [SerializeField] private int _defaultBoardHeight = 3;

        [Min(2)] [SerializeField] private int _defaultRequiredSequenceLength = 3;

        [Tooltip(
            "Adiciona Golden à casa central quando largura e altura " +
            "são ímpares.")]
        [SerializeField]
        private bool _addGoldenCenterCell = true;

        [SerializeField] private EncounterSide _defaultStartingSide =
            EncounterSide.Player;

        // ==========================================================
        // CONFIGURAÇÃO DA PONTUAÇÃO
        // ==========================================================

        [Header("Pontuação padrão")] [Min(2)] [SerializeField]
        private int _defaultMinimumScoringSequenceLength = 2;

        [Min(2)] [SerializeField] private int _defaultMaximumScoringSequenceLength = 3;

        [Min(0)]
        [Tooltip(
            "Multiplicador de vitória em porcentagem. " +
            "150 significa x1,5 e 100 significa x1. " +
            "Usar inteiro evita imprecisão de float no domínio.")]
        [SerializeField]
        private int _defaultVictoryMultiplierPercent = 150;

        // ==========================================================
        // CONFIGURAÇÃO DA IA
        // ==========================================================

        [Header("IA básica")] [Min(1)] [SerializeField]
        private int _enemyMaximumSearchDepth = 10;

        [Min(1)] [SerializeField] private int _enemyMaximumVisitedNodes = 100_000;

        [Min(0f)] [SerializeField] private float _enemyThinkingDelay = 0.35f;

        // ==========================================================
        // TRANSIÇÃO ENTRE RODADAS
        // ==========================================================

        [Header("Transição entre rodadas padrão")]
        [Tooltip(
            "Quando ativo, cria outro tabuleiro padrão se ambos continuarem " +
            "vivos. Rodadas especiais iniciadas externamente nunca são " +
            "substituídas automaticamente por um tabuleiro comum.")]
        [SerializeField]
        private bool _automaticallyStartNextDefaultRound = true;

        [Min(0f)] [SerializeField] private float _nextRoundDelay = 1.25f;

        // ==========================================================
        // SERVIÇOS DE DOMÍNIO E APLICAÇÃO
        // ==========================================================

        private MoveValidator _moveValidator;
        private MoveService _moveService;
        private SequenceEvaluator _sequenceEvaluator;
        private BasicTicTacToePolicy _enemyPolicy;
        private ScorePipeline _scorePipeline;
        private ClashResolver _clashResolver;
        private DamageResolver _damageResolver;

        // ==========================================================
        // COROUTINES CONTROLADAS PELO ENCONTRO
        // ==========================================================

        private Coroutine _enemyTurnRoutine;
        private Coroutine _presentationRoutine;
        private Coroutine _nextRoundRoutine;

        // ==========================================================
        // CONTROLE INTERNO
        // ==========================================================

        private bool _isListeningToBoardView;
        private bool _currentRoundUsesDefaultBoard;
        private bool _presentationCompletionPending;
        private int _roundGeneration;

        // ==========================================================
        // ESTADO PÚBLICO PARA CONSULTA
        // ==========================================================

        public BoardState Board { get; private set; }

        public CombatantState PlayerState { get; private set; }
        public CombatantState EnemyState { get; private set; }

        public EncounterSide CurrentSide { get; private set; }

        public EncounterPhase Phase { get; private set; } =
            EncounterPhase.NotStarted;

        public int RoundNumber { get; private set; }
        public int RequiredSequenceLength { get; private set; }
        public int MinimumScoringSequenceLength { get; private set; }
        public int MaximumScoringSequenceLength { get; private set; }
        public decimal VictoryMultiplier { get; private set; }

        public RoundResult LastRoundResult { get; private set; }

        public RoundResolution LastRoundResolution { get; private set; }

        public EncounterResult LastEncounterResult { get; private set; }

        public bool HasCombatants =>
            PlayerState != null && EnemyState != null;

        public bool IsRoundActive =>
            Phase == EncounterPhase.WaitingForPlayer ||
            Phase == EncounterPhase.WaitingForEnemy ||
            Phase == EncounterPhase.ResolvingMove;

        public bool IsEncounterOver =>
            Phase == EncounterPhase.EncounterEnded;

        // ==========================================================
        // EVENTOS
        // ==========================================================

        public event Action<CombatantState, CombatantState>
            EncounterStarted;

        public event Action<BoardState>
            RoundStarted;

        public event Action<EncounterSide>
            TurnChanged;

        public event Action<EncounterPhase>
            PhaseChanged;

        public event Action<AppliedMove>
            MoveApplied;

        public event Action<RejectedMove>
            MoveRejected;

        public event Action<RoundResult>
            RoundBoardCompleted;

        public event Action<RoundResolution>
            RoundResolved;

        public event Action<RoundResolution>
            RoundPresentationCompleted;

        public event Action<EncounterResult>
            EncounterEnded;

        public event Action<Exception>
            Faulted;

        // ==========================================================
        // CICLO DE VIDA DA UNITY
        // ==========================================================

        private void Awake()
        {
            ValidateUnityConfiguration();
            EnsureServicesAreCreated();
        }

        private void OnEnable()
        {
            StartListeningToBoardView();
            SynchronizeInputAvailability();

            /*
             * Desabilitar um MonoBehaviour interrompe suas coroutines.
             *
             * O domínio já pode ter sido resolvido antes da animação. Nesse
             * caso, ao habilitar o componente concluímos somente a apresentação
             * pendente e seguimos o fluxo.
             *
             * O dano nunca é aplicado novamente.
             */
            if (Phase ==
                EncounterPhase.PresentingRoundResolution &&
                _presentationCompletionPending &&
                LastRoundResolution != null)
            {
                if (_combatReportAnimator != null &&
                    HasCombatants)
                {
                    _combatReportAnimator.SynchronizeCombatants(
                        PlayerState,
                        EnemyState);
                }

                FinishRoundLifecycle(
                    LastRoundResolution,
                    _roundGeneration,
                    Board);

                return;
            }

            if (Phase == EncounterPhase.WaitingForEnemy)
            {
                ScheduleEnemyTurn();
            }
            else if (
                Phase == EncounterPhase.WaitingForNextRound)
            {
                ScheduleNextDefaultRoundIfAllowed();
            }
        }

        private void Start()
        {
            if (_startDefaultEncounterOnStart &&
                !HasCombatants)
            {
                StartDefaultEncounter();
            }
        }

        private void OnDisable()
        {
            StopListeningToBoardView();
            StopEnemyTurnRoutine();
            StopPresentationRoutine();
            StopNextRoundRoutine();

            if (_combatReportAnimator != null)
            {
                _combatReportAnimator.ResetTemporaryPresentation();
            }

            if (_boardView != null)
            {
                _boardView.SetInputEnabled(false);
            }
        }

        // ==========================================================
        // INÍCIO DO ENCONTRO
        // ==========================================================

        /// <summary>
        /// Cria os combatentes e inicia o encontro configurado no Inspector.
        ///
        /// Esta conveniência é útil durante o desenvolvimento.
        ///
        /// Futuramente, RunState ou EncounterDefinition fornecerá estados com
        /// vida, escudo e identidade provenientes da progressão real.
        /// </summary>
        public void StartDefaultEncounter()
        {
            ValidateDefaultConfiguration();

            CombatantState player = new CombatantState(
                "player",
                ScoreActor.Player,
                _defaultPlayerMaximumHealth);

            CombatantState enemy = new CombatantState(
                "enemy:default",
                ScoreActor.Enemy,
                _defaultEnemyMaximumHealth);

            ConfigureCombatants(player, enemy);
            StartDefaultRound();
        }

        /// <summary>
        /// Inicia um encontro especial com combatentes e tabuleiro fornecidos.
        ///
        /// Esse método pode ser utilizado para carregar um save, enfrentar um
        /// boss ou iniciar um cenário de teste.
        ///
        /// A rodada é marcada como externa. Portanto, o controlador não criará
        /// um tabuleiro padrão automaticamente ao final.
        /// </summary>
        public void StartEncounter(
            CombatantState playerState,
            CombatantState enemyState,
            BoardState board,
            int requiredSequenceLength,
            int minimumScoringSequenceLength,
            int maximumScoringSequenceLength,
            decimal victoryMultiplier,
            EncounterSide startingSide)
        {
            ConfigureCombatants(
                playerState,
                enemyState);

            StartRoundInternal(
                board,
                requiredSequenceLength,
                minimumScoringSequenceLength,
                maximumScoringSequenceLength,
                victoryMultiplier,
                startingSide,
                usesDefaultBoard: false);
        }

        /// <summary>
        /// Troca os estados autoritativos do encontro sem iniciar uma rodada.
        ///
        /// Não use este método para alterar a vida durante o combate.
        /// Ele representa a composição de um novo encontro e cancela qualquer
        /// fluxo anterior.
        /// </summary>
        public void ConfigureCombatants(
            CombatantState playerState,
            CombatantState enemyState)
        {
            ValidateCombatants(
                playerState,
                enemyState);

            ValidateUnityConfiguration();
            EnsureServicesAreCreated();

            StopAllManagedRoutines();

            /*
             * Toda vez que um encontro ou rodada é substituído, a geração
             * aumenta. Coroutines antigas guardam a geração anterior e não
             * conseguem alterar o novo encontro.
             */
            _roundGeneration++;

            PlayerState = playerState;
            EnemyState = enemyState;

            Board = null;
            RoundNumber = 0;

            RequiredSequenceLength = 0;
            MinimumScoringSequenceLength = 0;
            MaximumScoringSequenceLength = 0;
            VictoryMultiplier = 0m;

            LastRoundResult = null;
            LastRoundResolution = null;
            LastEncounterResult = null;

            _currentRoundUsesDefaultBoard = false;
            _presentationCompletionPending = false;

            SetPhase(EncounterPhase.NotStarted);

            if (_combatReportAnimator != null)
            {
                _combatReportAnimator.SynchronizeCombatants(
                    PlayerState,
                    EnemyState);
            }

            EncounterStarted?.Invoke(
                PlayerState,
                EnemyState);
        }

        // ==========================================================
        // INÍCIO DAS RODADAS
        // ==========================================================

        /// <summary>
        /// Cria uma nova rodada padrão usando os combatentes atuais.
        /// </summary>
        public void StartDefaultRound()
        {
            ValidateDefaultConfiguration();
            EnsureEncounterCanStartRound();

            BoardDefinition definition =
                BoardDefinition.CreateRectangular(
                    _defaultBoardWidth,
                    _defaultBoardHeight);

            List<CellState> initialCells =
                new List<CellState>();

            /*
             * Se largura e altura forem ímpares, existe uma única casa central.
             * Essa casa recebe o modificador Golden para manter a regra padrão
             * estabelecida na Fase 1.
             */
            if (_addGoldenCenterCell &&
                _defaultBoardWidth % 2 != 0 &&
                _defaultBoardHeight % 2 != 0)
            {
                BoardCoordinate center =
                    new BoardCoordinate(
                        _defaultBoardWidth / 2,
                        _defaultBoardHeight / 2);

                initialCells.Add(
                    new CellState(
                        center,
                        CellMark.None,
                        new[]
                        {
                            CellModifierId.Golden
                        }));
            }

            BoardState board =
                new BoardState(
                    definition,
                    initialCells);

            StartRoundInternal(
                board,
                _defaultRequiredSequenceLength,
                _defaultMinimumScoringSequenceLength,
                _defaultMaximumScoringSequenceLength,
                GetDefaultVictoryMultiplier(),
                _defaultStartingSide,
                usesDefaultBoard: true);
        }

        /// <summary>
        /// Inicia uma rodada externa usando a configuração de pontuação padrão.
        /// </summary>
        public void StartRound(
            BoardState board,
            int requiredSequenceLength,
            EncounterSide startingSide)
        {
            StartRoundInternal(
                board,
                requiredSequenceLength,
                _defaultMinimumScoringSequenceLength,
                _defaultMaximumScoringSequenceLength,
                GetDefaultVictoryMultiplier(),
                startingSide,
                usesDefaultBoard: false);
        }

        /// <summary>
        /// Inicia uma rodada externa com configuração explícita de pontuação.
        ///
        /// Essa sobrecarga permite que encontros especiais mudem o intervalo de
        /// sequências ou o multiplicador sem adicionar condicionais de boss aqui.
        /// </summary>
        public void StartRound(
            BoardState board,
            int requiredSequenceLength,
            int minimumScoringSequenceLength,
            int maximumScoringSequenceLength,
            decimal victoryMultiplier,
            EncounterSide startingSide)
        {
            StartRoundInternal(
                board,
                requiredSequenceLength,
                minimumScoringSequenceLength,
                maximumScoringSequenceLength,
                victoryMultiplier,
                startingSide,
                usesDefaultBoard: false);
        }

        private void StartRoundInternal(
            BoardState board,
            int requiredSequenceLength,
            int minimumScoringSequenceLength,
            int maximumScoringSequenceLength,
            decimal victoryMultiplier,
            EncounterSide startingSide,
            bool usesDefaultBoard)
        {
            ValidateRoundInputs(
                board,
                requiredSequenceLength,
                minimumScoringSequenceLength,
                maximumScoringSequenceLength,
                victoryMultiplier,
                startingSide);

            EnsureEncounterCanStartRound();
            ValidateUnityConfiguration();
            EnsureServicesAreCreated();

            StopAllManagedRoutines();

            _roundGeneration++;

            int startedGeneration =
                _roundGeneration;

            Board = board;
            RoundNumber++;

            RequiredSequenceLength =
                requiredSequenceLength;

            MinimumScoringSequenceLength =
                minimumScoringSequenceLength;

            MaximumScoringSequenceLength =
                maximumScoringSequenceLength;

            VictoryMultiplier =
                victoryMultiplier;

            CurrentSide =
                startingSide;

            LastRoundResult = null;
            LastRoundResolution = null;

            _currentRoundUsesDefaultBoard =
                usesDefaultBoard;

            _presentationCompletionPending =
                false;

            SetPhase(EncounterPhase.NotStarted);

            _boardView.RenderBoard(Board);
            _boardView.ClearAllHighlights();

            if (_combatReportAnimator != null)
            {
                _combatReportAnimator.ResetTemporaryPresentation();

                _combatReportAnimator.SynchronizeCombatants(
                    PlayerState,
                    EnemyState);
            }

            RoundStarted?.Invoke(Board);

            /*
             * Um observador pode iniciar outro encontro durante o evento.
             * Conferir a geração impede a continuação de uma rodada que deixou
             * de ser a rodada atual.
             */
            if (!IsCurrentRound(
                    startedGeneration,
                    board))
            {
                return;
            }

            if (TryFinishTerminalInitialBoard(
                    startedGeneration))
            {
                return;
            }

            BeginTurn(
                startingSide,
                startedGeneration);
        }

        // ==========================================================
        // JOGADA DO JOGADOR
        // ==========================================================

        private void HandleBoardCellClicked(
            BoardCoordinate coordinate)
        {
            if (Phase !=
                EncounterPhase.WaitingForPlayer ||
                CurrentSide != EncounterSide.Player ||
                Board == null)
            {
                return;
            }

            try
            {
                ResolveMove(
                    EncounterSide.Player,
                    coordinate,
                    _roundGeneration);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        // ==========================================================
        // APLICAÇÃO COMPARTILHADA DAS JOGADAS
        // ==========================================================

        /// <summary>
        /// Ponto único de aplicação das jogadas do jogador e da IA.
        ///
        /// Nenhum dos dois altera o BoardState diretamente. Ambos enviam sua
        /// intenção ao mesmo MoveService.
        /// </summary>
        private void ResolveMove(
            EncounterSide side,
            BoardCoordinate coordinate,
            int expectedGeneration)
        {
            if (!IsCurrentRound(
                    expectedGeneration,
                    Board))
            {
                return;
            }

            if (side != CurrentSide ||
                !IsExpectedWaitingPhase(side))
            {
                throw new InvalidOperationException(
                    side +
                    " tentou jogar fora de seu turno. Fase atual: " +
                    Phase +
                    "; participante atual: " +
                    CurrentSide +
                    ".");
            }

            BoardState activeBoard =
                Board;

            CellMark mark =
                GetMark(side);

            SetPhase(
                EncounterPhase.ResolvingMove);

            MoveValidationResult validation =
                _moveService.TryApplyNormalMove(
                    activeBoard,
                    coordinate,
                    mark);

            if (!validation.IsValid)
            {
                /*
                 * Uma jogada inválida do jogador pode acontecer normalmente,
                 * por exemplo quando ele clica em uma casa já ocupada.
                 *
                 * Uma jogada inválida da IA representa falha da política,
                 * porque a IA deveria escolher apenas ações legais.
                 */
                if (side == EncounterSide.Enemy)
                {
                    throw new InvalidOperationException(
                        "A IA escolheu uma jogada rejeitada pelo " +
                        "MoveService: " +
                        validation.RejectionReason +
                        ".");
                }

                SetPhase(
                    EncounterPhase.WaitingForPlayer);

                MoveRejected?.Invoke(
                    new RejectedMove(
                        side,
                        mark,
                        coordinate,
                        validation.RejectionReason));

                return;
            }

            /*
             * O domínio já foi alterado. Agora pedimos à apresentação que
             * desenhe apenas a casa modificada.
             */
            _boardView.RefreshCell(coordinate);

            IReadOnlyList<SequenceMatch>
                winningSequences =
                    _sequenceEvaluator
                        .EvaluateWinningSequences(
                            activeBoard,
                            mark,
                            RequiredSequenceLength);

            MoveApplied?.Invoke(
                new AppliedMove(
                    side,
                    mark,
                    coordinate,
                    activeBoard.Version,
                    winningSequences));

            if (!IsCurrentRound(
                    expectedGeneration,
                    activeBoard))
            {
                return;
            }

            if (winningSequences.Count > 0)
            {
                CompleteRound(
                    RoundEndReason.WinningSequence,
                    coordinate,
                    expectedGeneration);

                return;
            }

            EncounterSide nextSide =
                GetOppositeSide(side);

            IReadOnlyList<BoardCoordinate>
                nextLegalMoves =
                    _moveValidator.GetLegalNormalMoves(
                        activeBoard,
                        GetMark(nextSide));

            if (nextLegalMoves.Count == 0)
            {
                CompleteRound(
                    RoundEndReason.NoLegalMove,
                    coordinate,
                    expectedGeneration);

                return;
            }

            BeginTurn(
                nextSide,
                expectedGeneration);
        }

        private void BeginTurn(
            EncounterSide side,
            int expectedGeneration)
        {
            if (!IsCurrentRound(
                    expectedGeneration,
                    Board))
            {
                return;
            }

            ValidateSide(
                side,
                nameof(side));

            CurrentSide = side;

            SetPhase(
                side == EncounterSide.Player
                    ? EncounterPhase.WaitingForPlayer
                    : EncounterPhase.WaitingForEnemy);

            TurnChanged?.Invoke(side);

            if (!IsCurrentRound(
                    expectedGeneration,
                    Board))
            {
                return;
            }

            if (side == EncounterSide.Enemy)
            {
                ScheduleEnemyTurn();
            }
        }

        // ==========================================================
        // TURNO DA IA
        // ==========================================================

        private void ScheduleEnemyTurn()
        {
            if (!isActiveAndEnabled ||
                Phase != EncounterPhase.WaitingForEnemy ||
                Board == null ||
                _enemyTurnRoutine != null)
            {
                return;
            }

            _enemyTurnRoutine =
                StartCoroutine(
                    ExecuteEnemyTurn(
                        _roundGeneration,
                        Board));
        }

        private IEnumerator ExecuteEnemyTurn(
            int expectedGeneration,
            BoardState expectedBoard)
        {
            if (_enemyThinkingDelay > 0f)
            {
                yield return
                    new WaitForSeconds(
                        _enemyThinkingDelay);
            }
            else
            {
                /*
                 * Mesmo com atraso zero, aguardamos um frame para a Unity
                 * desenhar a jogada anterior antes da resposta da IA.
                 */
                yield return null;
            }

            if (!IsCurrentRound(
                    expectedGeneration,
                    expectedBoard) ||
                Phase != EncounterPhase.WaitingForEnemy ||
                CurrentSide != EncounterSide.Enemy)
            {
                _enemyTurnRoutine = null;
                yield break;
            }

            bool foundMove;
            BoardCoordinate chosenMove;

            try
            {
                foundMove =
                    _enemyPolicy.TryChooseMove(
                        expectedBoard,
                        EnemyMark,
                        PlayerMark,
                        RequiredSequenceLength,
                        out chosenMove);
            }
            catch (Exception exception)
            {
                _enemyTurnRoutine = null;
                Fail(exception);
                yield break;
            }

            _enemyTurnRoutine = null;

            try
            {
                if (!foundMove)
                {
                    CompleteRound(
                        RoundEndReason.NoLegalMove,
                        null,
                        expectedGeneration);

                    yield break;
                }

                ResolveMove(
                    EncounterSide.Enemy,
                    chosenMove,
                    expectedGeneration);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        // ==========================================================
        // ENCERRAMENTO DE TABULEIROS INICIAIS ESPECIAIS
        // ==========================================================

        private bool TryFinishTerminalInitialBoard(
            int expectedGeneration)
        {
            IReadOnlyList<SequenceMatch> playerWins =
                EvaluateWinningSequences(PlayerMark);

            IReadOnlyList<SequenceMatch> enemyWins =
                EvaluateWinningSequences(EnemyMark);

            if (playerWins.Count > 0 ||
                enemyWins.Count > 0)
            {
                CompleteRound(
                    RoundEndReason
                        .InitialBoardWasAlreadyTerminal,
                    null,
                    expectedGeneration,
                    playerWins,
                    enemyWins);

                return true;
            }

            IReadOnlyList<BoardCoordinate>
                startingMoves =
                    _moveValidator.GetLegalNormalMoves(
                        Board,
                        GetMark(CurrentSide));

            if (startingMoves.Count > 0)
            {
                return false;
            }

            CompleteRound(
                RoundEndReason.NoLegalMove,
                null,
                expectedGeneration,
                playerWins,
                enemyWins);

            return true;
        }

        // ==========================================================
        // RESOLUÇÃO DA RODADA
        // ==========================================================

        /// <summary>
        /// Fecha o tabuleiro, resolve score, confronto e dano e somente depois
        /// inicia a apresentação.
        /// </summary>
        private void CompleteRound(
            RoundEndReason reason,
            BoardCoordinate? lastMove,
            int expectedGeneration,
            IReadOnlyList<SequenceMatch>
                knownPlayerWins = null,
            IReadOnlyList<SequenceMatch>
                knownEnemyWins = null)
        {
            BoardState completedBoard =
                Board;

            if (!IsCurrentRound(
                    expectedGeneration,
                    completedBoard))
            {
                return;
            }

            SetPhase(
                EncounterPhase.ResolvingRound);

            IReadOnlyList<SequenceMatch> playerWins =
                knownPlayerWins ??
                EvaluateWinningSequences(PlayerMark);

            IReadOnlyList<SequenceMatch> enemyWins =
                knownEnemyWins ??
                EvaluateWinningSequences(EnemyMark);

            RoundOutcome outcome =
                DetermineRoundOutcome(
                    playerWins,
                    enemyWins);

            RoundResult round =
                new RoundResult(
                    RoundNumber,
                    outcome,
                    reason,
                    completedBoard,
                    lastMove,
                    playerWins,
                    enemyWins);

            LastRoundResult = round;

            RoundBoardCompleted?.Invoke(round);

            /*
             * Um inscrito no evento pode ter iniciado outra rodada.
             */
            if (!IsCurrentRound(
                    expectedGeneration,
                    completedBoard))
            {
                return;
            }

            RoundResolution resolution =
                ResolveRound(round);

            LastRoundResolution = resolution;
            _presentationCompletionPending = true;

            RoundResolved?.Invoke(resolution);

            if (!IsCurrentRound(
                    expectedGeneration,
                    completedBoard) ||
                !_presentationCompletionPending)
            {
                return;
            }

            /*
             * O animador é opcional.
             *
             * A ausência dele não chama outro cálculo. O domínio já resolveu
             * pontuação, confronto e dano.
             */
            if (_combatReportAnimator == null ||
                !isActiveAndEnabled)
            {
                FinishRoundLifecycle(
                    resolution,
                    expectedGeneration,
                    completedBoard);

                return;
            }

            _combatReportAnimator.ValidateResolution(
                resolution.PlayerScore,
                resolution.EnemyScore,
                resolution.Clash,
                resolution.Damage,
                PlayerState,
                EnemyState);

            SetPhase(
                EncounterPhase
                    .PresentingRoundResolution);

            _presentationRoutine =
                StartCoroutine(
                    PresentRound(
                        resolution,
                        expectedGeneration,
                        completedBoard));
        }

        /// <summary>
        /// Executa toda a parte autoritativa da resolução.
        ///
        /// Quando este método termina, a vida e o escudo já possuem seus valores
        /// finais. A animação apenas mostrará essa transição.
        /// </summary>
        private RoundResolution ResolveRound(
            RoundResult round)
        {
            CombatantSnapshot playerBefore =
                CombatantSnapshot.Capture(
                    PlayerState);

            CombatantSnapshot enemyBefore =
                CombatantSnapshot.Capture(
                    EnemyState);

            ScorePipelineResult playerScore =
                _scorePipeline.Resolve(
                    new ScorePipelineRequest(
                        ScoreActor.Player,
                        PlayerMark,
                        round.Board,
                        MinimumScoringSequenceLength,
                        MaximumScoringSequenceLength,
                        round.Outcome ==
                        RoundOutcome.PlayerVictory,
                        VictoryMultiplier));

            ScorePipelineResult enemyScore =
                _scorePipeline.Resolve(
                    new ScorePipelineRequest(
                        ScoreActor.Enemy,
                        EnemyMark,
                        round.Board,
                        MinimumScoringSequenceLength,
                        MaximumScoringSequenceLength,
                        round.Outcome ==
                        RoundOutcome.EnemyVictory,
                        VictoryMultiplier));

            ClashReport clash =
                _clashResolver.Resolve(
                    playerScore,
                    enemyScore);

            /*
             * Esta é a única chamada que altera vida e escudo.
             *
             * Ela acontece antes da animação e também quando o animador não
             * existe. Não há fallback com Mathf.Min nem subtração manual no
             * controlador.
             */
            DamageReport damage =
                _damageResolver.Resolve(
                    clash,
                    PlayerState,
                    EnemyState);

            CombatantSnapshot playerAfter =
                CombatantSnapshot.Capture(
                    PlayerState);

            CombatantSnapshot enemyAfter =
                CombatantSnapshot.Capture(
                    EnemyState);

            return new RoundResolution(
                round,
                playerScore,
                enemyScore,
                clash,
                damage,
                playerBefore,
                playerAfter,
                enemyBefore,
                enemyAfter);
        }

        // ==========================================================
        // APRESENTAÇÃO DA RODADA
        // ==========================================================

        private IEnumerator PresentRound(
            RoundResolution resolution,
            int expectedGeneration,
            BoardState expectedBoard)
        {
            yield return
                _combatReportAnimator.PlayResolution(
                    resolution.PlayerScore,
                    resolution.EnemyScore,
                    resolution.Clash,
                    resolution.Damage,
                    PlayerState,
                    EnemyState);

            _presentationRoutine = null;

            FinishRoundLifecycle(
                resolution,
                expectedGeneration,
                expectedBoard);
        }

        /// <summary>
        /// Continua o fluxo depois que a apresentação visual termina.
        ///
        /// Este método não calcula e não aplica dano.
        /// </summary>
        private void FinishRoundLifecycle(
            RoundResolution resolution,
            int expectedGeneration,
            BoardState expectedBoard)
        {
            if (!IsCurrentRound(
                    expectedGeneration,
                    expectedBoard) ||
                !_presentationCompletionPending ||
                !ReferenceEquals(
                    LastRoundResolution,
                    resolution))
            {
                return;
            }

            _presentationCompletionPending = false;

            if (_combatReportAnimator != null)
            {
                _combatReportAnimator.SynchronizeCombatants(
                    PlayerState,
                    EnemyState);
            }

            RoundPresentationCompleted?.Invoke(
                resolution);

            if (!IsCurrentRound(
                    expectedGeneration,
                    expectedBoard))
            {
                return;
            }

            if (PlayerState.IsDefeated ||
                EnemyState.IsDefeated)
            {
                CompleteEncounter(resolution);
                return;
            }

            SetPhase(
                EncounterPhase.WaitingForNextRound);

            ScheduleNextDefaultRoundIfAllowed();
        }

        // ==========================================================
        // ENCERRAMENTO DO ENCONTRO
        // ==========================================================

        private void CompleteEncounter(
            RoundResolution finalRound)
        {
            StopAllManagedRoutines();

            EncounterOutcome outcome =
                DetermineEncounterOutcome(
                    PlayerState,
                    EnemyState);

            EncounterResult result =
                new EncounterResult(
                    outcome,
                    finalRound,
                    CombatantSnapshot.Capture(
                        PlayerState),
                    CombatantSnapshot.Capture(
                        EnemyState));

            LastEncounterResult = result;

            SetPhase(
                EncounterPhase.EncounterEnded);

            EncounterEnded?.Invoke(result);
        }

        // ==========================================================
        // PRÓXIMA RODADA PADRÃO
        // ==========================================================

        private void ScheduleNextDefaultRoundIfAllowed()
        {
            if (!isActiveAndEnabled ||
                Phase !=
                EncounterPhase.WaitingForNextRound ||
                !_currentRoundUsesDefaultBoard ||
                !_automaticallyStartNextDefaultRound ||
                !HasCombatants ||
                PlayerState.IsDefeated ||
                EnemyState.IsDefeated ||
                _nextRoundRoutine != null)
            {
                return;
            }

            _nextRoundRoutine =
                StartCoroutine(
                    WaitAndStartNextDefaultRound(
                        _roundGeneration,
                        Board));
        }

        private IEnumerator WaitAndStartNextDefaultRound(
            int expectedGeneration,
            BoardState expectedBoard)
        {
            if (_nextRoundDelay > 0f)
            {
                yield return
                    new WaitForSeconds(
                        _nextRoundDelay);
            }
            else
            {
                yield return null;
            }

            _nextRoundRoutine = null;

            if (!IsCurrentRound(
                    expectedGeneration,
                    expectedBoard) ||
                Phase !=
                EncounterPhase.WaitingForNextRound)
            {
                yield break;
            }

            try
            {
                StartDefaultRound();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        // ==========================================================
        // CONSULTAS AUXILIARES
        // ==========================================================

        private IReadOnlyList<SequenceMatch>
            EvaluateWinningSequences(
                CellMark mark)
        {
            return _sequenceEvaluator
                .EvaluateWinningSequences(
                    Board,
                    mark,
                    RequiredSequenceLength);
        }

        private static RoundOutcome DetermineRoundOutcome(
            IReadOnlyList<SequenceMatch> playerWins,
            IReadOnlyList<SequenceMatch> enemyWins)
        {
            bool playerHasWon =
                playerWins.Count > 0;

            bool enemyHasWon =
                enemyWins.Count > 0;

            /*
             * Um tabuleiro carregado pode chegar com sequência vencedora dos
             * dois lados.
             *
             * Até existir uma regra configurável para essa situação, o
             * resultado seguro e determinístico é empate.
             */
            if (playerHasWon == enemyHasWon)
            {
                return RoundOutcome.Draw;
            }

            return playerHasWon
                ? RoundOutcome.PlayerVictory
                : RoundOutcome.EnemyVictory;
        }

        private static EncounterOutcome
            DetermineEncounterOutcome(
                CombatantState playerState,
                CombatantState enemyState)
        {
            if (playerState.IsDefeated &&
                enemyState.IsDefeated)
            {
                return EncounterOutcome.Draw;
            }

            if (enemyState.IsDefeated)
            {
                return EncounterOutcome.PlayerVictory;
            }

            if (playerState.IsDefeated)
            {
                return EncounterOutcome.EnemyVictory;
            }

            throw new InvalidOperationException(
                "O encontro não pode terminar enquanto os dois " +
                "combatentes estão vivos.");
        }

        private bool IsExpectedWaitingPhase(
            EncounterSide side)
        {
            return side == EncounterSide.Player
                ? Phase ==
                  EncounterPhase.WaitingForPlayer
                : Phase ==
                  EncounterPhase.WaitingForEnemy;
        }

        private bool IsCurrentRound(
            int expectedGeneration,
            BoardState expectedBoard)
        {
            return
                expectedGeneration == _roundGeneration &&
                expectedBoard != null &&
                ReferenceEquals(
                    Board,
                    expectedBoard);
        }

        private void SetPhase(
            EncounterPhase nextPhase)
        {
            if (Phase == nextPhase)
            {
                SynchronizeInputAvailability();
                return;
            }

            Phase = nextPhase;

            SynchronizeInputAvailability();

            PhaseChanged?.Invoke(Phase);
        }

        private void SynchronizeInputAvailability()
        {
            if (_boardView == null)
            {
                return;
            }

            bool acceptsPlayerInput =
                isActiveAndEnabled &&
                Phase ==
                EncounterPhase.WaitingForPlayer;

            _boardView.SetInputEnabled(
                acceptsPlayerInput);
        }

        // ==========================================================
        // TRATAMENTO DE FALHAS
        // ==========================================================

        private void Fail(Exception exception)
        {
            if (exception == null)
            {
                exception =
                    new InvalidOperationException(
                        "O encontro entrou em falha sem " +
                        "fornecer uma exceção.");
            }

            StopAllManagedRoutines();

            _presentationCompletionPending = false;

            SetPhase(
                EncounterPhase.Faulted);

            Debug.LogException(
                exception,
                this);

            Faulted?.Invoke(exception);
        }

        // ==========================================================
        // EVENTO DA BOARD VIEW
        // ==========================================================

        private void StartListeningToBoardView()
        {
            if (_boardView == null ||
                _isListeningToBoardView)
            {
                return;
            }

            _boardView.CellClicked +=
                HandleBoardCellClicked;

            _isListeningToBoardView = true;
        }

        private void StopListeningToBoardView()
        {
            if (_boardView == null ||
                !_isListeningToBoardView)
            {
                return;
            }

            _boardView.CellClicked -=
                HandleBoardCellClicked;

            _isListeningToBoardView = false;
        }

        // ==========================================================
        // CONTROLE DAS COROUTINES
        // ==========================================================

        private void StopAllManagedRoutines()
        {
            StopEnemyTurnRoutine();
            StopPresentationRoutine();
            StopNextRoundRoutine();

            if (_combatReportAnimator != null)
            {
                _combatReportAnimator
                    .ResetTemporaryPresentation();
            }
        }

        private void StopEnemyTurnRoutine()
        {
            if (_enemyTurnRoutine == null)
            {
                return;
            }

            StopCoroutine(_enemyTurnRoutine);
            _enemyTurnRoutine = null;
        }

        private void StopPresentationRoutine()
        {
            if (_presentationRoutine == null)
            {
                return;
            }

            StopCoroutine(_presentationRoutine);
            _presentationRoutine = null;
        }

        private void StopNextRoundRoutine()
        {
            if (_nextRoundRoutine == null)
            {
                return;
            }

            StopCoroutine(_nextRoundRoutine);
            _nextRoundRoutine = null;
        }

        // ==========================================================
        // CRIAÇÃO DOS SERVIÇOS
        // ==========================================================

        private void EnsureServicesAreCreated()
        {
            if (_moveValidator == null)
            {
                _moveValidator =
                    new MoveValidator();
            }

            if (_moveService == null)
            {
                _moveService =
                    new MoveService(
                        _moveValidator);
            }

            if (_sequenceEvaluator == null)
            {
                _sequenceEvaluator =
                    new SequenceEvaluator();
            }

            if (_enemyPolicy == null)
            {
                _enemyPolicy =
                    new BasicTicTacToePolicy(
                        _enemyMaximumSearchDepth,
                        _enemyMaximumVisitedNodes);
            }

            if (_scorePipeline == null)
            {
                _scorePipeline =
                    new ScorePipeline(
                        _sequenceEvaluator);
            }

            if (_clashResolver == null)
            {
                _clashResolver =
                    new ClashResolver();
            }

            if (_damageResolver == null)
            {
                _damageResolver =
                    new DamageResolver();
            }
        }

        // ==========================================================
        // VALIDAÇÃO DO ENCONTRO
        // ==========================================================

        private void EnsureEncounterCanStartRound()
        {
            if (!HasCombatants)
            {
                throw new InvalidOperationException(
                    "Configure PlayerState e EnemyState antes " +
                    "de iniciar uma rodada.");
            }

            if (PlayerState.IsDefeated ||
                EnemyState.IsDefeated)
            {
                throw new InvalidOperationException(
                    "Não é possível iniciar outra rodada depois que " +
                    "um combatente foi derrotado. Inicie um novo encontro.");
            }
        }

        private void ValidateUnityConfiguration()
        {
            if (_boardView == null)
            {
                throw new InvalidOperationException(
                    "Informe a BoardView no EncounterController.");
            }

            if (_enemyMaximumSearchDepth <= 0)
            {
                throw new InvalidOperationException(
                    "Enemy Maximum Search Depth deve ser maior que zero.");
            }

            if (_enemyMaximumVisitedNodes <= 0)
            {
                throw new InvalidOperationException(
                    "Enemy Maximum Visited Nodes deve ser maior que zero.");
            }

            if (_enemyThinkingDelay < 0f ||
                _nextRoundDelay < 0f)
            {
                throw new InvalidOperationException(
                    "Os atrasos de apresentação não podem ser negativos.");
            }

            if (_combatReportAnimator != null)
            {
                _combatReportAnimator
                    .ValidateConfiguration();
            }
        }

        private void ValidateDefaultConfiguration()
        {
            if (_defaultPlayerMaximumHealth <= 0 ||
                _defaultEnemyMaximumHealth <= 0)
            {
                throw new InvalidOperationException(
                    "As vidas máximas padrão devem ser maiores que zero.");
            }

            if (_defaultBoardWidth <= 0 ||
                _defaultBoardHeight <= 0)
            {
                throw new InvalidOperationException(
                    "As dimensões do tabuleiro padrão devem ser " +
                    "maiores que zero.");
            }

            ValidateRoundNumbers(
                _defaultRequiredSequenceLength,
                _defaultMinimumScoringSequenceLength,
                _defaultMaximumScoringSequenceLength,
                GetDefaultVictoryMultiplier());

            ValidateSide(
                _defaultStartingSide,
                nameof(_defaultStartingSide));
        }

        private static void ValidateRoundInputs(
            BoardState board,
            int requiredSequenceLength,
            int minimumScoringSequenceLength,
            int maximumScoringSequenceLength,
            decimal victoryMultiplier,
            EncounterSide startingSide)
        {
            if (board == null)
            {
                throw new ArgumentNullException(
                    nameof(board));
            }

            ValidateRoundNumbers(
                requiredSequenceLength,
                minimumScoringSequenceLength,
                maximumScoringSequenceLength,
                victoryMultiplier);

            ValidateSide(
                startingSide,
                nameof(startingSide));
        }

        private static void ValidateRoundNumbers(
            int requiredSequenceLength,
            int minimumScoringSequenceLength,
            int maximumScoringSequenceLength,
            decimal victoryMultiplier)
        {
            if (requiredSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredSequenceLength),
                    requiredSequenceLength,
                    "A condição de vitória precisa ter tamanho mínimo 2.");
            }

            if (minimumScoringSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumScoringSequenceLength),
                    minimumScoringSequenceLength,
                    "O menor tamanho pontuado precisa ser pelo menos 2.");
            }

            if (maximumScoringSequenceLength <
                minimumScoringSequenceLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumScoringSequenceLength),
                    maximumScoringSequenceLength,
                    "O maior tamanho pontuado não pode ser menor " +
                    "que o menor.");
            }

            if (victoryMultiplier < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(victoryMultiplier),
                    victoryMultiplier,
                    "O multiplicador de vitória não pode ser negativo.");
            }
        }

        private static void ValidateCombatants(
            CombatantState playerState,
            CombatantState enemyState)
        {
            if (playerState == null)
            {
                throw new ArgumentNullException(
                    nameof(playerState));
            }

            if (enemyState == null)
            {
                throw new ArgumentNullException(
                    nameof(enemyState));
            }

            if (playerState.Actor != ScoreActor.Player)
            {
                throw new ArgumentException(
                    "playerState precisa pertencer a ScoreActor.Player.",
                    nameof(playerState));
            }

            if (enemyState.Actor != ScoreActor.Enemy)
            {
                throw new ArgumentException(
                    "enemyState precisa pertencer a ScoreActor.Enemy.",
                    nameof(enemyState));
            }

            if (string.Equals(
                    playerState.CombatantId,
                    enemyState.CombatantId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Player e Enemy precisam possuir CombatantId diferentes.");
            }

            if (playerState.IsDefeated ||
                enemyState.IsDefeated)
            {
                throw new ArgumentException(
                    "Um novo encontro precisa começar com os dois " +
                    "combatentes vivos.");
            }
        }

        // ==========================================================
        // CONVERSÕES E CONSULTAS SIMPLES
        // ==========================================================

        private decimal GetDefaultVictoryMultiplier()
        {
            return
                _defaultVictoryMultiplierPercent / 100m;
        }

        private static CellMark GetMark(
            EncounterSide side)
        {
            ValidateSide(
                side,
                nameof(side));

            return side == EncounterSide.Player
                ? PlayerMark
                : EnemyMark;
        }

        private static EncounterSide GetOppositeSide(
            EncounterSide side)
        {
            ValidateSide(
                side,
                nameof(side));

            return side == EncounterSide.Player
                ? EncounterSide.Enemy
                : EncounterSide.Player;
        }

        private static void ValidateSide(
            EncounterSide side,
            string parameterName)
        {
            if (side != EncounterSide.Player &&
                side != EncounterSide.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    side,
                    "O participante deve ser Player ou Enemy.");
            }
        }

        // ==========================================================
        // PREENCHIMENTO AUTOMÁTICO NO INSPECTOR
        // ==========================================================

        /// <summary>
        /// Reset é chamado pela Unity quando o componente é adicionado ou quando
        /// o usuário seleciona Reset no menu do componente.
        ///
        /// Ele procura BoardView e CombatReportAnimator no mesmo GameObject.
        /// </summary>
        private void Reset()
        {
            _boardView =
                GetComponent<BoardView>();

            _combatReportAnimator =
                GetComponent<CombatReportAnimator>();
        }

#if UNITY_EDITOR
        /// <summary>
        /// OnValidate é executado no Editor quando um valor do Inspector muda.
        ///
        /// Ele apenas mostra avisos. As validações autoritativas continuam nos
        /// métodos normais, pois builds do jogo não executam OnValidate.
        /// </summary>
        private void OnValidate()
        {
            if (_boardView == null)
            {
                Debug.LogWarning(
                    "Informe a BoardView no EncounterController.",
                    this);
            }

            if (_defaultRequiredSequenceLength < 2)
            {
                Debug.LogWarning(
                    "A condição padrão de vitória precisa ser pelo menos 2.",
                    this);
            }

            if (_defaultMaximumScoringSequenceLength <
                _defaultMinimumScoringSequenceLength)
            {
                Debug.LogWarning(
                    "O maior tamanho de sequência pontuada não pode ser " +
                    "menor que o menor tamanho.",
                    this);
            }
        }
#endif

        /*
         * ==========================================================
         * ADIÇÕES FUTURAS POSSÍVEIS
         * ==========================================================
         *
         * 1. EncounterDefinition deverá reunir tabuleiro, combatentes,
         *    política da IA, condição de vitória e regras de pontuação.
         *
         *    Quando esse tipo existir, StartEncounter poderá recebê-lo sem
         *    transformar o controlador em uma coleção de condicionais
         *    específicas de cada boss.
         *
         * 2. A futura fase de reação poderá ser extraída para um
         *    EncounterEngine ou ReactionRule puro.
         *
         *    Ela decidirá se uma sequência encerra o tabuleiro ou concede uma
         *    resposta. O controlador somente seguirá a decisão retornada.
         *
         * 3. Na Fase 3, um motor de efeitos determinístico produzirá
         *    contribuições de pontuação e dano em gatilhos explícitos.
         *
         *    Ele será chamado antes dos pipelines, nunca por eventos static
         *    nem pela apresentação.
         *
         * 4. IAgentPolicy permitirá substituir BasicTicTacToePolicy pelas
         *    inteligências especializadas dos bosses.
         *
         *    Todas continuarão enviando a ação escolhida para o mesmo
         *    MoveService.
         *
         * 5. A busca da IA poderá ser executada sobre um snapshot fora da
         *    thread principal.
         *
         *    O resultado precisará conservar a geração da rodada para não
         *    aplicar uma decisão atrasada depois de um reinício.
         *
         * 6. Ao derrotar o inimigo, EncounterEnded é o ponto correto para
         *    iniciar a recompensa de runas e conceder pontos de progressão.
         *
         *    A recompensa não deve acontecer em RoundResolved, pois o inimigo
         *    ainda pode continuar vivo.
         *
         * 7. Cura, dano passivo, reflexão e morte simultânea deverão produzir
         *    relatórios próprios.
         *
         *    O controlador deverá encadear esses relatórios antes de decidir
         *    se o encontro terminou, preservando a regra:
         *
         *    "domínio antes da animação".
         *
         * 8. Testes PlayMode recomendados:
         *
         *    - resolução com e sem animador;
         *    - interrupção durante a apresentação;
         *    - reinício durante a IA;
         *    - empate;
         *    - escudo total;
         *    - overkill;
         *    - derrota de cada lado;
         *    - tabuleiro inicial terminal;
         *    - rodada especial que não é substituída automaticamente.
         */
    }
}