using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Combat
{
    /// <summary>
    /// Resolve o confronto entre a pontuação final do jogador e a pontuação final
    /// do inimigo.
    ///
    /// A regra básica do confronto é uma anulação de valores iguais nos dois lados:
    ///
    /// - jogador 12 contra inimigo 8: anulam-se 8 pontos de cada lado;
    /// - restam 4 pontos do jogador e 0 do inimigo;
    /// - jogador 7 contra inimigo 7: os dois chegam a 0 e o confronto empata.
    ///
    /// Esta classe é um serviço de domínio puro. Isso significa que ela não é um
    /// MonoBehaviour, não deve ser adicionada a um GameObject e não conhece textos,
    /// partículas, animações ou barras de vida. Sua única responsabilidade é aplicar
    /// a regra matemática e devolver um ClashReport autoritativo.
    ///
    /// Separar o cálculo da apresentação é importante porque a partida precisa ter
    /// exatamente o mesmo resultado mesmo quando uma animação for pulada, acelerada
    /// ou estiver desativada.
    /// </summary>
    public sealed class ClashResolver
    {
        /// <summary>
        /// Compara os dois resultados produzidos pelo ScorePipeline e cria o
        /// relatório completo do confronto.
        ///
        /// Recebemos ScorePipelineResult, e não apenas dois números, para preservar
        /// a origem de cada placar. O ClashReport poderá, assim, levar para a futura
        /// apresentação as sequências e todos os passos que formaram os valores.
        /// </summary>
        /// <param name="playerScoreResult">
        /// Resultado de pontuação que pertence ao jogador.
        /// </param>
        /// <param name="enemyScoreResult">
        /// Resultado de pontuação que pertence ao inimigo.
        /// </param>
        /// <returns>
        /// Um relatório imutável contendo a anulação, os saldos e o lado que ficou
        /// com pontuação sem oposição.
        /// </returns>
        public ClashReport Resolve(
            ScorePipelineResult playerScoreResult,
            ScorePipelineResult enemyScoreResult)
        {
            /*
             * Verificamos null aqui para produzir um erro claro na entrada pública
             * do serviço. Sem essa proteção, o acesso a Breakdown abaixo causaria
             * NullReferenceException, que informa apenas que "algo era null", sem
             * indicar qual argumento foi configurado incorretamente.
             */
            if (playerScoreResult == null)
            {
                throw new ArgumentNullException(nameof(playerScoreResult));
            }

            if (enemyScoreResult == null)
            {
                throw new ArgumentNullException(nameof(enemyScoreResult));
            }

            int playerScore = playerScoreResult.Breakdown.FinalScore;
            int enemyScore = enemyScoreResult.Breakdown.FinalScore;

            /*
             * O menor placar é a quantidade máxima que os dois lados conseguem
             * anular igualmente. Usar essa única referência evita manter dois
             * cálculos parecidos que poderiam divergir no futuro.
             *
             * Exemplos:
             * - min(12, 8) = 8;
             * - min(7, 7) = 7;
             * - min(0, 5) = 0.
             */
            int cancelledScorePerSide = Math.Min(
                playerScore,
                enemyScore);

            int playerRemainingScore =
                playerScore - cancelledScorePerSide;

            int enemyRemainingScore =
                enemyScore - cancelledScorePerSide;

            /*
             * Depois de subtrair o mesmo valor dos dois lados, no máximo um saldo
             * pode ser positivo. Esse fato torna o resultado inequívoco:
             *
             * - saldo do jogador > 0: PlayerAdvantage;
             * - saldo do inimigo > 0: EnemyAdvantage;
             * - ambos em 0: Tie.
             *
             * Não usamos IsWinner do ScorePipeline aqui. "Vencer a rodada" e
             * "obter vantagem no confronto de pontos" são fatos diferentes: uma
             * runa ou regra poderá fazer o vencedor da rodada terminar com placar
             * menor que o oponente.
             */
            ClashOutcome outcome = DetermineOutcome(
                playerRemainingScore,
                enemyRemainingScore);

            /*
             * ClashReport repete as validações estruturais mais importantes, como
             * dono do placar, marcas diferentes e versão igual do tabuleiro. Essa
             * segunda barreira é intencional: o relatório é a fronteira confiável
             * que será entregue à animação e ao futuro DamageResolver.
             */
            return new ClashReport(
                playerScoreResult,
                enemyScoreResult,
                cancelledScorePerSide,
                playerRemainingScore,
                enemyRemainingScore,
                outcome);
        }

        /// <summary>
        /// Traduz os saldos matemáticos para um resultado de domínio.
        ///
        /// O método é separado para que Resolve permaneça legível como uma sequência
        /// de passos: ler placares, anular, calcular saldos, decidir o resultado e
        /// criar o relatório.
        /// </summary>
        private static ClashOutcome DetermineOutcome(
            int playerRemainingScore,
            int enemyRemainingScore)
        {
            if (playerRemainingScore > 0)
            {
                return ClashOutcome.PlayerAdvantage;
            }

            if (enemyRemainingScore > 0)
            {
                return ClashOutcome.EnemyAdvantage;
            }

            return ClashOutcome.Tie;
        }

        /*
         * ADIÇÕES FUTURAS POSSÍVEIS
         *
         * 1. Aqui poderá ser introduzido um ClashRequest caso a resolução passe a
         *    precisar de contexto adicional, como identificador da rodada ou regras
         *    específicas do encontro. Evite adicionar muitos parâmetros soltos ao
         *    método Resolve.
         *
         * 2. Efeitos que alteram pontuação devem, de preferência, continuar entrando
         *    antes do confronto como ScoreContribution. Assim o ScoreBreakdown
         *    explica a origem da alteração e o ClashResolver permanece simples.
         *
         * 3. Se futuramente existir uma regra que atua DURANTE o confronto, como
         *    "preservar os primeiros 3 pontos que seriam anulados", ela poderá ser
         *    representada por etapas próprias de confronto em um relatório ampliado.
         *    Não coloque essa regra na animação.
         *
         * 4. O próximo DamageResolver deverá consumir UnopposedScore, mas defesa,
         *    escudo, vulnerabilidade, reflexão e alteração de vida não pertencem a
         *    esta classe. Pontuação restante é a entrada do dano, não o dano final.
         *
         * 5. Quando o motor determinístico de efeitos existir, gatilhos como
         *    BeforeClash e AfterClash poderão envolver esta resolução. A ordem dos
         *    efeitos deverá ser registrada fora da apresentação para permitir
         *    replays e testes reproduzíveis.
         */
    }
}