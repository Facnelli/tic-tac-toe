using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Sequences;

namespace TicTacToeRoguelike.Application.AI
{
    /// <summary>
    /// Escolhe uma jogada normal para um participante do jogo da velha.
    ///
    /// Esta classe pertence à camada Application porque coordena vários serviços
    /// do domínio para tomar uma decisão, mas não representa uma regra do tabuleiro.
    /// Ela não depende de MonoBehaviour, GameObject, Vector2Int ou UnityEngine.
    /// Assim, a mesma IA pode ser utilizada numa cena, em testes automatizados
    /// ou em simulações executadas fora da Unity.
    ///
    /// A política utiliza busca adversarial com aprofundamento iterativo:
    ///
    /// 1. Começa procurando poucas jogadas à frente;
    /// 2. Guarda a melhor decisão encontrada numa profundidade completa;
    /// 3. Aumenta gradualmente a profundidade enquanto houver orçamento;
    /// 4. Usa poda alpha-beta para ignorar ramos que não podem mudar a decisão;
    /// 5. Usa uma tabela de transposição para reutilizar posições já analisadas.
    ///
    /// Em tabuleiros pequenos, a busca consegue percorrer a árvore inteira e a
    /// decisão é comprovadamente ótima para as regras consideradas aqui. Em
    /// tabuleiros maiores, o orçamento limita o custo e uma avaliação heurística
    /// estima a qualidade das posições que ainda não puderam ser resolvidas.
    /// </summary>
    public sealed class BasicTicTacToePolicy
    {
        /*
         * Pontuações terminais precisam ser muito maiores do que qualquer valor
         * heurístico. Isso garante que uma vitória real seja sempre preferida a
         * uma posição apenas promissora.
         */
        private const int VictoryScore = 1_000_000_000;
        private const int VictoryPlyPenalty = 1_000;
        private const int MaximumHeuristicMagnitude = 250_000_000;
        private const int MaximumWindowValue = 10_000_000;

        private readonly MoveValidator _moveValidator;
        private readonly MoveService _moveService;
        private readonly SequenceEvaluator _sequenceEvaluator;

        /// <summary>
        /// Maior quantidade de jogadas futuras que a política tentará investigar.
        ///
        /// Aprofundar mais melhora decisão em tabuleiros grandes, mas aumenta o
        /// custo exponencialmente. O valor padrão resolve o tabuleiro tradicional
        /// com folga e ainda mantém um limite seguro para formatos maiores.
        /// </summary>
        public int MaximumSearchDepth { get; }

        /// <summary>
        /// Quantidade máxima de nós visitados numa decisão inteira.
        ///
        /// Um nó representa uma posição examinada na árvore de possibilidades.
        /// Diferentemente de um limite baseado em milissegundos, este orçamento é
        /// determinístico: o mesmo estado e a mesma configuração produzem a mesma
        /// decisão em computadores de velocidades diferentes.
        /// </summary>
        public int MaximumVisitedNodes { get; }

        /// <summary>
        /// Profundidade da última iteração concluída integralmente.
        ///
        /// Esta informação é útil para testes, ferramentas de debug e ajuste da
        /// dificuldade. Ela não precisa ser exibida ao jogador.
        /// </summary>
        public int LastCompletedDepth { get; private set; }

        /// <summary>
        /// Quantidade de nós examinados na decisão mais recente.
        /// </summary>
        public int LastVisitedNodeCount { get; private set; }

        /// <summary>
        /// Informa se a decisão mais recente pôde ser matematicamente comprovada.
        ///
        /// Isso acontece quando encontramos uma vitória imediata, ou quando a
        /// busca alcança todas as jogadas que ainda poderiam caber no tabuleiro.
        /// </summary>
        public bool LastDecisionWasProvenOptimal { get; private set; }

        public BasicTicTacToePolicy(
            int maximumSearchDepth = 10,
            int maximumVisitedNodes = 100_000)
        {
            if (maximumSearchDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSearchDepth),
                    "A profundidade máxima da busca deve ser maior que zero.");
            }

