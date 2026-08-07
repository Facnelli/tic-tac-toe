using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Combat
{
    /// <summary>
    /// Resolve e aplica, de forma autoritativa, o dano originado por um
    /// ClashReport.
    ///
    /// Esta classe é a ponte entre duas partes:
    ///
    /// 1. ClashReport informa qual lado obteve vantagem e quanto de pontuação
    ///    sobreviveu ao confronto;
    /// 2. CombatantState guarda a vida e o escudo verdadeiros dos participantes;
    /// 3. DamageResolver transforma o saldo em dano, altera somente o alvo correto
    ///    e cria um DamageReport com todos os valores usados;
    /// 4. A apresentação apenas lê o DamageReport e anima o que já aconteceu.
    ///
    /// "Autoritativo" significa que nenhum outro lugar deve repetir esta conta ou
    /// retirar vida por conta própria. Em especial, MatchAnimator, barras de vida
    /// e textos da interface não devem calcular dano.
    ///
    /// O DamageResolver pertence ao domínio puro. Ele não herda MonoBehaviour,
    /// não usa UnityEngine e não deve ser adicionado a um GameObject.
    /// </summary>
    public sealed class DamageResolver
    {
        /// <summary>
        /// Resolve o dano básico, sem aumento, prevenção ou reflexão.
        ///
        /// Este overload (uma segunda forma de chamar o mesmo método) deixa o uso
        /// atual simples. Internamente, ele encaminha a chamada para a versão
        /// completa usando zero em todos os modificadores.
        /// </summary>
        public DamageReport Resolve(
            ClashReport clash,
            CombatantState playerState,
            CombatantState enemyState)
        {
            return Resolve(
                clash,
                playerState,
                enemyState,
                increasedDamage: 0,
                preventedDamage: 0,
                reflectedDamageGenerated: 0);
        }

        /// <summary>
        /// Resolve o dano com modificadores já totalizados, aplica a mudança ao
        /// alvo e devolve um relatório imutável da transição.
        ///
        /// Nesta etapa da arquitetura, os modificadores são totais inteiros. O
        /// resolvedor ainda não conhece runas concretas. Futuramente, um pipeline
        /// de DamageContribution poderá calcular esses totais e chamar este método
        /// sem mudar a regra central de vida e escudo.
        /// </summary>
        /// <param name="clash">
        /// Relatório do confronto que originou o possível dano.
        /// </param>
        /// <param name="playerState">
        /// Estado autoritativo do combatente que ocupa o lado Player.
        /// </param>
        /// <param name="enemyState">
        /// Estado autoritativo do combatente que ocupa o lado Enemy.
        /// </param>
        /// <param name="increasedDamage">
        /// Quantidade total acrescentada ao dano bruto antes da prevenção e do
        /// escudo. Exemplo: vulnerabilidade acrescenta 3 pontos.
        /// </param>
        /// <param name="preventedDamage">
        /// Quantidade solicitada de prevenção antes do escudo. Se a prevenção for
        /// maior que o dano disponível, somente a parte necessária será registrada
        /// como prevenção efetiva; o dano nunca fica negativo.
        /// </param>
        /// <param name="reflectedDamageGenerated">
        /// Quantidade de dano refletido gerada por esta resolução. Nesta fase esse
        /// valor é apenas registrado: ele não altera o atacante silenciosamente.
        /// Uma reflexão futura deverá produzir sua própria resolução e relatório.
        /// </param>
        public DamageReport Resolve(
            ClashReport clash,
            CombatantState playerState,
            CombatantState enemyState,
            int increasedDamage,
            int preventedDamage,
            int reflectedDamageGenerated)
        {
            ValidateInputs(
                clash,
                playerState,
                enemyState,
                increasedDamage,
                preventedDamage,
                reflectedDamageGenerated);

            /*
             * Um empate é um resultado real do pipeline, não um erro e nem uma
             * ausência de resultado. Por isso devolvemos um DamageReport explícito
             * de dano zero, em vez de retornar null.
             */
            if (clash.IsTie)
            {
                ValidateTieModifiers(
                    increasedDamage,
                    preventedDamage,
                    reflectedDamageGenerated);

                return DamageReport.CreateNoDamage(clash);
            }

            ScoreActor sourceActor = clash.AdvantageActor;
            ScoreActor targetActor = clash.DisadvantageActor;

            /*
             * O ClashReport escolhe o alvo, não o chamador. Isso impede um erro
             * grave, como o jogador obter vantagem e o dano ser aplicado ao
             * próprio jogador por causa de uma referência passada na ordem errada.
             */
            CombatantState targetState = SelectTargetState(
                targetActor,
                playerState,
                enemyState);

            if (targetState.IsDefeated)
            {
                throw new InvalidOperationException(
                    "Não é possível resolver dano comum contra um combatente que " +
                    "já está derrotado. O EncounterController deve encerrar o " +
                    "encontro antes de iniciar outra rodada.");
            }

            int rawDamage = clash.UnopposedScore;

            /*
             * A soma é feita primeiro em long. Um int comporta até cerca de 2,1
             * bilhões; somar dois int diretamente poderia causar overflow e
             * transformar um dano muito grande em um número negativo.
             */
            long damageBeforePrevention =
                (long)rawDamage + increasedDamage;

            if (damageBeforePrevention > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "O dano bruto somado aos aumentos ultrapassou o maior valor " +
                    "suportado por int. Revise os limites dos modificadores de dano.");
            }

            int damageBeforePreventionAsInt =
                (int)damageBeforePrevention;

            /*
             * Prevenção efetiva nunca pode superar o dano que existe. Se um alvo
             * possui 20 de defesa contra um golpe de 8, ele previne 8 e o dano
             * termina em zero; não existe dano -12 nem "cura acidental".
             */
            int effectivePreventedDamage = Math.Min(
                preventedDamage,
                damageBeforePreventionAsInt);

            int damageAfterModifiers =
                damageBeforePreventionAsInt - effectivePreventedDamage;

            /*
             * Regra atual do escudo: uma unidade de Shield absorve uma unidade de
             * dano e é consumida. Os dois valores ficam separados no relatório
             * porque uma futura runa poderá alterar essa eficiência.
             */
            int damageAbsorbedByShield = Math.Min(
                damageAfterModifiers,
                targetState.Shield);

            int shieldConsumed = damageAbsorbedByShield;

            int requestedHealthDamage =
                damageAfterModifiers - damageAbsorbedByShield;

            /*
             * Dano solicitado e dano aplicado são números diferentes quando há
             * overkill. Um alvo com 3 de vida atingido por 8 recebe efetivamente
             * 3, enquanto os outros 5 são registrados como overkill.
             */
            int appliedHealthDamage = Math.Min(
                requestedHealthDamage,
                targetState.CurrentHealth);

            int overkill =
                requestedHealthDamage - appliedHealthDamage;

            CombatantSnapshot targetBefore =
                CombatantSnapshot.Capture(targetState);

            bool willChangeTargetState =
                shieldConsumed > 0 || appliedHealthDamage > 0;

            /*
             * CombatantState protege sua versão, mas fazemos esta verificação
             * antes de alterar vida ou escudo. Assim, se o limite extremamente
             * raro for alcançado, a operação falha sem deixar o estado parcialmente
             * modificado.
             */
            if (willChangeTargetState &&
                targetState.Version == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "A versão do CombatantState atingiu o limite suportado e não " +
                    "pode registrar uma nova alteração de dano.");
            }

            /*
             * Aplicamos o dano solicitado à vida, e não apenas a parte que cabia
             * nela. CombatantState limita a vida em zero; DamageReport conserva a
             * diferença como overkill. Toda a alteração ocorre em uma única chamada
             * e, portanto, a versão avança no máximo uma vez.
             */
            targetState.ApplyResolvedDamage(
                shieldConsumed,
                requestedHealthDamage);

            CombatantSnapshot targetAfter =
                CombatantSnapshot.Capture(targetState);

            return new DamageReport(
                clash,
                sourceActor,
                targetActor,
                targetBefore,
                targetAfter,
                rawDamage,
                increasedDamage,
                effectivePreventedDamage,
                damageAfterModifiers,
                damageAbsorbedByShield,
                shieldConsumed,
                requestedHealthDamage,
                appliedHealthDamage,
                overkill,
                reflectedDamageGenerated);
        }

        private static void ValidateInputs(
            ClashReport clash,
            CombatantState playerState,
            CombatantState enemyState,
            int increasedDamage,
            int preventedDamage,
            int reflectedDamageGenerated)
        {
            if (clash == null)
            {
                throw new ArgumentNullException(nameof(clash));
            }

            if (playerState == null)
            {
                throw new ArgumentNullException(nameof(playerState));
            }

            if (enemyState == null)
            {
                throw new ArgumentNullException(nameof(enemyState));
            }

            ValidateStateActor(
                playerState,
                ScoreActor.Player,
                nameof(playerState));

            ValidateStateActor(
                enemyState,
                ScoreActor.Enemy,
                nameof(enemyState));

            if (string.Equals(
                    playerState.CombatantId,
                    enemyState.CombatantId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Player e Enemy não podem usar o mesmo CombatantId. Cada " +
                    "participante precisa possuir uma identidade própria.");
            }

            ValidateNonNegative(
                increasedDamage,
                nameof(increasedDamage),
                "O aumento de dano não pode ser negativo.");

            ValidateNonNegative(
                preventedDamage,
                nameof(preventedDamage),
                "A prevenção de dano não pode ser negativa.");

            ValidateNonNegative(
                reflectedDamageGenerated,
                nameof(reflectedDamageGenerated),
                "O dano refletido gerado não pode ser negativo.");
        }

        private static void ValidateStateActor(
            CombatantState state,
            ScoreActor expectedActor,
            string parameterName)
        {
            if (state.Actor != expectedActor)
            {
                throw new ArgumentException(
                    "O estado informado em " + parameterName +
                    " pertence a " + state.Actor +
                    ", mas deveria pertencer a " + expectedActor + ".",
                    parameterName);
            }
        }

        private static void ValidateTieModifiers(
            int increasedDamage,
            int preventedDamage,
            int reflectedDamageGenerated)
        {
            if (increasedDamage != 0 ||
                preventedDamage != 0 ||
                reflectedDamageGenerated != 0)
            {
                throw new ArgumentException(
                    "Um confronto empatado não possui dano bruto para receber " +
                    "aumento, prevenção ou reflexão. Danos independentes do " +
                    "confronto deverão usar uma futura origem de dano própria.");
            }
        }

        private static CombatantState SelectTargetState(
            ScoreActor targetActor,
            CombatantState playerState,
            CombatantState enemyState)
        {
            switch (targetActor)
            {
                case ScoreActor.Player:
                    return playerState;

                case ScoreActor.Enemy:
                    return enemyState;

                default:
                    /*
                     * Um ClashReport válido e não empatado nunca deve chegar aqui.
                     * Ainda assim, a proteção torna o erro claro caso o enum ou as
                     * regras de confronto sejam ampliados no futuro.
                     */
                    throw new InvalidOperationException(
                        "O ClashReport não possui um alvo de dano válido.");
            }
        }

        private static void ValidateNonNegative(
            int value,
            string parameterName,
            string message)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    message);
            }
        }

        /*
         * ADIÇÕES FUTURAS POSSÍVEIS
         *
         * 1. DamageContribution e DamageStep poderão substituir os três totais
         *    recebidos por este resolvedor. Cada runa ou efeito informará origem,
         *    prioridade, duração, operação e alvo, permitindo que a animação mostre
         *    o cálculo passo a passo sem conhecer a regra.
         *
         * 2. DerivedStats poderá reunir defesa plana, redução percentual,
         *    vulnerabilidade, imunidades e perfuração. O DamageResolver deverá
         *    receber uma fotografia dessas estatísticas, nunca consultar
         *    GameObjects, ScriptableObjects ou componentes visuais diretamente.
         *
         * 3. DamageAbsorbedByShield e ShieldConsumed hoje usam a proporção 1 para
         *    1. Uma política de escudo poderá permitir eficiência diferente,
         *    escudo que não é consumido ou ataques que ignoram parte da proteção.
         *
         * 4. ReflectedDamageGenerated é somente registrado. A reflexão deverá
         *    originar uma segunda resolução contra o atacante, ligada à primeira
         *    por um identificador. Isso evita alteração escondida de dois estados
         *    e permite representar corretamente uma possível derrota simultânea.
         *
         * 5. Dano passivo no começo do turno, dano direto de runa e dano de
         *    ambiente não possuem ClashReport. Um futuro DamageOrigin poderá
         *    representar essas causas sem fabricar um confronto falso.
         *
         * 6. Tipos de dano, como comum, verdadeiro ou perfurante, poderão definir
         *    quais etapas são usadas. O tipo deverá ser um dado explícito para que
         *    testes, replays e interface saibam por que uma proteção foi ignorada.
         *
         * 7. O DamageResolver não concede pontos de progressão. Depois que todos
         *    os danos e reações terminarem, o EncounterController deverá verificar
         *    a derrota do inimigo ou boss e somente então conceder a recompensa.
         *
         * 8. Se um efeito atingir vários alvos, cada CombatantState deverá gerar
         *    seu próprio DamageReport. Um relatório composto poderá reuni-los sem
         *    perder o antes e depois de cada participante.
         */
    }
}