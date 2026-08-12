using System;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Descreve a alteração final ocorrida em uma única casa do tabuleiro.
    ///
    /// BoardChange é somente um relatório. Ele não altera BoardState e não decide
    /// se determinada mutação é permitida.
    ///
    /// Before e After são cópias independentes dos estados recebidos. Isso é
    /// importante porque CellState é mutável dentro do assembly Domain.
    /// Se guardássemos as referências originais, uma alteração posterior poderia
    /// modificar retroativamente o relatório.
    /// </summary>
    public sealed class BoardChange
    {
        /// <summary>
        /// Casa afetada pela alteração.
        /// </summary>
        public BoardCoordinate Coordinate { get; }

        /// <summary>
        /// Cópia do estado da casa antes da alteração.
        /// </summary>
        public CellState Before { get; }

        /// <summary>
        /// Cópia do estado da casa depois da alteração.
        ///
        /// No Marco 7, a apresentação poderá usar este estado para atualizar a
        /// casa sem tentar reconstruir a regra executada.
        /// </summary>
        public CellState After { get; }

        /// <summary>
        /// Informa se a combinação de símbolos mudou.
        ///
        /// Por exemplo:
        ///
        /// None -> X
        /// X -> X | O
        /// X | O -> O
        /// </summary>
        public bool MarksChanged =>
            Before.Marks != After.Marks;

        /// <summary>
        /// Informa se os modificadores lógicos da casa mudaram.
        ///
        /// A ordem da coleção não importa. Modificadores representam um conjunto
        /// de propriedades aplicadas à casa.
        /// </summary>
        public bool ModifiersChanged =>
            !HaveSameModifiers(Before, After);

        /// <summary>
        /// Cria o relatório de uma alteração real em uma única coordenada.
        ///
        /// Os dois estados precisam:
        ///
        /// - existir;
        /// - representar a mesma coordenada;
        /// - possuir alguma diferença em símbolos ou modificadores.
        ///
        /// Uma operação que não mudou nada não deve produzir BoardChange.
        /// </summary>
        public BoardChange(
            CellState before,
            CellState after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            if (before.Coordinate != after.Coordinate)
            {
                throw new ArgumentException(
                    $"Os estados anterior e posterior precisam representar a " +
                    $"mesma coordenada. Before: {before.Coordinate}; " +
                    $"After: {after.Coordinate}.",
                    nameof(after));
            }

            if (HaveEquivalentState(before, after))
            {
                throw new ArgumentException(
                    $"A casa {before.Coordinate} não possui diferença entre os " +
                    "estados anterior e posterior.",
                    nameof(after));
            }

            Coordinate = before.Coordinate;

            /*
             * Não armazenamos as referências recebidas.
             *
             * Assim, o relatório permanece representando o momento em que foi
             * criado, mesmo que o BoardState continue sendo alterado depois.
             */
            Before = CloneCell(before);
            After = CloneCell(after);
        }

        private static CellState CloneCell(
            CellState source)
        {
            /*
             * Não chamamos CellState.Clone para deixar explícitos os dados que
             * fazem parte deste snapshot.
             *
             * O construtor de CellState também cria sua própria lista interna de
             * modificadores, portanto as duas instâncias ficam independentes.
             */
            return new CellState(
                source.Coordinate,
                source.Marks,
                source.Modifiers);
        }

        private static bool HaveEquivalentState(
            CellState left,
            CellState right)
        {
            return left.Marks == right.Marks &&
                   HaveSameModifiers(left, right);
        }

        private static bool HaveSameModifiers(
            CellState left,
            CellState right)
        {
            if (left.Modifiers.Count != right.Modifiers.Count)
            {
                return false;
            }

            /*
             * A ordem de inserção não altera o significado lógico.
             *
             * [Golden, Frozen] e [Frozen, Golden], por exemplo, representam o
             * mesmo conjunto de modificadores.
             */
            for (int index = 0;
                 index < left.Modifiers.Count;
                 index++)
            {
                CellModifierId modifier =
                    left.Modifiers[index];

                if (!right.HasModifier(modifier))
                {
                    return false;
                }
            }

            return true;
        }

        /*
         * Integrações futuras:
         *
         * 1. BoardMutationService produzirá estes objetos depois de aplicar uma
         *    operação autorizada.
         *
         * 2. BoardView usará After para atualizar somente as casas afetadas.
         *
         * 3. Efeitos visuais poderão consultar MarksChanged e ModifiersChanged
         *    para escolher animações adequadas.
         *
         * 4. Caso modificadores futuros possuam duração, intensidade ou origem,
         *    a comparação deverá considerar esses dados.
         *
         * 5. BoardChange não deve receber referências a GameObject, Sprite,
         *    MonoBehaviour ou outros tipos da Unity.
         */
    }
}