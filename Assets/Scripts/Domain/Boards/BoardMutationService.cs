using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Actions;

namespace TicTacToeRoguelike.Domain.Boards
{
    /// <summary>
    /// Identifica uma operação autorizada sobre o BoardState.
    ///
    /// Diferentemente de uma jogada normal, uma mutação não pergunta se a
    /// alteração é permitida. Ela representa uma decisão que já foi autorizada
    /// por uma regra anterior.
    ///
    /// No futuro, por exemplo:
    ///
    /// - uma runa poderá autorizar RemoveMark;
    /// - um efeito poderá autorizar ClearMarks;
    /// - uma casa especial poderá autorizar AddMark sobre uma casa ocupada;
    /// - uma habilidade poderá aplicar modificadores em várias casas.
    ///
    /// None existe apenas como valor neutro e nunca representa um comando válido.
    /// </summary>
    public enum BoardMutationType
    {
        None = 0,

        AddMark = 1,
        RemoveMark = 2,
        ClearMarks = 3,

        AddModifier = 4,
        RemoveModifier = 5,
        ClearModifiers = 6,

        /// <summary>
        /// Remove marcas e modificadores da casa.
        /// </summary>
        ClearCell = 7
    }

    /// <summary>
    /// Descreve uma única operação autorizada sobre uma casa.
    ///
    /// O comando é imutável. Ele informa o que deverá acontecer, mas não possui
    /// referência ao BoardState e não executa a alteração sozinho.
    ///
    /// Os métodos de fábrica impedem combinações inválidas, como:
    ///
    /// - ClearMarks carregando uma marca;
    /// - AddMark recebendo X | O;
    /// - AddModifier recebendo um modificador inexistente.
    /// </summary>
    public sealed class BoardMutationCommand
    {
        /// <summary>
        /// Categoria da operação.
        /// </summary>
        public BoardMutationType Type { get; }

        /// <summary>
        /// Casa afetada pela operação.
        ///
        /// A existência da casa depende do BoardState e será validada pelo
        /// BoardMutationService antes que qualquer alteração seja aplicada.
        /// </summary>
        public BoardCoordinate Coordinate { get; }

        /// <summary>
        /// Marca usada por AddMark e RemoveMark.
        ///
        /// Nos demais tipos, este valor será CellMark.None.
        /// </summary>
        public CellMark Mark { get; }

        /// <summary>
        /// Modificador usado por AddModifier e RemoveModifier.
        ///
        /// Nos demais tipos, este valor não estará preenchido.
        /// Nullable permite representar claramente a ausência do modificador.
        /// </summary>
        public CellModifierId? Modifier { get; }

        private BoardMutationCommand(
            BoardMutationType type,
            BoardCoordinate coordinate,
            CellMark mark,
            CellModifierId? modifier)
        {
            ValidateType(type);
            ValidatePayload(type, mark, modifier);

            Type = type;
            Coordinate = coordinate;
            Mark = mark;
            Modifier = modifier;
        }

        /// <summary>
        /// Cria uma operação que adiciona uma única marca.
        ///
        /// Essa operação permite sobreposição. Se a casa possuir X e o comando
        /// adicionar O, o estado final poderá conter X | O.
        ///
        /// A legalidade da sobreposição não é decidida aqui.
        /// </summary>
        public static BoardMutationCommand AddMark(
            BoardCoordinate coordinate,
            CellMark mark)
        {
            return new BoardMutationCommand(
                BoardMutationType.AddMark,
                coordinate,
                mark,
                null);
        }

        /// <summary>
        /// Remove somente a marca informada, preservando a outra marca e os
        /// modificadores da casa.
        /// </summary>
        public static BoardMutationCommand RemoveMark(
            BoardCoordinate coordinate,
            CellMark mark)
        {
            return new BoardMutationCommand(
                BoardMutationType.RemoveMark,
                coordinate,
                mark,
                null);
        }

        /// <summary>
        /// Remove todas as marcas, mas preserva os modificadores.
        /// </summary>
        public static BoardMutationCommand ClearMarks(
            BoardCoordinate coordinate)
        {
            return new BoardMutationCommand(
                BoardMutationType.ClearMarks,
                coordinate,
                CellMark.None,
                null);
        }

