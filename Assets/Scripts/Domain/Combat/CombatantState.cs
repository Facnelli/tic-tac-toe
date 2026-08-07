using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Combat
{
    /// <summary>
    /// Guarda o estado mutável e autoritativo de um participante do encontro.
    ///
    /// "Autoritativo" significa que esta é a fonte de verdade da vida e do
    /// escudo. A barra de vida, os textos da interface e as animações apenas leem
    /// estes valores; elas nunca mantêm uma segunda vida separada nem decidem
    /// quanto dano foi recebido.
    ///
    /// Esta classe pertence ao domínio puro. Portanto, não herda MonoBehaviour,
    /// não usa UnityEngine e não deve ser adicionada a um GameObject.
    ///
    /// O CombatantState também não calcula dano. O futuro DamageResolver decidirá
    /// quanto do ataque foi bloqueado, quanto escudo foi consumido e quanto chegou
    /// à vida. Depois dessa resolução, ele pedirá a este estado que aplique os
    /// valores já calculados por meio de ApplyResolvedDamage.
    /// </summary>
    public sealed class CombatantState
    {
        /// <summary>
        /// Identificador estável desta instância de combatente.
        ///
        /// Exemplos:
        /// - "player";
        /// - "enemy:goblin:encounter-12";
        /// - "boss:the-architect".
        ///
        /// Não use o nome traduzido que aparece na interface como identificador.
        /// O nome visível pode mudar de idioma, enquanto este valor precisa
        /// continuar igual em saves, relatórios e replays.
        /// </summary>
        public string CombatantId { get; }

        /// <summary>
        /// Lado lógico ocupado pelo combatente no encontro atual.
        ///
        /// Nesta fase, o jogo possui exatamente Player e Enemy. Environment não é
        /// aceito porque uma regra do cenário pode originar um efeito, mas não
        /// possui vida para ser derrotada.
        /// </summary>
        public ScoreActor Actor { get; }

        /// <summary>
        /// Maior quantidade de vida que o combatente pode possuir atualmente.
        ///
        /// O valor é sempre maior que zero. Bônus permanentes de uma run poderão
        /// futuramente participar da criação deste estado ou de DerivedStats, sem
        /// deixar a interface alterar o máximo diretamente.
        /// </summary>
        public int MaximumHealth { get; }

        /// <summary>
        /// Vida atual, sempre limitada entre zero e MaximumHealth.
        ///
        /// O setter é privado para impedir códigos externos de escrever algo como
        /// CurrentHealth = -20. Toda mudança passa por uma operação validada.
        /// </summary>
        public int CurrentHealth { get; private set; }

        /// <summary>
        /// Proteção consumível disponível antes da vida.
        ///
        /// Shield apenas armazena o recurso. Quem decide quanto escudo um golpe
        /// consome é o futuro DamageResolver, pois runas poderão alterar a ordem,
        /// a eficiência ou até permitir que certos danos ignorem escudos.
        /// </summary>
        public int Shield { get; private set; }

        /// <summary>
        /// Número incrementado sempre que vida ou escudo sofrem uma alteração real.
        ///
        /// A versão ajuda relatórios, saves e animações a saberem a qual momento do
        /// combatente pertencem. Uma operação que não muda nenhum valor não aumenta
        /// Version.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>
        /// Verdadeiro enquanto ainda existe pelo menos um ponto de vida.
        ///
        /// Escudo não mantém um combatente vivo quando CurrentHealth chegou a zero.
        /// Isso evita situações estranhas em que um participante derrotado recebe
        /// escudo e volta a agir sem uma regra explícita de ressurreição.
        /// </summary>
        public bool IsAlive => CurrentHealth > 0;

        /// <summary>
        /// Verdadeiro quando a vida chegou exatamente a zero.
        ///
        /// Como CurrentHealth nunca pode ficar negativa, esta verificação é simples
        /// e todos os sistemas concordam sobre o momento da derrota.
        /// </summary>
        public bool IsDefeated => CurrentHealth == 0;

        /// <summary>
        /// Verdadeiro quando nenhuma vida está faltando.
        /// </summary>
        public bool IsAtFullHealth => CurrentHealth == MaximumHealth;

        /// <summary>
        /// Quantidade de vida necessária para chegar novamente ao máximo.
        /// </summary>
        public int MissingHealth => MaximumHealth - CurrentHealth;

        /// <summary>
        /// Proporção de vida atual entre zero e um.
        ///
        /// Exemplos:
        /// - 50 de 100 retorna 0,5;
        /// - 100 de 100 retorna 1;
        /// - 0 de 100 retorna 0.
        ///
        /// A interface poderá usar essa proporção para preencher uma barra sem
        /// precisar repetir a divisão. Usamos decimal para manter o domínio
        /// determinístico; a view poderá converter para float somente ao desenhar.
        /// </summary>
        public decimal HealthRatio =>
            (decimal)CurrentHealth / MaximumHealth;

        /// <summary>
        /// Cria um combatente com a vida completamente cheia e sem escudo.
        ///
        /// Este é o construtor mais comum para o começo de um encontro.
        /// </summary>
        public CombatantState(
            string combatantId,
            ScoreActor actor,
            int maximumHealth)
            : this(
                combatantId,
                actor,
                maximumHealth,
                maximumHealth,
                0)
        {
        }

        /// <summary>
        /// Cria um combatente a partir de valores completos de vida e escudo.
        ///
        /// Este construtor é útil para carregar um save, iniciar um encontro com
        /// um boss ferido ou construir estados específicos em testes.
        /// </summary>
        public CombatantState(
            string combatantId,
            ScoreActor actor,
            int maximumHealth,
            int currentHealth,
            int shield = 0)
        {
            ValidateCombatantId(combatantId);
            ValidateActor(actor);
            ValidateInitialResources(
                maximumHealth,
                currentHealth,
                shield);

            CombatantId = combatantId;
            Actor = actor;
            MaximumHealth = maximumHealth;
            CurrentHealth = currentHealth;
            Shield = shield;

            /*
             * Os valores recebidos no construtor formam o estado inicial. Eles não
             * contam como uma mudança ocorrida durante o encontro, então a versão
             * começa em zero.
             */
            Version = 0;
        }

        /// <summary>
        /// Cria uma cópia independente representando o mesmo momento lógico.
        ///
        /// A cópia é útil para saves, prévias e futuras simulações da IA. Alterar a
        /// cópia não altera o combatente real, e vice-versa.
        /// </summary>
        public CombatantState Clone()
        {
            CombatantState clone = new CombatantState(
                CombatantId,
                Actor,
                MaximumHealth,
                CurrentHealth,
                Shield);

            /*
             * O clone representa exatamente o mesmo instante, por isso recebe a
             * mesma versão. A partir daqui, cada instância controla sua própria
             * sequência de alterações.
             */
            clone.Version = Version;

            return clone;
        }

        /// <summary>
        /// Aplica uma resolução de dano já calculada pelo DamageResolver.
        ///
        /// O método é internal propositalmente. Classes do mesmo domínio poderão
        /// chamá-lo, mas componentes da apresentação não conseguirão retirar vida
        /// diretamente para acompanhar uma animação.
        /// </summary>
        /// <param name="shieldConsumed">
        /// Quantidade de escudo que a resolução determinou que deve ser gasta.
        /// Nunca pode ser maior que o escudo atualmente disponível.
        /// </param>
        /// <param name="healthDamage">
        /// Dano que atravessou todas as proteções e chegou à vida. Pode ser maior
        /// que a vida atual; nesse caso, a vida termina em zero, nunca negativa.
        /// </param>
        /// <returns>
        /// True quando vida ou escudo mudaram; false quando os dois valores eram
        /// zero e nenhuma alteração real ocorreu.
        /// </returns>
        internal bool ApplyResolvedDamage(
            int shieldConsumed,
            int healthDamage)
        {
            ValidateNonNegative(
                shieldConsumed,
                nameof(shieldConsumed),
                "O escudo consumido não pode ser negativo.");

            ValidateNonNegative(
                healthDamage,
                nameof(healthDamage),
                "O dano à vida não pode ser negativo.");

            if (shieldConsumed > Shield)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shieldConsumed),
                    shieldConsumed,
                    "Não é possível consumir mais escudo do que o combatente possui.");
            }

            if (IsDefeated &&
                (shieldConsumed > 0 || healthDamage > 0))
            {
                throw new InvalidOperationException(
                    "Um combatente derrotado não pode receber uma nova resolução " +
                    "de dano. Uma futura ressurreição deverá ser uma regra explícita.");
            }

            if (shieldConsumed == 0 && healthDamage == 0)
            {
                return false;
            }

            Shield -= shieldConsumed;

            /*
             * Math.Max impede vida negativa em situações de overkill. Por exemplo,
             * receber 20 de dano com apenas 5 de vida resulta em 0, não em -15.
             * O futuro DamageReport ainda poderá registrar os 15 pontos de overkill
             * antes de solicitar esta alteração.
             */
            CurrentHealth = Math.Max(
                0,
                CurrentHealth - healthDamage);

            RegisterChange();
            return true;
        }

        /// <summary>
        /// Restaura vida sem ultrapassar MaximumHealth.
        ///
        /// A cura está separada do dano porque efeitos futuros poderão reagir de
        /// maneiras diferentes a DamageApplied e HealingApplied.
        /// </summary>
        /// <returns>
        /// True quando alguma vida foi realmente restaurada. Retorna false ao
        /// tentar curar zero ou um combatente que já está com a vida cheia.
        /// </returns>
        internal bool RestoreHealth(int amount)
        {
            ValidateNonNegative(
                amount,
                nameof(amount),
                "A quantidade de cura não pode ser negativa.");

            if (amount == 0 || IsAtFullHealth)
            {
                return false;
            }

            if (IsDefeated)
            {
                throw new InvalidOperationException(
                    "Uma cura comum não pode reviver um combatente derrotado. " +
                    "Uma futura ressurreição deverá ser uma regra explícita.");
            }

            /*
             * A soma é feita em long para evitar overflow. Sem isso, somar um valor
             * muito grande a um int poderia dar a volta e produzir um número
             * negativo antes de aplicarmos o limite de vida máxima.
             */
            long healedHealth =
                (long)CurrentHealth + amount;

            CurrentHealth = (int)Math.Min(
                MaximumHealth,
                healedHealth);

            RegisterChange();
            return true;
        }

        /// <summary>
        /// Acrescenta escudo ao combatente.
        ///
        /// O método não decide de onde o escudo veio nem quanto uma runa deve
        /// conceder. Ele apenas aplica uma quantidade já validada por uma futura
        /// regra ou serviço do domínio.
        /// </summary>
        internal bool AddShield(int amount)
        {
            ValidateNonNegative(
                amount,
                nameof(amount),
                "A quantidade de escudo adicionada não pode ser negativa.");

            if (amount == 0)
            {
                return false;
            }

            if (IsDefeated)
            {
                throw new InvalidOperationException(
                    "Não é possível conceder escudo a um combatente derrotado.");
            }

            long resultingShield =
                (long)Shield + amount;

            if (resultingShield > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "A quantidade resultante de escudo ultrapassa o limite " +
                    "numérico suportado pelo estado do combatente.");
            }

            Shield = (int)resultingShield;

            RegisterChange();
            return true;
        }

        /// <summary>
        /// Remove uma quantidade de escudo por uma razão que não é dano.
        ///
        /// Isso poderá ser usado, por exemplo, por uma regra de fim de rodada ou
        /// por um efeito que dissipa proteção. O valor nunca é reduzido abaixo de
        /// zero.
        /// </summary>
        internal bool RemoveShield(int amount)
        {
            ValidateNonNegative(
                amount,
                nameof(amount),
                "A quantidade de escudo removida não pode ser negativa.");

            if (amount == 0 || Shield == 0)
            {
                return false;
            }

            Shield = Math.Max(0, Shield - amount);

            RegisterChange();
            return true;
        }

        /// <summary>
        /// Remove todo o escudo disponível.
        ///
        /// Retorna false quando o combatente já não possuía escudo. Isso é útil
        /// para não criar uma versão nova nem uma animação sem mudança real.
        /// </summary>
        internal bool ClearShield()
        {
            if (Shield == 0)
            {
                return false;
            }

            Shield = 0;

            RegisterChange();
            return true;
        }

        private static void ValidateCombatantId(string combatantId)
        {
            if (string.IsNullOrWhiteSpace(combatantId))
            {
                throw new ArgumentException(
                    "Um combatente precisa possuir um CombatantId estável.",
                    nameof(combatantId));
            }
        }

        private static void ValidateActor(ScoreActor actor)
        {
            if (actor != ScoreActor.Player &&
                actor != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actor),
                    actor,
                    "Um CombatantState deve pertencer a Player ou Enemy.");
            }
        }

        private static void ValidateInitialResources(
            int maximumHealth,
            int currentHealth,
            int shield)
        {
            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHealth),
                    maximumHealth,
                    "A vida máxima precisa ser maior que zero.");
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
                    "O escudo inicial não pode ser negativo.");
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

        /// <summary>
        /// Registra uma alteração real do estado.
        ///
        /// Centralizar este incremento evita que uma nova operação futura altere
        /// vida ou escudo e esqueça de atualizar Version.
        /// </summary>
        private void RegisterChange()
        {
            if (Version == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "A versão do CombatantState atingiu o limite suportado.");
            }

            Version++;
        }

        /*
         * ADIÇÕES FUTURAS POSSÍVEIS
         *
         * 1. FlatDamageReduction, vulnerabilidade, reflexão e imunidades não foram
         *    adicionadas como números soltos aqui. Elas poderão nascer como
         *    DerivedStats e DamageContribution, conservando a fonte e a duração de
         *    cada efeito. Assim, duas runas de defesa não sobrescrevem uma à outra.
         *
         * 2. Um HealingReport e um HealingResolver poderão chamar RestoreHealth e
         *    registrar origem, valor solicitado, cura efetiva e excesso de cura.
         *    A animação deverá consumir esse relatório em vez de calcular a cura.
         *
         * 3. Uma operação própria poderá alterar MaximumHealth. Ela precisará dizer
         *    claramente se a vida atual acompanha o aumento e o que acontece quando
         *    o máximo é reduzido abaixo da vida atual.
         *
         * 4. Ressurreição deverá ser uma regra explícita. Não faça uma cura comum
         *    transformar silenciosamente um combatente derrotado em vivo sem gerar
         *    um relatório e os gatilhos apropriados.
         *
         * 5. CombatantId e ScoreActor têm responsabilidades diferentes. O primeiro
         *    identifica uma instância; o segundo representa o lado Player/Enemy no
         *    confronto atual. Quando existirem aliados ou vários inimigos, sistemas
         *    de pontuação poderão migrar de ScoreActor para CombatantId.
         *
         * 6. Efeitos ativos, runas equipadas e tags do combatente poderão compor um
         *    futuro ActorSnapshot. O estado não deve guardar referências a
         *    ScriptableObject, GameObject, Sprite ou qualquer tipo da Unity.
         *
         * 7. Poderá ser criado um CombatantStateSnapshot serializável para salvar a
         *    run. O snapshot deverá conservar CombatantId, recursos e Version sem
         *    expor setters públicos no estado vivo da partida.
         */
    }
}