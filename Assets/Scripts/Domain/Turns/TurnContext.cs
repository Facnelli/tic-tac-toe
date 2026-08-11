using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Turns
{
    /// <summary>
    /// Representa o estado atual de um Turno.
    ///
    /// Open:
    /// o participante ainda pode executar ações.
    ///
    /// Completed:
    /// o participante executou ações reais e consumiu todo o orçamento.
    /// Somente este estado poderá alimentar a avaliação de reação.
    ///
    /// Skipped:
    /// uma regra impediu o participante de aproveitar sua oportunidade.
    /// Um Turno pulado não representa uma tentativa de reação.
    ///
    /// Cancelled:
    /// o Turno foi abandonado antes de qualquer ação autoritativa, por exemplo
    /// porque o encontro foi reiniciado ou invalidado.
    /// </summary>
    public enum TurnStatus
    {
        Open,
        Completed,
        Skipped,
        Cancelled
    }

    /// <summary>
    /// Representa uma oportunidade de jogo de um único participante.
    ///
    /// Um Turno pode conter uma ou mais ações. Por exemplo:
    ///
    /// - com orçamento 1, colocar uma peça conclui o Turno;
    /// - com orçamento 2, a primeira ação mantém o Turno aberto;
    /// - a segunda ação conclui o mesmo Turno.
    ///
    /// Portanto, duas ações dentro deste objeto continuam pertencendo ao mesmo
    /// Turno. Dois TurnContext diferentes representam duas oportunidades
    /// diferentes, mesmo quando pertencem ao mesmo participante.
    ///
    /// Esta classe não escolhe quem joga depois. Escolher o próximo participante
    /// será responsabilidade do futuro fluxo do encontro, que precisará considerar
    /// reação, Turnos extras e efeitos de pulo.
    /// </summary>
    public sealed class TurnContext
    {
        /// <summary>
        /// Identidade estável deste Turno dentro do encontro.
        ///
        /// O futuro EncounterEngine deverá fornecer números crescentes:
        /// 1, 2, 3...
        ///
        /// Dessa maneira, dois Turnos consecutivos do mesmo participante não
        /// poderão ser confundidos entre si.
        /// </summary>
        public long TurnId { get; }

        /// <summary>
        /// Participante que possui esta oportunidade.
        ///
        /// Apenas Player e Enemy podem possuir um Turno. Environment e None
        /// não representam participantes capazes de executar ações.
        /// </summary>
        public ScoreActor Actor { get; }

        /// <summary>
        /// Quantidade máxima de ações que podem ser aplicadas neste Turno.
        ///
        /// O valor padrão é 1, preservando o comportamento atual do jogo.
        /// Futuras runas ou regras poderão criar um Turno com orçamento maior.
        /// </summary>
        public int ActionBudget { get; }

        /// <summary>
        /// Quantidade de ações aplicadas com sucesso neste Turno.
        ///
        /// Uma tentativa rejeitada não deve chamar RegisterAppliedAction e,
        /// portanto, não consome este contador.
        /// </summary>
        public int ActionsConsumed { get; private set; }

        /// <summary>
        /// Quantidade de ações que ainda podem ser aplicadas.
        /// </summary>
        public int RemainingActions =>
            ActionBudget - ActionsConsumed;

        /// <summary>
        /// Versão do tabuleiro no instante em que o Turno foi aberto.
        ///
        /// Essa informação permitirá comprovar quais alterações aconteceram
        /// durante a oportunidade.
        /// </summary>
        public long InitialBoardVersion { get; }

        /// <summary>
        /// Versão mais recente do tabuleiro conhecida por este Turno.
        ///
        /// Enquanto nenhuma ação for registrada, será igual a
        /// InitialBoardVersion. Depois de encerrado, representará a versão final
        /// usada pelo futuro TurnCompletion.
        /// </summary>
        public long CurrentBoardVersion { get; private set; }

        /// <summary>
        /// Estado atual do Turno.
        /// </summary>
        public TurnStatus Status { get; private set; }

        /// <summary>
        /// Informa se o Turno ainda aceita ações.
        /// </summary>
        public bool IsOpen =>
            Status == TurnStatus.Open;

        /// <summary>
        /// Informa se o Turno alcançou algum estado terminal.
        /// </summary>
        public bool IsClosed =>
            Status != TurnStatus.Open;

        /// <summary>
        /// Informa se pelo menos uma ação foi realmente aplicada.
        /// </summary>
        public bool HasPlayedAction =>
            ActionsConsumed > 0;

        /// <summary>
        /// Informa se ainda existe orçamento disponível.
        ///
        /// Um Turno fechado nunca aceita outra ação, mesmo que seu contador
        /// indique que parte do orçamento não foi utilizada.
        /// </summary>
        public bool HasRemainingActions =>
            IsOpen &&
            RemainingActions > 0;

        /// <summary>
        /// Somente um Turno concluído por ações reais poderá ocasionar uma
        /// avaliação da ReactionRule.
        ///
        /// Turnos pulados e cancelados retornam false.
        /// </summary>
        public bool CanEvaluateReaction =>
            Status == TurnStatus.Completed &&
            HasPlayedAction;

        /// <summary>
        /// Cria uma oportunidade de jogo.
        /// </summary>
        /// <param name="actor">
        /// Participante responsável pelo Turno.
        /// </param>
        /// <param name="turnId">
        /// Identidade positiva e crescente fornecida pelo fluxo do encontro.
        /// </param>
        /// <param name="initialBoardVersion">
        /// Versão do tabuleiro quando o Turno foi aberto.
        /// </param>
        /// <param name="actionBudget">
        /// Quantidade máxima de ações. O padrão preserva um lance por Turno.
        /// </param>
        public TurnContext(
            ScoreActor actor,
            long turnId,
            long initialBoardVersion,
            int actionBudget = 1)
        {
            ValidateActor(actor);

            if (turnId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnId),
                    turnId,
                    "A identidade do Turno precisa ser maior que zero.");
            }

            if (initialBoardVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialBoardVersion),
                    initialBoardVersion,
                    "A versão inicial do tabuleiro não pode ser negativa.");
            }

            if (actionBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionBudget),
                    actionBudget,
                    "O orçamento do Turno precisa possuir ao menos uma ação.");
            }

            Actor = actor;
            TurnId = turnId;
            InitialBoardVersion = initialBoardVersion;
            CurrentBoardVersion = initialBoardVersion;
            ActionBudget = actionBudget;
            ActionsConsumed = 0;
            Status = TurnStatus.Open;
        }

        /// <summary>
        /// Registra uma ação que já foi validada e aplicada com sucesso.
        ///
        /// Este método não executa a ação e não modifica o BoardState. No Marco 4,
        /// o ActionExecutor será responsável por:
        ///
        /// 1. validar a intenção;
        /// 2. tentar aplicá-la;
        /// 3. produzir um ActionResult;
        /// 4. chamar este método somente quando o resultado for aplicado.
        ///
        /// Assim, ações rejeitadas nunca consomem orçamento.
        /// </summary>
        /// <param name="boardVersionAfterAction">
        /// Versão do tabuleiro observada depois da ação.
        ///
        /// Versões iguais são permitidas porque uma ação futura pode alterar
        /// outros estados, como pontuação ou runas, sem modificar o tabuleiro.
        /// Uma versão mais antiga nunca é aceita.
        /// </param>
        /// <returns>
        /// True quando esta ação consumiu a última unidade do orçamento e
        /// concluiu o Turno; false quando ainda há ações disponíveis.
        /// </returns>
        public bool RegisterAppliedAction(
            long boardVersionAfterAction)
        {
            EnsureOpen();

            ValidateBoardVersion(
                boardVersionAfterAction,
                nameof(boardVersionAfterAction));

            ActionsConsumed++;
            CurrentBoardVersion = boardVersionAfterAction;

            if (ActionsConsumed == ActionBudget)
            {
                Status = TurnStatus.Completed;
            }

            return Status == TurnStatus.Completed;
        }

        /// <summary>
        /// Encerra a oportunidade porque o participante foi obrigado a pular.
        ///
        /// Um Turno só pode ser pulado antes de executar ações. Permitir o pulo
        /// depois de uma ação esconderia uma alteração real do tabuleiro e poderia
        /// impedir indevidamente a avaliação da reação.
        /// </summary>
        public void Skip(long finalBoardVersion)
        {
            EnsureCanEndWithoutPlayedAction();

            ValidateBoardVersion(
                finalBoardVersion,
                nameof(finalBoardVersion));

            CurrentBoardVersion = finalBoardVersion;
            Status = TurnStatus.Skipped;
        }

        /// <summary>
        /// Cancela um Turno que ainda não executou nenhuma ação.
        ///
        /// Cancelamento representa invalidação do fluxo, não uma decisão de jogo.
        /// Por isso também não pode alimentar a ReactionRule.
        /// </summary>
        public void Cancel(long finalBoardVersion)
        {
            EnsureCanEndWithoutPlayedAction();

            ValidateBoardVersion(
                finalBoardVersion,
                nameof(finalBoardVersion));

            CurrentBoardVersion = finalBoardVersion;
            Status = TurnStatus.Cancelled;
        }

        private static void ValidateActor(ScoreActor actor)
        {
            if (actor != ScoreActor.Player &&
                actor != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actor),
                    actor,
                    "Somente Player ou Enemy podem possuir um Turno.");
            }
        }

        private void ValidateBoardVersion(
            long boardVersion,
            string parameterName)
        {
            if (boardVersion < CurrentBoardVersion)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    boardVersion,
                    "A versão do tabuleiro não pode regredir durante " +
                    "o mesmo Turno.");
            }
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException(
                    $"O Turno {TurnId} já está encerrado com o estado " +
                    $"{Status} e não aceita novas ações.");
            }
        }

        private void EnsureCanEndWithoutPlayedAction()
        {
            EnsureOpen();

            if (HasPlayedAction)
            {
                throw new InvalidOperationException(
                    $"O Turno {TurnId} já aplicou uma ação e não pode ser " +
                    "marcado como pulado ou cancelado.");
            }
        }

        /*
         * Integração deste Marco:
         *
         * TurnCompletion.CreateFrom cria uma fotografia imutável depois que
         * este contexto alcança Completed, Skipped ou Cancelled.
         *
         * Adições futuras:
         *
         * 1. ActionExecutor chamará RegisterAppliedAction somente depois de
         *    receber um ActionResult aplicado com sucesso.
         *
         * 2. Efeitos de runa poderão alterar ActionBudget ao criar o próximo
         *    TurnContext. O orçamento não deve ser alterado no meio do Turno.
         *
         * 3. Turnos extras deverão criar outro TurnContext e outro TurnId.
         *    Aumentar o orçamento deste objeto significaria ações extras no mesmo
         *    Turno, que possui comportamento diferente para a reação.
         *
         * 4. O próximo participante não será armazenado aqui. Essa escolha será
         *    realizada pelo futuro EncounterEngine.
         */
    }
}