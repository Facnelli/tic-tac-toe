using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Boards;
using UnityEngine;
using UnityEngine.UI;

namespace TicTacToeRoguelike.Presentation.Board
{

    /// <summary>
    /// Constrói e atualiza a representação visual de uma BoardState.
    ///
    /// BoardView não cria regras, não escolhe símbolos e não aplica movimentos.
    /// Seu trabalho é transformar o tabuleiro lógico em objetos visuais e comunicar
    /// ao coordenador quando uma coordenada foi clicada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Referências visuais")]
        [Tooltip("Prefab que representa uma casa existente do tabuleiro.")]
        [SerializeField]
        private BoardCellView _cellPrefab;

        [Tooltip("Objeto que receberá as casas e os espaços vazios como filhos.")] [SerializeField]
        private Transform _cellContainer;

        [Tooltip("Componente que organiza os filhos do contêiner em linhas e colunas.")] [SerializeField]
        private GridLayoutGroup _gridLayout;

        private readonly Dictionary<BoardCoordinate, BoardCellView>
            _cellViews =
                new Dictionary<BoardCoordinate, BoardCellView>();

        private bool _acceptsInput = true;

        /// <summary>
        /// Evento emitido quando uma casa visual recebe um clique permitido pela
        /// camada de apresentação.
        ///
        /// O futuro EncounterController escutará este evento, escolherá o símbolo do
        /// participante atual e enviará a jogada ao MoveService. A BoardView não
        /// conhece nenhuma dessas regras.
        /// </summary>
        public event Action<BoardCoordinate> CellClicked;

        /// <summary>
        /// Estado lógico exibido no momento.
        /// Permanece nulo até RenderBoard ser chamado.
        /// </summary>
        public BoardState DisplayedBoard { get; private set; }

        /// <summary>
        /// Views existentes, indexadas pela coordenada lógica.
        /// Posições ausentes em um tabuleiro especial não aparecem neste dicionário.
        /// </summary>
        public IReadOnlyDictionary<BoardCoordinate, BoardCellView> CellViews =>
            _cellViews;

        public bool AcceptsInput => _acceptsInput;

        /// <summary>
        /// Constrói a apresentação completa de um tabuleiro lógico.
        ///
        /// A instância recebida não é clonada. BoardView guarda apenas a referência
        /// necessária para ler o estado durante as atualizações visuais; qualquer
        /// alteração continuará sendo feita por serviços do domínio.
        /// </summary>
        public void RenderBoard(BoardState board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            ValidateVisualConfiguration();
            ClearBoard();

            DisplayedBoard = board;

            /*
             * GridLayoutGroup distribui os filhos em sequência. Fixar a quantidade
             * de colunas com a largura da definição faz cada mudança de linha
             * acontecer na mesma posição lógica do tabuleiro.
             */
            _gridLayout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount =
                board.Definition.Width;

            /*
             * Percorremos todo o retângulo delimitado por Width e Height, e não
             * apenas Definition.Cells. Um tabuleiro especial pode possuir buracos.
             * Nesses pontos criamos um espaço sem imagem e sem eventos para impedir
             * que o GridLayoutGroup desloque as casas seguintes para a esquerda.
             */
            for (int y = 0; y < board.Definition.Height; y++)
            {
                for (int x = 0; x < board.Definition.Width; x++)
                {
                    BoardCoordinate coordinate =
                        new BoardCoordinate(x, y);

                    if (board.Definition.ContainsCell(coordinate))
                    {
                        CreateCellView(coordinate);
                    }
                    else
                    {
                        CreateEmptyLayoutSlot(coordinate);
                    }
                }
            }

            RefreshAllCells();
        }

        /// <summary>
        /// Atualiza todas as casas usando a BoardState atualmente exibida.
        ///
        /// É apropriado depois de uma operação que modifica várias casas, como um
        /// futuro efeito de runa aplicado a uma linha inteira.
        /// </summary>
        public void RefreshAllCells()
        {
            EnsureBoardIsDisplayed();

            foreach (BoardCoordinate coordinate
                     in DisplayedBoard.Definition.Cells)
            {
                RefreshCell(coordinate);
            }
        }

        /// <summary>
        /// Atualiza somente uma casa.
        ///
        /// Após uma jogada normal, o coordenador pode chamar este método para evitar
        /// redesenhar o tabuleiro inteiro. Uma otimização deve preservar o mesmo
        /// resultado visual; ela nunca pode introduzir uma segunda fonte de estado.
        /// </summary>
        public void RefreshCell(BoardCoordinate coordinate)
        {
            EnsureBoardIsDisplayed();

            if (!DisplayedBoard.TryGetCell(
                    coordinate,
                    out CellState cell))
            {
                throw new ArgumentException(
                    $"A coordenada {coordinate} não existe no tabuleiro exibido.",
                    nameof(coordinate));
            }

            if (!_cellViews.TryGetValue(
                    coordinate,
                    out BoardCellView cellView))
            {
                throw new InvalidOperationException(
                    $"Não existe uma BoardCellView para a casa {coordinate}.");
            }

            cellView.Render(cell);
        }

        /// <summary>
        /// Ativa ou bloqueia o encaminhamento de cliques.
        ///
        /// Esse bloqueio é apenas uma ferramenta de apresentação para turnos da IA,
        /// animações ou telas pausadas. Ele não substitui o MoveValidator: qualquer
        /// jogada encaminhada ainda precisa ser validada pelo domínio.
        /// </summary>
        public void SetInputEnabled(bool isEnabled)
        {
            _acceptsInput = isEnabled;
        }

