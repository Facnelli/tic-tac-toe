using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Combat
{
    /// <summary>
    /// Fotografia imutável dos recursos de um combatente em um instante.
    ///
    /// Uma fotografia, também chamada de "snapshot", é uma cópia somente para
    /// leitura. Ela permite que um relatório conserve como a vida e o escudo
    /// estavam antes e depois do dano, mesmo que o CombatantState continue sendo
    /// alterado nas rodadas seguintes.
    ///
    /// Esta classe pertence ao domínio puro: não usa MonoBehaviour, UnityEngine,
    /// GameObject ou qualquer elemento visual.
    /// </summary>
    public sealed class CombatantSnapshot
    {
        /// <summary>
        /// Identificador estável da instância de combatente fotografada.
        /// </summary>
        public string CombatantId { get; }

        /// <summary>
        /// Lado lógico ocupado pelo combatente: Player ou Enemy.
        /// </summary>
        public ScoreActor Actor { get; }

        /// <summary>
        /// Vida máxima existente no momento da fotografia.
        /// </summary>
        public int MaximumHealth { get; }

        /// <summary>
        /// Vida atual existente no momento da fotografia.
        /// </summary>
        public int CurrentHealth { get; }

        /// <summary>
        /// Escudo disponível no momento da fotografia.
        /// </summary>
        public int Shield { get; }

        /// <summary>
        /// Versão do CombatantState no momento da fotografia.
        ///
        /// A versão ajuda a detectar se um relatório tentou relacionar estados
        /// que não são consecutivos.
        /// </summary>
        public long Version { get; }

        /// <summary>
        /// Verdadeiro quando ainda existe pelo menos um ponto de vida.
        /// </summary>
        public bool IsAlive => CurrentHealth > 0;

        /// <summary>
        /// Verdadeiro quando a vida chegou a zero.
        /// </summary>
        public bool IsDefeated => CurrentHealth == 0;

        /// <summary>
        /// Quantidade de vida que falta para alcançar MaximumHealth.
        /// </summary>
        public int MissingHealth => MaximumHealth - CurrentHealth;

        /// <summary>
        /// Proporção de vida entre zero e um.
        ///
        /// A interface poderá converter este decimal para float apenas quando
        /// precisar desenhar a barra de vida.
        /// </summary>
        public decimal HealthRatio =>
            (decimal)CurrentHealth / MaximumHealth;

        private CombatantSnapshot(
            string combatantId,
            ScoreActor actor,
            int maximumHealth,
            int currentHealth,
            int shield,
            long version)
        {
            ValidateValues(
                combatantId,
                actor,
                maximumHealth,
                currentHealth,
                shield,
                version);

            CombatantId = combatantId;
            Actor = actor;
            MaximumHealth = maximumHealth;
            CurrentHealth = currentHealth;
            Shield = shield;
            Version = version;
        }

        /// <summary>
        /// Captura os valores atuais de um CombatantState.
        ///
        /// Depois da captura, alterar o estado original não modifica esta
        /// fotografia. Essa independência é essencial para replays e animações.
        /// </summary>
        public static CombatantSnapshot Capture(CombatantState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new CombatantSnapshot(
                state.CombatantId,
                state.Actor,
                state.MaximumHealth,
                state.CurrentHealth,
                state.Shield,
                state.Version);
        }

        private static void ValidateValues(
            string combatantId,
            ScoreActor actor,
            int maximumHealth,
            int currentHealth,
            int shield,
            long version)
        {
            if (string.IsNullOrWhiteSpace(combatantId))
            {
                throw new ArgumentException(
                    "Um CombatantSnapshot precisa possuir um CombatantId.",
                    nameof(combatantId));
            }

            if (actor != ScoreActor.Player &&
                actor != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actor),
                    actor,
                    "A fotografia deve pertencer a Player ou Enemy.");
            }

            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHealth),
                    maximumHealth,
                    "A vida máxima da fotografia deve ser maior que zero.");
            }

            if (currentHealth < 0 || currentHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentHealth),
                    currentHealth,
                    "A vida atual deve estar entre zero e a vida máxima.");
            }

            if (shield < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shield),
                    shield,
                    "O escudo da fotografia não pode ser negativo.");
            }

            if (version < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(version),
                    version,
                    "A versão da fotografia não pode ser negativa.");
            }
        }
    }

    /// <summary>
    /// Relatório imutável de uma resolução de dano.
    ///
    /// O DamageReport não decide a fórmula e não modifica CombatantState. O
    /// futuro DamageResolver executará essas tarefas e entregará ao relatório os
    /// valores já resolvidos. Esta classe apenas valida e conserva o fato que
    /// aconteceu.
    ///
    /// Exemplo simplificado:
    ///
    /// - 12 pontos sobreviveram ao confronto;
    /// - uma defesa impediu 2, deixando 10;
    /// - o escudo absorveu 4 e consumiu 4 unidades;
    /// - 6 chegaram à vida;
    /// - se o alvo possuía 5 de vida, 5 foram aplicados e 1 virou overkill.
    ///
    /// A animação poderá reproduzir cada número sem recalcular nenhuma regra.
    /// </summary>
    public sealed class DamageReport
    {
        /// <summary>
        /// Confronto que originou este dano.
        ///
        /// Nesta fase, todo dano nasce do saldo de um ClashReport. Futuramente,
        /// dano passivo e reflexão poderão utilizar um contexto de origem mais
        /// geral, conforme descrito nas adições futuras ao final do arquivo.
        /// </summary>
        public ClashReport Clash { get; }

        /// <summary>
        /// Verdadeiro quando houve vantagem e, portanto, um alvo para a resolução.
        ///
        /// Um empate também produz um DamageReport válido, mas sem atacante, alvo
        /// ou mudança de estado. Isso permite que o pipeline sempre entregue um
        /// resultado explícito em vez de usar null para significar "nada ocorreu".
        /// </summary>
        public bool HasTarget => TargetBefore != null;

        /// <summary>
        /// Lado que originou o dano.
        ///
        /// Em um empate vale ScoreActor.None.
        /// </summary>
        public ScoreActor SourceActor { get; }

        /// <summary>
        /// Lado que recebeu o dano.
        ///
        /// Em um empate vale ScoreActor.None.
        /// </summary>
        public ScoreActor TargetActor { get; }

        /// <summary>
        /// Estado imutável do alvo imediatamente antes da aplicação.
        ///
        /// Em um empate esta propriedade vale null porque não existe alvo.
        /// Sempre consulte HasTarget antes de utilizá-la em código genérico.
        /// </summary>
        public CombatantSnapshot TargetBefore { get; }

        /// <summary>
        /// Estado imutável do alvo imediatamente depois da aplicação.
        ///
        /// Em um empate esta propriedade vale null.
        /// </summary>
        public CombatantSnapshot TargetAfter { get; }

        /// <summary>
        /// Atalho para o identificador do alvo.
        ///
        /// Retorna null quando o confronto terminou empatado.
        /// </summary>
        public string TargetCombatantId =>
            HasTarget ? TargetBefore.CombatantId : null;

        /// <summary>
        /// Pontuação sem oposição recebida do ClashReport.
        ///
        /// Este é o ponto inicial do pipeline de dano, antes de defesa,
        /// vulnerabilidade, imunidade ou outros modificadores.
        /// </summary>
        public int RawDamage { get; }

        /// <summary>
        /// Total acrescentado ao dano bruto por efeitos ofensivos.
        ///
        /// Multiplicadores futuros também poderão ser resolvidos como o aumento
        /// efetivo que produziram. O relatório guarda o resultado; a origem
        /// detalhada de cada parcela poderá vir de DamageContribution na Fase 3.
        /// </summary>
        public int IncreasedDamage { get; }

        /// <summary>
        /// Total impedido antes da interação com o escudo.
        ///
        /// Aqui podem entrar defesa, resistência, imunidade ou reduções de efeitos.
        /// O valor é agregado nesta fase e nunca pode tornar o dano negativo.
        /// </summary>
        public int PreventedDamage { get; }

        /// <summary>
        /// Dano restante depois dos modificadores e antes do escudo.
        ///
        /// Invariante usada pelo relatório:
        /// DamageAfterModifiers = RawDamage + IncreasedDamage - PreventedDamage.
        /// </summary>
        public int DamageAfterModifiers { get; }

        /// <summary>
        /// Parte do dano que foi neutralizada pelo escudo.
        ///
        /// Este valor é separado de ShieldConsumed. Normalmente ambos serão
        /// iguais, mas uma runa futura poderá fazer cada unidade de escudo absorver
        /// mais de um ponto de dano ou permitir absorção sem consumo.
        /// </summary>
        public int DamageAbsorbedByShield { get; }

        /// <summary>
        /// Quantidade efetivamente removida do recurso Shield do alvo.
        /// </summary>
        public int ShieldConsumed { get; }

        /// <summary>
        /// Dano que tentou alcançar a vida depois do escudo.
        ///
        /// Ele pode ser maior que a vida disponível. Por isso ainda o separamos do
        /// dano realmente aplicado.
        /// </summary>
        public int RequestedHealthDamage { get; }

        /// <summary>
        /// Quantidade efetivamente retirada de CurrentHealth.
        ///
        /// Este valor nunca ultrapassa a vida que o alvo possuía antes do golpe.
        /// </summary>
        public int AppliedHealthDamage { get; }

        /// <summary>
        /// Parte do dano à vida que excedeu a vida disponível.
        ///
        /// Exemplo: causar 8 a um alvo com 3 de vida aplica 3 e registra 5 de
        /// overkill. A vida armazenada continua em zero, nunca em -5.
        /// </summary>
        public int Overkill { get; }

        /// <summary>
        /// Dano de reflexão gerado por esta resolução.
        ///
        /// O valor ainda não altera o atacante dentro deste relatório. Quando a
        /// reflexão for implementada, ela deverá produzir uma nova resolução de
        /// dano ligada a esta origem, evitando mudanças escondidas de vida.
        /// </summary>
        public int ReflectedDamageGenerated { get; }

        /// <summary>
        /// Verdadeiro quando o confronto terminou empatado e nenhuma aplicação de
        /// dano foi necessária.
        /// </summary>
        public bool IsNoDamage => !HasTarget;

        /// <summary>
        /// Verdadeiro quando algum recurso do alvo realmente mudou.
        /// </summary>
        public bool ChangedTargetState =>
            HasTarget &&
            (ShieldConsumed > 0 || AppliedHealthDamage > 0);

        /// <summary>
        /// Verdadeiro quando nenhum ponto alcançou a vida do alvo.
        ///
        /// Isso pode acontecer por redução completa ou absorção pelo escudo.
        /// </summary>
        public bool PreventedAllHealthDamage =>
            AppliedHealthDamage == 0;

        /// <summary>
        /// Verdadeiro quando existia dano depois dos modificadores e todo ele foi
        /// absorvido pelo escudo.
        /// </summary>
        public bool WasFullyAbsorbedByShield =>
            DamageAfterModifiers > 0 &&
            DamageAbsorbedByShield == DamageAfterModifiers;

        /// <summary>
        /// Verdadeiro quando este dano fez um alvo vivo chegar a zero de vida.
        /// </summary>
        public bool CausedDefeat =>
            HasTarget &&
            TargetBefore.IsAlive &&
            TargetAfter.IsDefeated;

        /// <summary>
        /// Cria um relatório com uma transição de estado já resolvida.
        ///
        /// O construtor é internal propositalmente. A apresentação poderá ler o
        /// relatório, mas apenas uma regra da assembly de domínio, como o futuro
        /// DamageResolver, poderá criá-lo.
        /// </summary>
        internal DamageReport(
            ClashReport clash,
            ScoreActor sourceActor,
            ScoreActor targetActor,
            CombatantSnapshot targetBefore,
            CombatantSnapshot targetAfter,
            int rawDamage,
            int increasedDamage,
            int preventedDamage,
            int damageAfterModifiers,
            int damageAbsorbedByShield,
            int shieldConsumed,
            int requestedHealthDamage,
            int appliedHealthDamage,
            int overkill,
            int reflectedDamageGenerated)
        {
            Clash = clash ??
                    throw new ArgumentNullException(nameof(clash));

            ValidateNonNegativeValues(
                rawDamage,
                increasedDamage,
                preventedDamage,
                damageAfterModifiers,
                damageAbsorbedByShield,
                shieldConsumed,
                requestedHealthDamage,
                appliedHealthDamage,
                overkill,
                reflectedDamageGenerated);

            if (Clash.IsTie)
            {
                ValidateTieReport(
                    sourceActor,
                    targetActor,
                    targetBefore,
                    targetAfter,
                    rawDamage,
                    increasedDamage,
                    preventedDamage,
                    damageAfterModifiers,
                    damageAbsorbedByShield,
                    shieldConsumed,
                    requestedHealthDamage,
                    appliedHealthDamage,
                    overkill,
                    reflectedDamageGenerated);
            }
            else
            {
                ValidateTargetedReport(
                    Clash,
                    sourceActor,
                    targetActor,
                    targetBefore,
                    targetAfter,
                    rawDamage,
                    increasedDamage,
                    preventedDamage,
                    damageAfterModifiers,
                    damageAbsorbedByShield,
                    shieldConsumed,
                    requestedHealthDamage,
                    appliedHealthDamage,
                    overkill);
            }

            SourceActor = sourceActor;
            TargetActor = targetActor;
            TargetBefore = targetBefore;
            TargetAfter = targetAfter;
            RawDamage = rawDamage;
            IncreasedDamage = increasedDamage;
            PreventedDamage = preventedDamage;
            DamageAfterModifiers = damageAfterModifiers;
            DamageAbsorbedByShield = damageAbsorbedByShield;
            ShieldConsumed = shieldConsumed;
            RequestedHealthDamage = requestedHealthDamage;
            AppliedHealthDamage = appliedHealthDamage;
            Overkill = overkill;
            ReflectedDamageGenerated = reflectedDamageGenerated;
        }

        /// <summary>
        /// Cria o relatório explícito de um confronto empatado.
        ///
        /// O DamageResolver poderá chamar este método sem fabricar um combatente
        /// alvo. Todos os valores de dano serão zero e HasTarget será false.
        /// </summary>
        internal static DamageReport CreateNoDamage(ClashReport clash)
        {
            return new DamageReport(
                clash,
                ScoreActor.None,
                ScoreActor.None,
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        private static void ValidateNonNegativeValues(
            int rawDamage,
            int increasedDamage,
            int preventedDamage,
            int damageAfterModifiers,
            int damageAbsorbedByShield,
            int shieldConsumed,
            int requestedHealthDamage,
            int appliedHealthDamage,
            int overkill,
            int reflectedDamageGenerated)
        {
            ValidateNonNegative(
                rawDamage,
                nameof(rawDamage));

            ValidateNonNegative(
                increasedDamage,
                nameof(increasedDamage));

            ValidateNonNegative(
                preventedDamage,
                nameof(preventedDamage));

            ValidateNonNegative(
                damageAfterModifiers,
                nameof(damageAfterModifiers));

            ValidateNonNegative(
                damageAbsorbedByShield,
                nameof(damageAbsorbedByShield));

            ValidateNonNegative(
                shieldConsumed,
                nameof(shieldConsumed));

            ValidateNonNegative(
                requestedHealthDamage,
                nameof(requestedHealthDamage));

            ValidateNonNegative(
                appliedHealthDamage,
                nameof(appliedHealthDamage));

            ValidateNonNegative(
                overkill,
                nameof(overkill));

            ValidateNonNegative(
                reflectedDamageGenerated,
                nameof(reflectedDamageGenerated));
        }

        private static void ValidateTieReport(
            ScoreActor sourceActor,
            ScoreActor targetActor,
            CombatantSnapshot targetBefore,
            CombatantSnapshot targetAfter,
            int rawDamage,
            int increasedDamage,
            int preventedDamage,
            int damageAfterModifiers,
            int damageAbsorbedByShield,
            int shieldConsumed,
            int requestedHealthDamage,
            int appliedHealthDamage,
            int overkill,
            int reflectedDamageGenerated)
        {
            if (sourceActor != ScoreActor.None ||
                targetActor != ScoreActor.None)
            {
                throw new ArgumentException(
                    "Um confronto empatado não possui atacante nem alvo de dano.");
            }

            if (targetBefore != null || targetAfter != null)
            {
                throw new ArgumentException(
                    "Um confronto empatado não pode possuir fotografias de alvo.");
            }

            long totalReportedValue =
                (long)rawDamage +
                increasedDamage +
                preventedDamage +
                damageAfterModifiers +
                damageAbsorbedByShield +
                shieldConsumed +
                requestedHealthDamage +
                appliedHealthDamage +
                overkill +
                reflectedDamageGenerated;

            if (totalReportedValue != 0)
            {
                throw new ArgumentException(
                    "Um confronto empatado deve produzir um relatório de dano zero.");
            }
        }

        private static void ValidateTargetedReport(
            ClashReport clash,
            ScoreActor sourceActor,
            ScoreActor targetActor,
            CombatantSnapshot targetBefore,
            CombatantSnapshot targetAfter,
            int rawDamage,
            int increasedDamage,
            int preventedDamage,
            int damageAfterModifiers,
            int damageAbsorbedByShield,
            int shieldConsumed,
            int requestedHealthDamage,
            int appliedHealthDamage,
            int overkill)
        {
            if (sourceActor != clash.AdvantageActor)
            {
                throw new ArgumentException(
                    "O atacante deve ser o participante que obteve vantagem no " +
                    "ClashReport.",
                    nameof(sourceActor));
            }

            if (targetActor != clash.DisadvantageActor)
            {
                throw new ArgumentException(
                    "O alvo deve ser o participante que ficou em desvantagem no " +
                    "ClashReport.",
                    nameof(targetActor));
            }

            if (targetBefore == null)
            {
                throw new ArgumentNullException(nameof(targetBefore));
            }

            if (targetAfter == null)
            {
                throw new ArgumentNullException(nameof(targetAfter));
            }

            if (targetBefore.Actor != targetActor ||
                targetAfter.Actor != targetActor)
            {
                throw new ArgumentException(
                    "As duas fotografias precisam pertencer ao alvo do dano.");
            }

            if (!string.Equals(
                    targetBefore.CombatantId,
                    targetAfter.CombatantId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "As fotografias anterior e posterior precisam pertencer ao " +
                    "mesmo CombatantId.");
            }

            if (targetBefore.MaximumHealth != targetAfter.MaximumHealth)
            {
                throw new ArgumentException(
                    "A resolução de dano comum não pode alterar MaximumHealth.");
            }

            if (targetBefore.IsDefeated)
            {
                throw new ArgumentException(
                    "Não é possível registrar dano comum contra um alvo que já " +
                    "estava derrotado.",
                    nameof(targetBefore));
            }

            if (rawDamage != clash.UnopposedScore)
            {
                throw new ArgumentException(
                    "RawDamage deve ser igual ao UnopposedScore do confronto.",
                    nameof(rawDamage));
            }

            /*
             * Fazemos a soma em long para validar a fórmula sem risco de overflow
             * de int. O futuro DamageResolver também deverá detectar e tratar
             * combinações extremas antes de criar o relatório.
             */
            long expectedDamageAfterModifiers =
                (long)rawDamage + increasedDamage - preventedDamage;

            if (expectedDamageAfterModifiers < 0 ||
                expectedDamageAfterModifiers > int.MaxValue ||
                damageAfterModifiers != expectedDamageAfterModifiers)
            {
                throw new ArgumentException(
                    "DamageAfterModifiers deve corresponder ao dano bruto mais " +
                    "aumentos, menos prevenções, sem ficar fora do intervalo de int.",
                    nameof(damageAfterModifiers));
            }

            if (damageAbsorbedByShield > damageAfterModifiers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageAbsorbedByShield),
                    damageAbsorbedByShield,
                    "O escudo não pode absorver mais dano do que chegou até ele.");
            }

            if (shieldConsumed > targetBefore.Shield)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shieldConsumed),
                    shieldConsumed,
                    "Não é possível consumir mais escudo do que o alvo possuía.");
            }

            if (targetAfter.Shield != targetBefore.Shield - shieldConsumed)
            {
                throw new ArgumentException(
                    "A diferença entre o escudo anterior e o posterior não " +
                    "corresponde a ShieldConsumed.",
                    nameof(shieldConsumed));
            }

            if (damageAfterModifiers - damageAbsorbedByShield !=
                requestedHealthDamage)
            {
                throw new ArgumentException(
                    "RequestedHealthDamage deve ser o dano após modificadores " +
                    "menos o dano absorvido pelo escudo.",
                    nameof(requestedHealthDamage));
            }

            int expectedAppliedHealthDamage =
                Math.Min(
                    requestedHealthDamage,
                    targetBefore.CurrentHealth);

            if (appliedHealthDamage != expectedAppliedHealthDamage)
            {
                throw new ArgumentException(
                    "AppliedHealthDamage deve corresponder ao dano que realmente " +
                    "cabia na vida disponível.",
                    nameof(appliedHealthDamage));
            }

            if (requestedHealthDamage - appliedHealthDamage != overkill)
            {
                throw new ArgumentException(
                    "Overkill deve ser a parte do dano solicitado que excedeu a " +
                    "vida disponível.",
                    nameof(overkill));
            }

            if (targetAfter.CurrentHealth !=
                targetBefore.CurrentHealth - appliedHealthDamage)
            {
                throw new ArgumentException(
                    "A diferença entre a vida anterior e a posterior não " +
                    "corresponde a AppliedHealthDamage.",
                    nameof(appliedHealthDamage));
            }

            ValidateStateVersions(
                targetBefore,
                targetAfter,
                shieldConsumed > 0 || appliedHealthDamage > 0);
        }

        private static void ValidateStateVersions(
            CombatantSnapshot targetBefore,
            CombatantSnapshot targetAfter,
            bool stateChanged)
        {
            if (!stateChanged)
            {
                if (targetAfter.Version != targetBefore.Version)
                {
                    throw new ArgumentException(
                        "Uma resolução sem mudança de vida ou escudo deve manter " +
                        "a mesma versão do CombatantState.");
                }

                return;
            }

            if (targetBefore.Version == long.MaxValue)
            {
                throw new ArgumentException(
                    "Não existe uma versão posterior válida depois de long.MaxValue.",
                    nameof(targetBefore));
            }

            if (targetAfter.Version != targetBefore.Version + 1)
            {
                throw new ArgumentException(
                    "Uma aplicação de dano que altera o estado deve avançar a " +
                    "versão exatamente uma vez.");
            }
        }

        private static void ValidateNonNegative(
            int value,
            string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Um valor do relatório de dano não pode ser negativo.");
            }
        }

        /*
         * ADIÇÕES FUTURAS POSSÍVEIS
         *
         * 1. Na Fase 3, DamageContribution e DamageStep poderão detalhar a origem,
         *    fase, prioridade, operação e valor de cada modificação. Os campos
         *    agregados deste relatório continuarão úteis como totais autoritativos
         *    para a interface e para testes.
         *
         * 2. Dano passivo, dano direto de runa e dano de ambiente não precisam de
         *    um confronto de pontuação. Quando essas regras forem implementadas,
         *    Clash poderá ser substituído por um DamageOrigin que aceite diferentes
         *    causas e possua um identificador determinístico.
         *
         * 3. A reflexão deverá gerar outra resolução contra o atacante. Este
         *    relatório registra ReflectedDamageGenerated, mas não altera dois
         *    CombatantState ao mesmo tempo. Relatórios ligados tornam morte
         *    simultânea, replays e animações muito mais fáceis de explicar.
         *
         * 4. Tipos de dano, como físico, mágico, verdadeiro ou perfurante, poderão
         *    alterar quais proteções participam do cálculo. Eles devem entrar como
         *    dados explícitos, nunca como condicionais escondidas no MatchAnimator.
         *
         * 5. Um escudo futuro poderá ter eficiência diferente de 1 para 1. Por
         *    isso DamageAbsorbedByShield e ShieldConsumed são propriedades
         *    separadas e não precisam possuir o mesmo valor.
         *
         * 6. O DamageReport não concede pontos de progressão. Essa recompensa só
         *    deverá ocorrer quando o estado do encontro confirmar que o inimigo ou
         *    boss foi derrotado, depois de todos os danos e reações pendentes.
         *
         * 7. Se futuramente um único golpe atingir vários alvos, cada alvo deverá
         *    receber seu próprio DamageReport. Um relatório composto poderá reunir
         *    todos eles sem esconder a transição individual de cada CombatantState.
         */
    }
}