        /// <summary>
        /// Adiciona um modificador lógico à casa.
        /// </summary>
        public static BoardMutationCommand AddModifier(
            BoardCoordinate coordinate,
            CellModifierId modifier)
        {
            return new BoardMutationCommand(
                BoardMutationType.AddModifier,
                coordinate,
                CellMark.None,
                modifier);
        }

        /// <summary>
        /// Remove um modificador lógico da casa.
        /// </summary>
        public static BoardMutationCommand RemoveModifier(
            BoardCoordinate coordinate,
            CellModifierId modifier)
        {
            return new BoardMutationCommand(
                BoardMutationType.RemoveModifier,
                coordinate,
                CellMark.None,
                modifier);
        }

        /// <summary>
        /// Remove todos os modificadores, preservando as marcas.
        /// </summary>
        public static BoardMutationCommand ClearModifiers(
            BoardCoordinate coordinate)
        {
            return new BoardMutationCommand(
                BoardMutationType.ClearModifiers,
                coordinate,
                CellMark.None,
                null);
        }

        /// <summary>
        /// Remove marcas e modificadores da casa.
        ///
        /// Uma casa já totalmente vazia não será considerada alterada.
        /// </summary>
        public static BoardMutationCommand ClearCell(
            BoardCoordinate coordinate)
        {
            return new BoardMutationCommand(
                BoardMutationType.ClearCell,
                coordinate,
                CellMark.None,
                null);
        }