            if (maximumVisitedNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumVisitedNodes),
                    "O orçamento de nós deve ser maior que zero.");
            }

            MaximumSearchDepth = maximumSearchDepth;
            MaximumVisitedNodes = maximumVisitedNodes;

            /*
             * MoveService e MoveValidator compartilham a mesma instância do
             * validador. Assim, a lista de opções e a aplicação simulada usam
             * exatamente as mesmas regras.
             */
            _moveValidator = new MoveValidator();
            _moveService = new MoveService(_moveValidator);
            _sequenceEvaluator = new SequenceEvaluator();
        }

        /// <summary>
        /// Tenta escolher a melhor jogada normal para controlledMark.
        ///
        /// Retorna true e preenche move quando existe ao menos uma ação legal.
        /// Retorna false quando o participante não pode realizar nenhuma jogada.
        /// A ausência de jogadas é uma situação normal, por isso não lançamos uma
        /// exceção nesse caso.
        ///
        /// O BoardState recebido nunca é alterado. Toda possibilidade é aplicada
        /// somente a um clone independente.
        /// </summary>
        public bool TryChooseMove(
            BoardState board,
            CellMark controlledMark,
            CellMark opponentMark,
            int requiredSequenceLength,
            out BoardCoordinate move)
        {
            ValidateDecisionArguments(
                board,
                controlledMark,
                opponentMark,
                requiredSequenceLength);

            LastCompletedDepth = 0;
            LastVisitedNodeCount = 0;
            LastDecisionWasProvenOptimal = false;

            IReadOnlyList<BoardCoordinate> legalMoves =
                _moveValidator.GetLegalNormalMoves(
                    board,
                    controlledMark);

            if (legalMoves.Count == 0)
            {
                move = default(BoardCoordinate);
                return false;
            }

            /*
             * Esta lista já contém clones com cada jogada aplicada. Além de
             * fornecer uma escolha segura caso o orçamento seja muito pequeno,
             * ela ordena primeiro os candidatos mais promissores. Uma boa ordem
             * aumenta bastante a quantidade de ramos descartados pela poda.
             */
            List<MoveCandidate> rootCandidates =
                CreateOrderedCandidates(
                    board,
                    legalMoves,
                    controlledMark,
                    controlledMark,
                    opponentMark,
                    requiredSequenceLength);

            move = rootCandidates[0].Coordinate;

            /*
             * Uma vitória imediata possui a maior pontuação possível. Nenhuma
             * resposta futura pode tornar outra jogada melhor do que vencer agora.
             */
            if (rootCandidates[0].CreatesImmediateWin)
            {
                LastCompletedDepth = 1;
                LastDecisionWasProvenOptimal = true;
                return true;
            }

            int remainingPlyUpperBound =
                CountRemainingPlacementCapacity(board);

            int deepestRequestedIteration =
                Math.Min(
                    MaximumSearchDepth,
                    remainingPlyUpperBound);

            SearchContext context = new SearchContext(
                controlledMark,
                opponentMark,
                requiredSequenceLength,
                MaximumVisitedNodes);

            /*
             * Aprofundamento iterativo é importante quando existe orçamento.
             * Se uma iteração profunda for interrompida, ainda possuímos a melhor
             * jogada da última profundidade finalizada por completo.
             */
            for (int depth = 1;
                 depth <= deepestRequestedIteration;
                 depth++)
            {
                RootSearchResult iteration = SearchRoot(
                    rootCandidates,
                    depth,
                    context);

                if (!iteration.Completed)
                {
                    break;
                }

                move = iteration.BestMove;
                LastCompletedDepth = depth;

                /*
                 * Colocar a melhor jogada anterior no começo melhora a ordenação
                 * da próxima iteração, sem introduzir aleatoriedade.
                 */
                MoveCandidate principalCandidate =
                    FindCandidate(
                        rootCandidates,
                        move);

                rootCandidates.Remove(principalCandidate);
                rootCandidates.Insert(0, principalCandidate);

                if (iteration.BestScore >=
                    VictoryScore - VictoryPlyPenalty)
                {
                    LastDecisionWasProvenOptimal = true;
                    break;
                }

                if (depth >= remainingPlyUpperBound)
                {
                    LastDecisionWasProvenOptimal = true;
                    break;
                }
            }

            LastVisitedNodeCount = context.VisitedNodes;
            return true;
        }

        /// <summary>
        /// Executa uma iteração completa da raiz da árvore.
        ///
        /// A raiz sempre maximiza a vantagem da marca controlada. Depois de cada
        /// candidato, o oponente passa a minimizar essa mesma vantagem.
        /// </summary>
        private RootSearchResult SearchRoot(
            IList<MoveCandidate> candidates,
            int depth,
            SearchContext context)
        {
            int alpha = -VictoryScore;
            int beta = VictoryScore;
            int bestScore = int.MinValue;
            BoardCoordinate bestMove = candidates[0].Coordinate;

            for (int index = 0;
                 index < candidates.Count;
                 index++)
            {
                if (!context.TryVisitNode())
                {
                    return RootSearchResult.Incomplete();
                }

                MoveCandidate candidate = candidates[index];
                int score;

                if (candidate.CreatesImmediateWin)
                {
                    score = VictoryScore - VictoryPlyPenalty;
                }
                else
                {
                    NodeSearchResult child = AlphaBeta(
                        candidate.BoardAfterMove,
                        context.OpponentMark,
                        depth - 1,
                        1,
                        alpha,
                        beta,
                        context);

                    if (!child.Completed)
                    {
                        return RootSearchResult.Incomplete();
                    }

                    score = child.Score;
                }

                /*
                 * Usamos apenas >, e não >=. Em caso de empate, permanece o
                 * candidato encontrado primeiro na ordem determinística.
                 */
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = candidate.Coordinate;
                }

                alpha = Math.Max(alpha, bestScore);
            }

            return RootSearchResult.Success(
                bestMove,
                bestScore);
        }

        /// <summary>
        /// Busca adversarial com poda alpha-beta.
        ///
        /// Alpha representa a melhor garantia já encontrada para a IA.
        /// Beta representa a melhor garantia já encontrada para o oponente.
        /// Quando alpha alcança beta, continuar naquele ramo não pode mudar a
        /// decisão de um participante racional e o restante pode ser descartado.
        /// </summary>
        private NodeSearchResult AlphaBeta(
            BoardState board,
            CellMark currentMark,
            int remainingDepth,
            int plyFromRoot,
            int alpha,
            int beta,
            SearchContext context)
        {
            if (!context.TryVisitNode())
            {
                return NodeSearchResult.Incomplete();
            }

            if (remainingDepth <= 0)
            {
                return NodeSearchResult.Success(
                    EvaluatePosition(
                        board,
                        context.ControlledMark,
                        context.OpponentMark,
                        context.RequiredSequenceLength));
            }

            int originalAlpha = alpha;
            int originalBeta = beta;
            string positionKey = CreatePositionKey(
                board,
                currentMark);

            if (context.TranspositionTable.TryGetValue(
                positionKey,
                out TranspositionEntry cached) &&
                cached.Depth >= remainingDepth)
            {
                if (cached.Bound == TranspositionBound.Exact)
                {
                    return NodeSearchResult.Success(cached.Score);
                }

                if (cached.Bound == TranspositionBound.Lower)
                {
                    alpha = Math.Max(alpha, cached.Score);
                }
                else
                {
                    beta = Math.Min(beta, cached.Score);
                }

                if (alpha >= beta)
                {
                    return NodeSearchResult.Success(cached.Score);
                }
            }

            IReadOnlyList<BoardCoordinate> legalMoves =
                _moveValidator.GetLegalNormalMoves(
                    board,
                    currentMark);

            if (legalMoves.Count == 0)
            {
                int terminalScore = EvaluatePosition(
                    board,
                    context.ControlledMark,
                    context.OpponentMark,
                    context.RequiredSequenceLength);

                StoreTransposition(
                    context,
                    positionKey,
                    remainingDepth,
                    terminalScore,
                    TranspositionBound.Exact);

                return NodeSearchResult.Success(terminalScore);
            }

            bool maximizing =
                currentMark == context.ControlledMark;

            List<MoveCandidate> candidates =
                CreateOrderedCandidates(
                    board,
                    legalMoves,
                    currentMark,
                    context.ControlledMark,
                    context.OpponentMark,
                    context.RequiredSequenceLength);

            int bestScore = maximizing
                ? int.MinValue
                : int.MaxValue;

            for (int index = 0;
                 index < candidates.Count;
                 index++)
            {
                MoveCandidate candidate = candidates[index];
                int score;

                if (candidate.CreatesImmediateWin)
                {
                    score = ScoreTerminalVictory(
                        currentMark,
                        context.ControlledMark,
                        plyFromRoot + 1);
                }
                else
                {
                    CellMark nextMark = maximizing
                        ? context.OpponentMark
                        : context.ControlledMark;

                    NodeSearchResult child = AlphaBeta(
                        candidate.BoardAfterMove,
                        nextMark,
                        remainingDepth - 1,
                        plyFromRoot + 1,
                        alpha,
                        beta,
                        context);

                    if (!child.Completed)
                    {
                        return NodeSearchResult.Incomplete();
                    }

                    score = child.Score;
                }

                if (maximizing)
                {
                    bestScore = Math.Max(bestScore, score);
                    alpha = Math.Max(alpha, bestScore);
                }
                else
                {
                    bestScore = Math.Min(bestScore, score);
                    beta = Math.Min(beta, bestScore);
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            TranspositionBound bound;

            if (bestScore <= originalAlpha)
            {
                bound = TranspositionBound.Upper;
            }
            else if (bestScore >= originalBeta)
            {
                bound = TranspositionBound.Lower;
            }
            else
            {
                bound = TranspositionBound.Exact;
            }

            StoreTransposition(
                context,
                positionKey,
                remainingDepth,
                bestScore,
                bound);

            return NodeSearchResult.Success(bestScore);
        }

        /// <summary>
        /// Cria e ordena as posições resultantes das jogadas legais.
        ///
        /// A ordenação não define o resultado final; ela apenas ajuda a busca a
        /// encontrar bons limites cedo. O desempate por Y e depois X preserva a
        /// repetibilidade entre execuções.
        /// </summary>
        private List<MoveCandidate> CreateOrderedCandidates(
            BoardState board,
            IReadOnlyList<BoardCoordinate> legalMoves,
            CellMark markToPlay,
            CellMark controlledMark,
            CellMark opponentMark,
            int requiredSequenceLength)
        {
            List<MoveCandidate> candidates =
                new List<MoveCandidate>(legalMoves.Count);

            for (int index = 0;
                 index < legalMoves.Count;
                 index++)
            {
                BoardCoordinate coordinate = legalMoves[index];
                BoardState simulation = board.Clone();

                MoveValidationResult application =
                    _moveService.TryApplyNormalMove(
                        simulation,
                        coordinate,
                        markToPlay);

                if (!application.IsValid)
                {
                    /*
                     * A coordenada veio do mesmo MoveValidator. Se a aplicação
                     * falhar, existe uma inconsistência de programação entre a
                     * enumeração de ações e sua execução.
                     */
                    throw new InvalidOperationException(
                        "Uma jogada enumerada como legal não pôde ser aplicada " +
                        "à cópia usada pela IA.");
                }

                bool createsImmediateWin =
                    _sequenceEvaluator.HasWinningSequence(
                        simulation,
                        markToPlay,
                        requiredSequenceLength);

                int orderingScore = createsImmediateWin
                    ? ScoreTerminalVictory(
                        markToPlay,
                        controlledMark,
                        1)
                    : EvaluatePosition(
                        simulation,
                        controlledMark,
                        opponentMark,
                        requiredSequenceLength);

                candidates.Add(
                    new MoveCandidate(
                        coordinate,
                        simulation,
                        createsImmediateWin,
                        orderingScore));
            }

            bool maximizing = markToPlay == controlledMark;

            candidates.Sort(
                (left, right) => CompareCandidates(
                    left,
                    right,
                    maximizing));

            return candidates;
        }

        /// <summary>
        /// Estima a vantagem da marca controlada numa posição ainda não resolvida.
        ///
        /// A avaliação examina todas as janelas consecutivas que possuem o tamanho
        /// da condição de vitória. Quanto mais símbolos já pertencem a um ator,
        /// maior o valor daquela janela. O crescimento é exponencial porque passar
        /// de dois para três símbolos próximos da vitória costuma ser muito mais
        /// importante do que passar de zero para um.
        ///
        /// Uma casa ocupada pelo adversário normalmente torna a janela impossível.
        /// A casa Golden é tratada corretamente: se ainda não contém a marca, ela
        /// permite a sobreposição e continua sendo alcançável por ambos.
        /// </summary>
        private static int EvaluatePosition(
            BoardState board,
            CellMark controlledMark,
            CellMark opponentMark,
            int requiredSequenceLength)
        {
            long score = 0;

            foreach (BoardCoordinate start
                     in board.Definition.Cells)
            {
                foreach (BoardDirection direction
                         in board.Definition.SequenceDirections)
                {
                    if (!TryEvaluateWindow(
                        board,
                        start,
                        direction,
                        controlledMark,
                        opponentMark,
                        requiredSequenceLength,
                        out int controlledCount,
                        out bool controlledCanComplete,
                        out int opponentCount,
                        out bool opponentCanComplete))
                    {
                        continue;
                    }

                    if (controlledCanComplete)
                    {
                        score += GetWindowValue(controlledCount);
                    }

                    if (opponentCanComplete)
                    {
                        score -= GetWindowValue(opponentCount);
                    }

                    if (score >= MaximumHeuristicMagnitude)
                    {
                        return MaximumHeuristicMagnitude;
                    }

                    if (score <= -MaximumHeuristicMagnitude)
                    {
                        return -MaximumHeuristicMagnitude;
                    }
                }
            }

            return (int)score;
        }

        /// <summary>
        /// Avalia uma janela linear sem criar listas temporárias.
        ///
        /// Retornar false significa que a janela atravessaria uma casa inexistente,
        /// situação comum em bordas e em tabuleiros especiais com buracos.
        /// </summary>
        private static bool TryEvaluateWindow(
            BoardState board,
            BoardCoordinate start,
            BoardDirection direction,
            CellMark controlledMark,
            CellMark opponentMark,
            int requiredSequenceLength,
            out int controlledCount,
            out bool controlledCanComplete,
            out int opponentCount,
            out bool opponentCanComplete)
        {
            controlledCount = 0;
            opponentCount = 0;
            controlledCanComplete = true;
            opponentCanComplete = true;

            BoardCoordinate current = start;

            for (int offset = 0;
                 offset < requiredSequenceLength;
                 offset++)
            {
                if (!board.TryGetCell(
                    current,
                    out CellState cell))
                {
                    return false;
                }

                bool hasControlled =
                    cell.HasMark(controlledMark);

                bool hasOpponent =
                    cell.HasMark(opponentMark);

                if (hasControlled)
                {
                    controlledCount++;
                }

                if (hasOpponent)
                {
                    opponentCount++;
                }

                bool allowsOverlap =
                    cell.HasModifier(CellModifierId.Golden);

                /*
                 * Se a casa possui apenas a marca adversária e não aceita
                 * sobreposição, a linha não pode mais ser completada por este ator.
                 * Uma casa contendo as duas marcas já conta para os dois lados.
                 */
                if (hasOpponent &&
                    !hasControlled &&
                    !allowsOverlap)
                {
                    controlledCanComplete = false;
                }

                if (hasControlled &&
                    !hasOpponent &&
                    !allowsOverlap)
                {
                    opponentCanComplete = false;
                }

                if (offset < requiredSequenceLength - 1 &&
                    !board.Definition.TryGetNextCell(
                        current,
                        direction,
                        out current))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetWindowValue(int markCount)
        {
            int value = 1;

            for (int index = 0;
                 index < markCount;
                 index++)
            {
                if (value >= MaximumWindowValue / 8)
                {
                    return MaximumWindowValue;
                }

                value *= 8;
            }

            return value;
        }

        private int CountRemainingPlacementCapacity(BoardState board)
        {
            int capacity = 0;

            foreach (BoardCoordinate coordinate
                     in board.Definition.Cells)
            {
                CellState cell = board.GetCell(coordinate);

                if (cell.IsEmpty)
                {
                    capacity += cell.HasModifier(CellModifierId.Golden)
                        ? 2
                        : 1;
                }
                else if (cell.HasModifier(CellModifierId.Golden) &&
                         !cell.ContainsBothMarks)
                {
                    capacity++;
                }
            }

            return Math.Max(1, capacity);
        }

        private static int ScoreTerminalVictory(
            CellMark winningMark,
            CellMark controlledMark,
            int plyFromRoot)
        {
            int timingAdjustment =
                Math.Min(
                    VictoryScore / 4,
                    plyFromRoot * VictoryPlyPenalty);

            return winningMark == controlledMark
                ? VictoryScore - timingAdjustment
                : -VictoryScore + timingAdjustment;
        }

        private static int CompareCandidates(
            MoveCandidate left,
            MoveCandidate right,
            bool maximizing)
        {
            int scoreComparison = maximizing
                ? right.OrderingScore.CompareTo(left.OrderingScore)
                : left.OrderingScore.CompareTo(right.OrderingScore);

            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            int rowComparison =
                left.Coordinate.Y.CompareTo(
                    right.Coordinate.Y);

            return rowComparison != 0
                ? rowComparison
                : left.Coordinate.X.CompareTo(
                    right.Coordinate.X);
        }

        private static string CreatePositionKey(
            BoardState board,
            CellMark currentMark)
        {
            /*
             * Uma string é usada deliberadamente como chave exata. Um hash de 64
             * bits seria menor, mas uma colisão extremamente rara poderia fazer a
             * IA reutilizar a avaliação de outro tabuleiro e escolher incorretamente.
             * Para os tabuleiros atuais, a segurança vale a pequena alocação.
             */
            char[] key =
                new char[board.Definition.CellCount + 1];

            key[0] = currentMark == CellMark.X
                ? 'X'
                : 'O';

            for (int index = 0;
                 index < board.Definition.Cells.Count;
                 index++)
            {
                CellState cell = board.GetCell(
                    board.Definition.Cells[index]);

                key[index + 1] =
                    (char)('0' + (int)cell.Marks);
            }

            return new string(key);
        }

        private static void StoreTransposition(
            SearchContext context,
            string positionKey,
            int depth,
            int score,
            TranspositionBound bound)
        {
            if (context.TranspositionTable.TryGetValue(
                positionKey,
                out TranspositionEntry existing) &&
                existing.Depth > depth)
            {
                return;
            }

            context.TranspositionTable[positionKey] =
                new TranspositionEntry(
                    depth,
                    score,
                    bound);
        }

        private static MoveCandidate FindCandidate(
            IList<MoveCandidate> candidates,
            BoardCoordinate coordinate)
        {
            for (int index = 0;
                 index < candidates.Count;
                 index++)
            {
                if (candidates[index].Coordinate == coordinate)
                {
                    return candidates[index];
                }
            }

            throw new InvalidOperationException(
                "A melhor jogada não foi encontrada entre os candidatos da raiz.");
        }

        private static void ValidateDecisionArguments(
            BoardState board,
            CellMark controlledMark,
            CellMark opponentMark,
            int requiredSequenceLength)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            ValidateSingleMark(controlledMark, nameof(controlledMark));
            ValidateSingleMark(opponentMark, nameof(opponentMark));

            if (controlledMark == opponentMark)
            {
                throw new ArgumentException(
                    "A marca controlada e a marca adversária devem ser diferentes.",
                    nameof(opponentMark));
            }

            if (requiredSequenceLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredSequenceLength),
                    requiredSequenceLength,
                    "A condição de sequência deve possuir tamanho mínimo 2.");
            }
        }

        private static void ValidateSingleMark(
            CellMark mark,
            string parameterName)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentException(
                    "Informe exatamente uma marca: X ou O.",
                    parameterName);
            }
        }

        /*
         * Tipos auxiliares privados mantêm os detalhes do algoritmo escondidos.
         * O futuro EncounterController precisa conhecer apenas TryChooseMove.
         */
        private sealed class SearchContext
        {
            public CellMark ControlledMark { get; }
            public CellMark OpponentMark { get; }
            public int RequiredSequenceLength { get; }
            public int MaximumNodes { get; }
            public int VisitedNodes { get; private set; }

            public Dictionary<string, TranspositionEntry>
                TranspositionTable { get; }

            public SearchContext(
                CellMark controlledMark,
                CellMark opponentMark,
                int requiredSequenceLength,
                int maximumNodes)
            {
                ControlledMark = controlledMark;
                OpponentMark = opponentMark;
                RequiredSequenceLength = requiredSequenceLength;
                MaximumNodes = maximumNodes;
                VisitedNodes = 0;

                TranspositionTable =
                    new Dictionary<string, TranspositionEntry>();
            }

            public bool TryVisitNode()
            {
                if (VisitedNodes >= MaximumNodes)
                {
                    return false;
                }

                VisitedNodes++;
                return true;
            }
        }

        private sealed class MoveCandidate
        {
            public BoardCoordinate Coordinate { get; }
            public BoardState BoardAfterMove { get; }
            public bool CreatesImmediateWin { get; }
            public int OrderingScore { get; }

            public MoveCandidate(
                BoardCoordinate coordinate,
                BoardState boardAfterMove,
                bool createsImmediateWin,
                int orderingScore)
            {
                Coordinate = coordinate;
                BoardAfterMove = boardAfterMove;
                CreatesImmediateWin = createsImmediateWin;
                OrderingScore = orderingScore;
            }
        }

        private readonly struct RootSearchResult
        {
            public bool Completed { get; }
            public BoardCoordinate BestMove { get; }
            public int BestScore { get; }

            private RootSearchResult(
                bool completed,
                BoardCoordinate bestMove,
                int bestScore)
            {
                Completed = completed;
                BestMove = bestMove;
                BestScore = bestScore;
            }

            public static RootSearchResult Success(
                BoardCoordinate bestMove,
                int bestScore)
            {
                return new RootSearchResult(
                    true,
                    bestMove,
                    bestScore);
            }

            public static RootSearchResult Incomplete()
            {
                return new RootSearchResult(
                    false,
                    default(BoardCoordinate),
                    0);
            }
        }

        private readonly struct NodeSearchResult
        {
            public bool Completed { get; }
            public int Score { get; }

            private NodeSearchResult(
                bool completed,
                int score)
            {
                Completed = completed;
                Score = score;
            }

            public static NodeSearchResult Success(int score)
            {
                return new NodeSearchResult(true, score);
            }

            public static NodeSearchResult Incomplete()
            {
                return new NodeSearchResult(false, 0);
            }
        }

        private readonly struct TranspositionEntry
        {
            public int Depth { get; }
            public int Score { get; }
            public TranspositionBound Bound { get; }

            public TranspositionEntry(
                int depth,
                int score,
                TranspositionBound bound)
            {
                Depth = depth;
                Score = score;
                Bound = bound;
            }
        }

        private enum TranspositionBound
        {
            Exact,
            Lower,
            Upper
        }

        /*
         * Adições futuras:
         *
         * 1. Poderá ser criada uma interface IAgentPolicy para permitir que
         *    inimigos comuns e bosses forneçam políticas diferentes sem alterar o
         *    EncounterController.
         *
         * 2. A avaliação poderá receber um DecisionContext somente para leitura,
         *    contendo vida, placar, fase de reação, runas, efeitos ativos e regras
         *    específicas do encontro. Isso permitirá otimizar a decisão completa
         *    do roguelike, não apenas a formação de sequências no tabuleiro.
         *
         * 3. Para tabuleiros enormes, poderá ser adicionada uma estratégia como
         *    Monte Carlo Tree Search ou beam search. Têmpera simulada também pode
         *    participar da ordenação de candidatos, mas não deve substituir a
         *    modelagem da melhor resposta adversária sem medições que comprovem
         *    ganho de qualidade.
         *
         * 4. O limite de nós poderá variar por definição de inimigo. Um inimigo
         *    inicial usaria busca curta; um boss estratégico poderia receber mais
         *    profundidade e uma função de avaliação própria.
         *
         * 5. A tabela de transposição poderá usar uma chave incremental como
         *    Zobrist Hashing se as medições mostrarem que a criação de strings virou
         *    gargalo. Nesse caso, testes contra colisões serão essenciais.
         *
         * 6. O processamento poderá ser movido para uma tarefa em segundo plano ou
         *    dividido entre frames. O BoardState usado pela tarefa deverá ser um
         *    snapshot, e componentes Unity nunca deverão ser acessados fora da
         *    thread principal.
         *
         * 7. Quando o motor de efeitos existir, simulações de jogada deverão usar
         *    o mesmo pipeline determinístico de comandos e efeitos da partida real.
         *    Assim, a IA compreenderá runas próprias, runas do jogador e habilidades
         *    de bosses sem duplicar regras nesta classe.
         *
         * 8. Poderá ser criado um relatório de decisão contendo a variação principal,
         *    pontuação estimada e motivo da escolha para ferramentas de debug e
         *    balanceamento, sem revelar essas informações ao jogador durante a run.
         */
    }
}