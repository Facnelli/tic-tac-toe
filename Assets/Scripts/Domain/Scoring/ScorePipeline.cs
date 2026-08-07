using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Sequences;

namespace TicTacToeRoguelike.Domain.Scoring
{
    /// <summary>
    /// Reúne todas as informações necessárias para calcular o placar de um
    /// participante ao final de uma rodada.
    ///
    /// Este objeto existe para evitar métodos com muitos parâmetros soltos. Além de
    /// tornar a chamada mais legível, ele também cria uma fronteira clara para as
    /// futuras regras do encontro e para o futuro motor de efeitos.
    ///
    /// A requisição não calcula nada. Ela apenas registra as entradas que serão
    /// consumidas pelo ScorePipeline.
    /// </summary>
    public sealed class ScorePipelineRequest
    {
        private readonly ReadOnlyCollection<ScoreContribution>
            _additionalContributions;

        /// <summary>
        /// Participante cujo placar está sendo calculado.
        /// </summary>
        public ScoreActor Participant { get; }

        /// <summary>
        /// Símbolo usado por esse participante nesta rodada.
        ///
        /// Participant e Mark são mantidos separados porque, futuramente, uma
        /// regra poderá trocar os símbolos ou permitir outros participantes.
        /// Portanto, o pipeline não deve presumir que Player sempre usa X.
        /// </summary>
        public CellMark Mark { get; }

        /// <summary>
        /// Estado autoritativo do tabuleiro que será avaliado.
        /// </summary>
        public BoardState Board { get; }

        /// <summary>
        /// Menor tamanho de sequência que produz pontos.
        ///
        /// A mecânica atual usa 2. O valor permanece explícito para que um encontro
        /// especial possa, futuramente, ignorar pares sem alterar o avaliador.
        /// </summary>
        public int MinimumSequenceLength { get; }

        /// <summary>
        /// Maior tamanho de sequência que será enumerado para pontuação.
        ///
        /// O limite é importante porque uma linha longa também contém várias
        /// subsequências menores. Por exemplo, uma linha de quatro símbolos contém
        /// três pares, duas trincas e uma sequência de quatro.
        /// </summary>
        public int MaximumSequenceLength { get; }

        /// <summary>
        /// Informa se este participante venceu a rodada.
        ///
        /// O pipeline não decide o vencedor. Essa decisão já foi tomada pelas
        /// regras do encontro e chega aqui como um fato.
        /// </summary>
        public bool IsWinner { get; }

        /// <summary>
        /// Fator aplicado na fase VictoryMultiplier quando IsWinner for verdadeiro.
        ///
        /// Exemplo: 1,5m significa multiplicar o placar por 1,5. O sufixo m indica
        /// um decimal, tipo usado para manter a conta determinística.
        /// </summary>
        public decimal VictoryMultiplier { get; }

        /// <summary>
        /// Contribuições já produzidas por outras regras.
        ///
        /// Nesta fase elas poderão vir de regras do encontro. Futuramente, o motor
        /// determinístico de efeitos produzirá aqui contribuições de runas, casas,
        /// inimigos e bosses.
        /// </summary>
        public IReadOnlyList<ScoreContribution> AdditionalContributions =>
            _additionalContributions;

        public ScorePipelineRequest(
            ScoreActor participant,
            CellMark mark,
            BoardState board,
            int minimumSequenceLength,
            int maximumSequenceLength,
            bool isWinner,
            decimal victoryMultiplier,
            IEnumerable<ScoreContribution> additionalContributions = null)
        {
            ValidateParticipant(participant);
            ValidateSingleMark(mark);

            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (minimumSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumSequenceLength),
                    minimumSequenceLength,
                    "O menor tamanho de sequência deve ser pelo menos 2.");
            }

