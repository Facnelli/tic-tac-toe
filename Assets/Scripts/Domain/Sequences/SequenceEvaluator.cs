using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.Sequences
{
    /// <summary>
    /// Identifica a direção lógica na qual uma sequência foi encontrada.
    ///
    /// A direção é informação de domínio, não apenas visual. Futuramente, uma runa
    /// poderá conceder bônus somente para diagonais, linhas horizontais ou colunas.
    /// </summary>
    public enum SequenceDirection
    {
        /// <summary>
        /// O valor de X aumenta enquanto Y permanece igual.
        /// </summary>
        Horizontal,

        /// <summary>
        /// O valor de Y aumenta enquanto X permanece igual.
        /// </summary>
        Vertical,

        /// <summary>
        /// X e Y aumentam juntos.
        ///
        /// Dependendo da orientação visual usada pela interface, essa diagonal poderá
        /// aparecer subindo ou descendo na tela. O domínio se importa apenas com as
        /// coordenadas, não com a orientação visual.
        /// </summary>
        MainDiagonal,

        /// <summary>
        /// X diminui enquanto Y aumenta.
        /// </summary>
        SecondaryDiagonal
    }

    /// <summary>
    /// Representa uma sequência específica encontrada no tabuleiro.
    ///
    /// Uma SequenceMatch não altera o tabuleiro. Ela funciona como um registro
    /// imutável do resultado produzido pelo SequenceEvaluator.
    ///
    /// Por exemplo, uma linha de quatro símbolos pode produzir:
    /// - três SequenceMatch de tamanho 2;
    /// - duas SequenceMatch de tamanho 3;
    /// - uma SequenceMatch de tamanho 4.
    /// </summary>
    public sealed class SequenceMatch
    {
        private readonly ReadOnlyCollection<BoardCoordinate> _cells;

        /// <summary>
        /// Símbolo para o qual esta sequência foi avaliada.
        ///
        /// Uma casa que contenha X e O poderá aparecer tanto numa sequência de X
        /// quanto em outra sequência de O.
        /// </summary>
        public CellMark Mark { get; }

        /// <summary>
        /// Direção na qual as casas da sequência estão organizadas.
        /// </summary>
        public SequenceDirection Direction { get; }

        /// <summary>
        /// Casas que formam a sequência, em ordem do início até o fim.
        ///
        /// A coleção é somente para leitura para impedir que outro sistema altere
        /// acidentalmente o resultado produzido pelo avaliador.
        /// </summary>
        public IReadOnlyList<BoardCoordinate> Cells => _cells;

        /// <summary>
        /// Quantidade de casas que formam a sequência.
        /// </summary>
        public int Length => _cells.Count;

        /// <summary>
        /// Primeira coordenada da sequência.
        /// </summary>
        public BoardCoordinate Start => _cells[0];

        /// <summary>
        /// Última coordenada da sequência.
        /// </summary>
        public BoardCoordinate End => _cells[_cells.Count - 1];

        /// <summary>
        /// O construtor é internal porque as sequências devem ser criadas pelo
        /// SequenceEvaluator, depois que todas as regras de formação forem verificadas.
        /// </summary>
        internal SequenceMatch(
            CellMark mark,
            SequenceDirection direction,
            IList<BoardCoordinate> cells)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentException(
                    "Uma sequência deve pertencer exatamente ao símbolo X ou O.",
                    nameof(mark));
            }

            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count < 2)
            {
                throw new ArgumentException(
                    "Uma sequência precisa possuir pelo menos duas casas.",
                    nameof(cells));
            }

            Mark = mark;
            Direction = direction;

            /*
             * Criamos uma nova lista em vez de guardar diretamente a coleção recebida.
             *
             * Sem essa cópia, o código que criou a lista ainda poderia alterá-la
             * depois da construção da SequenceMatch. Isso faria um resultado antigo
             * mudar sem que o tabuleiro tivesse mudado.
             */
            List<BoardCoordinate> cellsCopy =
                new List<BoardCoordinate>(cells);

            _cells = new ReadOnlyCollection<BoardCoordinate>(
                cellsCopy);
        }

        /// <summary>
        /// Informa se uma determinada coordenada participa desta sequência.
        ///
        /// Esse método poderá ser utilizado pela apresentação para descobrir quais
        /// casas devem ser iluminadas durante uma animação.
        /// </summary>
        public bool Contains(BoardCoordinate coordinate)
        {
            for (int index = 0; index < _cells.Count; index++)
            {
                if (_cells[index].Equals(coordinate))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Encontra sequências contínuas de símbolos no BoardState.
    ///
    /// O avaliador reconhece as direções lineares configuradas na
    /// BoardDefinition:
    /// - horizontal;
    /// - vertical;
    /// - diagonal principal;
    /// - diagonal secundária.
    ///
    /// Somente as direções permitidas pela definição do tabuleiro são avaliadas.
    ///
    /// Ele apenas consulta o tabuleiro. Não adiciona símbolos, não calcula pontos,
    /// não decide o vencedor e não inicia animações.
    /// </summary>
    public sealed class SequenceEvaluator
    {

        /// <summary>
        /// Encontra todas as subsequências que poderão participar da pontuação.
        ///
        /// A busca começa em tamanho 2, preservando a mecânica atual. O parâmetro
        /// maximumLength determina o maior tamanho que deve ser produzido.
        ///
        /// Exemplo para uma linha contínua de quatro símbolos e maximumLength igual
        /// a 4:
        ///
        /// - tamanho 2: três resultados;
        /// - tamanho 3: dois resultados;
        /// - tamanho 4: um resultado.
        ///
        /// Este método não soma os valores encontrados. O sistema de pontuação
        /// decidirá quanto vale cada sequência e quais modificadores serão aplicados.
        /// </summary>
        public IReadOnlyList<SequenceMatch> EvaluateScoringSequences(
            BoardState board,
            CellMark mark,
            int maximumLength)
        {
            return EvaluateSequences(
                board,
                mark,
                2,
                maximumLength);
        }

        /// <summary>
        /// Encontra todas as sequências com exatamente o tamanho exigido pela
        /// condição de vitória.
        ///
        /// Uma linha maior também contém sequências menores. Portanto, uma linha de
        /// quatro X contém duas sequências de três X e satisfaz uma condição de
        /// vitória de tamanho 3.
        ///
        /// Este método apenas encontra os resultados. A decisão de encerrar a rodada
        /// continuará a pertencer ao futuro coordenador da partida.
        /// </summary>
        public IReadOnlyList<SequenceMatch> EvaluateWinningSequences(
            BoardState board,
            CellMark mark,
            int requiredLength)
        {
            return EvaluateSequences(
                board,
                mark,
                requiredLength,
                requiredLength);
        }

        /// <summary>
        /// Informa se existe pelo menos uma sequência capaz de satisfazer a condição
        /// de vitória.
        ///
        /// Este atalho é útil quando o chamador precisa apenas de true ou false.
        /// Para obter as coordenadas que formaram a vitória, utilize
        /// EvaluateWinningSequences.
        /// </summary>
        public bool HasWinningSequence(
            BoardState board,
            CellMark mark,
            int requiredLength)
        {
            IReadOnlyList<SequenceMatch> winningSequences =
                EvaluateWinningSequences(
                    board,
                    mark,
                    requiredLength);

            return winningSequences.Count > 0;
        }

        /// <summary>
        /// Encontra todas as subsequências num intervalo de tamanhos.
        ///
        /// Este é o método central do avaliador. Os métodos de pontuação e vitória
        /// apenas fornecem intervalos diferentes para ele.
        ///
        /// minimumLength e maximumLength são inclusivos. Isso significa que os dois
        /// limites também fazem parte da busca.
        /// </summary>
        public IReadOnlyList<SequenceMatch> EvaluateSequences(
            BoardState board,
            CellMark mark,
            int minimumLength,
            int maximumLength)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            ValidateSingleMark(mark);

            if (minimumLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumLength),
                    minimumLength,
                    "O tamanho mínimo de uma sequência deve ser pelo menos 2.");
            }

            if (maximumLength < minimumLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumLength),
                    maximumLength,
                    "O tamanho máximo não pode ser menor que o tamanho mínimo.");
            }

            List<SequenceMatch> foundSequences =
                new List<SequenceMatch>();

            /*
             * Percorremos Definition.Cells em vez de percorrer todas as combinações
             * entre Width e Height.
             *
             * Em um tabuleiro especial, algumas coordenadas dentro do retângulo podem
             * não representar casas existentes. A BoardDefinition é a fonte correta
             * para descobrir quais casas devem ser avaliadas.
             */
            foreach (BoardCoordinate coordinate in board.Definition.Cells)
            {
                /*
                 * Uma mesma coordenada precisa ser testada em todas as direções
                 * permitidas pela definição do tabuleiro.
                 *
                 * A casa poderá ser o começo de uma linha horizontal, mas estar no meio
                 * de outra linha vertical. Cada direção é analisada separadamente.
                 */
                foreach (BoardDirection boardDirection
                         in board.Definition.SequenceDirections)
                {
                    DirectionStep direction =
                        CreateDirectionStep(boardDirection);

                    TryAddSequencesStartingAt(
                        board,
                        coordinate,
                        mark,
                        direction,
                        minimumLength,
                        maximumLength,
                        foundSequences);
                }
            }

            /*
             * AsReadOnly cria uma camada que impede operações como Add e Remove.
             *
             * Isso protege a lista de resultados, mas não cria outra avaliação.
             * Cada SequenceMatch já é imutável e também protege a sua própria lista
             * de coordenadas.
             */
            return foundSequences.AsReadOnly();
        }

        /// <summary>
        /// Verifica se a coordenada é o começo real de uma linha contínua. Quando for,
        /// encontra a linha completa e produz as suas subsequências.
        /// </summary>
        private static void TryAddSequencesStartingAt(
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark,
            DirectionStep direction,
            int minimumLength,
            int maximumLength,
            List<SequenceMatch> destination)
        {
            /*
             * Se a casa atual não contém o símbolo procurado, ela não pode iniciar
             * uma sequência desse símbolo.
             *
             * CellState.HasMark entende combinações de Flags. Assim, uma casa com
             * X | O retorna true tanto ao procurar X quanto ao procurar O.
             */
            if (!CellContainsMark(
                board,
                coordinate,
                mark))
            {
                return;
            }

            BoardCoordinate previousCoordinate =
                new BoardCoordinate(
                    coordinate.X - direction.StepX,
                    coordinate.Y - direction.StepY);

            /*
             * Se a casa anterior na mesma direção também contém o símbolo, a
             * coordenada atual está no meio de uma linha que já será avaliada a partir
             * do seu começo verdadeiro.
             *
             * Retornar aqui é o que impede a mesma linha de ser processada repetidas
             * vezes a partir de cada uma das suas casas.
             */
            if (CellContainsMark(
                board,
                previousCoordinate,
                mark))
            {
                return;
            }

            List<BoardCoordinate> contiguousLine =
                GetContiguousLine(
                    board,
                    coordinate,
                    mark,
                    direction);

            /*
             * Uma linha menor que o mínimo solicitado não produz resultado.
             *
             * Uma casa isolada, por exemplo, possui comprimento 1 e não é considerada
             * uma sequência pela regra atual.
             */
            if (contiguousLine.Count < minimumLength)
            {
                return;
            }

            AddSubsequences(
                contiguousLine,
                mark,
                direction.Direction,
                minimumLength,
                maximumLength,
                destination);
        }

        /// <summary>
        /// Percorre uma direção até encontrar uma interrupção.
        ///
        /// Uma linha é interrompida quando:
        /// - a coordenada não existe no formato do tabuleiro;
        /// - a casa existe, mas não contém o símbolo procurado.
        /// </summary>
        private static List<BoardCoordinate> GetContiguousLine(
            BoardState board,
            BoardCoordinate start,
            CellMark mark,
            DirectionStep direction)
        {
            List<BoardCoordinate> contiguousLine =
                new List<BoardCoordinate>();

            BoardCoordinate current = start;

            while (CellContainsMark(
                board,
                current,
                mark))
            {
                contiguousLine.Add(current);

                current = new BoardCoordinate(
                    current.X + direction.StepX,
                    current.Y + direction.StepY);
            }

            return contiguousLine;
        }

        /// <summary>
        /// Desmembra uma linha contínua em todas as janelas de tamanhos permitidos.
        ///
        /// Uma "janela" é um trecho consecutivo dentro da linha. Para uma linha com
        /// quatro casas A, B, C e D, as janelas de tamanho 3 são:
        ///
        /// - A, B, C;
        /// - B, C, D.
        /// </summary>
        private static void AddSubsequences(
            IList<BoardCoordinate> contiguousLine,
            CellMark mark,
            SequenceDirection direction,
            int minimumLength,
            int maximumLength,
            List<SequenceMatch> destination)
        {
            int effectiveMaximumLength =
                Math.Min(
                    maximumLength,
                    contiguousLine.Count);

            /*
             * O primeiro laço escolhe o tamanho da subsequência.
             *
             * O segundo escolhe onde essa subsequência começa dentro da linha.
             */
            for (int length = minimumLength;
                 length <= effectiveMaximumLength;
                 length++)
            {
                int possibleStartCount =
                    contiguousLine.Count - length + 1;

                for (int startIndex = 0;
                     startIndex < possibleStartCount;
                     startIndex++)
                {
                    List<BoardCoordinate> sequenceCells =
                        new List<BoardCoordinate>(length);

                    for (int offset = 0;
                         offset < length;
                         offset++)
                    {
                        sequenceCells.Add(
                            contiguousLine[startIndex + offset]);
                    }

                    destination.Add(
                        new SequenceMatch(
                            mark,
                            direction,
                            sequenceCells));
                }
            }
        }

        /// <summary>
        /// Verifica simultaneamente se a casa existe e se ela contém o símbolo.
        ///
        /// TryGetCell é importante para tabuleiros especiais: uma coordenada ausente
        /// representa apenas o fim da linha, e não um erro que deva lançar exceção.
        /// </summary>
        private static bool CellContainsMark(
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark)
        {
            if (!board.TryGetCell(
                coordinate,
                out CellState cell))
            {
                return false;
            }

            return cell.HasMark(mark);
        }

        /// <summary>
        /// Garante que a avaliação esteja procurando somente X ou somente O.
        ///
        /// CellMark.X | CellMark.O é um conteúdo válido para uma casa, mas não é um
        /// participante separado. Avaliamos X e O individualmente.
        /// </summary>
        private static void ValidateSingleMark(CellMark mark)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentException(
                    "A avaliação deve procurar exatamente um símbolo: X ou O.",
                    nameof(mark));
            }
        }
        
        private static DirectionStep CreateDirectionStep(
            BoardDirection direction)
        {
            if (direction == BoardDirection.Horizontal)
            {
                return new DirectionStep(
                    SequenceDirection.Horizontal,
                    direction.DeltaX,
                    direction.DeltaY);
            }

            if (direction == BoardDirection.Vertical)
            {
                return new DirectionStep(
                    SequenceDirection.Vertical,
                    direction.DeltaX,
                    direction.DeltaY);
            }

            if (direction == BoardDirection.MainDiagonal)
            {
                return new DirectionStep(
                    SequenceDirection.MainDiagonal,
                    direction.DeltaX,
                    direction.DeltaY);
            }

            if (direction == BoardDirection.SecondaryDiagonal)
            {
                return new DirectionStep(
                    SequenceDirection.SecondaryDiagonal,
                    direction.DeltaX,
                    direction.DeltaY);
            }

            throw new ArgumentException(
                "A direção do tabuleiro não possui uma direção de sequência correspondente.",
                nameof(direction));
        }

        /// <summary>
        /// Pequena estrutura interna que relaciona uma direção aos deslocamentos
        /// aplicados às coordenadas.
        ///
        /// Por exemplo, StepX igual a 1 e StepY igual a 0 significa avançar uma
        /// coluna sem mudar de linha.
        /// </summary>
        private readonly struct DirectionStep
        {
            public SequenceDirection Direction { get; }
            public int StepX { get; }
            public int StepY { get; }

            public DirectionStep(
                SequenceDirection direction,
                int stepX,
                int stepY)
            {
                Direction = direction;
                StepX = stepX;
                StepY = stepY;
            }
        }

        /*
         * Adições futuras:
         *
         * 1. Poderá ser criado um SequenceEvaluationResult contendo todas as
         *    sequências de X e O numa única avaliação, junto da BoardState.Version.
         *    Assim, jogador, inimigo, IA e pontuação poderão reutilizar o mesmo
         *    resultado enquanto o tabuleiro não mudar.
         *
         * 2. Regras de pontuação poderão selecionar apenas determinados tamanhos ou
         *    direções. Uma runa poderá, por exemplo, duplicar o valor das diagonais
         *    sem modificar o algoritmo que encontra as sequências.
         *
         * 3. Poderão ser adicionadas direções ou formatos não lineares, como cruzes,
         *    cantos e áreas. Esses padrões provavelmente deverão usar avaliadores
         *    próprios em vez de transformar este arquivo numa classe muito grande.
         *
         * 4. Uma regra de vitória futura poderá exigir uma linha máxima inteira, em
         *    vez de aceitar uma janela numa linha maior. Essa diferença deve
         *    ser representada numa configuração de regra, sem ficar escondida na UI.
         *
         * 5. Se tabuleiros muito grandes tornarem as alocações relevantes, poderá ser
         *    implementada uma avaliação otimizada que conte sequências sem criar uma
         *    lista para cada janela. Para os tabuleiros atuais, a implementação mais
         *    explícita favorece legibilidade e segurança.
         *
         * 6. Eventos ou dados visuais poderão indicar quais sequências surgiram após
         *    a última jogada. A apresentação poderá então iluminar apenas os novos
         *    resultados, sem decidir por conta própria o que é uma sequência.
         *
         * 7. Modificadores futuros de casa poderão fazer uma célula substituir um
         *    símbolo, quebrar uma sequência ou valer como coringa. Essa interpretação
         *    poderá ser extraída para um serviço como ISequenceCellMatcher.
         */
    }
}