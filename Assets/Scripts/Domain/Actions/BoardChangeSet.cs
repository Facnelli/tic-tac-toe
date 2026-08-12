using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Reúne todas as alterações de casas produzidas por uma única GameAction.
    ///
    /// Uma jogada normal produzirá normalmente um conjunto com uma casa.
    /// Uma futura runa poderá remover uma linha inteira e produzir várias
    /// alterações dentro do mesmo conjunto.
    ///
    /// Cada coordenada aparece no máximo uma vez. Quando uma ação realiza várias
    /// operações sobre a mesma casa, o relatório deve consolidar:
    ///
    /// - o estado existente antes da ação;
    /// - o estado final depois de todas as operações.
    ///
    /// Estados intermediários não pertencem ao BoardChangeSet final.
    /// </summary>
    public sealed class BoardChangeSet :
        IReadOnlyList<BoardChange>
    {
        private static readonly BoardChangeSet EmptyInstance =
            new BoardChangeSet(
                new BoardChange[0]);

        private readonly ReadOnlyCollection<BoardChange> _changes;

        /// <summary>
        /// Conjunto compartilhado sem alterações.
        ///
        /// Será usado por ações rejeitadas e por ações aplicadas que modificarem
        /// pontuação, runas ou outro estado sem alterar o BoardState.
        /// </summary>
        public static BoardChangeSet Empty =>
            EmptyInstance;

        /// <summary>
        /// Lista somente para leitura das casas alteradas.
        ///
        /// A ordem recebida no construtor é preservada. O futuro serviço que
        /// construir o conjunto deverá fornecer uma ordem determinística.
        /// </summary>
        public IReadOnlyList<BoardChange> Changes =>
            _changes;

        public int Count =>
            _changes.Count;

        public bool IsEmpty =>
            Count == 0;

        public BoardChange this[int index] =>
            _changes[index];

        /// <summary>
        /// Cria um conjunto a partir das alterações informadas.
        ///
        /// A coleção é copiada. Alterar posteriormente a lista usada pelo chamador
        /// não modifica este BoardChangeSet.
        /// </summary>
        public BoardChangeSet(
            IEnumerable<BoardChange> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            List<BoardChange> validatedChanges =
                new List<BoardChange>();

            HashSet<BoardCoordinate> changedCoordinates =
                new HashSet<BoardCoordinate>();

            foreach (BoardChange change in changes)
            {
                if (change == null)
                {
                    throw new ArgumentException(
                        "A coleção não pode conter uma alteração nula.",
                        nameof(changes));
                }

                /*
                 * Uma coordenada deve possuir apenas um estado anterior e um
                 * estado final dentro de uma ação.
                 *
                 * Sem essa regra, a apresentação não saberia se deve mostrar
                 * estados intermediários ou somente o resultado final.
                 */
                if (!changedCoordinates.Add(change.Coordinate))
                {
                    throw new ArgumentException(
                        $"A coordenada {change.Coordinate} foi informada mais de " +
                        "uma vez. As operações dessa casa precisam ser consolidadas.",
                        nameof(changes));
                }

                validatedChanges.Add(change);
            }

            _changes = new ReadOnlyCollection<BoardChange>(
                validatedChanges);
        }

        /// <summary>
        /// Atalho para criar o relatório de uma única casa alterada.
        ///
        /// Será útil para PlaceMarkAction.
        /// </summary>
        public static BoardChangeSet CreateSingle(
            BoardChange change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            return new BoardChangeSet(
                new[]
                {
                    change
                });
        }

        /// <summary>
        /// Informa se determinada coordenada aparece no conjunto.
        /// </summary>
        public bool ContainsCoordinate(
            BoardCoordinate coordinate)
        {
            return TryGetChange(
                coordinate,
                out BoardChange ignoredChange);
        }

        /// <summary>
        /// Procura a alteração de uma casa.
        ///
        /// Retorna false e deixa change como null quando a coordenada não foi
        /// alterada pela ação.
        /// </summary>
        public bool TryGetChange(
            BoardCoordinate coordinate,
            out BoardChange change)
        {
            for (int index = 0;
                 index < _changes.Count;
                 index++)
            {
                BoardChange candidate =
                    _changes[index];

                if (candidate.Coordinate == coordinate)
                {
                    change = candidate;
                    return true;
                }
            }

            change = null;
            return false;
        }

        public IEnumerator<BoardChange> GetEnumerator()
        {
            return _changes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /*
         * Integrações futuras:
         *
         * 1. MoveService produzirá um conjunto com uma única casa.
         *
         * 2. BoardMutationService poderá produzir conjuntos com várias casas.
         *
         * 3. ActionResult armazenará este relatório ao lado das versões do
         *    tabuleiro.
         *
         * 4. BoardView poderá percorrer diretamente o conjunto:
         *
         *    foreach (BoardChange change in changeSet)
         *    {
         *        RefreshCell(change.Coordinate, change.After);
         *    }
         *
         * 5. O conjunto não aplica alterações e não possui referência ao
         *    BoardState autoritativo.
         */
    }
}