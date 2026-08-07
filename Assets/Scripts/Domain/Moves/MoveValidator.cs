using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.Moves
{
    /// <summary>
    /// Explica por que uma jogada normal foi rejeitada.
    ///
    /// Manter o motivo separado de uma mensagem textual permite que diferentes
    /// sistemas reajam à rejeição sem depender de frases específicas.
    ///
    /// Por exemplo:
    /// - a interface poderá mostrar uma mensagem traduzida;
    /// - a IA poderá simplesmente ignorar a coordenada;
    /// - os testes poderão verificar exatamente qual regra impediu a jogada.
    /// </summary>
    public enum MoveRejectionReason
    {
        /// <summary>
        /// Indica que nenhuma regra rejeitou a jogada.
        /// </summary>
        None,

        /// <summary>
        /// A coordenada não corresponde a uma casa existente no tabuleiro.
        ///
        /// Isso também cobre posições que estão dentro dos limites retangulares,
        /// mas foram removidas pela BoardDefinition.
        /// </summary>
        CellDoesNotExist,

        /// <summary>
        /// A casa já contém o símbolo que está tentando ser colocado.
        /// </summary>
        MarkAlreadyPresent,

        /// <summary>
        /// A casa está ocupada e não possui uma regra que permita sobreposição.
        /// </summary>
        OccupiedCellDoesNotAllowOverlap
    }

    /// <summary>
    /// Resultado imutável da validação de uma jogada.
    ///
    /// Um simples bool informaria apenas se a jogada é válida ou inválida.
    /// Este tipo também conserva o motivo de uma possível rejeição.
    ///
    /// readonly struct significa que o resultado é um pequeno valor imutável.
    /// Depois de criado, as suas propriedades não podem ser alteradas.
    /// </summary>
    public readonly struct MoveValidationResult
    {
        /// <summary>
        /// Informa se a jogada passou por todas as regras atuais.
        /// </summary>
        public bool IsValid =>
            RejectionReason == MoveRejectionReason.None;

        /// <summary>
        /// Motivo da rejeição.
        ///
        /// Quando IsValid for true, este valor será None.
        /// </summary>
        public MoveRejectionReason RejectionReason { get; }

        private MoveValidationResult(
            MoveRejectionReason rejectionReason)
        {
            RejectionReason = rejectionReason;
        }

        /// <summary>
        /// Cria o resultado de uma jogada permitida.
        /// </summary>
        public static MoveValidationResult Allowed()
        {
            return new MoveValidationResult(
                MoveRejectionReason.None);
        }

        /// <summary>
        /// Cria o resultado de uma jogada rejeitada.
        ///
        /// None não pode ser usado como motivo de rejeição, pois representa
        /// justamente a ausência de erro.
        /// </summary>
        public static MoveValidationResult Rejected(
            MoveRejectionReason reason)
        {
            if (reason == MoveRejectionReason.None)
            {
                throw new ArgumentException(
                    "Uma jogada rejeitada precisa possuir um motivo.",
                    nameof(reason));
            }

            /*
             * Enums aceitam conversões numéricas. Sem esta verificação seria
             * possível criar algo como (MoveRejectionReason)100, mesmo que esse
             * valor não tenha sido declarado.
             */
            if (!Enum.IsDefined(
                typeof(MoveRejectionReason),
                reason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "O motivo de rejeição informado não é reconhecido.");
            }

            return new MoveValidationResult(reason);
        }
    }

    /// <summary>
    /// Determina se jogadas normais podem ser realizadas no tabuleiro.
    ///
    /// Uma jogada normal é a colocação de um único símbolo durante o turno de
    /// um participante. Alterações especiais causadas por runas não precisam
    /// necessariamente obedecer às mesmas regras.
    ///
    /// Esta classe apenas consulta o BoardState. Ela não adiciona símbolos,
    /// não troca turnos e não inicia animações.
    /// </summary>
    public sealed class MoveValidator
    {
        /// <summary>
        /// Valida a colocação de um símbolo em uma coordenada.
        ///
        /// O resultado informa tanto se a jogada é permitida quanto o motivo
        /// de uma possível rejeição.
        /// </summary>
        public MoveValidationResult ValidateNormalMove(
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark)
        {
            if (board == null)
            {
                /*
                 * Um BoardState nulo representa um erro de programação, e não uma
                 * jogada inválida realizada normalmente durante a partida.
                 *
                 * Por isso lançamos uma exceção em vez de retornar uma rejeição.
                 */
                throw new ArgumentNullException(nameof(board));
            }

            ValidateSingleMark(mark);

            /*
             * TryGetCell é usado porque uma coordenada inexistente pode vir de uma
             * entrada do jogador, de uma IA ou de um tabuleiro com casas removidas.
             *
             * Nesse caso, a inexistência é tratada como uma jogada rejeitada,
             * sem interromper a execução do jogo com uma exceção.
             */
            if (!board.TryGetCell(
                coordinate,
                out CellState cell))
            {
                return MoveValidationResult.Rejected(
                    MoveRejectionReason.CellDoesNotExist);
            }

            /*
             * Esta verificação precisa ocorrer antes das regras de casa vazia e
             * casa dourada.
             *
             * Mesmo uma casa dourada não pode receber X novamente se já possui X.
             * A sobreposição permitida por ela é entre símbolos diferentes.
             */
            if (cell.HasMark(mark))
            {
                return MoveValidationResult.Rejected(
                    MoveRejectionReason.MarkAlreadyPresent);
            }

            /*
             * Toda casa vazia aceita uma jogada normal.
             *
             * Não precisamos verificar o modificador nesse caso, pois ser dourada
             * ou comum não muda a regra básica de uma casa vazia.
             */
            if (cell.IsEmpty)
            {
                return MoveValidationResult.Allowed();
            }

            /*
             * Se chegamos até aqui, a casa:
             * - existe;
             * - já está ocupada;
             * - ainda não possui o símbolo da nova jogada.
             *
             * A casa dourada permite acrescentar esse segundo símbolo. O operador
             * de alteração continuará sendo CellState.AddMark, portanto o símbolo
             * anterior será preservado e a casa passará a conter X e O.
             */
            if (cell.HasModifier(CellModifierId.Golden))
            {
                return MoveValidationResult.Allowed();
            }

            /*
             * Uma casa comum ocupada não permite que outro símbolo seja colocado
             * sobre o símbolo existente.
             */
            return MoveValidationResult.Rejected(
                MoveRejectionReason.OccupiedCellDoesNotAllowOverlap);
        }

        /// <summary>
        /// Atalho para situações em que apenas true ou false é necessário.
        ///
        /// A interface poderá usar este método para habilitar ou desabilitar uma
        /// interação. Quando o motivo da rejeição for importante, deve utilizar
        /// ValidateNormalMove diretamente.
        /// </summary>
        public bool CanPlayNormalMove(
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark)
        {
            return ValidateNormalMove(
                board,
                coordinate,
                mark).IsValid;
        }

        /// <summary>
        /// Retorna todas as coordenadas nas quais o símbolo pode realizar uma
        /// jogada normal.
        ///
        /// A ordem segue BoardDefinition.Cells, que já possui uma organização
        /// determinística. Isso ajuda a manter testes e decisões da IA reproduzíveis.
        /// </summary>
        public IReadOnlyList<BoardCoordinate> GetLegalNormalMoves(
            BoardState board,
            CellMark mark)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            ValidateSingleMark(mark);

            List<BoardCoordinate> legalMoves =
                new List<BoardCoordinate>();

            /*
             * Percorremos somente as casas existentes na definição.
             *
             * Isso evita testar todas as posições de Width por Height em tabuleiros
             * especiais que possuam buracos ou formatos não retangulares.
             */
            foreach (BoardCoordinate coordinate in board.Definition.Cells)
            {
                if (CanPlayNormalMove(
                    board,
                    coordinate,
                    mark))
                {
                    legalMoves.Add(coordinate);
                }
            }

            /*
             * A lista é criada exclusivamente para esta consulta.
             *
             * Mesmo que quem recebeu o resultado crie ou altere sua própria cópia,
             * isso não modifica o BoardState. O tipo IReadOnlyList também comunica
             * que o objetivo do retorno é apenas consultar as jogadas encontradas.
             */
            return legalMoves;
        }

        /// <summary>
        /// Garante que uma jogada esteja tentando colocar exatamente um símbolo.
        ///
        /// CellMark usa Flags, portanto CellMark.X | CellMark.O é uma combinação
        /// válida para representar o conteúdo de uma casa. Porém, uma jogada normal
        /// deve colocar somente X ou somente O por vez.
        /// </summary>
        private static void ValidateSingleMark(CellMark mark)
        {
            if (mark != CellMark.X &&
                mark != CellMark.O)
            {
                throw new ArgumentException(
                    "Uma jogada normal deve colocar exatamente um símbolo: X ou O.",
                    nameof(mark));
            }
        }

        /*
         * Adições futuras:
         *
         * 1. Regras de encontro poderão bloquear determinadas casas, impedir
         *    jogadas durante certas fases ou permitir outros tipos de sobreposição.
         *
         * 2. Poderá ser criado um MoveValidationContext com informações como:
         *    participante, turno, origem da ação, runas ativas e fase da rodada.
         *    Isso permitirá validar regras que não dependem somente do tabuleiro.
         *
         * 3. Modificadores como Frozen ou Locked poderão produzir novos valores em
         *    MoveRejectionReason. A interface poderá então mostrar uma explicação
         *    específica ao jogador.
         *
         * 4. Regras adicionais poderão ser separadas em objetos como IMoveRule.
         *    O MoveValidator percorreria essas regras sem crescer indefinidamente
         *    conforme novas runas e encontros fossem implementados.
         *
         * 5. Jogadas especiais de runas poderão possuir um validador separado.
         *    Uma runa que destrói símbolos, por exemplo, não deve ser impedida pela
         *    regra usada para colocar normalmente X ou O.
         */
    }
}