        public bool TryGetCellView(
            BoardCoordinate coordinate,
            out BoardCellView cellView)
        {
            return _cellViews.TryGetValue(
                coordinate,
                out cellView);
        }

        /// <summary>
        /// Obtém obrigatoriamente a view de uma casa existente.
        /// Use TryGetCellView quando uma coordenada ausente for uma possibilidade
        /// normal para o chamador.
        /// </summary>
        public BoardCellView GetCellView(BoardCoordinate coordinate)
        {
            if (_cellViews.TryGetValue(
                    coordinate,
                    out BoardCellView cellView))
            {
                return cellView;
            }

            throw new KeyNotFoundException(
                $"Não existe uma BoardCellView para a coordenada {coordinate}.");
        }

        public void SetCellHighlight(
            BoardCoordinate coordinate,
            Color color)
        {
            GetCellView(coordinate).SetHighlight(color);
        }

        public void ClearCellHighlight(BoardCoordinate coordinate)
        {
            GetCellView(coordinate).ClearHighlight();
        }

        /// <summary>
        /// Remove todos os destaques temporários sem alterar o estado do tabuleiro.
        /// </summary>
        public void ClearAllHighlights()
        {
            foreach (BoardCellView cellView in _cellViews.Values)
            {
                cellView.ClearHighlight();
            }
        }

        /// <summary>
        /// Remove a representação atual e volta ao estado sem tabuleiro.
        /// </summary>
        public void ClearBoard()
        {
            foreach (BoardCellView cellView in _cellViews.Values)
            {
                /*
                 * Remover a inscrição deixa explícito que a ligação terminou.
                 * Também prepara o código para um futuro pooling, no qual a view não
                 * será destruída e poderá ser inscrita em outra BoardView.
                 */
                cellView.Clicked -= HandleCellClicked;
            }

            _cellViews.Clear();
            DisplayedBoard = null;

            if (_cellContainer == null)
            {
                return;
            }

            /*
             * Destroy conclui a remoção somente no fim do frame. Desativar o filho
             * primeiro evita que casas antigas e novas apareçam juntas por um frame
             * quando RenderBoard troca o tabuleiro exibido.
             */
            for (int childIndex = _cellContainer.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                GameObject child =
                    _cellContainer.GetChild(childIndex).gameObject;

                child.SetActive(false);
                Destroy(child);
            }
        }

        private void CreateCellView(BoardCoordinate coordinate)
        {
            BoardCellView cellView =
                Instantiate(_cellPrefab, _cellContainer);

            cellView.name =
                $"Cell ({coordinate.X}, {coordinate.Y})";

            cellView.Initialize(coordinate);
            cellView.Clicked += HandleCellClicked;

            _cellViews.Add(coordinate, cellView);
        }

        private void CreateEmptyLayoutSlot(BoardCoordinate coordinate)
        {
            GameObject emptySlot =
                new GameObject(
                    $"Empty Slot ({coordinate.X}, {coordinate.Y})",
                    typeof(RectTransform));

            emptySlot.transform.SetParent(
                _cellContainer,
                false);

            /*
             * O espaço não recebe Image, Button ou BoardCellView. Portanto, ele não
             * é clicável e não representa uma casa lógica; serve somente para o
             * posicionamento dentro do GridLayoutGroup.
             *
             * Aqui poderá ser adicionada futuramente uma view própria para posições
             * bloqueadas caso algum boss queira mostrar pedras, bordas ou portais no
             * lugar dos buracos atualmente invisíveis.
             */
        }

        private void HandleCellClicked(BoardCoordinate coordinate)
        {
            if (!_acceptsInput)
            {
                return;
            }

            CellClicked?.Invoke(coordinate);
        }

        private void EnsureBoardIsDisplayed()
        {
            if (DisplayedBoard != null)
            {
                return;
            }

            throw new InvalidOperationException(
                "BoardView precisa receber uma BoardState por RenderBoard antes " +
                "de atualizar suas casas.");
        }

        private void ValidateVisualConfiguration()
        {
            if (_cellPrefab == null)
            {
                throw new InvalidOperationException(
                    "Informe o prefab de casa no campo Cell Prefab da BoardView.");
            }

            if (_cellContainer == null)
            {
                throw new InvalidOperationException(
                    "Informe o contêiner das casas no campo Cell Container da " +
                    "BoardView.");
            }

            if (_gridLayout == null)
            {
                throw new InvalidOperationException(
                    "Informe o GridLayoutGroup no campo Grid Layout da BoardView.");
            }

            /*
             * GridLayoutGroup organiza apenas os filhos diretos do GameObject no
             * qual está instalado. Exigir o mesmo Transform evita uma configuração
             * que compila normalmente, mas deixa as casas sem organização visual.
             */
            if (_gridLayout.transform != _cellContainer)
            {
                throw new InvalidOperationException(
                    "Grid Layout precisa estar no mesmo GameObject informado em " +
                    "Cell Container.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_cellPrefab == null)
            {
                Debug.LogWarning(
                    "Informe o prefab de casa na BoardView.",
                    this);
            }

            if (_cellContainer == null)
            {
                Debug.LogWarning(
                    "Informe o contêiner das casas na BoardView.",
                    this);
            }

            if (_gridLayout == null)
            {
                Debug.LogWarning(
                    "Informe o GridLayoutGroup na BoardView.",
                    this);
            }
            else if (_cellContainer != null &&
                     _gridLayout.transform != _cellContainer)
            {
                Debug.LogWarning(
                    "Grid Layout deve estar no mesmo GameObject usado como " +
                    "Cell Container.",
                    this);
            }
        }
#endif
    }
}