        private static void ValidateType(
            BoardMutationType type)
        {
            if (type == BoardMutationType.None ||
                !Enum.IsDefined(typeof(BoardMutationType), type))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(type),
                    type,
                    "O tipo de mutação informado não é reconhecido.");
            }
        }

        private static void ValidatePayload(
            BoardMutationType type,
            CellMark mark,
            CellModifierId? modifier)
        {
            switch (type)
            {
                case BoardMutationType.AddMark:
                case BoardMutationType.RemoveMark:
                    ValidateSingleMark(mark);

                    if (modifier.HasValue)
                    {
                        throw new ArgumentException(
                            "Uma mutação de marca não pode carregar modificador.",
                            nameof(modifier));
                    }

                    return;

                case BoardMutationType.AddModifier:
                case BoardMutationType.RemoveModifier:
                    if (mark != CellMark.None)
                    {
                        throw new ArgumentException(
                            "Uma mutação de modificador não pode carregar marca.",
                            nameof(mark));
                    }

                    if (!modifier.HasValue)
                    {
                        throw new ArgumentException(
                            "A mutação precisa informar um modificador.",
                            nameof(modifier));
                    }

                    ValidateModifier(modifier.Value);
                    return;

                case BoardMutationType.ClearMarks:
                case BoardMutationType.ClearModifiers:
                case BoardMutationType.ClearCell:
                    if (mark != CellMark.None ||
                        modifier.HasValue)
                    {
                        throw new ArgumentException(
                            "Uma operação de limpeza não pode carregar marca " +
                            "ou modificador.");
                    }

                    return;

                default:
                    /*
                     * ValidateType já impede que este ponto seja alcançado.
                     * A exceção permanece como proteção para futuras alterações.
                     */
                    throw new ArgumentOutOfRangeException(
                        nameof(type),
                        type,
                        "O tipo de mutação não possui validação implementada.");
            }
        }

        private static void ValidateSingleMark(
            CellMark mark)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mark),
                    mark,
                    "A mutação precisa receber exatamente X ou O.");
            }
        }

        private static void ValidateModifier(
            CellModifierId modifier)
        {
            if (!Enum.IsDefined(
                    typeof(CellModifierId),
                    modifier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modifier),
                    modifier,
                    "O modificador informado não é reconhecido.");
            }
        }
    }

    /// <summary>
    /// Aplica operações previamente autorizadas ao BoardState.
    ///
    /// Este serviço não decide:
    ///
    /// - se é o Turno correto;
    /// - se o ator pode executar a ação;
    /// - se uma runa está disponível;
    /// - se uma jogada normal é legal;
    /// - se a Rodada terminou;
    /// - se o orçamento do TurnContext deve ser consumido.
    ///
    /// Essas validações pertencem ao futuro ActionExecutor e às regras que
    /// autorizaram os comandos.
    ///
    /// O serviço possui três responsabilidades:
    ///
    /// 1. validar estruturalmente todos os comandos antes da alteração;
    /// 2. aplicar o estado final autorizado;
    /// 3. produzir um BoardChangeSet consolidado.
    /// </summary>
    public sealed class BoardMutationService
    {
        private static readonly CellMark[] SupportedMarks =
        {
            CellMark.X,
            CellMark.O
        };

        /// <summary>
        /// Aplica uma coleção de mutações já autorizadas.
        ///
        /// A coleção inteira é copiada e validada antes que o tabuleiro real seja
        /// alterado. Portanto, uma coordenada inexistente encontrada no último
        /// comando não deixa os comandos anteriores parcialmente aplicados.
        ///
        /// Vários comandos podem afetar a mesma casa. O BoardChangeSet resultante
        /// conterá somente:
        ///
        /// - o estado anterior à primeira operação;
        /// - o estado final depois de todas as operações.
        ///
        /// Estados intermediários não aparecem no relatório.
        /// </summary>
        /// <param name="board">
        /// Tabuleiro autoritativo que receberá as alterações.
        /// </param>
        /// <param name="commands">
        /// Operações autorizadas, executadas na ordem recebida.
        /// </param>
        /// <returns>
        /// Relatório consolidado das casas que realmente terminaram alteradas.
        ///
        /// Se todos os comandos forem inócuos ou se cancelarem entre si, retorna
        /// BoardChangeSet.Empty e não altera a versão do tabuleiro real.
        /// </returns>
        public BoardChangeSet ApplyAuthorized(
            BoardState board,
            IEnumerable<BoardMutationCommand> commands)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            List<BoardCoordinate> touchedCoordinates;

            List<BoardMutationCommand> validatedCommands =
                ValidateAndCopyCommands(
                    board,
                    commands,
                    out touchedCoordinates);

            if (validatedCommands.Count == 0)
            {
                return BoardChangeSet.Empty;
            }

            /*
             * Primeiro aplicamos os comandos sobre uma cópia.
             *
             * Essa cópia funciona como uma prévia do resultado final. O tabuleiro
             * real continua intocado durante essa etapa.
             *
             * Além de evitar alterações parciais por comandos estruturalmente
             * inválidos, a prévia permite reconhecer operações que se cancelam:
             *
             * AddMark X
             * RemoveMark X
             *
             * Se a casa começou sem X, o estado final continuará igual ao inicial.
             * Nesse caso, o tabuleiro real não será alterado e sua versão também
             * permanecerá igual.
             */
            BoardState previewBoard =
                board.Clone();

            for (int index = 0;
                 index < validatedCommands.Count;
                 index++)
            {
                ApplyCommand(
                    previewBoard,
                    validatedCommands[index]);
            }

            long boardVersionBefore =
                board.Version;

            List<BoardChange> changes =
                new List<BoardChange>();

            /*
             * A ordem do relatório segue a primeira aparição de cada coordenada
             * nos comandos. Isso produz uma ordem determinística sem repetir casas.
             */
            for (int index = 0;
                 index < touchedCoordinates.Count;
                 index++)
            {
                BoardCoordinate coordinate =
                    touchedCoordinates[index];

                CellState currentCell =
                    board.GetCell(coordinate);

                CellState desiredCell =
                    previewBoard.GetCell(coordinate);

                if (HaveEquivalentState(
                    currentCell,
                    desiredCell))
                {
                    /*
                     * A casa pode ter recebido comandos, mas terminado exatamente
                     * como começou. Ela não produz BoardChange.
                     */
                    continue;
                }

                CellState before =
                    CreateSnapshot(currentCell);

                /*
                 * Aplicamos somente a diferença final ao tabuleiro real.
                 *
                 * Isso evita incrementar a versão por estados intermediários que
                 * foram cancelados antes do fim da operação composta.
                 */
                ApplyFinalState(
                    board,
                    coordinate,
                    desiredCell);

                CellState after =
                    CreateSnapshot(
                        board.GetCell(coordinate));

                if (!HaveEquivalentState(
                    after,
                    desiredCell))
                {
                    throw new InvalidOperationException(
                        $"O estado final aplicado à casa {coordinate} não " +
                        "corresponde ao resultado autorizado.");
                }

                changes.Add(
                    new BoardChange(
                        before,
                        after));
            }

            ValidateResultConsistency(
                board,
                boardVersionBefore,
                changes);

            if (changes.Count == 0)
            {
                return BoardChangeSet.Empty;
            }

            return new BoardChangeSet(changes);
        }

        private static List<BoardMutationCommand>
            ValidateAndCopyCommands(
                BoardState board,
                IEnumerable<BoardMutationCommand> commands,
                out List<BoardCoordinate> touchedCoordinates)
        {
            List<BoardMutationCommand> validatedCommands =
                new List<BoardMutationCommand>();

            touchedCoordinates =
                new List<BoardCoordinate>();

            HashSet<BoardCoordinate> touchedLookup =
                new HashSet<BoardCoordinate>();

            /*
             * A enumeração acontece por completo antes da aplicação.
             *
             * Isso também protege o tabuleiro caso a coleção recebida lance uma
             * exceção durante sua própria enumeração.
             */
            foreach (BoardMutationCommand command in commands)
            {
                if (command == null)
                {
                    throw new ArgumentException(
                        "A coleção de mutações não pode conter um comando nulo.",
                        nameof(commands));
                }

                if (!board.ContainsCell(command.Coordinate))
                {
                    throw new ArgumentException(
                        $"A coordenada {command.Coordinate} não corresponde " +
                        "a uma casa existente no tabuleiro.",
                        nameof(commands));
                }

                validatedCommands.Add(command);

                if (touchedLookup.Add(command.Coordinate))
                {
                    touchedCoordinates.Add(
                        command.Coordinate);
                }
            }

            return validatedCommands;
        }

        private static void ApplyCommand(
            BoardState board,
            BoardMutationCommand command)
        {
            switch (command.Type)
            {
                case BoardMutationType.AddMark:
                    /*
                     * AddMark é chamado diretamente, sem MoveValidator.
                     *
                     * Por isso, esta operação consegue produzir X | O mesmo que a
                     * casa não possua o modificador normalmente exigido para uma
                     * jogada comum. A regra chamadora já deve ter autorizado isso.
                     */
                    board.AddMark(
                        command.Coordinate,
                        command.Mark);
                    return;

                case BoardMutationType.RemoveMark:
                    board.RemoveMark(
                        command.Coordinate,
                        command.Mark);
                    return;

                case BoardMutationType.ClearMarks:
                    board.ClearMarks(
                        command.Coordinate);
                    return;

                case BoardMutationType.AddModifier:
                    board.AddModifier(
                        command.Coordinate,
                        command.Modifier.Value);
                    return;

                case BoardMutationType.RemoveModifier:
                    board.RemoveModifier(
                        command.Coordinate,
                        command.Modifier.Value);
                    return;

                case BoardMutationType.ClearModifiers:
                    board.ClearModifiers(
                        command.Coordinate);
                    return;

                case BoardMutationType.ClearCell:
                    board.ClearMarks(
                        command.Coordinate);

                    board.ClearModifiers(
                        command.Coordinate);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"A mutação {command.Type} não possui aplicação " +
                        "implementada.");
            }
        }

        private static void ApplyFinalState(
            BoardState board,
            BoardCoordinate coordinate,
            CellState desiredCell)
        {
            CellState currentCell =
                board.GetCell(coordinate);

            /*
             * Primeiro removemos marcas que não devem permanecer.
             * Depois adicionamos as marcas que faltam.
             *
             * Isso permite qualquer transição válida:
             *
             * X     -> O
             * X     -> X | O
             * X | O -> O
             * O     -> None
             */
            for (int index = 0;
                 index < SupportedMarks.Length;
                 index++)
            {
                CellMark mark =
                    SupportedMarks[index];

                bool currentlyHasMark =
                    currentCell.HasMark(mark);

                bool shouldHaveMark =
                    desiredCell.HasMark(mark);

                if (currentlyHasMark &&
                    !shouldHaveMark)
                {
                    bool removed =
                        board.RemoveMark(
                            coordinate,
                            mark);

                    EnsureExpectedChange(
                        removed,
                        coordinate,
                        $"remover a marca {mark}");
                }
            }

            for (int index = 0;
                 index < SupportedMarks.Length;
                 index++)
            {
                CellMark mark =
                    SupportedMarks[index];

                bool currentlyHasMark =
                    currentCell.HasMark(mark);

                bool shouldHaveMark =
                    desiredCell.HasMark(mark);

                if (!currentlyHasMark &&
                    shouldHaveMark)
                {
                    bool added =
                        board.AddMark(
                            coordinate,
                            mark);

                    EnsureExpectedChange(
                        added,
                        coordinate,
                        $"adicionar a marca {mark}");
                }
            }

            /*
             * Copiamos a lista antes de remover modificadores.
             *
             * Não devemos percorrer diretamente currentCell.Modifiers enquanto
             * removemos itens dela, pois modificar uma coleção durante sua
             * enumeração causaria uma exceção.
             */
            List<CellModifierId> currentModifiers =
                new List<CellModifierId>(
                    currentCell.Modifiers);

            for (int index = 0;
                 index < currentModifiers.Count;
                 index++)
            {
                CellModifierId modifier =
                    currentModifiers[index];

                if (!desiredCell.HasModifier(modifier))
                {
                    bool removed =
                        board.RemoveModifier(
                            coordinate,
                            modifier);

                    EnsureExpectedChange(
                        removed,
                        coordinate,
                        $"remover o modificador {modifier}");
                }
            }

            /*
             * A ordem da coleção de modificadores não possui significado lógico.
             * Retemos os modificadores existentes e adicionamos somente os ausentes.
             */
            for (int index = 0;
                 index < desiredCell.Modifiers.Count;
                 index++)
            {
                CellModifierId modifier =
                    desiredCell.Modifiers[index];

                if (!currentCell.HasModifier(modifier))
                {
                    bool added =
                        board.AddModifier(
                            coordinate,
                            modifier);

                    EnsureExpectedChange(
                        added,
                        coordinate,
                        $"adicionar o modificador {modifier}");
                }
            }
        }

        private static void EnsureExpectedChange(
            bool wasApplied,
            BoardCoordinate coordinate,
            string operationDescription)
        {
            if (!wasApplied)
            {
                throw new InvalidOperationException(
                    $"O BoardState deveria {operationDescription} na casa " +
                    $"{coordinate}, mas informou que nenhuma alteração ocorreu.");
            }
        }

        private static CellState CreateSnapshot(
            CellState source)
        {
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
            if (left.Modifiers.Count !=
                right.Modifiers.Count)
            {
                return false;
            }

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

        private static void ValidateResultConsistency(
            BoardState board,
            long boardVersionBefore,
            List<BoardChange> changes)
        {
            if (changes.Count == 0)
            {
                if (board.Version != boardVersionBefore)
                {
                    throw new InvalidOperationException(
                        "Nenhuma alteração final foi registrada, mas a versão " +
                        "do tabuleiro real foi modificada.");
                }

                return;
            }

            if (board.Version <= boardVersionBefore)
            {
                throw new InvalidOperationException(
                    "O BoardChangeSet possui alterações, mas a versão do " +
                    "tabuleiro não aumentou.");
            }
        }

        /*
         * Integrações futuras:
         *
         * 1. Na sessão 4.5, ActionExecutor poderá utilizar este serviço para
         *    operações autorizadas que não sejam jogadas normais.
         *
         * 2. PlaceMarkAction continuará delegando sua regra ao MoveService.
         *    BoardMutationService não deve substituir a validação de uma jogada
         *    normal.
         *
         * 3. Futuras ações de runa poderão construir vários comandos:
         *
         *    BoardMutationCommand.RemoveMark(coordinate, CellMark.O)
         *    BoardMutationCommand.AddModifier(coordinate, CellModifierId.Golden)
         *
         * 4. Uma runa que limpe uma linha poderá fornecer um ClearMarks para cada
         *    coordenada afetada. O resultado conterá apenas as casas que realmente
         *    mudaram.
         *
         * 5. Se surgirem efeitos com duração, intensidade ou origem, novos comandos
         *    deverão manipular CellEffectInstance em vez de sobrecarregar
         *    CellModifierId.
         *
         * 6. Este serviço não deve receber GameObject, MonoBehaviour, Sprite,
         *    Vector2Int ou qualquer outro tipo da Unity.
         */
    }
}