using System;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Domain.Moves
{
    /// <summary>
    /// Executa jogadas normais sobre o estado lógico do tabuleiro.
    ///
    /// Este serviço reúne duas etapas que precisam permanecer juntas:
    ///
    /// 1. verificar se a jogada é permitida;
    /// 2. aplicar o símbolo somente quando a validação for aprovada.
    ///
    /// Dessa maneira, os sistemas que coordenam o jogador e a IA não precisam
    /// acessar diretamente os métodos internos de alteração do BoardState.
    ///
    /// Este serviço não controla turnos, pontuação, sequências, animações ou
    /// condições de encerramento da rodada.
    /// </summary>
    public sealed class MoveService
    {
        private readonly MoveValidator _validator;

        /// <summary>
        /// Cria o serviço com um MoveValidator padrão.
        ///
        /// Este construtor é conveniente enquanto o projeto ainda não possui um
        /// local central responsável por montar e conectar os serviços do domínio.
        /// </summary>
        public MoveService()
            : this(new MoveValidator())
        {
        }

        /// <summary>
        /// Cria o serviço com um validador fornecido externamente.
        ///
        /// Receber a dependência pelo construtor facilita testes e permite que,
        /// futuramente, uma configuração da partida forneça um validador preparado
        /// com regras específicas do encontro.
        /// </summary>
        public MoveService(MoveValidator validator)
        {
            _validator = validator ??
                throw new ArgumentNullException(nameof(validator));
        }

        /// <summary>
        /// Tenta aplicar uma jogada normal no tabuleiro.
        ///
        /// Quando a jogada for rejeitada:
        /// - o resultado explicará o motivo;
        /// - nenhuma alteração será realizada;
        /// - BoardState.Version permanecerá igual.
        ///
        /// Quando a jogada for permitida:
        /// - o símbolo será adicionado à casa;
        /// - BoardState.Version será incrementada;
        /// - o resultado retornado terá IsValid igual a true.
        ///
        /// O nome começa com Try porque uma rejeição é uma possibilidade normal
        /// durante a partida e, portanto, não deve lançar uma exceção.
        /// </summary>
        public MoveValidationResult TryApplyNormalMove(
            BoardState board,
            BoardCoordinate coordinate,
            CellMark mark)
        {
            /*
             * A validação sempre acontece antes da alteração.
             *
             * Isso é especialmente importante para a casa dourada: AddMark sabe
             * adicionar um símbolo preservando o anterior, mas não sabe decidir se
             * aquela sobreposição é permitida. Essa decisão pertence ao validador.
             */
            MoveValidationResult validation =
                _validator.ValidateNormalMove(
                    board,
                    coordinate,
                    mark);

            if (!validation.IsValid)
            {
                /*
                 * Uma jogada rejeitada termina aqui.
                 *
                 * Como BoardState ainda não foi alterado, não precisamos desfazer
                 * nada e seu número de versão permanece inalterado.
                 */
                return validation;
            }

            /*
             * Se o validador aprovou a jogada, a casa existe e ainda não contém
             * o símbolo solicitado. Portanto, AddMark deve realizar uma alteração.
             */
            bool wasApplied = board.AddMark(
                coordinate,
                mark);

            if (!wasApplied)
            {
                /*
                 * Chegar a este ponto indicaria uma inconsistência entre as regras
                 * do MoveValidator e o comportamento do BoardState.
                 *
                 * Isso representa um erro de programação, e não uma tentativa
                 * inválida comum do jogador. Por esse motivo lançamos uma exceção
                 * em vez de devolver um resultado rejeitado.
                 */
                throw new InvalidOperationException(
                    "A jogada foi aprovada pelo MoveValidator, mas o " +
                    "BoardState não aplicou o símbolo.");
            }

            /*
             * Neste momento, IsValid também significa que a jogada foi aplicada.
             *
             * Não existe uma etapa posterior que ainda possa rejeitá-la dentro
             * deste serviço.
             */
            return validation;
        }

        /// <summary>
        /// Sobrecarga conveniente para sistemas que já possuem X e Y separados.
        ///
        /// Ela cria a BoardCoordinate e encaminha todo o trabalho ao método
        /// principal, evitando duplicar as regras de execução.
        /// </summary>
        public MoveValidationResult TryApplyNormalMove(
            BoardState board,
            int x,
            int y,
            CellMark mark)
        {
            return TryApplyNormalMove(
                board,
                new BoardCoordinate(x, y),
                mark);
        }

        /*
         * Adições futuras:
         *
         * 1. Poderá ser criado um MoveExecutionResult contendo, além da validação,
         *    o estado anterior e posterior da casa. A camada de apresentação poderá
         *    usar esses dados para escolher animações sem consultar estados antigos.
         *
         * 2. Uma jogada aplicada poderá produzir um evento de domínio, como
         *    NormalMoveApplied. O coordenador da rodada poderá encaminhar esse evento
         *    para animações, áudio, tutoriais e reações de runas.
         *
         * 3. Poderá existir um MoveContext contendo participante, origem da ação,
         *    turno, runas ativas e identificador da jogada.
         *
         * 4. Efeitos que alteram várias casas poderão utilizar um serviço separado
         *    ou uma operação composta. Eles não devem fingir ser jogadas normais,
         *    pois podem obedecer a regras diferentes.
         *
         * 5. Caso futuras regras possam alterar o tabuleiro durante a própria
         *    validação, poderá ser implementada uma proteção baseada em
         *    BoardState.Version para garantir que o estado não mudou entre validar
         *    e aplicar.
         */
    }
}