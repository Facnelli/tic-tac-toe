using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Combat
{
    /// <summary>
    /// Resultado possível da comparação entre as pontuações dos dois participantes.
    ///
    /// É importante não confundir este resultado com o vencedor da rodada ou do
    /// encontro. Um participante pode ter formado a sequência vencedora da rodada
    /// e, ainda assim, terminar o confronto de pontuações em empate ou desvantagem
    /// por causa de modificadores aplicados ao placar.
    /// </summary>
    public enum ClashOutcome
    {
        /// <summary>
        /// As duas pontuações foram completamente anuladas uma pela outra.
        /// </summary>
        Tie,

        /// <summary>
        /// Restou pontuação do jogador depois da anulação do valor em comum.
        /// </summary>
        PlayerAdvantage,

        /// <summary>
        /// Restou pontuação do inimigo depois da anulação do valor em comum.
        /// </summary>
        EnemyAdvantage
    }

    /// <summary>
    /// Relatório imutável de um confronto entre a pontuação do jogador e a do
    /// inimigo.
    ///
    /// O confronto básico funciona como dois valores que se anulam:
    ///
    /// - jogador com 12 e inimigo com 8 anulam 8 pontos de cada lado;
    /// - o jogador termina com 4 pontos sem oposição;
    /// - esses 4 pontos ainda não são dano aplicado à vida.
    ///
    /// A distinção entre pontuação restante e dano é proposital. O futuro
    /// DamageResolver poderá transformar o saldo em dano considerando escudos,
    /// defesa, vulnerabilidade, reflexão e outras regras sem modificar este
    /// relatório nem recalcular o confronto.
    ///
    /// Esta classe é um registro passivo: ela não calcula pontuação, não escolhe
    /// sequências, não altera vida e não inicia animações. O futuro ClashResolver
    /// fará a regra do confronto e criará uma instância já resolvida deste tipo.
    /// </summary>
    public sealed class ClashReport
    {
        /// <summary>
        /// Resultado completo produzido pelo ScorePipeline para o jogador.
        ///
        /// Guardamos o ScorePipelineResult inteiro, e não apenas seu número final,
        /// para que a apresentação possa acessar as sequências e todos os passos
        /// que realmente originaram a pontuação mostrada.
        /// </summary>
        public ScorePipelineResult PlayerScoreResult { get; }

        /// <summary>
        /// Resultado completo produzido pelo ScorePipeline para o inimigo.
        /// </summary>
        public ScorePipelineResult EnemyScoreResult { get; }

        /// <summary>
        /// Atalho para o detalhamento matemático da pontuação do jogador.
        /// </summary>
        public ScoreBreakdown PlayerBreakdown =>
            PlayerScoreResult.Breakdown;

        /// <summary>
        /// Atalho para o detalhamento matemático da pontuação do inimigo.
        /// </summary>
        public ScoreBreakdown EnemyBreakdown =>
            EnemyScoreResult.Breakdown;

        /// <summary>
        /// Versão do BoardState à qual as duas pontuações pertencem.
        ///
        /// Se os resultados tiverem versões diferentes, o relatório nem poderá
        /// ser criado. Comparar placares de dois momentos distintos do tabuleiro
        /// produziria um confronto que nunca existiu de verdade.
        /// </summary>
        public long BoardVersion { get; }

        /// <summary>
        /// Pontuação final do jogador antes do confronto.
        /// </summary>
        public int PlayerScore => PlayerBreakdown.FinalScore;

        /// <summary>
        /// Pontuação final do inimigo antes do confronto.
        /// </summary>
        public int EnemyScore => EnemyBreakdown.FinalScore;

        /// <summary>
        /// Quantidade retirada de CADA participante durante a anulação.
        ///
        /// Por exemplo, no confronto 12 contra 8, esta propriedade vale 8. Isso
        /// significa que 8 foram retirados do jogador e outros 8 do inimigo.
        /// A animação poderá usar este valor para reduzir visualmente os dois
        /// números ao mesmo tempo, sem refazer a fórmula.
        /// </summary>
        public int CancelledScorePerSide { get; }

        /// <summary>
        /// Soma de tudo que foi anulado nos dois lados.
        ///
        /// Usamos long porque o dobro de int.MaxValue não cabe em um int. Essa
        /// situação é rara, mas pode ocorrer futuramente com combinações extremas
        /// de multiplicadores e não deve causar estouro numérico no relatório.
        /// </summary>
        public long TotalCancelledScore =>
            (long)CancelledScorePerSide * 2L;

        /// <summary>
        /// Pontuação do jogador que sobrou depois do confronto.
        /// </summary>
        public int PlayerRemainingScore { get; }

        /// <summary>
        /// Pontuação do inimigo que sobrou depois do confronto.
        /// </summary>
        public int EnemyRemainingScore { get; }

        /// <summary>
        /// Indica qual foi o resultado da comparação.
        /// </summary>
        public ClashOutcome Outcome { get; }

        /// <summary>
        /// Verdadeiro quando algum participante terminou com vantagem.
        /// </summary>
        public bool HasAdvantage => Outcome != ClashOutcome.Tie;

        /// <summary>
        /// Verdadeiro quando nenhuma pontuação restou após o confronto.
        /// </summary>
        public bool IsTie => Outcome == ClashOutcome.Tie;

        /// <summary>
        /// Participante que terminou com pontuação sem oposição.
        ///
        /// Em um empate, retorna ScoreActor.None porque não existe vencedor do
        /// confronto. Environment nunca pode ser vencedor de um ClashReport.
        /// </summary>
        public ScoreActor AdvantageActor
        {
            get
            {
                switch (Outcome)
                {
                    case ClashOutcome.PlayerAdvantage:
                        return ScoreActor.Player;

                    case ClashOutcome.EnemyAdvantage:
                        return ScoreActor.Enemy;

                    case ClashOutcome.Tie:
                        return ScoreActor.None;

                    default:
                        throw new InvalidOperationException(
                            "O ClashReport contém um resultado desconhecido.");
                }
            }
        }

        /// <summary>
        /// Participante que ficou em desvantagem no confronto.
        ///
        /// Em um empate, retorna ScoreActor.None.
        /// </summary>
        public ScoreActor DisadvantageActor
        {
            get
            {
                switch (Outcome)
                {
                    case ClashOutcome.PlayerAdvantage:
                        return ScoreActor.Enemy;

                    case ClashOutcome.EnemyAdvantage:
                        return ScoreActor.Player;

                    case ClashOutcome.Tie:
                        return ScoreActor.None;

                    default:
                        throw new InvalidOperationException(
                            "O ClashReport contém um resultado desconhecido.");
                }
            }
        }

        /// <summary>
        /// Quantidade que sobrou do lado vencedor do confronto.
        ///
        /// Este valor será uma entrada natural para o futuro DamageResolver, mas
        /// ainda não deve ser subtraído diretamente da vida. Em um empate vale 0.
        /// </summary>
        public int UnopposedScore
        {
            get
            {
                switch (Outcome)
                {
                    case ClashOutcome.PlayerAdvantage:
                        return PlayerRemainingScore;

                    case ClashOutcome.EnemyAdvantage:
                        return EnemyRemainingScore;

                    case ClashOutcome.Tie:
                        return 0;

                    default:
                        throw new InvalidOperationException(
                            "O ClashReport contém um resultado desconhecido.");
                }
            }
        }

        /// <summary>
        /// Informa se pelo menos uma pontuação original ultrapassou int.MaxValue e
        /// precisou ser limitada pelo ScoreBreakdown.
        ///
        /// O confronto continua válido, mas esta informação permite registrar um
        /// aviso de balanceamento ou depuração sem procurar nos dois relatórios.
        /// </summary>
        public bool HasClampedScore =>
            PlayerBreakdown.WasClampedToIntegerRange ||
            EnemyBreakdown.WasClampedToIntegerRange;

        /// <summary>
        /// Cria um relatório a partir dos valores já resolvidos pelo ClashResolver.
        ///
        /// O construtor é internal para que qualquer código do jogo consiga ler o
        /// relatório, mas apenas o domínio desta assembly consiga criá-lo. Assim,
        /// componentes visuais não podem inventar um resultado diferente do
        /// resultado autoritativo.
        /// </summary>
        internal ClashReport(
            ScorePipelineResult playerScoreResult,
            ScorePipelineResult enemyScoreResult,
            int cancelledScorePerSide,
            int playerRemainingScore,
            int enemyRemainingScore,
            ClashOutcome outcome)
        {
            PlayerScoreResult = playerScoreResult ??
                throw new ArgumentNullException(nameof(playerScoreResult));

            EnemyScoreResult = enemyScoreResult ??
                throw new ArgumentNullException(nameof(enemyScoreResult));

            ValidateScoreResultOwners(
                PlayerScoreResult,
                EnemyScoreResult);

            ValidateSameBoardVersion(
                PlayerScoreResult,
                EnemyScoreResult);

            ValidateDifferentMarks(
                PlayerScoreResult,
                EnemyScoreResult);

            ValidateDefinedOutcome(outcome);

            ValidateResolvedValues(
                PlayerScoreResult.Breakdown.FinalScore,
                EnemyScoreResult.Breakdown.FinalScore,
                cancelledScorePerSide,
                playerRemainingScore,
                enemyRemainingScore,
                outcome);

            BoardVersion = PlayerScoreResult.BoardVersion;
            CancelledScorePerSide = cancelledScorePerSide;
            PlayerRemainingScore = playerRemainingScore;
            EnemyRemainingScore = enemyRemainingScore;
            Outcome = outcome;
        }

        private static void ValidateScoreResultOwners(
            ScorePipelineResult playerScoreResult,
            ScorePipelineResult enemyScoreResult)
        {
            if (playerScoreResult.Participant != ScoreActor.Player ||
                playerScoreResult.Breakdown.Participant != ScoreActor.Player)
            {
                throw new ArgumentException(
                    "O primeiro resultado do confronto deve pertencer ao jogador.",
                    nameof(playerScoreResult));
            }

            if (enemyScoreResult.Participant != ScoreActor.Enemy ||
                enemyScoreResult.Breakdown.Participant != ScoreActor.Enemy)
            {
                throw new ArgumentException(
                    "O segundo resultado do confronto deve pertencer ao inimigo.",
                    nameof(enemyScoreResult));
            }
        }

        private static void ValidateSameBoardVersion(
            ScorePipelineResult playerScoreResult,
            ScorePipelineResult enemyScoreResult)
        {
            if (playerScoreResult.BoardVersion != enemyScoreResult.BoardVersion)
            {
                throw new ArgumentException(
                    "Os dois placares precisam pertencer à mesma versão do " +
                    "BoardState. Resolva novamente ambos usando o mesmo estado.");
            }
        }

        private static void ValidateDifferentMarks(
            ScorePipelineResult playerScoreResult,
            ScorePipelineResult enemyScoreResult)
        {
            /*
             * O ScorePipeline já garante individualmente que cada marca é X ou O.
             * Aqui precisamos apenas impedir que os dois lados representem a mesma
             * marca, o que faria as duas pontuações descreverem o mesmo participante
             * lógico do tabuleiro.
             */
            if (playerScoreResult.Mark == enemyScoreResult.Mark)
            {
                throw new ArgumentException(
                    "Jogador e inimigo precisam usar marcas diferentes no confronto.");
            }
        }

        private static void ValidateDefinedOutcome(ClashOutcome outcome)
        {
            if (!Enum.IsDefined(typeof(ClashOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome,
                    "O resultado de confronto informado não existe.");
            }
        }

        private static void ValidateResolvedValues(
            int playerScore,
            int enemyScore,
            int cancelledScorePerSide,
            int playerRemainingScore,
            int enemyRemainingScore,
            ClashOutcome outcome)
        {
            if (cancelledScorePerSide < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cancelledScorePerSide),
                    cancelledScorePerSide,
                    "A pontuação anulada não pode ser negativa.");
            }

            if (playerRemainingScore < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerRemainingScore),
                    playerRemainingScore,
                    "A pontuação restante do jogador não pode ser negativa.");
            }

            if (enemyRemainingScore < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyRemainingScore),
                    enemyRemainingScore,
                    "A pontuação restante do inimigo não pode ser negativa.");
            }

            /*
             * Estas igualdades são invariantes: condições que precisam ser sempre
             * verdadeiras para o objeto representar um confronto coerente.
             *
             * Validá-las aqui funciona como uma última barreira contra erros no
             * futuro ClashResolver. Se alguém modificar a fórmula e esquecer de
             * atualizar um dos saldos, o erro aparecerá imediatamente na origem.
             */
            if (playerScore - cancelledScorePerSide != playerRemainingScore)
            {
                throw new ArgumentException(
                    "A pontuação restante do jogador não corresponde ao placar " +
                    "original menos a pontuação anulada.",
                    nameof(playerRemainingScore));
            }

            if (enemyScore - cancelledScorePerSide != enemyRemainingScore)
            {
                throw new ArgumentException(
                    "A pontuação restante do inimigo não corresponde ao placar " +
                    "original menos a pontuação anulada.",
                    nameof(enemyRemainingScore));
            }

            /*
             * Na regra básica, a anulação precisa consumir por completo pelo menos
             * um lado. Portanto, os dois participantes nunca podem terminar com
             * saldo positivo ao mesmo tempo.
             */
            if (playerRemainingScore > 0 && enemyRemainingScore > 0)
            {
                throw new ArgumentException(
                    "Um confronto resolvido não pode deixar saldo positivo nos " +
                    "dois lados ao mesmo tempo.");
            }

            bool isTie =
                playerRemainingScore == 0 &&
                enemyRemainingScore == 0;

            bool hasPlayerAdvantage =
                playerRemainingScore > 0 &&
                enemyRemainingScore == 0;

            bool hasEnemyAdvantage =
                enemyRemainingScore > 0 &&
                playerRemainingScore == 0;

            if ((outcome == ClashOutcome.Tie && !isTie) ||
                (outcome == ClashOutcome.PlayerAdvantage &&
                 !hasPlayerAdvantage) ||
                (outcome == ClashOutcome.EnemyAdvantage &&
                 !hasEnemyAdvantage))
            {
                throw new ArgumentException(
                    "O resultado declarado não corresponde às pontuações " +
                    "restantes do confronto.",
                    nameof(outcome));
            }
        }

        /*
         * Adições futuras:
         *
         * 1. Poderá ser incluído um identificador determinístico de rodada ou de
         *    confronto. Ele ajudará saves, logs e replays a relacionar este
         *    relatório aos futuros DamageReport e eventos visuais.
         *
         * 2. Se surgirem pontos que não podem ser anulados, como uma forma de
         *    "pontuação perfurante", o relatório poderá separar o saldo em canais.
         *    Essa regra deverá nascer no ClashResolver; a apresentação continuará
         *    apenas lendo os valores finais registrados aqui.
         *
         * 3. Efeitos de defesa, escudo, vulnerabilidade, reflexão, dano passivo e
         *    dano mínimo não pertencem ao ClashReport. Eles poderão ser descritos
         *    por DamageContribution e DamageReport no pipeline de dano.
         *
         * 4. Um modo futuro com três ou mais participantes exigirá substituir as
         *    propriedades fixas Player/Enemy por uma coleção identificada por um
         *    ActorId. Para o confronto atual de dois lados, propriedades explícitas
         *    são mais fáceis de compreender, validar e animar.
         *
         * 5. A interface poderá animar PlayerBreakdown.Steps e
         *    EnemyBreakdown.Steps, depois reduzir ambos os números usando
         *    CancelledScorePerSide e, por fim, encaminhar UnopposedScore ao visual
         *    de dano. Nenhuma dessas etapas deverá recalcular regras.
         */
    }
}