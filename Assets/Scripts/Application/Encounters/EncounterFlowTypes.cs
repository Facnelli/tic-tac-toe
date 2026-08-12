using System;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Application.Encounters
{
    /// <summary>
    /// Identifica um dos dois lados participantes do encontro.
    ///
    /// EncounterSide é útil para input, eventos e apresentação. As regras de
    /// domínio continuam utilizando ScoreActor, pois ele também consegue
    /// representar None e Environment em relatórios de pontuação e efeitos.
    /// </summary>
    public enum EncounterSide
    {
        Player = 0,
        Enemy = 1
    }

    /// <summary>
    /// Fase autoritativa do fluxo puro do encontro.
    ///
    /// A fase não contém WaitingForPlayer e WaitingForEnemy separadamente.
    /// Enquanto WaitingForAction estiver ativa, o ator é obtido do TurnContext.
    /// Isso impede que a fase diga "Player" enquanto o TurnContext pertence ao
    /// Enemy.
    ///
    /// A apresentação poderá mapear estas fases para input, animações e atrasos,
    /// mas não poderá avançá-las por conta própria.
    /// </summary>
    public enum EncounterPhase
    {
        NotStarted = 0,

        /// <summary>
        /// Existe um TurnContext aberto e o motor aceita GameActions.
        /// </summary>
        WaitingForAction = 1,

        /// <summary>
        /// O Turno acabou e o motor está recontando sequências, consultando
        /// disponibilidade e aplicando ReactionRule.
        /// </summary>
        ResolvingTurn = 2,

        /// <summary>
        /// Uma ReactionDecision terminal foi produzida e a pontuação, o confronto
        /// e o dano da Rodada estão sendo resolvidos.
        /// </summary>
        ResolvingRound = 3,

        /// <summary>
        /// Toda resolução autoritativa da Rodada terminou. A vida já possui o
        /// valor final e a apresentação pode reproduzir os relatórios sem
        /// recalcular nem aplicar dano novamente.
        /// </summary>
        RoundResolved = 4,

        /// <summary>
        /// Os dois combatentes continuam vivos e uma nova Rodada pode começar.
        /// </summary>
        WaitingForNextRound = 5,

        /// <summary>
        /// Pelo menos um combatente foi derrotado e existe EncounterResult.
        /// </summary>
        EncounterEnded = 6,

        /// <summary>
        /// Uma inconsistência inesperada impediu a continuação segura do fluxo.
        /// </summary>
        Faulted = 7
    }

    /// <summary>
    /// Resultado competitivo de uma Rodada.
    ///
    /// Este enum resume Winner da ReactionDecision para consumidores que desejam
    /// somente distinguir vitória do Player, vitória do Enemy ou empate.
    /// </summary>
    public enum RoundOutcome
    {
        PlayerVictory = 0,
        EnemyVictory = 1,
        Draw = 2
    }

    /// <summary>
    /// Resultado final do Confronto, decidido pela vida dos combatentes depois
    /// da última resolução de dano.
    /// </summary>
    public enum EncounterOutcome
    {
        PlayerVictory = 0,
        EnemyVictory = 1,
        Draw = 2
    }

    /// <summary>
    /// Conversões centralizadas entre lado, ator e marca padrão.
    ///
    /// Manter estas associações em um único local evita que Player → X e
    /// Enemy → O sejam repetidos pelo motor, IA e apresentação.
    /// </summary>
    public static class EncounterSideRules
    {
        public static ScoreActor ToActor(
            EncounterSide side)
        {
            switch (side)
            {
                case EncounterSide.Player:
                    return ScoreActor.Player;

                case EncounterSide.Enemy:
                    return ScoreActor.Enemy;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(side),
                        side,
                        "O lado do encontro informado não existe.");
            }
        }

        public static EncounterSide FromActor(
            ScoreActor actor)
        {
            switch (actor)
            {
                case ScoreActor.Player:
                    return EncounterSide.Player;

                case ScoreActor.Enemy:
                    return EncounterSide.Enemy;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(actor),
                        actor,
                        "Somente Player ou Enemy ocupam um lado do encontro.");
            }
        }

        public static CellMark GetNormalMoveMark(
            EncounterSide side)
        {
            switch (side)
            {
                case EncounterSide.Player:
                    return CellMark.X;

                case EncounterSide.Enemy:
                    return CellMark.O;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(side),
                        side,
                        "O lado do encontro informado não existe.");
            }
        }

        public static EncounterSide GetOpposite(
            EncounterSide side)
        {
            switch (side)
            {
                case EncounterSide.Player:
                    return EncounterSide.Enemy;

                case EncounterSide.Enemy:
                    return EncounterSide.Player;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(side),
                        side,
                        "O lado do encontro informado não existe.");
            }
        }
    }

    /// <summary>
    /// Configuração lógica imutável compartilhada pelas Rodadas de um encontro.
    ///
    /// A definição do formato do tabuleiro não fica aqui. Cada Rodada recebe seu
    /// próprio BoardState, permitindo bosses e efeitos que usem formatos
    /// diferentes sem recriar os combatentes.
    /// </summary>
    public sealed class EncounterRules
    {
        /// <summary>
        /// Tamanho que caracteriza uma condição de vitória para ReactionState.
        /// </summary>
        public int RequiredSequenceLength { get; }

        /// <summary>
        /// Menor sequência que produz pontos no ScorePipeline.
        /// </summary>
        public int MinimumScoringSequenceLength { get; }

        /// <summary>
        /// Maior sequência enumerada para pontuação.
        /// </summary>
        public int MaximumScoringSequenceLength { get; }

        /// <summary>
        /// Fator concedido somente ao VictoryMultiplierRecipient da decisão
        /// terminal. Em empate nenhum participante recebe este fator.
        /// </summary>
        public decimal VictoryMultiplier { get; }

        public EncounterRules(
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
                    "Uma condição de vitória deve possuir tamanho mínimo 2.");
            }

            if (minimumScoringSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumScoringSequenceLength),
                    minimumScoringSequenceLength,
                    "O menor tamanho de pontuação deve ser pelo menos 2.");
            }

            if (maximumScoringSequenceLength <
                minimumScoringSequenceLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumScoringSequenceLength),
                    maximumScoringSequenceLength,
                    "O maior tamanho de pontuação não pode ser menor que o menor.");
            }

            if (victoryMultiplier < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(victoryMultiplier),
                    victoryMultiplier,
                    "O multiplicador de vitória não pode ser negativo.");
            }

            RequiredSequenceLength = requiredSequenceLength;
            MinimumScoringSequenceLength =
                minimumScoringSequenceLength;
            MaximumScoringSequenceLength =
                maximumScoringSequenceLength;
            VictoryMultiplier = victoryMultiplier;
        }
    }

    /// <summary>
    /// Fato imutável que encerra logicamente uma Rodada.
    ///
    /// RoundResult não decide o vencedor. Ele exige uma ReactionDecision terminal
    /// e expõe Winner, EndReason e VictoryMultiplierRecipient diretamente dessa
    /// decisão. Dessa forma, o EncounterEngine e a apresentação não conseguem
    /// deduzir outro resultado a partir das mesmas contagens.
    ///
    /// A pontuação e o dano ainda não fazem parte deste objeto. Eles aparecem em
    /// RoundResolution, produzido na etapa autoritativa seguinte.
    /// </summary>
    public sealed class RoundResult
    {
        public int RoundNumber { get; }
        public BoardState Board { get; }
        public ReactionDecision FinalDecision { get; }

        public long BoardVersion =>
            FinalDecision.CurrentState.BoardVersion;

        public ScoreActor Winner =>
            FinalDecision.Winner;

        public ReactionEndReason EndReason =>
            FinalDecision.EndReason;

        public ScoreActor VictoryMultiplierRecipient =>
            FinalDecision.VictoryMultiplierRecipient;

        public bool IsDraw =>
            FinalDecision.IsDraw;

        public RoundOutcome Outcome =>
            DetermineRoundOutcome(Winner);

        public RoundResult(
            int roundNumber,
            BoardState board,
            ReactionDecision finalDecision)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundNumber),
                    roundNumber,
                    "O número da Rodada deve ser maior que zero.");
            }

            RoundNumber = roundNumber;

            Board = board ??
                throw new ArgumentNullException(nameof(board));

            FinalDecision = finalDecision ??
                throw new ArgumentNullException(nameof(finalDecision));

            if (!FinalDecision.EndsBoardMatch)
            {
                throw new ArgumentException(
                    "RoundResult exige uma ReactionDecision que encerre a Rodada.",
                    nameof(finalDecision));
            }

            if (FinalDecision.CurrentState.BoardVersion !=
                Board.Version)
            {
                throw new ArgumentException(
                    "A decisão final e o BoardState precisam pertencer à mesma versão.",
                    nameof(finalDecision));
            }
        }

        private static RoundOutcome DetermineRoundOutcome(
            ScoreActor winner)
        {
            switch (winner)
            {
                case ScoreActor.Player:
                    return RoundOutcome.PlayerVictory;

                case ScoreActor.Enemy:
                    return RoundOutcome.EnemyVictory;

                case ScoreActor.None:
                    return RoundOutcome.Draw;

                default:
                    throw new InvalidOperationException(
                        "O vencedor da Rodada precisa ser Player, Enemy ou None.");
            }
        }
    }

    /// <summary>
    /// Pacote imutável com toda a resolução autoritativa de uma Rodada.
    ///
    /// Quando este objeto existe:
    ///
    /// 1. os dois ScorePipelineResult já foram produzidos;
    /// 2. ClashResolver já comparou os placares;
    /// 3. DamageResolver já alterou vida e escudo;
    /// 4. os snapshots registram o antes e o depois dos combatentes.
    ///
    /// A apresentação apenas reproduz esses fatos. Ela nunca chama dano outra vez.
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
            PlayerAfter.IsDefeated ||
            EnemyAfter.IsDefeated;

        public RoundResolution(
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
                    "Os placares da resolução pertencem aos lados errados.");
            }

            if (Round.BoardVersion != PlayerScore.BoardVersion ||
                Round.BoardVersion != EnemyScore.BoardVersion)
            {
                throw new ArgumentException(
                    "A Rodada e os placares precisam pertencer à mesma versão do tabuleiro.");
            }

            if (!ReferenceEquals(
                    Clash.PlayerScoreResult,
                    PlayerScore) ||
                !ReferenceEquals(
                    Clash.EnemyScoreResult,
                    EnemyScore))
            {
                throw new ArgumentException(
                    "O confronto precisa ter sido criado a partir dos placares armazenados.");
            }

            if (!ReferenceEquals(
                    Damage.Clash,
                    Clash))
            {
                throw new ArgumentException(
                    "O dano precisa ter sido originado pelo confronto armazenado.");
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

            ValidateDamageSnapshots();
        }

        private void ValidateDamageSnapshots()
        {
            if (!Damage.HasTarget)
            {
                return;
            }

            CombatantSnapshot expectedBefore =
                Damage.TargetActor == ScoreActor.Player
                    ? PlayerBefore
                    : EnemyBefore;

            CombatantSnapshot expectedAfter =
                Damage.TargetActor == ScoreActor.Player
                    ? PlayerAfter
                    : EnemyAfter;

            if (!HaveEquivalentSnapshot(
                    Damage.TargetBefore,
                    expectedBefore) ||
                !HaveEquivalentSnapshot(
                    Damage.TargetAfter,
                    expectedAfter))
            {
                throw new ArgumentException(
                    "Os snapshots do alvo no DamageReport não correspondem à resolução.");
            }
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
                    $"Os snapshots de {sideName} pertencem ao participante errado.");
            }

            if (!string.Equals(
                    before.CombatantId,
                    after.CombatantId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Os snapshots anterior e posterior de {sideName} " +
                    "precisam representar o mesmo combatente.");
            }
        }

        private static bool HaveEquivalentSnapshot(
            CombatantSnapshot left,
            CombatantSnapshot right)
        {
            return left.Actor == right.Actor &&
                   left.Version == right.Version &&
                   left.MaximumHealth == right.MaximumHealth &&
                   left.CurrentHealth == right.CurrentHealth &&
                   left.Shield == right.Shield &&
                   string.Equals(
                       left.CombatantId,
                       right.CombatantId,
                       StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Relatório final e imutável do Confronto.
    ///
    /// O resultado é derivado dos snapshots posteriores da última Rodada. Assim,
    /// ninguém consegue declarar vitória enquanto os dois combatentes continuam
    /// vivos ou informar um vencedor diferente daquele determinado pela vida.
    /// </summary>
    public sealed class EncounterResult
    {
        public EncounterOutcome Outcome { get; }
        public RoundResolution FinalRound { get; }

        public CombatantSnapshot Player =>
            FinalRound.PlayerAfter;

        public CombatantSnapshot Enemy =>
            FinalRound.EnemyAfter;

        public EncounterResult(
            RoundResolution finalRound)
        {
            FinalRound = finalRound ??
                throw new ArgumentNullException(nameof(finalRound));

            if (!FinalRound.CausedEncounterEnd)
            {
                throw new ArgumentException(
                    "EncounterResult exige que ao menos um combatente esteja derrotado.",
                    nameof(finalRound));
            }

            Outcome = DetermineEncounterOutcome(
                Player,
                Enemy);
        }

        private static EncounterOutcome DetermineEncounterOutcome(
            CombatantSnapshot player,
            CombatantSnapshot enemy)
        {
            if (player.IsDefeated &&
                enemy.IsDefeated)
            {
                return EncounterOutcome.Draw;
            }

            if (enemy.IsDefeated)
            {
                return EncounterOutcome.PlayerVictory;
            }

            if (player.IsDefeated)
            {
                return EncounterOutcome.EnemyVictory;
            }

            throw new InvalidOperationException(
                "Um Confronto não pode terminar enquanto os dois combatentes estão vivos.");
        }
    }

    /*
     * Integrações futuras:
     *
     * 1. EncounterState conservará BoardState, ReactionState, TurnContext,
     *    combatentes, fase e os últimos relatórios usando estes tipos.
     *
     * 2. EncounterEngine será o único responsável por avançar EncounterPhase e
     *    criar RoundResult, RoundResolution e EncounterResult.
     *
     * 3. EncounterController deixará de possuir enums e DTOs aninhados. Ele
     *    observará o motor e converterá relatórios prontos em animações e eventos.
     *
     * 4. AppliedMove e RejectedMove antigos não foram copiados. ActionResult já é
     *    o contrato comum para jogada normal, runa, Player e IA.
     *
     * 5. RoundResult não guarda LastMove: um Turno pode conter várias ações e uma
     *    runa pode alterar diversas casas. ActionResult e BoardChangeSet conservam
     *    essas mudanças sem reduzir a Rodada a uma única coordenada.
     *
     * 6. Nenhum tipo deste arquivo deve depender de MonoBehaviour, GameObject,
     *    Coroutine, Sprite, ScriptableObject ou qualquer outro tipo da Unity.
     */
}