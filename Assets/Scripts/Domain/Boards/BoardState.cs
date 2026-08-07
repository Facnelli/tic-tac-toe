using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TicTacToeRoguelike.Domain.Boards
{
    /// <summary>
    /// Representa o estado lógico completo do tabuleiro durante uma rodada.
    ///
    /// BoardDefinition descreve o formato do tabuleiro.
    /// BoardState guarda o conteúdo atual das casas desse formato.
    ///
    /// Esta classe é a fonte de verdade do tabuleiro. A interface visual deve
    /// consultar este estado e representá-lo, mas não deve manter uma segunda
    /// cópia autoritativa dos símbolos ou modificadores.
    /// </summary>
    public sealed class BoardState
    {
        private readonly Dictionary<BoardCoordinate, CellState> _cells;

        private readonly ReadOnlyDictionary<BoardCoordinate, CellState>
            _readOnlyCells;

        /// <summary>
        /// Definição imutável que informa o formato deste tabuleiro.
        ///
        /// Duas rodadas diferentes podem possuir estados diferentes usando a
        /// mesma definição, desde que compartilhem o mesmo formato.
        /// </summary>
        public BoardDefinition Definition { get; }

        /// <summary>
        /// Casas existentes no tabuleiro, organizadas por coordenada.
        ///
        /// IReadOnlyDictionary permite consultar as casas, mas não permite
        /// adicionar, substituir ou remover entradas diretamente.
        /// </summary>
        public IReadOnlyDictionary<BoardCoordinate, CellState> Cells =>
            _readOnlyCells;

        /// <summary>
        /// Número de casas que realmente existem no tabuleiro.
        ///
        /// Em um tabuleiro especial esse número pode ser menor que
        /// Width multiplicado por Height, pois podem existir posições bloqueadas.
        /// </summary>
        public int CellCount => _cells.Count;

        /// <summary>
        /// Número incrementado sempre que ocorre uma alteração real no tabuleiro.
        ///
        /// Se uma operação não modificar nada, a versão permanece igual.
        /// Por exemplo, tentar adicionar X a uma casa que já contém X não aumenta
        /// a versão.
        ///
        /// O futuro SequenceEvaluator poderá usar esse número para descobrir se
        /// uma avaliação armazenada em cache ainda corresponde ao tabuleiro atual.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>
        /// Cria um estado vazio seguindo o formato de uma BoardDefinition.
        ///
        /// initialCells é opcional e permite criar casas que já começam com
        /// símbolos ou modificadores. Isso será usado, por exemplo, para iniciar
        /// o tabuleiro atual com uma casa dourada.
        ///
        /// As casas não informadas em initialCells serão criadas vazias e sem
        /// modificadores.
        /// </summary>
        public BoardState(
            BoardDefinition definition,
            IEnumerable<CellState> initialCells = null)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));

            /*
             * Este dicionário temporário serve para validar as casas iniciais antes
             * de construir o estado definitivo.
             *
             * Ele também permite encontrar cada casa inicial rapidamente pela sua
             * coordenada, sem precisar percorrer a coleção várias vezes.
             */
            Dictionary<BoardCoordinate, CellState> initialCellLookup =
                new Dictionary<BoardCoordinate, CellState>();

            if (initialCells != null)
            {
                foreach (CellState initialCell in initialCells)
                {
                    if (initialCell == null)
                    {
                        throw new ArgumentException(
                            "A coleção de casas iniciais não pode conter uma casa nula.",
                            nameof(initialCells));
                    }

                    if (!Definition.ContainsCell(initialCell.Coordinate))
                    {
                        throw new ArgumentException(
                            $"A casa inicial {initialCell.Coordinate} não existe " +
                            "na definição deste tabuleiro.",
                            nameof(initialCells));
                    }

                    /*
                     * Dictionary.Add lançaria uma exceção genérica caso a coordenada
                     * fosse repetida. TryAdd não está disponível em todas as versões
                     * de C# usadas pela Unity, então verificamos manualmente.
                     */
                    if (initialCellLookup.ContainsKey(initialCell.Coordinate))
                    {
                        throw new ArgumentException(
                            $"A casa inicial {initialCell.Coordinate} foi " +
                            "informada mais de uma vez.",
                            nameof(initialCells));
                    }

                    initialCellLookup.Add(
                        initialCell.Coordinate,
                        initialCell);
                }
            }

            _cells = new Dictionary<BoardCoordinate, CellState>(
                Definition.CellCount);

            /*
             * Percorremos Definition.Cells, em vez de percorrer initialCells,
             * porque a definição é quem determina quais casas realmente existem.
             *
             * Dessa maneira, até as casas que não possuem um estado inicial
             * informado são criadas corretamente.
             */
            foreach (BoardCoordinate coordinate in Definition.Cells)
            {
                CellState cell;

                if (initialCellLookup.TryGetValue(
                    coordinate,
                    out CellState initialCell))
                {
                    /*
                     * Criamos uma cópia da casa recebida.
                     *
                     * Se guardássemos a mesma instância, quem criou o BoardState
                     * ainda possuiria uma referência para aquela casa. Uma alteração
                     * nessa referência poderia modificar o tabuleiro por fora.
                     */
                    cell = initialCell.Clone();
                }
                else
                {
                    cell = new CellState(coordinate);
                }

                _cells.Add(coordinate, cell);
            }

            /*
             * ReadOnlyDictionary é uma camada somente para leitura sobre o
             * dicionário verdadeiro. Ele não cria outra cópia dos dados.
             *
             * Portanto, quando BoardState altera _cells, as consultas feitas por
             * Cells enxergam imediatamente o novo estado.
             */
            _readOnlyCells =
                new ReadOnlyDictionary<BoardCoordinate, CellState>(_cells);

            /*
             * Os dados fornecidos no construtor são considerados o estado inicial
             * do tabuleiro, e não alterações realizadas durante a rodada.
             */
            Version = 0;
        }

        /// <summary>
        /// Informa se existe uma casa jogável na coordenada.
        ///
        /// Esse método não verifica se a casa está vazia. Ele verifica apenas se
        /// a coordenada faz parte do formato definido para o tabuleiro.
        /// </summary>
        public bool ContainsCell(BoardCoordinate coordinate)
        {
            return _cells.ContainsKey(coordinate);
        }

        public bool ContainsCell(int x, int y)
        {
            return ContainsCell(new BoardCoordinate(x, y));
        }

        /// <summary>
        /// Obtém uma casa existente.
        ///
        /// Uma exceção é lançada se a coordenada não fizer parte do tabuleiro.
        /// Use TryGetCell quando uma coordenada inexistente for uma possibilidade
        /// normal, como durante a procura de sequências nas bordas.
        /// </summary>
        public CellState GetCell(BoardCoordinate coordinate)
        {
            return GetRequiredCell(coordinate);
        }

        public CellState GetCell(int x, int y)
        {
            return GetCell(new BoardCoordinate(x, y));
        }

        /// <summary>
        /// Tenta encontrar uma casa sem lançar exceção.
        ///
        /// Retorna true e preenche cell quando a casa existe.
        /// Retorna false e deixa cell como null quando ela não existe.
        ///
        /// Esse padrão chamado Try é comum em C#: uma falha esperada deixa de ser
        /// tratada como erro excepcional.
        /// </summary>
        public bool TryGetCell(
            BoardCoordinate coordinate,
            out CellState cell)
        {
            return _cells.TryGetValue(coordinate, out cell);
        }

        public bool TryGetCell(
            int x,
            int y,
            out CellState cell)
        {
            return TryGetCell(
                new BoardCoordinate(x, y),
                out cell);
        }

        /// <summary>
        /// Cria uma cópia independente do tabuleiro inteiro.
        ///
        /// A definição pode ser compartilhada porque BoardDefinition é imutável.
        /// As CellState, porém, precisam ser copiadas porque guardam dados que
        /// mudam durante a rodada.
        ///
        /// A IA poderá alterar essa cópia para testar jogadas sem modificar o
        /// tabuleiro real mostrado ao jogador.
        /// </summary>
        public BoardState Clone()
        {
            BoardState clone = new BoardState(
                Definition,
                _cells.Values);

            /*
             * A cópia representa exatamente o mesmo momento lógico do tabuleiro,
             * portanto começa com a mesma versão.
             *
             * Depois disso, cada tabuleiro incrementará sua própria Version
             * independentemente.
             */
            clone.Version = Version;

            return clone;
        }

        /// <summary>
        /// Adiciona um símbolo a uma casa existente.
        ///
        /// Este método não decide se a jogada é permitida. O futuro serviço de
        /// ações legais deverá consultar MoveValidator antes de chamá-lo.
        ///
        /// Retorna true somente quando o tabuleiro foi realmente alterado.
        /// </summary>
        internal bool AddMark(
            BoardCoordinate coordinate,
            CellMark mark)
        {
            CellState cell = GetRequiredCell(coordinate);
            bool changed = cell.AddMark(mark);

            return RegisterChange(changed);
        }

        /// <summary>
        /// Remove somente o símbolo informado de uma casa.
        ///
        /// Isso poderá ser usado por runas que removem o símbolo de um ator sem
        /// necessariamente esvaziar completamente a casa.
        /// </summary>
        internal bool RemoveMark(
            BoardCoordinate coordinate,
            CellMark mark)
        {
            CellState cell = GetRequiredCell(coordinate);
            bool changed = cell.RemoveMark(mark);

            return RegisterChange(changed);
        }

        /// <summary>
        /// Remove todos os símbolos de uma casa.
        ///
        /// Os modificadores da casa são preservados. Uma casa dourada continuará
        /// dourada mesmo depois que seus símbolos forem removidos.
        /// </summary>
        internal bool ClearMarks(BoardCoordinate coordinate)
        {
            CellState cell = GetRequiredCell(coordinate);
            bool changed = cell.ClearMarks();

            return RegisterChange(changed);
        }

        /// <summary>
        /// Adiciona um modificador lógico a uma casa.
        ///
        /// Retorna false quando o modificador já estava presente.
        /// </summary>
        internal bool AddModifier(
            BoardCoordinate coordinate,
            CellModifierId modifier)
        {
            CellState cell = GetRequiredCell(coordinate);
            bool changed = cell.AddModifier(modifier);

            return RegisterChange(changed);
        }

        /// <summary>
        /// Remove um modificador lógico de uma casa.
        ///
        /// Os símbolos presentes na casa são preservados.
        /// </summary>
        internal bool RemoveModifier(
            BoardCoordinate coordinate,
            CellModifierId modifier)
        {
            CellState cell = GetRequiredCell(coordinate);
            bool changed = cell.RemoveModifier(modifier);

            return RegisterChange(changed);
        }

        /// <summary>
        /// Remove todos os modificadores de uma casa.
        ///
        /// Os símbolos presentes na casa são preservados.
        /// </summary>
        internal bool ClearModifiers(BoardCoordinate coordinate)
        {
            CellState cell = GetRequiredCell(coordinate);
            bool changed = cell.ClearModifiers();

            return RegisterChange(changed);
        }

        /// <summary>
        /// Obtém uma casa que obrigatoriamente precisa existir.
        ///
        /// As operações de alteração utilizam este método porque tentar modificar
        /// uma coordenada inexistente representa um erro de programação, e não uma
        /// situação normal da partida.
        /// </summary>
        private CellState GetRequiredCell(BoardCoordinate coordinate)
        {
            if (_cells.TryGetValue(coordinate, out CellState cell))
            {
                return cell;
            }

            throw new KeyNotFoundException(
                $"A coordenada {coordinate} não corresponde a uma casa " +
                "existente neste tabuleiro.");
        }

        /// <summary>
        /// Registra uma alteração bem-sucedida e devolve o mesmo resultado.
        ///
        /// Centralizar esse comportamento evita esquecer de atualizar Version
        /// quando novos tipos de alteração forem adicionados.
        /// </summary>
        private bool RegisterChange(bool changed)
        {
            if (changed)
            {
                Version++;
            }

            return changed;
        }

        /*
         * Adições futuras:
         *
         * 1. Poderá ser criado um BoardChangeSet para reunir várias mudanças em
         *    uma única operação. Uma runa que remova uma linha inteira poderá,
         *    por exemplo, gerar uma mudança composta em vez de várias mudanças
         *    isoladas.
         *
         * 2. As alterações poderão produzir eventos de domínio contendo casa,
         *    estado anterior, estado posterior e origem da mudança. A camada
         *    visual poderá usar esses eventos para tocar animações na ordem certa.
         *
         * 3. Poderá ser criado um BoardStateSnapshot contendo apenas dados
         *    serializáveis para implementar salvamento e carregamento da run.
         *
         * 4. Um identificador próprio do BoardState poderá ser adicionado caso
         *    sistemas de cache precisem distinguir o tabuleiro real das cópias
         *    utilizadas pela IA.
         *
         * 5. Se uma futura mecânica alterar o próprio formato durante a rodada,
         *    deverá ser definida uma operação específica para substituir ou
         *    reconstruir a BoardDefinition. Não se deve adicionar ou remover
         *    entradas diretamente de _cells.
         */
    }
}