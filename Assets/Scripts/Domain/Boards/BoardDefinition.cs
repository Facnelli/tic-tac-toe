using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TicTacToeRoguelike.Domain.Boards
{
    /// <summary>
    /// Identifica uma posição lógica dentro de um tabuleiro.
    ///
    /// Este tipo não utiliza Vector2Int porque o domínio do jogo deve poder
    /// funcionar sem depender da Unity. Isso permitirá executar testes,
    /// simulações da IA e carregamento de partidas sem precisar de uma cena.
    /// </summary>
    public readonly struct BoardCoordinate : IEquatable<BoardCoordinate>
    {
        public int X { get; }
        public int Y { get; }

        public BoardCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BoardCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is BoardCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            // O HashSet usa esse valor para localizar coordenadas rapidamente.
            // A operação unchecked permite que o inteiro dê a volta caso ultrapasse
            // seu limite, pois isso é aceitável na geração de um código hash.
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public static bool operator ==(
            BoardCoordinate left,
            BoardCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            BoardCoordinate left,
            BoardCoordinate right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Representa uma direção na qual uma sequência pode continuar.
    ///
    /// Uma direção usa somente passos entre -1 e 1. Por exemplo:
    /// (1, 0) avança horizontalmente;
    /// (0, 1) avança verticalmente;
    /// (1, 1) avança na diagonal;
    /// (-1, 1) avança na diagonal oposta.
    /// </summary>
    public readonly struct BoardDirection : IEquatable<BoardDirection>
    {
        public static readonly BoardDirection Horizontal =
            new BoardDirection(1, 0);

        public static readonly BoardDirection Vertical =
            new BoardDirection(0, 1);

        public static readonly BoardDirection MainDiagonal =
            new BoardDirection(1, 1);

        public static readonly BoardDirection SecondaryDiagonal =
            new BoardDirection(-1, 1);

        public int DeltaX { get; }
        public int DeltaY { get; }

        public BoardDirection(int deltaX, int deltaY)
        {
            if (deltaX < -1 || deltaX > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaX),
                    "A direção horizontal deve estar entre -1 e 1.");
            }

            if (deltaY < -1 || deltaY > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaY),
                    "A direção vertical deve estar entre -1 e 1.");
            }

            if (deltaX == 0 && deltaY == 0)
            {
                throw new ArgumentException(
                    "Uma direção precisa movimentar ao menos um eixo.");
            }

            /*
             * Direções opostas representam a mesma linha.
             *
             * Por exemplo, (1, 0) e (-1, 0) percorrem uma linha horizontal.
             * Padronizar a direção evita que o SequenceEvaluator conte a mesma
             * sequência duas vezes apenas porque ela foi percorrida ao contrário.
             */
            bool mustInvert =
                deltaY < 0 ||
                (deltaY == 0 && deltaX < 0);

            if (mustInvert)
            {
                deltaX *= -1;
                deltaY *= -1;
            }

            DeltaX = deltaX;
            DeltaY = deltaY;
        }

        public bool Equals(BoardDirection other)
        {
            return DeltaX == other.DeltaX &&
                   DeltaY == other.DeltaY;
        }

        public override bool Equals(object obj)
        {
            return obj is BoardDirection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DeltaX * 397) ^ DeltaY;
            }
        }

        public override string ToString()
        {
            return $"({DeltaX}, {DeltaY})";
        }

        public static bool operator ==(
            BoardDirection left,
            BoardDirection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            BoardDirection left,
            BoardDirection right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Descreve a estrutura imutável de um tabuleiro.
    ///
    /// A definição informa:
    /// - os limites usados para organizar o tabuleiro;
    /// - quais coordenadas realmente existem;
    /// - em quais direções sequências podem ser formadas.
    ///
    /// Ela não guarda símbolos, ocupantes ou modificadores temporários.
    /// Esses dados pertencem ao estado de uma rodada.
    /// </summary>
    public sealed class BoardDefinition
    {
        private static readonly BoardDirection[] StandardDirections =
        {
            BoardDirection.Horizontal,
            BoardDirection.Vertical,
            BoardDirection.MainDiagonal,
            BoardDirection.SecondaryDiagonal
        };

        private readonly HashSet<BoardCoordinate> _cellLookup;

        private readonly ReadOnlyCollection<BoardCoordinate> _cells;
        private readonly ReadOnlyCollection<BoardDirection> _sequenceDirections;

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Lista ordenada de todas as casas que fazem parte do tabuleiro.
        ///
        /// Um IReadOnlyList impede que sistemas externos adicionem ou removam
        /// casas depois que a definição foi criada.
        /// </summary>
        public IReadOnlyList<BoardCoordinate> Cells => _cells;

        /// <summary>
        /// Direções nas quais o SequenceEvaluator poderá procurar sequências.
        /// </summary>
        public IReadOnlyList<BoardDirection> SequenceDirections =>
            _sequenceDirections;

        public int CellCount => _cells.Count;

        public BoardDefinition(
            int width,
            int height,
            IEnumerable<BoardCoordinate> cells,
            IEnumerable<BoardDirection> sequenceDirections)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "A largura do tabuleiro deve ser maior que zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height),
                    "A altura do tabuleiro deve ser maior que zero.");
            }

            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (sequenceDirections == null)
            {
                throw new ArgumentNullException(nameof(sequenceDirections));
            }

            Width = width;
            Height = height;

            _cellLookup = new HashSet<BoardCoordinate>();
            List<BoardCoordinate> validatedCells =
                new List<BoardCoordinate>();

            foreach (BoardCoordinate coordinate in cells)
            {
                if (!IsInsideBounds(coordinate))
                {
                    throw new ArgumentException(
                        $"A coordenada {coordinate} está fora dos limites " +
                        $"{width}x{height}.",
                        nameof(cells));
                }

                // HashSet.Add retorna false quando o item já existe.
                // Dessa forma, uma definição inválida não cria duas casas
                // ocupando a mesma coordenada lógica.
                if (!_cellLookup.Add(coordinate))
                {
                    throw new ArgumentException(
                        $"A coordenada {coordinate} foi informada mais de uma vez.",
                        nameof(cells));
                }

                validatedCells.Add(coordinate);
            }

            if (validatedCells.Count == 0)
            {
                throw new ArgumentException(
                    "O tabuleiro precisa possuir ao menos uma casa.",
                    nameof(cells));
            }

            /*
             * Uma ordem determinística é importante para salvamento, replay,
             * testes e IA. A lista ficará sempre organizada de cima para baixo
             * e, dentro de cada linha, da esquerda para a direita.
             */
            validatedCells.Sort(CompareCoordinates);

            HashSet<BoardDirection> uniqueDirections =
                new HashSet<BoardDirection>();

            List<BoardDirection> validatedDirections =
                new List<BoardDirection>();

            foreach (BoardDirection direction in sequenceDirections)
            {
                if (!uniqueDirections.Add(direction))
                {
                    throw new ArgumentException(
                        $"A direção {direction} foi informada mais de uma vez.",
                        nameof(sequenceDirections));
                }

                validatedDirections.Add(direction);
            }

            if (validatedDirections.Count == 0)
            {
                throw new ArgumentException(
                    "O tabuleiro precisa permitir ao menos uma direção de sequência.",
                    nameof(sequenceDirections));
            }

            _cells = validatedCells.AsReadOnly();
            _sequenceDirections = validatedDirections.AsReadOnly();
        }

        /// <summary>
        /// Cria o tabuleiro retangular tradicional, no qual todas as casas
        /// dentro dos limites existem.
        /// </summary>
        public static BoardDefinition CreateRectangular(
            int width,
            int height)
        {
            ValidateDimensions(width, height);

            List<BoardCoordinate> cells =
                new List<BoardCoordinate>(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cells.Add(new BoardCoordinate(x, y));
                }
            }

            return new BoardDefinition(
                width,
                height,
                cells,
                StandardDirections);
        }

        /// <summary>
        /// Cria um tabuleiro retangular removendo determinadas posições.
        ///
        /// Isso já permite formatos como cruzes, molduras e arenas com buracos,
        /// sem criar casas invisíveis apenas para completar um retângulo.
        /// </summary>
        public static BoardDefinition CreateWithBlockedCells(
            int width,
            int height,
            IEnumerable<BoardCoordinate> blockedCells)
        {
            ValidateDimensions(width, height);

            if (blockedCells == null)
            {
                throw new ArgumentNullException(nameof(blockedCells));
            }

            HashSet<BoardCoordinate> blockedLookup =
                new HashSet<BoardCoordinate>();

            foreach (BoardCoordinate blockedCell in blockedCells)
            {
                if (!IsInsideBounds(blockedCell, width, height))
                {
                    throw new ArgumentException(
                        $"A coordenada bloqueada {blockedCell} está fora dos " +
                        $"limites {width}x{height}.",
                        nameof(blockedCells));
                }

                if (!blockedLookup.Add(blockedCell))
                {
                    throw new ArgumentException(
                        $"A coordenada bloqueada {blockedCell} foi repetida.",
                        nameof(blockedCells));
                }
            }

            List<BoardCoordinate> playableCells =
                new List<BoardCoordinate>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    BoardCoordinate coordinate =
                        new BoardCoordinate(x, y);

                    if (!blockedLookup.Contains(coordinate))
                    {
                        playableCells.Add(coordinate);
                    }
                }
            }

            return new BoardDefinition(
                width,
                height,
                playableCells,
                StandardDirections);
        }

        /// <summary>
        /// Informa se uma coordenada realmente corresponde a uma casa existente.
        ///
        /// Estar dentro de Width e Height não é suficiente: um tabuleiro especial
        /// pode possuir buracos dentro desses limites.
        /// </summary>
        public bool ContainsCell(BoardCoordinate coordinate)
        {
            return _cellLookup.Contains(coordinate);
        }

        public bool ContainsCell(int x, int y)
        {
            return ContainsCell(new BoardCoordinate(x, y));
        }

        /// <summary>
        /// Tenta localizar a próxima casa em uma direção.
        ///
        /// O SequenceEvaluator usará esse método em vez de realizar diretamente
        /// cálculos de largura e altura. Assim, a regra sobre quais casas existem
        /// continua centralizada na definição do tabuleiro.
        /// </summary>
        public bool TryGetNextCell(
            BoardCoordinate current,
            BoardDirection direction,
            out BoardCoordinate next)
        {
            next = new BoardCoordinate(
                current.X + direction.DeltaX,
                current.Y + direction.DeltaY);

            return ContainsCell(next);
        }

        public bool IsInsideBounds(BoardCoordinate coordinate)
        {
            return IsInsideBounds(coordinate, Width, Height);
        }

        private static bool IsInsideBounds(
            BoardCoordinate coordinate,
            int width,
            int height)
        {
            return coordinate.X >= 0 &&
                   coordinate.X < width &&
                   coordinate.Y >= 0 &&
                   coordinate.Y < height;
        }

        private static void ValidateDimensions(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "A largura do tabuleiro deve ser maior que zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height),
                    "A altura do tabuleiro deve ser maior que zero.");
            }
        }

        private static int CompareCoordinates(
            BoardCoordinate left,
            BoardCoordinate right)
        {
            int rowComparison = left.Y.CompareTo(right.Y);

            return rowComparison != 0
                ? rowComparison
                : left.X.CompareTo(right.X);
        }

        /*
         * Adição futura:
         *
         * TryGetNextCell poderá consultar conexões personalizadas em vez de
         * apenas somar DeltaX e DeltaY. Isso permitirá tabuleiros circulares,
         * portais entre casas ou bordas que se conectam ao lado oposto sem
         * obrigar SequenceEvaluator e IA a conhecerem essas peculiaridades.
         */
    }
}