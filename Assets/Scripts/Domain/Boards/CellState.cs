using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TicTacToeRoguelike.Domain.Boards
{
    /// <summary>
    /// Representa os símbolos presentes em uma casa.
    ///
    /// O atributo Flags permite combinar valores do enum usando o operador |.
    /// Dessa forma, uma casa pode conter X, O ou os dois símbolos sem precisar
    /// de um estado separado chamado Both.
    /// </summary>
    [Flags]
    public enum CellMark
    {
        None = 0,
        X = 1 << 0,
        O = 1 << 1
    }

    /// <summary>
    /// Identifica modificadores permanentes ou semipermanentes aplicados
    /// diretamente a uma casa.
    ///
    /// Não existe um valor None porque a ausência de modificadores é
    /// representada por uma coleção vazia.
    /// </summary>
    public enum CellModifierId
    {
        Golden

        /*
         * Adições futuras:
         *
         * Outros modificadores simples poderão ser adicionados aqui, como:
         * Frozen,
         * Burning,
         * Locked.
         *
         * Modificadores que precisem guardar valores, duração ou origem não
         * deverão ser representados somente por este enum. Para esses casos,
         * poderá ser criada futuramente uma classe CellEffectInstance.
         */
    }

    /// <summary>
    /// Guarda o estado lógico atual de uma casa existente no tabuleiro.
    ///
    /// Esta classe não possui referências a textos, imagens, cores, prefabs
    /// ou animações. A camada visual apenas consultará este estado para decidir
    /// o que deve exibir.
    /// </summary>
    public sealed class CellState
    {
        /*
         * Máscara contendo todos os símbolos reconhecidos atualmente.
         *
         * Ela é usada para impedir que um valor inválido, como (CellMark)100,
         * seja armazenado. Enums podem receber conversões numéricas mesmo que
         * o número não corresponda a um valor declarado.
         */
        private const CellMark SupportedMarks =
            CellMark.X |
            CellMark.O;

        private readonly List<CellModifierId> _modifiers;
        private readonly ReadOnlyCollection<CellModifierId> _readOnlyModifiers;

        /// <summary>
        /// Posição desta casa dentro do tabuleiro.
        ///
        /// A CellState conhece sua coordenada, mas não sabe se ela pertence a
        /// determinado BoardDefinition. O futuro BoardState fará essa validação
        /// ao criar todas as casas.
        /// </summary>
        public BoardCoordinate Coordinate { get; }

        /// <summary>
        /// Combinação dos símbolos presentes atualmente na casa.
        /// </summary>
        public CellMark Marks { get; private set; }

        /// <summary>
        /// Lista somente para leitura dos modificadores aplicados à casa.
        ///
        /// ReadOnlyCollection impede que sistemas externos alterem a lista
        /// diretamente e contornem as regras desta classe.
        /// </summary>
        public IReadOnlyList<CellModifierId> Modifiers =>
            _readOnlyModifiers;

        public bool IsEmpty => Marks == CellMark.None;

        public bool ContainsBothMarks =>
            Marks == SupportedMarks;

        public CellState(
            BoardCoordinate coordinate,
            CellMark initialMarks = CellMark.None,
            IEnumerable<CellModifierId> initialModifiers = null)
        {
            ValidateMarkCombination(initialMarks);

            Coordinate = coordinate;
            Marks = initialMarks;

            _modifiers = new List<CellModifierId>();
            _readOnlyModifiers =
                new ReadOnlyCollection<CellModifierId>(_modifiers);

            if (initialModifiers == null)
            {
                return;
            }

            foreach (CellModifierId modifier in initialModifiers)
            {
                ValidateModifier(modifier);

                /*
                 * Uma casa não deve receber duas vezes o mesmo identificador.
                 *
                 * Se futuramente um efeito puder acumular várias aplicações,
                 * cada aplicação deverá ser uma CellEffectInstance diferente,
                 * contendo sua própria intensidade, origem e duração.
                 */
                if (_modifiers.Contains(modifier))
                {
                    throw new ArgumentException(
                        $"O modificador {modifier} foi informado mais de uma vez.",
                        nameof(initialModifiers));
                }

                _modifiers.Add(modifier);
            }
        }

        /// <summary>
        /// Informa se determinado símbolo está presente na casa.
        ///
        /// Este método recebe apenas um símbolo por vez. Para verificar se a
        /// casa contém X e O, consulte cada símbolo separadamente ou utilize
        /// ContainsBothMarks.
        /// </summary>
        public bool HasMark(CellMark mark)
        {
            ValidateSingleMark(mark);

            /*
             * O operador & mantém somente os bits compartilhados.
             *
             * Exemplo:
             * Marks possui X e O;
             * Marks & CellMark.X resulta em X;
             * portanto, a casa contém X.
             */
            return (Marks & mark) == mark;
        }

        /// <summary>
        /// Informa se a casa possui determinado modificador.
        /// </summary>
        public bool HasModifier(CellModifierId modifier)
        {
            ValidateModifier(modifier);
            return _modifiers.Contains(modifier);
        }

        /// <summary>
        /// Adiciona um símbolo sem remover os símbolos que já estão na casa.
        ///
        /// Este método não verifica se a jogada é legal. Por exemplo, ele não
        /// decide se uma casa ocupada permite sobreposição. Essa regra pertence
        /// ao MoveValidator.
        ///
        /// Retorna false se o símbolo já estava presente.
        /// </summary>
        internal bool AddMark(CellMark mark)
        {
            ValidateSingleMark(mark);

            if (HasMark(mark))
            {
                return false;
            }

            /*
             * O operador | adiciona o bit do novo símbolo e preserva os demais.
             *
             * Se Marks for X e mark for O, o resultado conterá X e O.
             */
            Marks |= mark;
            return true;
        }

        /// <summary>
        /// Remove somente o símbolo informado e preserva qualquer outro.
        ///
        /// Isso será importante para runas capazes de remover o símbolo do
        /// jogador ou do inimigo sem esvaziar completamente a casa.
        ///
        /// Retorna false se o símbolo não estava presente.
        /// </summary>
        internal bool RemoveMark(CellMark mark)
        {
            ValidateSingleMark(mark);

            if (!HasMark(mark))
            {
                return false;
            }

            /*
             * ~mark inverte os bits do símbolo e o operador & mantém todos os
             * demais. Na prática, isso desliga somente o símbolo solicitado.
             */
            Marks &= ~mark;
            return true;
        }

        /// <summary>
        /// Remove todos os símbolos da casa.
        ///
        /// Retorna false quando a casa já estava vazia.
        /// </summary>
        internal bool ClearMarks()
        {
            if (IsEmpty)
            {
                return false;
            }

            Marks = CellMark.None;
            return true;
        }

        /// <summary>
        /// Adiciona um modificador à casa.
        ///
        /// Retorna false se o modificador já estava aplicado.
        /// </summary>
        internal bool AddModifier(CellModifierId modifier)
        {
            ValidateModifier(modifier);

            if (_modifiers.Contains(modifier))
            {
                return false;
            }

            _modifiers.Add(modifier);
            return true;
        }

        /// <summary>
        /// Remove um modificador da casa.
        ///
        /// Retorna false se o modificador não estava aplicado.
        /// </summary>
        internal bool RemoveModifier(CellModifierId modifier)
        {
            ValidateModifier(modifier);
            return _modifiers.Remove(modifier);
        }

        /// <summary>
        /// Remove todos os modificadores da casa.
        ///
        /// Retorna false quando não havia nenhum modificador.
        /// </summary>
        internal bool ClearModifiers()
        {
            if (_modifiers.Count == 0)
            {
                return false;
            }

            _modifiers.Clear();
            return true;
        }

        /// <summary>
        /// Produz uma cópia independente desta casa.
        ///
        /// A IA poderá usar cópias do BoardState para simular jogadas sem
        /// modificar acidentalmente o tabuleiro real da partida.
        /// </summary>
        internal CellState Clone()
        {
            return new CellState(
                Coordinate,
                Marks,
                _modifiers);
        }

        private static void ValidateSingleMark(CellMark mark)
        {
            if (mark != CellMark.X && mark != CellMark.O)
            {
                throw new ArgumentException(
                    "A operação deve receber exatamente um símbolo: X ou O.",
                    nameof(mark));
            }
        }

        private static void ValidateMarkCombination(CellMark marks)
        {
            /*
             * Se algum bit permanecer depois de remover os bits suportados,
             * significa que o valor contém um símbolo desconhecido.
             */
            if ((marks & ~SupportedMarks) != 0)
            {
                throw new ArgumentException(
                    $"A combinação de símbolos {marks} contém valores desconhecidos.",
                    nameof(marks));
            }
        }

        private static void ValidateModifier(CellModifierId modifier)
        {
            if (!Enum.IsDefined(typeof(CellModifierId), modifier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modifier),
                    modifier,
                    "O modificador informado não é reconhecido.");
            }
        }

        /*
         * Adições futuras:
         *
         * 1. CellEffectInstance poderá representar efeitos com duração,
         *    intensidade, origem e possibilidade de acumulação.
         *
         * 2. Um snapshot serializável poderá ser criado para salvar a partida.
         *
         * 3. Outros símbolos poderão ser adicionados a CellMark caso um modo
         *    futuro utilize mais de dois participantes. Nesse caso também será
         *    necessário atualizar SupportedMarks.
         *
         * 4. Alterações poderão gerar registros de domínio para animações.
         *    Por exemplo, RemoveMark poderá produzir uma informação que permita
         *    à apresentação tocar o efeito visual de um símbolo sendo destruído.
         */
    }
}