            if (maximumSequenceLength < minimumSequenceLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSequenceLength),
                    maximumSequenceLength,
                    "O maior tamanho não pode ser menor que o menor tamanho.");
            }

            /*
             * Um fator negativo inverteria o sinal do placar e poderia transformar
             * uma penalidade em bônus. O valor zero é permitido e representa um
             * bônus de vitória completamente anulado.
             *
             * Mesmo quando IsWinner é falso, validamos o valor. Isso impede que uma
             * configuração inválida permaneça escondida até uma rodada futura em
             * que o participante finalmente vença.
             */
            if (victoryMultiplier < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(victoryMultiplier),
                    victoryMultiplier,
                    "O multiplicador de vitória não pode ser negativo.");
            }

            Participant = participant;
            Mark = mark;
            Board = board;
            MinimumSequenceLength = minimumSequenceLength;
            MaximumSequenceLength = maximumSequenceLength;
            IsWinner = isWinner;
            VictoryMultiplier = victoryMultiplier;

            List<ScoreContribution> contributionCopy =
                new List<ScoreContribution>();

            if (additionalContributions != null)
            {
                foreach (ScoreContribution contribution
                         in additionalContributions)
                {
                    if (contribution == null)
                    {
                        throw new ArgumentException(
                            "As contribuições adicionais não podem conter null.",
                            nameof(additionalContributions));
                    }

                    if (contribution.Target != participant)
                    {
                        throw new ArgumentException(
                            "Toda contribuição adicional precisa ter como alvo " +
                            "o participante desta requisição.",
                            nameof(additionalContributions));
                    }

                    contributionCopy.Add(contribution);
                }
            }

            /*
             * A cópia impede que o criador da requisição altere sua lista original
             * depois do construtor. ReadOnlyCollection impede Add/Remove por quem
             * recebe a requisição. Juntas, as duas medidas tornam a entrada estável.
             */
            _additionalContributions =
                new ReadOnlyCollection<ScoreContribution>(contributionCopy);
        }

        private static void ValidateParticipant(ScoreActor participant)
        {
            if (participant != ScoreActor.Player &&
                participant != ScoreActor.Enemy)
            {
                throw new ArgumentException(
                    "O placar deve pertencer a Player ou Enemy.",
                    nameof(participant));
            }
        }

        private static void ValidateSingleMark(CellMark mark)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentException(
                    "A pontuação deve avaliar exatamente um símbolo: X ou O.",
                    nameof(mark));
            }
        }
    }

    /// <summary>
    /// Resultado imutável produzido pelo ScorePipeline.
    ///
    /// Ele conserva tanto as sequências encontradas quanto a conta resolvida. Dessa
    /// maneira, a apresentação pode iluminar as casas e reproduzir os ScoreSteps sem
    /// executar novamente o SequenceEvaluator ou qualquer fórmula de pontuação.
    /// </summary>
    public sealed class ScorePipelineResult
    {
        private readonly ReadOnlyCollection<SequenceMatch> _scoringSequences;

        public ScoreActor Participant { get; }
        public CellMark Mark { get; }
        public bool IsWinner { get; }

        /// <summary>
        /// Versão que o BoardState possuía durante esta resolução.
        ///
        /// A versão ajuda logs, replays e caches a verificar a qual estado do
        /// tabuleiro o resultado pertence.
        /// </summary>
        public long BoardVersion { get; }

        public int MinimumSequenceLength { get; }
        public int MaximumSequenceLength { get; }

        /// <summary>
        /// Sequências que realmente deram origem aos pontos básicos.
        /// </summary>
        public IReadOnlyList<SequenceMatch> ScoringSequences =>
            _scoringSequences;

        /// <summary>
        /// Relatório matemático detalhado e já resolvido.
        /// </summary>
        public ScoreBreakdown Breakdown { get; }

        internal ScorePipelineResult(
            ScoreActor participant,
            CellMark mark,
            bool isWinner,
            long boardVersion,
            int minimumSequenceLength,
            int maximumSequenceLength,
            IList<SequenceMatch> scoringSequences,
            ScoreBreakdown breakdown)
        {
            if (scoringSequences == null)
            {
                throw new ArgumentNullException(nameof(scoringSequences));
            }

            Participant = participant;
            Mark = mark;
            IsWinner = isWinner;
            BoardVersion = boardVersion;
            MinimumSequenceLength = minimumSequenceLength;
            MaximumSequenceLength = maximumSequenceLength;
            Breakdown = breakdown ??
                throw new ArgumentNullException(nameof(breakdown));

            _scoringSequences =
                new ReadOnlyCollection<SequenceMatch>(
                    new List<SequenceMatch>(scoringSequences));
        }
    }

    /// <summary>
    /// Converte fatos da rodada em um relatório autoritativo de pontuação.
    ///
    /// O fluxo executado é:
    ///
    /// 1. encontrar as sequências do participante no BoardState;
    /// 2. criar uma contribuição BasePoints para cada sequência;
    /// 3. acrescentar contribuições externas de regras e efeitos;
    /// 4. acrescentar o multiplicador de vitória quando aplicável;
    /// 5. entregar tudo ao ScoreBreakdown.Resolve;
    /// 6. devolver sequências e cálculo juntos em ScorePipelineResult.
    ///
    /// A regra básica preservada do protótipo é: cada sequência vale seu tamanho.
    /// Portanto, uma sequência de 2 casas vale 2 pontos e uma de 4 vale 4.
    ///
    /// Esta classe é domínio puro: não usa MonoBehaviour, GameObject, animação,
    /// texto de UI nem qualquer outro tipo da Unity.
    /// </summary>
    public sealed class ScorePipeline
    {
        private const string VictoryMultiplierSourceId =
            "rule:standard-victory-multiplier";

        private readonly SequenceEvaluator _sequenceEvaluator;

        /// <summary>
        /// Cria o pipeline com seu avaliador padrão.
        /// </summary>
        public ScorePipeline()
            : this(new SequenceEvaluator())
        {
        }

        /// <summary>
        /// Permite fornecer um SequenceEvaluator explicitamente.
        ///
        /// Essa sobrecarga é útil para deixar a dependência visível em um ponto de
        /// composição. O avaliador atual não possui estado mutável, então também
        /// poderá ser compartilhado por outros serviços do encontro.
        /// </summary>
        public ScorePipeline(SequenceEvaluator sequenceEvaluator)
        {
            _sequenceEvaluator = sequenceEvaluator ??
                throw new ArgumentNullException(nameof(sequenceEvaluator));
        }

        /// <summary>
        /// Executa todo o cálculo para um participante.
        ///
        /// O método apenas lê o BoardState. Ele não adiciona símbolos, não encerra
        /// a rodada, não causa dano e não inicia animações.
        /// </summary>
        public ScorePipelineResult Resolve(ScorePipelineRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            long boardVersionBeforeEvaluation = request.Board.Version;

            IReadOnlyList<SequenceMatch> evaluatedSequences =
                _sequenceEvaluator.EvaluateSequences(
                    request.Board,
                    request.Mark,
                    request.MinimumSequenceLength,
                    request.MaximumSequenceLength);

            long boardVersionAfterEvaluation = request.Board.Version;

            /*
             * Normalmente todo o código de gameplay da Unity roda na thread
             * principal, então o tabuleiro não muda no meio desta chamada. Mesmo
             * assim, a verificação protege uma integração futura com tarefas ou
             * simulações paralelas: misturar sequências de duas versões produziria
             * um relatório que nunca correspondeu a um estado real.
             */
            if (boardVersionBeforeEvaluation != boardVersionAfterEvaluation)
            {
                throw new InvalidOperationException(
                    "O BoardState foi alterado durante a avaliação de pontuação. " +
                    "Tente resolver o placar novamente usando uma versão estável.");
            }

            List<SequenceMatch> scoringSequences =
                CopyAndValidateSequences(
                    evaluatedSequences,
                    request.Mark);

            List<ScoreContribution> contributions =
                new List<ScoreContribution>(
                    scoringSequences.Count +
                    request.AdditionalContributions.Count +
                    (request.IsWinner ? 1 : 0));

            for (int index = 0;
                 index < scoringSequences.Count;
                 index++)
            {
                SequenceMatch sequence = scoringSequences[index];

                contributions.Add(
                    CreateSequenceContribution(
                        request.Participant,
                        sequence));
            }

            /*
             * As contribuições externas não são executadas aqui. Elas são apenas
             * reunidas com as contribuições de sequência. ScoreBreakdown.Resolve
             * será o único lugar que ordenará fases, prioridades e realizará a
             * matemática, evitando duas implementações diferentes da fórmula.
             */
            for (int index = 0;
                 index < request.AdditionalContributions.Count;
                 index++)
            {
                contributions.Add(
                    request.AdditionalContributions[index]);
            }

            if (request.IsWinner)
            {
                contributions.Add(
                    ScoreContribution.CreateVictoryMultiplier(
                        VictoryMultiplierSourceId,
                        "Multiplicador de vitória",
                        ScoreActor.Environment,
                        request.Participant,
                        request.VictoryMultiplier));
            }

            ScoreBreakdown breakdown =
                ScoreBreakdown.Resolve(
                    request.Participant,
                    contributions);

            return new ScorePipelineResult(
                request.Participant,
                request.Mark,
                request.IsWinner,
                boardVersionBeforeEvaluation,
                request.MinimumSequenceLength,
                request.MaximumSequenceLength,
                scoringSequences,
                breakdown);
        }

        private static List<SequenceMatch> CopyAndValidateSequences(
            IEnumerable<SequenceMatch> sequences,
            CellMark expectedMark)
        {
            if (sequences == null)
            {
                throw new ArgumentNullException(nameof(sequences));
            }

            List<SequenceMatch> copy = new List<SequenceMatch>();

            foreach (SequenceMatch sequence in sequences)
            {
                if (sequence == null)
                {
                    throw new InvalidOperationException(
                        "O SequenceEvaluator retornou uma sequência nula.");
                }

                if (sequence.Mark != expectedMark)
                {
                    throw new InvalidOperationException(
                        "O SequenceEvaluator retornou uma sequência de outro " +
                        "símbolo durante o cálculo de pontuação.");
                }

                copy.Add(sequence);
            }

            return copy;
        }

        private static ScoreContribution CreateSequenceContribution(
            ScoreActor participant,
            SequenceMatch sequence)
        {
            /*
             * O SourceId inclui símbolo, direção e todas as coordenadas. Assim,
             * duas janelas sobrepostas continuam sendo fontes diferentes, enquanto
             * a mesma sequência sempre produz a mesma identidade lógica.
             *
             * Não usamos GetHashCode porque códigos hash não servem como identidade
             * persistente: a implementação deles poderá mudar entre plataformas ou
             * versões do runtime.
             */
            string sourceId = BuildSequenceSourceId(sequence);
            string displayText = BuildSequenceDisplayText(sequence);

            return ScoreContribution.CreateBasePoints(
                sourceId,
                displayText,
                participant,
                participant,
                sequence.Length,
                priority: sequence.Length);
        }

        private static string BuildSequenceSourceId(
            SequenceMatch sequence)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("sequence:");
            builder.Append(GetMarkToken(sequence.Mark));
            builder.Append(':');
            builder.Append(GetDirectionToken(sequence.Direction));
            builder.Append(":length:");
            builder.Append(
                sequence.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(":cells:");

            for (int index = 0; index < sequence.Cells.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('|');
                }

                BoardCoordinate coordinate = sequence.Cells[index];

                builder.Append(
                    coordinate.X.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(
                    coordinate.Y.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string BuildSequenceDisplayText(
            SequenceMatch sequence)
        {
            string directionText = GetDirectionDisplayText(
                sequence.Direction);

            string cellWord = sequence.Length == 1
                ? "casa"
                : "casas";

            return string.Format(
                CultureInfo.InvariantCulture,
                "Sequência {0} de {1} {2}",
                directionText,
                sequence.Length,
                cellWord);
        }

        private static string GetMarkToken(CellMark mark)
        {
            switch (mark)
            {
                case CellMark.X:
                    return "x";

                case CellMark.O:
                    return "o";

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mark),
                        mark,
                        "O símbolo não possui um token de pontuação.");
            }
        }

        private static string GetDirectionToken(
            SequenceDirection direction)
        {
            switch (direction)
            {
                case SequenceDirection.Horizontal:
                    return "horizontal";

                case SequenceDirection.Vertical:
                    return "vertical";

                case SequenceDirection.MainDiagonal:
                    return "main-diagonal";

                case SequenceDirection.SecondaryDiagonal:
                    return "secondary-diagonal";

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(direction),
                        direction,
                        "A direção não possui um token de pontuação.");
            }
        }

        private static string GetDirectionDisplayText(
            SequenceDirection direction)
        {
            switch (direction)
            {
                case SequenceDirection.Horizontal:
                    return "horizontal";

                case SequenceDirection.Vertical:
                    return "vertical";

                case SequenceDirection.MainDiagonal:
                    return "diagonal principal";

                case SequenceDirection.SecondaryDiagonal:
                    return "diagonal secundária";

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(direction),
                        direction,
                        "A direção não possui um texto de apresentação.");
            }
        }

        /*
         * Adições futuras:
         *
         * 1. O valor básico de uma sequência poderá ser extraído para uma política
         *    como ISequenceScoreRule. Isso permitirá encontros em que diagonais,
         *    comprimentos específicos ou formatos especiais tenham valores próprios,
         *    sem colocar condicionais de boss dentro deste pipeline.
         *
         * 2. O motor determinístico de efeitos poderá receber um ScoreEffectContext
         *    contendo participante, tabuleiro, sequências e estado dos combatentes.
         *    Ele produzirá ScoreContribution; o ScorePipeline continuará sendo o
         *    ponto único que reúne e resolve essas contribuições.
         *
         * 3. Poderá ser criado um ScorePipelinePairResult para calcular jogador e
         *    inimigo a partir da mesma versão do tabuleiro. Esse relatório conjunto
         *    será uma entrada natural para o futuro ClashResolver.
         *
         * 4. Se tabuleiros grandes tornarem a enumeração de subsequências custosa,
         *    poderá existir um cache identificado por BoardState.Version, símbolo e
         *    intervalo de tamanhos. O cache não deve guardar resultados de uma versão
         *    anterior do tabuleiro.
         *
         * 5. DisplayText poderá ser substituído por uma chave de localização e seus
         *    argumentos. O domínio continuará gerando identidade e dados, enquanto a
         *    apresentação escolherá o idioma correto.
         *
         * 6. Efeitos que alteram outras contribuições, por exemplo "sequências de 3
         *    valem o dobro", deverão ser representados pelo futuro motor de efeitos.
         *    Evite modificar ScoreBreakdown.Steps depois da resolução, pois eles são
         *    o histórico autoritativo usado por animações, saves e replays.
         *
         * 7. O futuro EncounterController poderá criar duas requisições ao encerrar
         *    a rodada, uma para cada participante, e entregar ambos os resultados ao
         *    ClashResolver. O MatchAnimator deverá somente reproduzir os relatórios.
         */
    }
}