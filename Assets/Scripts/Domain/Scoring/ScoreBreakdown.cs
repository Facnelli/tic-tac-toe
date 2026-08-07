using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TicTacToeRoguelike.Domain.Scoring
{
    /// <summary>
    /// Identifica quem participa de uma contribuição de pontuação.
    ///
    /// Player e Enemy podem ser donos de um placar, fontes de um efeito ou alvos
    /// de uma alteração. Environment representa uma regra que não pertence a um
    /// combatente específico, como o bônus padrão concedido pela vitória.
    ///
    /// None existe para expressar "não informado" em integrações futuras, mas não
    /// é aceito em um ScoreBreakdown nem em uma contribuição já pronta para cálculo.
    /// </summary>
    public enum ScoreActor
    {
        None,
        Player,
        Enemy,
        Environment
    }

    /// <summary>
    /// Determina em qual parte da fórmula uma contribuição será aplicada.
    ///
    /// A fase não existe apenas para organizar a animação. Ela faz parte da regra
    /// matemática: somar 3 pontos antes dos multiplicadores produz um resultado
    /// diferente de somar os mesmos 3 pontos depois deles.
    /// </summary>
    public enum ScorePhase
    {
        /// <summary>
        /// Pontos originados diretamente das sequências encontradas.
        /// </summary>
        BasePoints,

        /// <summary>
        /// Pontos somados antes de qualquer multiplicador.
        /// </summary>
        FlatPoints,

        /// <summary>
        /// Alterações somadas ao multiplicador que começa em 1.
        ///
        /// Por exemplo, duas contribuições de +0,5 resultam no fator 2:
        /// 1 + 0,5 + 0,5 = 2.
        /// </summary>
        AdditiveMultiplier,

        /// <summary>
        /// Fatores independentes, multiplicados um após o outro.
        ///
        /// Por exemplo, dois fatores de 1,5 resultam em 2,25, e não em 2.
        /// </summary>
        IndependentMultiplier,

        /// <summary>
        /// Fatores próprios da condição de vitória do encontro.
        ///
        /// Esta fase separada permitirá que efeitos reduzam apenas o bônus de
        /// vitória do oponente sem interferir nos multiplicadores das runas.
        /// </summary>
        VictoryMultiplier,

        /// <summary>
        /// Pontos somados depois que todos os multiplicadores foram aplicados.
        /// </summary>
        FinalFlatPoints
    }

    /// <summary>
    /// Descreve a operação matemática realizada por uma contribuição.
    ///
    /// ScorePhase decide quando a contribuição ocorre; ScoreOperation explica
    /// como ela participa da conta. A operação é derivada da fase para impedir
    /// combinações incoerentes, como multiplicar durante BasePoints.
    /// </summary>
    public enum ScoreOperation
    {
        AddPoints,
        AddToMultiplier,
        Multiply
    }

    /// <summary>
    /// Descreve uma única origem de alteração da pontuação.
    ///
    /// Este objeto ainda não contém o resultado da conta. Ele funciona como uma
    /// instrução imutável dizendo:
    /// "na fase X, a fonte Y altera o placar do alvo Z pelo valor informado".
    ///
    /// O futuro ScorePipeline criará essas contribuições a partir das sequências,
    /// regras do encontro, runas, casas especiais e habilidades de bosses.
    /// </summary>
    public sealed class ScoreContribution
    {
        /// <summary>
        /// Identificador estável da fonte, apropriado para saves, replays e debug.
        ///
        /// Exemplos:
        /// "sequence:0,0-2,0", "rule:victory-bonus" ou "rune:ember:instance-4".
        /// Não use o texto visível da interface como identificador, pois ele poderá
        /// mudar com tradução sem que a identidade lógica da fonte tenha mudado.
        /// </summary>
        public string SourceId { get; }

        /// <summary>
        /// Texto curto que poderá ser apresentado ao jogador ou no log.
        /// </summary>
        public string DisplayText { get; }

        /// <summary>
        /// Participante que originou a alteração.
        ///
        /// Uma runa do inimigo que reduz o placar do jogador terá SourceOwner igual
        /// a Enemy e Target igual a Player.
        /// </summary>
        public ScoreActor SourceOwner { get; }

        /// <summary>
        /// Placar que recebe esta contribuição. Deve ser Player ou Enemy.
        /// </summary>
        public ScoreActor Target { get; }

        /// <summary>
        /// Momento matemático em que a contribuição será aplicada.
        /// </summary>
        public ScorePhase Phase { get; }

        /// <summary>
        /// Operação correspondente à fase escolhida.
        ///
        /// A propriedade é calculada, e não recebida no construtor. Dessa forma,
        /// não é possível criar uma contribuição cuja fase e operação discordem.
        /// </summary>
        public ScoreOperation Operation => GetOperationForPhase(Phase);

        /// <summary>
        /// Valor da contribuição.
        ///
        /// Exemplos:
        /// - 3 em BasePoints significa +3 pontos;
        /// - 0,5 em AdditiveMultiplier significa +0,5 ao fator que começa em 1;
        /// - 1,5 em IndependentMultiplier significa multiplicar por 1,5;
        /// - -2 em FinalFlatPoints significa retirar 2 após os multiplicadores.
        /// </summary>
        public decimal Amount { get; }

        /// <summary>
        /// Desempata contribuições dentro da mesma fase.
        ///
        /// Números menores são executados primeiro. A prioridade padrão é zero.
        /// Ela não permite mover uma contribuição para outra fase: todas as fases
        /// BasePoints sempre acontecem antes de qualquer FlatPoints, por exemplo.
        /// </summary>
        public int Priority { get; }

        public ScoreContribution(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            ScorePhase phase,
            decimal amount,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "Uma contribuição precisa possuir um SourceId estável.",
                    nameof(sourceId));
            }

            if (string.IsNullOrWhiteSpace(displayText))
            {
                throw new ArgumentException(
                    "Uma contribuição precisa possuir um texto de apresentação.",
                    nameof(displayText));
            }

            ValidateDefinedActor(sourceOwner, nameof(sourceOwner));

            if (sourceOwner == ScoreActor.None)
            {
                throw new ArgumentException(
                    "A fonte precisa possuir um dono. Use Environment para " +
                    "regras que não pertencem ao jogador nem ao inimigo.",
                    nameof(sourceOwner));
            }

            if (target != ScoreActor.Player &&
                target != ScoreActor.Enemy)
            {
                throw new ArgumentException(
                    "O alvo de uma contribuição deve ser Player ou Enemy.",
                    nameof(target));
            }

            if (!Enum.IsDefined(typeof(ScorePhase), phase))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(phase),
                    phase,
                    "A fase de pontuação informada não existe.");
            }

            /*
             * Um multiplicador negativo inverteria o sinal da pontuação. Isso pode
             * fazer uma penalidade virar bônus e torna a fórmula muito difícil de
             * compreender visualmente. Para zerar um multiplicador, use zero.
             *
             * Reduções aditivas continuam permitidas. Por exemplo, -0,25 diminui
             * o fator aditivo de 1 para 0,75. Se a soma tentar ficar abaixo de zero,
             * o fator efetivo será limitado a zero durante a resolução.
             */
            if ((phase == ScorePhase.IndependentMultiplier ||
                 phase == ScorePhase.VictoryMultiplier) &&
                amount < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Um fator multiplicativo não pode ser negativo.");
            }

            /*
             * BasePoints representa valor produzido por fatos positivos, como uma
             * sequência encontrada. Reduções pertencem a FlatPoints ou
             * FinalFlatPoints, o que mantém o relatório mais fácil de interpretar.
             */
            if (phase == ScorePhase.BasePoints && amount < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Pontos básicos não podem ser negativos.");
            }

            SourceId = sourceId;
            DisplayText = displayText;
            SourceOwner = sourceOwner;
            Target = target;
            Phase = phase;
            Amount = amount;
            Priority = priority;
        }

        /// <summary>
        /// Cria uma contribuição de pontos originados por uma sequência.
        /// </summary>
        public static ScoreContribution CreateBasePoints(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            decimal points,
            int priority = 0)
        {
            return new ScoreContribution(
                sourceId,
                displayText,
                sourceOwner,
                target,
                ScorePhase.BasePoints,
                points,
                priority);
        }

        /// <summary>
        /// Cria uma soma realizada antes dos multiplicadores.
        /// </summary>
        public static ScoreContribution CreateFlatPoints(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            decimal points,
            int priority = 0)
        {
            return new ScoreContribution(
                sourceId,
                displayText,
                sourceOwner,
                target,
                ScorePhase.FlatPoints,
                points,
                priority);
        }

        /// <summary>
        /// Cria uma alteração somada ao multiplicador base 1.
        /// </summary>
        public static ScoreContribution CreateAdditiveMultiplier(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            decimal multiplierDelta,
            int priority = 0)
        {
            return new ScoreContribution(
                sourceId,
                displayText,
                sourceOwner,
                target,
                ScorePhase.AdditiveMultiplier,
                multiplierDelta,
                priority);
        }

        /// <summary>
        /// Cria um fator multiplicativo independente.
        /// </summary>
        public static ScoreContribution CreateIndependentMultiplier(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            decimal factor,
            int priority = 0)
        {
            return new ScoreContribution(
                sourceId,
                displayText,
                sourceOwner,
                target,
                ScorePhase.IndependentMultiplier,
                factor,
                priority);
        }

        /// <summary>
        /// Cria um fator pertencente especificamente à condição de vitória.
        /// </summary>
        public static ScoreContribution CreateVictoryMultiplier(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            decimal factor,
            int priority = 0)
        {
            return new ScoreContribution(
                sourceId,
                displayText,
                sourceOwner,
                target,
                ScorePhase.VictoryMultiplier,
                factor,
                priority);
        }

        /// <summary>
        /// Cria uma soma realizada depois de todos os multiplicadores.
        /// </summary>
        public static ScoreContribution CreateFinalFlatPoints(
            string sourceId,
            string displayText,
            ScoreActor sourceOwner,
            ScoreActor target,
            decimal points,
            int priority = 0)
        {
            return new ScoreContribution(
                sourceId,
                displayText,
                sourceOwner,
                target,
                ScorePhase.FinalFlatPoints,
                points,
                priority);
        }

        internal static ScoreOperation GetOperationForPhase(
            ScorePhase phase)
        {
            switch (phase)
            {
                case ScorePhase.BasePoints:
                case ScorePhase.FlatPoints:
                case ScorePhase.FinalFlatPoints:
                    return ScoreOperation.AddPoints;

                case ScorePhase.AdditiveMultiplier:
                    return ScoreOperation.AddToMultiplier;

                case ScorePhase.IndependentMultiplier:
                case ScorePhase.VictoryMultiplier:
                    return ScoreOperation.Multiply;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(phase),
                        phase,
                        "A fase de pontuação informada não existe.");
            }
        }

        private static void ValidateDefinedActor(
            ScoreActor actor,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(ScoreActor), actor))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    actor,
                    "O participante de pontuação informado não existe.");
            }
        }
    }

    /// <summary>
    /// Registra o efeito já calculado de uma contribuição sobre o valor corrente.
    ///
    /// ScoreContribution descreve a intenção; ScoreStep descreve o que realmente
    /// aconteceu. A apresentação deve usar ScoreStep para animar a resolução sem
    /// repetir nenhuma regra matemática.
    /// </summary>
    public sealed class ScoreStep
    {
        /// <summary>
        /// Posição deste passo na resolução. A contagem começa em 1 para facilitar
        /// a leitura humana em logs e ferramentas de depuração.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Contribuição que originou este passo.
        /// </summary>
        public ScoreContribution Contribution { get; }

        /// <summary>
        /// Valor corrente imediatamente antes de aplicar a contribuição.
        /// </summary>
        public decimal ScoreBefore { get; }

        /// <summary>
        /// Valor corrente imediatamente depois de aplicar a contribuição.
        ///
        /// O valor ainda não foi arredondado nem limitado a zero. Essas operações
        /// acontecem somente ao final da fórmula.
        /// </summary>
        public decimal ScoreAfter { get; }

        /// <summary>
        /// Fator efetivo utilizado neste passo.
        ///
        /// Em somas simples ele vale 1. Em multiplicadores independentes ele é o
        /// próprio Amount. Na fase aditiva ele representa 1 mais todas as alterações
        /// aditivas acumuladas até este passo, limitado ao mínimo zero.
        /// </summary>
        public decimal EffectiveFactor { get; }

        internal ScoreStep(
            int order,
            ScoreContribution contribution,
            decimal scoreBefore,
            decimal scoreAfter,
            decimal effectiveFactor)
        {
            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(order),
                    order,
                    "A ordem de um passo precisa começar em 1.");
            }

            Order = order;
            Contribution = contribution ??
                throw new ArgumentNullException(nameof(contribution));
            ScoreBefore = scoreBefore;
            ScoreAfter = scoreAfter;
            EffectiveFactor = effectiveFactor;
        }
    }

    /// <summary>
    /// Resultado imutável e detalhado da pontuação de um participante.
    ///
    /// A fórmula aplicada é:
    ///
    /// Final = round(max(0,
    ///     ((BasePoints + FlatPoints)
    ///     * (1 + AdditiveMultiplier)
    ///     * IndependentMultiplierProduct
    ///     * VictoryMultiplierProduct)
    ///     + FinalFlatPoints))
    ///
    /// O fator (1 + AdditiveMultiplier) possui mínimo zero. Isso permite reduções
    /// sem transformar um multiplicador muito negativo em um bônus acidental.
    ///
    /// A classe não descobre sequências, não consulta runas e não causa dano. Ela
    /// recebe contribuições prontas, ordena-as e registra o resultado matemático.
    /// </summary>
    public sealed class ScoreBreakdown
    {
        private readonly ReadOnlyCollection<ScoreContribution> _contributions;
        private readonly ReadOnlyCollection<ScoreStep> _steps;

        /// <summary>
        /// Participante ao qual este placar pertence.
        /// </summary>
        public ScoreActor Participant { get; }

        /// <summary>
        /// Contribuições na ordem exata em que foram resolvidas.
        /// </summary>
        public IReadOnlyList<ScoreContribution> Contributions =>
            _contributions;

        /// <summary>
        /// Passos já calculados, prontos para UI, animações, logs e replays.
        /// </summary>
        public IReadOnlyList<ScoreStep> Steps => _steps;

        public decimal BasePoints { get; }
        public decimal FlatPoints { get; }
        public decimal AdditiveMultiplier { get; }

        /// <summary>
        /// Fator aditivo efetivo: max(0, 1 + AdditiveMultiplier).
        /// </summary>
        public decimal AdditiveMultiplierFactor { get; }

        public decimal IndependentMultiplierProduct { get; }
        public decimal VictoryMultiplierProduct { get; }
        public decimal FinalFlatPoints { get; }

        /// <summary>
        /// Produto de todos os grupos de multiplicadores.
        /// </summary>
        public decimal TotalMultiplier { get; }

        /// <summary>
        /// Resultado antes de impedir valores negativos.
        /// </summary>
        public decimal UnclampedScore { get; }

        /// <summary>
        /// Resultado após max(0, resultado), mas ainda sem arredondamento.
        /// </summary>
        public decimal NonNegativeScore { get; }

        /// <summary>
        /// Resultado decimal arredondado com MidpointRounding.AwayFromZero.
        ///
        /// Essa regra faz 2,5 virar 3. Ela é declarada explicitamente porque
        /// diferentes regras de arredondamento poderiam quebrar replays e testes.
        /// </summary>
        public decimal RoundedScore { get; }

        /// <summary>
        /// Valor inteiro que poderá seguir para o futuro ClashResolver.
        /// </summary>
        public int FinalScore { get; }

        /// <summary>
        /// Indica que RoundedScore ultrapassou int.MaxValue e FinalScore precisou
        /// ser limitado. O valor decimal completo continua disponível em
        /// RoundedScore para diagnóstico.
        /// </summary>
        public bool WasClampedToIntegerRange { get; }

        private ScoreBreakdown(
            ScoreActor participant,
            IList<ScoreContribution> contributions,
            IList<ScoreStep> steps,
            decimal basePoints,
            decimal flatPoints,
            decimal additiveMultiplier,
            decimal independentMultiplierProduct,
            decimal victoryMultiplierProduct,
            decimal finalFlatPoints,
            decimal unclampedScore)
        {
            Participant = participant;

            /*
             * Copiamos as listas para que ninguém consiga alterar um relatório
             * antigo guardando uma referência à lista usada durante o cálculo.
             * Esse padrão é o mesmo utilizado por SequenceMatch na Fase 1.
             */
            _contributions = new ReadOnlyCollection<ScoreContribution>(
                new List<ScoreContribution>(contributions));

            _steps = new ReadOnlyCollection<ScoreStep>(
                new List<ScoreStep>(steps));

            BasePoints = basePoints;
            FlatPoints = flatPoints;
            AdditiveMultiplier = additiveMultiplier;
            AdditiveMultiplierFactor = Math.Max(
                0m,
                1m + additiveMultiplier);
            IndependentMultiplierProduct = independentMultiplierProduct;
            VictoryMultiplierProduct = victoryMultiplierProduct;
            FinalFlatPoints = finalFlatPoints;
            TotalMultiplier =
                AdditiveMultiplierFactor *
                IndependentMultiplierProduct *
                VictoryMultiplierProduct;

            UnclampedScore = unclampedScore;
            NonNegativeScore = Math.Max(0m, unclampedScore);
            RoundedScore = decimal.Round(
                NonNegativeScore,
                0,
                MidpointRounding.AwayFromZero);

            if (RoundedScore > int.MaxValue)
            {
                /*
                 * Limitar aqui evita que uma combinação extrema de runas derrube
                 * a partida ao converter decimal para int. O aviso permanece no
                 * relatório por meio de WasClampedToIntegerRange.
                 */
                FinalScore = int.MaxValue;
                WasClampedToIntegerRange = true;
            }
            else
            {
                FinalScore = decimal.ToInt32(RoundedScore);
                WasClampedToIntegerRange = false;
            }
        }

        /// <summary>
        /// Resolve todas as contribuições e cria um relatório imutável.
        ///
        /// Este é o único ponto de entrada necessário para o futuro ScorePipeline.
        /// A coleção recebida pode estar vazia, produzindo um placar final igual a
        /// zero, mas não pode ser nula nem conter itens nulos.
        /// </summary>
        public static ScoreBreakdown Resolve(
            ScoreActor participant,
            IEnumerable<ScoreContribution> contributions)
        {
            ValidateParticipant(participant);

            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            List<IndexedContribution> indexedContributions =
                new List<IndexedContribution>();

            int originalIndex = 0;

            foreach (ScoreContribution contribution in contributions)
            {
                if (contribution == null)
                {
                    throw new ArgumentException(
                        "A coleção não pode conter uma contribuição nula.",
                        nameof(contributions));
                }

                if (contribution.Target != participant)
                {
                    throw new ArgumentException(
                        "Todas as contribuições precisam ter como alvo o " +
                        "participante deste ScoreBreakdown.",
                        nameof(contributions));
                }

                indexedContributions.Add(
                    new IndexedContribution(
                        contribution,
                        originalIndex));

                originalIndex++;
            }

            /*
             * A ordenação torna a resolução determinística:
             *
             * 1. a fórmula define a ordem das fases;
             * 2. Priority decide efeitos dentro da mesma fase;
             * 3. SourceId oferece um desempate estável entre fontes diferentes;
             * 4. a posição original preserva a ordem quando a mesma fonte criou
             *    mais de uma contribuição equivalente.
             *
             * Determinismo significa que as mesmas entradas sempre produzem os
             * mesmos passos. Isso é essencial para testes, saves e replays.
             */
            indexedContributions.Sort(CompareIndexedContributions);

            List<ScoreContribution> orderedContributions =
                new List<ScoreContribution>(indexedContributions.Count);

            for (int index = 0;
                 index < indexedContributions.Count;
                 index++)
            {
                orderedContributions.Add(
                    indexedContributions[index].Contribution);
            }

            return ResolveOrderedContributions(
                participant,
                orderedContributions);
        }

        private static ScoreBreakdown ResolveOrderedContributions(
            ScoreActor participant,
            IList<ScoreContribution> contributions)
        {
            List<ScoreStep> steps =
                new List<ScoreStep>(contributions.Count);

            decimal currentScore = 0m;
            decimal basePoints = 0m;
            decimal flatPoints = 0m;
            decimal additiveMultiplier = 0m;
            decimal independentMultiplierProduct = 1m;
            decimal victoryMultiplierProduct = 1m;
            decimal finalFlatPoints = 0m;

            /*
             * A fase AdditiveMultiplier precisa sempre recalcular a partir dos
             * pontos existentes antes dos multiplicadores. Se multiplicássemos o
             * resultado anterior a cada contribuição, dois bônus de +0,5 virariam
             * 1,5 x 1,5 = 2,25. A regra desejada é 1 + 0,5 + 0,5 = 2.
             */
            decimal pointsBeforeMultipliers = 0m;

            for (int index = 0; index < contributions.Count; index++)
            {
                ScoreContribution contribution = contributions[index];
                decimal scoreBefore = currentScore;
                decimal effectiveFactor = 1m;

                switch (contribution.Phase)
                {
                    case ScorePhase.BasePoints:
                        basePoints += contribution.Amount;
                        currentScore += contribution.Amount;
                        break;

                    case ScorePhase.FlatPoints:
                        flatPoints += contribution.Amount;
                        currentScore += contribution.Amount;
                        break;

                    case ScorePhase.AdditiveMultiplier:
                        /*
                         * Como as fases já estão ordenadas, currentScore contém
                         * exatamente BasePoints + FlatPoints quando esta fase começa.
                         */
                        if (index == 0 ||
                            contributions[index - 1].Phase !=
                            ScorePhase.AdditiveMultiplier)
                        {
                            pointsBeforeMultipliers = currentScore;
                        }

                        additiveMultiplier += contribution.Amount;
                        effectiveFactor = Math.Max(
                            0m,
                            1m + additiveMultiplier);
                        currentScore =
                            pointsBeforeMultipliers * effectiveFactor;
                        break;

                    case ScorePhase.IndependentMultiplier:
                        independentMultiplierProduct *= contribution.Amount;
                        effectiveFactor = contribution.Amount;
                        currentScore *= effectiveFactor;
                        break;

                    case ScorePhase.VictoryMultiplier:
                        victoryMultiplierProduct *= contribution.Amount;
                        effectiveFactor = contribution.Amount;
                        currentScore *= effectiveFactor;
                        break;

                    case ScorePhase.FinalFlatPoints:
                        finalFlatPoints += contribution.Amount;
                        currentScore += contribution.Amount;
                        break;

                    default:
                        throw new InvalidOperationException(
                            "Uma contribuição alcançou a resolução com uma " +
                            "fase desconhecida.");
                }

                steps.Add(
                    new ScoreStep(
                        index + 1,
                        contribution,
                        scoreBefore,
                        currentScore,
                        effectiveFactor));
            }

            return new ScoreBreakdown(
                participant,
                contributions,
                steps,
                basePoints,
                flatPoints,
                additiveMultiplier,
                independentMultiplierProduct,
                victoryMultiplierProduct,
                finalFlatPoints,
                currentScore);
        }

        private static int CompareIndexedContributions(
            IndexedContribution left,
            IndexedContribution right)
        {
            int phaseComparison = GetPhaseOrder(
                    left.Contribution.Phase)
                .CompareTo(GetPhaseOrder(
                    right.Contribution.Phase));

            if (phaseComparison != 0)
            {
                return phaseComparison;
            }

            int priorityComparison = left.Contribution.Priority.CompareTo(
                right.Contribution.Priority);

            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            int sourceComparison = string.CompareOrdinal(
                left.Contribution.SourceId,
                right.Contribution.SourceId);

            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int GetPhaseOrder(ScorePhase phase)
        {
            /*
             * Não dependemos do número interno do enum. Assim, reorganizar a lista
             * visualmente ou inserir um novo membro no enum não altera a fórmula de
             * modo silencioso; esta função precisará ser atualizada conscientemente.
             */
            switch (phase)
            {
                case ScorePhase.BasePoints:
                    return 0;

                case ScorePhase.FlatPoints:
                    return 1;

                case ScorePhase.AdditiveMultiplier:
                    return 2;

                case ScorePhase.IndependentMultiplier:
                    return 3;

                case ScorePhase.VictoryMultiplier:
                    return 4;

                case ScorePhase.FinalFlatPoints:
                    return 5;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(phase),
                        phase,
                        "A fase de pontuação informada não existe.");
            }
        }

        private static void ValidateParticipant(ScoreActor participant)
        {
            if (participant != ScoreActor.Player &&
                participant != ScoreActor.Enemy)
            {
                throw new ArgumentException(
                    "Um ScoreBreakdown deve pertencer a Player ou Enemy.",
                    nameof(participant));
            }
        }

        /// <summary>
        /// Mantém a posição original durante a ordenação.
        ///
        /// É um detalhe interno da resolução e, por isso, não faz parte da API que
        /// os outros sistemas do jogo precisam conhecer.
        /// </summary>
        private sealed class IndexedContribution
        {
            public ScoreContribution Contribution { get; }
            public int OriginalIndex { get; }

            public IndexedContribution(
                ScoreContribution contribution,
                int originalIndex)
            {
                Contribution = contribution;
                OriginalIndex = originalIndex;
            }
        }
    }

    /*
     * ADIÇÕES FUTURAS POSSÍVEIS
     *
     * 1. ScoreActor poderá ser substituído por um CombatantId quando existirem
     *    múltiplos inimigos, aliados ou modos com mais de dois participantes.
     *
     * 2. ScoreContribution poderá receber tags imutáveis, como "Fire", "Rune",
     *    "Diagonal" ou "Boss". O motor de efeitos poderá filtrar contribuições por
     *    essas tags sem depender do texto usado na interface.
     *
     * 3. Poderá ser adicionado um SourceInstanceId separado de SourceId. SourceId
     *    identifica o tipo da runa; SourceInstanceId diferenciaria duas cópias da
     *    mesma runa equipadas pelo mesmo participante.
     *
     * 4. Um ScoreRoundingPolicy poderá substituir a regra fixa AwayFromZero caso
     *    alguma runa ou encontro altere o arredondamento. A política deverá fazer
     *    parte do contexto determinístico para que saves e replays continuem iguais.
     *
     * 5. Um limite de pontuação configurável poderá substituir int.MaxValue. Isso
     *    permitirá que a interface anuncie "pontuação máxima" como regra de jogo,
     *    em vez de depender apenas do limite técnico do tipo int.
     *
     * 6. O motor de runas poderá produzir um relatório de gatilhos que referencie
     *    ScoreStep.Order. Assim, cada ativação visual poderá apontar exatamente para
     *    o passo que ela adicionou ou modificou.
     *
     * 7. Se efeitos futuros precisarem transformar ou cancelar contribuições já
     *    existentes, faça isso no ScorePipeline antes de chamar Resolve. Um
     *    ScoreBreakdown pronto deve continuar imutável e nunca ser editado.
     *
     * 8. Para localização, DisplayText poderá ser substituído por uma chave de
     *    tradução e argumentos. SourceId deve permanecer independente dessa troca.
     */
}