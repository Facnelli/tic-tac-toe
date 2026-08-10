using System;
using TMPro;
using TicTacToeRoguelike.Domain.Boards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TicTacToeRoguelike.Presentation.Board
{

    /// <summary>
    /// Representa visualmente uma única casa do tabuleiro.
    ///
    /// Esta classe pertence somente à camada de apresentação. Ela recebe uma
    /// CellState pronta, desenha seus símbolos e modificadores e informa quando a
    /// casa foi clicada. Ela não altera a BoardState e não decide se uma jogada é
    /// válida, pois essas responsabilidades pertencem ao domínio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardCellView :
        MonoBehaviour,
        IPointerClickHandler
    {
        [Header("Referências visuais")] [Tooltip("Texto usado para exibir o símbolo O.")] [SerializeField]
        private TextMeshProUGUI _oText;

        [Tooltip("Texto usado para exibir o símbolo X.")] [SerializeField]
        private TextMeshProUGUI _xText;

        [Tooltip("Imagem que recebe a cor base, modificadores e destaques.")] [SerializeField]
        private Image _backgroundImage;

        [Header("Cores da casa")]
        [Tooltip("Cor usada quando a casa não possui um modificador visual.")]
        [SerializeField]
        private Color _normalColor = Color.white;

        [Tooltip("Cor usada enquanto a casa possuir o modificador Golden.")] [SerializeField]
        private Color _goldenColor = new Color(1f, 0.84f, 0f, 1f);

        private BoardCoordinate _coordinate;
        private bool _isInitialized;

        /*
         * Estes campos guardam apenas o último estado desenhado na tela.
         *
         * Eles são necessários para recompor a aparência quando um destaque
         * temporário termina, mas não são uma segunda fonte de regras. Sistemas de
         * jogo nunca devem consultá-los para descobrir o conteúdo real da casa.
         */
        private CellMark _renderedMarks = CellMark.None;
        private bool _rendersGoldenModifier;

        private bool _isHighlighted;
        private Color _highlightColor;

        /// <summary>
        /// Informa que esta casa foi clicada.
        ///
        /// O evento carrega somente a coordenada. Ele não escolhe X ou O porque o
        /// símbolo depende do participante e do turno, informações que pertencem ao
        /// futuro coordenador da partida.
        /// </summary>
        public event Action<BoardCoordinate> Clicked;

        /// <summary>
        /// Coordenada lógica representada por este objeto visual.
        /// </summary>
        public BoardCoordinate Coordinate
        {
            get
            {
                EnsureIsInitialized();
                return _coordinate;
            }
        }

        /// <summary>
        /// Associa o prefab instanciado a uma coordenada do domínio.
        ///
        /// A limpeza do estado visual também prepara esta classe para um futuro
        /// sistema de pooling, no qual a mesma view poderá ser reutilizada em outra
        /// posição sem conservar símbolos ou destaques da utilização anterior.
        /// </summary>
        public void Initialize(BoardCoordinate coordinate)
        {
            ValidateVisualReferences();

            _coordinate = coordinate;
            _isInitialized = true;

            _renderedMarks = CellMark.None;
            _rendersGoldenModifier = false;
            _isHighlighted = false;

            RefreshMarkVisuals();
            RefreshBackgroundVisual();
        }

        /// <summary>
        /// Atualiza toda a aparência da casa a partir do estado lógico recebido.
        /// </summary>
        public void Render(CellState cell)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            EnsureIsInitialized();

            /*
             * Esta verificação detecta um erro de sincronização difícil de notar:
             * desenhar, por exemplo, os dados de (0, 0) no objeto visual de (1, 0).
             * O problema poderia aparecer durante uma atualização parcial do
             * tabuleiro e deixar a tela diferente da BoardState verdadeira.
             */
            if (cell.Coordinate != _coordinate)
            {
                throw new ArgumentException(
                    $"A view {_coordinate} não pode renderizar a casa " +
                    $"{cell.Coordinate}.",
                    nameof(cell));
            }

            _renderedMarks = cell.Marks;
            _rendersGoldenModifier =
                cell.HasModifier(CellModifierId.Golden);

            RefreshMarkVisuals();
            RefreshBackgroundVisual();
        }

        /// <summary>
        /// Aplica uma cor temporária sem modificar a CellState.
        ///
        /// Pode ser usado por animações de sequência, sugestões da IA ou efeitos
        /// visuais de runas. O destaque é apresentação, não um modificador lógico.
        /// </summary>
        public void SetHighlight(Color color)
        {
            EnsureIsInitialized();

            _isHighlighted = true;
            _highlightColor = color;
            RefreshBackgroundVisual();
        }

        /// <summary>
        /// Remove o destaque e restaura a aparência derivada da última CellState.
        /// Uma casa dourada, por exemplo, volta a ser dourada.
        /// </summary>
        public void ClearHighlight()
        {
            EnsureIsInitialized();

            _isHighlighted = false;
            RefreshBackgroundVisual();
        }

        /// <summary>
        /// Chamado automaticamente pelo EventSystem da Unity ao receber clique ou
        /// toque sobre a imagem deste objeto.
        ///
        /// Casas ocupadas e modificadores não são verificados aqui. Toda intenção
        /// aceita pelo coordenador seguirá para o mesmo MoveService, que consultará
        /// o MoveValidator antes de modificar o tabuleiro.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            EnsureIsInitialized();
            Clicked?.Invoke(_coordinate);
        }

        private void RefreshMarkVisuals()
        {
            /*
             * CellMark usa [Flags]. O operador & verifica cada bit separadamente.
             * Por isso, uma casa com CellMark.X | CellMark.O mostra os dois textos,
             * enquanto CellMark.None não mostra nenhum deles.
             */
            bool showsX =
                (_renderedMarks & CellMark.X) == CellMark.X;

            bool showsO =
                (_renderedMarks & CellMark.O) == CellMark.O;

            _xText.enabled = showsX;
            _oText.enabled = showsO;
        }

        private void RefreshBackgroundVisual()
        {
            /*
             * O destaque possui prioridade apenas na apresentação. O modificador
             * Golden continua guardado em _rendersGoldenModifier e reaparece quando
             * ClearHighlight é executado.
             *
             * Aqui poderá ser implementada futuramente uma composição de efeitos
             * visuais para modificadores como Frozen, Burning ou Corrupted. Quando
             * muitos efeitos coexistirem, recomenda-se delegar essa composição a
             * um componente visual específico em vez de criar uma sequência extensa
             * de ifs nesta classe.
             */
            if (_isHighlighted)
            {
                _backgroundImage.color = _highlightColor;
                return;
            }

            _backgroundImage.color =
                _rendersGoldenModifier
                    ? _goldenColor
                    : _normalColor;
        }

        private void EnsureIsInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            throw new InvalidOperationException(
                "BoardCellView precisa ser inicializada pela BoardView antes " +
                "de receber estado, destaque ou clique.");
        }

        private void ValidateVisualReferences()
        {
            if (_xText == null)
            {
                throw new InvalidOperationException(
                    "Informe o texto de X no prefab da BoardCellView.");
            }

            if (_oText == null)
            {
                throw new InvalidOperationException(
                    "Informe o texto de O no prefab da BoardCellView.");
            }

            if (_backgroundImage == null)
            {
                throw new InvalidOperationException(
                    "Informe a imagem de fundo no prefab da BoardCellView.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            /*
             * OnValidate roda somente no Editor. Os avisos ajudam a descobrir um
             * prefab incompleto antes de iniciar a partida, mas não impedem que o
             * prefab seja configurado gradualmente no Inspector.
             */
            if (_xText == null)
            {
                Debug.LogWarning(
                    "Informe o texto de X na BoardCellView.",
                    this);
            }

            if (_oText == null)
            {
                Debug.LogWarning(
                    "Informe o texto de O na BoardCellView.",
                    this);
            }

            if (_backgroundImage == null)
            {
                Debug.LogWarning(
                    "Informe a imagem de fundo na BoardCellView.",
                    this);
            }
        }
#endif
    }
}