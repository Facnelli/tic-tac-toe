using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Reactions
{
    /// <summary>
    /// Fotografia imutável do placar de reação em uma versão específica do
    /// tabuleiro.
    ///
    /// Este placar não é acumulativo. Sempre que uma ação modificar o tabuleiro,
    /// o SequenceEvaluator deverá recontar todas as sequências de vitória dos
    /// dois participantes e um novo ReactionState deverá ser criado.
    ///
    /// Exemplo:
    /// - o jogador possui uma sequência: 1 x 0;
    /// - uma runa quebra essa sequência: 0 x 0;
    /// - a casa é preenchida novamente: 1 x 0.
    ///
    /// A classe não decide se uma partida terminou. Essa responsabilidade ficará
    /// na ReactionRule, que também conhecerá quem concluiu o turno
    /// e se ainda existem ações capazes de alterar o tabuleiro.
    /// </summary>
    public sealed class ReactionState
    {
        /// <summary>
        /// Quantidade de sequências de vitória do jogador que existem atualmente
        /// no tabuleiro avaliado.
        /// </summary>
        public int PlayerSequenceCount { get; }

        /// <summary>
        /// Quantidade de sequências de vitória do inimigo que existem atualmente
        /// no tabuleiro avaliado.
        /// </summary>
        public int EnemySequenceCount { get; }

        /// <summary>
        /// Versão do BoardState utilizada para produzir esta recontagem.
        ///
        /// Essa informação permitirá rejeitar resultados atrasados de IA,
        /// animações ou efeitos que tenham sido calculados para uma versão antiga
        /// do tabuleiro.
        /// </summary>
        public long BoardVersion { get; }

        /// <summary>
        /// Participante que está à frente no placar atual.
        ///
        /// Retorna ScoreActor.None quando os dois valores estão empatados.
        /// </summary>
        public ScoreActor Leader { get; }

        /// <summary>
        /// Participante que precisa igualar ou superar o placar do líder.
        ///
        /// Retorna ScoreActor.None quando não existe vantagem e, portanto, não há
        /// uma reação pendente.
        /// </summary>
        public ScoreActor PendingResponder { get; }

        /// <summary>
        /// Indica que os dois participantes possuem a mesma quantidade de
        /// sequências, inclusive quando ambos possuem zero.
        /// </summary>
        public bool IsTied => Leader == ScoreActor.None;

        /// <summary>
        /// Indica que existe um participante atrás no placar e com direito ou
        /// dever de reação.
        ///
        /// A existência de uma vantagem não garante que ainda haverá outro
        /// Turno. A futura ReactionRule poderá encerrar imediatamente a partida
        /// quando nenhuma ação estiver disponível.
        /// </summary>
        public bool IsReactionActive => PendingResponder != ScoreActor.None;

        /// <summary>
        /// Diferença positiva entre o líder e o participante que está atrás.
        /// Retorna zero quando há empate.
        /// </summary>
        public int LeaderAdvantage =>
            Math.Abs(PlayerSequenceCount - EnemySequenceCount);

        /// <summary>
        /// Cria uma fotografia a partir da recontagem completa do tabuleiro.
        ///
        /// Nenhum valor anterior é recebido porque sequências quebradas devem
        /// desaparecer do placar e sequências reconstruídas devem voltar a ser
        /// contabilizadas naturalmente pela nova avaliação.
        /// </summary>
        /// <param name="playerSequenceCount">
        /// Total atual de sequências de vitória do jogador.
        /// </param>
        /// <param name="enemySequenceCount">
        /// Total atual de sequências de vitória do inimigo.
        /// </param>
        /// <param name="boardVersion">
        /// Versão do BoardState que foi avaliada.
        /// </param>
        public ReactionState(
            int playerSequenceCount,
            int enemySequenceCount,
            long boardVersion)
        {
            if (playerSequenceCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerSequenceCount),
                    playerSequenceCount,
                    "A quantidade de sequências do jogador não pode ser " +
                    "negativa.");
            }

            if (enemySequenceCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemySequenceCount),
                    enemySequenceCount,
                    "A quantidade de sequências do inimigo não pode ser " +
                    "negativa.");
            }

            if (boardVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boardVersion),
                    boardVersion,
                    "A versão do tabuleiro não pode ser negativa.");
            }

            PlayerSequenceCount = playerSequenceCount;
            EnemySequenceCount = enemySequenceCount;
            BoardVersion = boardVersion;

            Leader = DetermineLeader(
                playerSequenceCount,
                enemySequenceCount);

            PendingResponder = DeterminePendingResponder(Leader);
        }

        /// <summary>
        /// Retorna a contagem do participante solicitado sem obrigar os próximos
        /// serviços a repetirem condicionais Player/Enemy.
        /// </summary>
        public int GetSequenceCount(ScoreActor actor)
        {
            switch (actor)
            {
                case ScoreActor.Player:
                    return PlayerSequenceCount;

                case ScoreActor.Enemy:
                    return EnemySequenceCount;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(actor),
                        actor,
                        "A reação aceita somente Player ou Enemy.");
            }
        }

        private static ScoreActor DetermineLeader(
            int playerSequenceCount,
            int enemySequenceCount)
        {
            if (playerSequenceCount > enemySequenceCount)
            {
                return ScoreActor.Player;
            }

            if (enemySequenceCount > playerSequenceCount)
            {
                return ScoreActor.Enemy;
            }

            return ScoreActor.None;
        }

        private static ScoreActor DeterminePendingResponder(
            ScoreActor leader)
        {
            switch (leader)
            {
                case ScoreActor.Player:
                    return ScoreActor.Enemy;

                case ScoreActor.Enemy:
                    return ScoreActor.Player;

                case ScoreActor.None:
                    return ScoreActor.None;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(leader),
                        leader,
                        "Somente Player, Enemy ou None podem representar a " +
                        "liderança da reação.");
            }
        }
    }
}