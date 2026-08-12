using System;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Explica por que uma GameAction não foi aplicada.
    ///
    /// Rejeições representam situações previstas durante o jogo.
    /// Inconsistências de programação continuam sendo representadas por exceções.
    /// </summary>
    public enum ActionRejectionReason
    {
        /// <summary>
        /// Nenhuma regra rejeitou a ação.
        ///
        /// Este valor aparece somente em resultados aplicados.
        /// </summary>
        None = 0,

        /// <summary>
        /// O encontro está em uma fase que não aceita ações.
        /// </summary>
        EncounterDoesNotAcceptActions = 1,

        /// <summary>
        /// Não existe um Turno aberto capaz de receber a ação.
        /// </summary>
        TurnIsNotOpen = 2,

        /// <summary>
        /// A ação foi criada para outro Turno.
        /// </summary>
        TurnIdDoesNotMatch = 3,

        /// <summary>
        /// O ator da ação não possui o Turno atual.
        /// </summary>
        ActorDoesNotOwnTurn = 4,

        /// <summary>
        /// O tabuleiro mudou depois que a intenção foi criada.
        /// </summary>
        BoardVersionDoesNotMatch = 5,

        /// <summary>
        /// O executor não reconhece a categoria da ação.
        /// </summary>
        ActionTypeNotSupported = 6,

        /// <summary>
        /// A marca solicitada não pertence ao ator da ação.
        /// </summary>
        MarkDoesNotBelongToActor = 7,

        /// <summary>
        /// A coordenada não corresponde a uma casa existente.
        /// </summary>
        CellDoesNotExist = 8,

        /// <summary>
        /// A casa já contém a marca solicitada.
        /// </summary>
        MarkAlreadyPresent = 9,

        /// <summary>
        /// A casa ocupada não permite sobreposição.
        /// </summary>
        OccupiedCellDoesNotAllowOverlap = 10,

        /// <summary>
        /// A ação deixou de estar disponível no estado atual.
        /// </summary>
        ActionNotAvailable = 11
    }

    /// <summary>
    /// Relatório imutável da tentativa de executar uma GameAction.
    ///
    /// O resultado informa:
    ///
    /// - qual intenção foi processada;
    /// - se ela foi aplicada ou rejeitada;
    /// - qual regra causou a rejeição;
    /// - as versões do tabuleiro antes e depois;
    /// - quais casas realmente mudaram.
    ///
    /// Esta classe não altera BoardState, não consome TurnContext e não decide
    /// qual participante jogará em seguida.
    /// </summary>
    public sealed class ActionResult
    {
        /// <summary>
        /// Intenção que produziu este resultado.
        /// </summary>
        public GameAction Action { get; }

        public bool WasApplied =>
            RejectionReason == ActionRejectionReason.None;

        public bool WasRejected =>
            !WasApplied;

        /// <summary>
        /// Motivo da rejeição.
        ///
        /// Será None quando a ação tiver sido aplicada.
        /// </summary>
        public ActionRejectionReason RejectionReason { get; }

        /// <summary>
        /// Versão real imediatamente antes da tentativa.
        /// </summary>
        public long BoardVersionBefore { get; }

        /// <summary>
        /// Versão real depois da tentativa.
        ///
        /// Em uma rejeição, será igual a BoardVersionBefore.
        /// </summary>
        public long BoardVersionAfter { get; }

        /// <summary>
        /// Casas alteradas pela ação.
        ///
        /// Resultados rejeitados sempre utilizam BoardChangeSet.Empty.
        /// Ações aplicadas que não alterem o tabuleiro também podem usar Empty.
        /// </summary>
        public BoardChangeSet BoardChanges { get; }

        /// <summary>
        /// Informa se o BoardState sofreu alguma alteração.
        /// </summary>
        public bool DidChangeBoard =>
            BoardVersionAfter > BoardVersionBefore;

        private ActionResult(
            GameAction action,
            ActionRejectionReason rejectionReason,
            long boardVersionBefore,
            long boardVersionAfter,
            BoardChangeSet boardChanges)
        {
            Action = action;
            RejectionReason = rejectionReason;
            BoardVersionBefore = boardVersionBefore;
            BoardVersionAfter = boardVersionAfter;
            BoardChanges = boardChanges;
        }

        /// <summary>
        /// Cria o relatório de uma ação aplicada.
        ///
        /// A versão esperada pela ação precisa corresponder à versão real anterior.
        ///
        /// Quando a versão aumentar, BoardChanges precisa conter ao menos uma
        /// alteração. Quando a versão permanecer igual, o conjunto precisa estar
        /// vazio.
        /// </summary>
        public static ActionResult Applied(
            GameAction action,
            long boardVersionBefore,
            long boardVersionAfter,
            BoardChangeSet boardChanges)
        {
            ValidateAction(action);

            ValidateBoardVersion(
                boardVersionBefore,
                nameof(boardVersionBefore));

            ValidateBoardVersion(
                boardVersionAfter,
                nameof(boardVersionAfter));

            if (boardChanges == null)
            {
                throw new ArgumentNullException(nameof(boardChanges));
            }

            /*
             * Uma intenção somente pode ser aplicada sobre a versão para a qual
             * foi criada.
             */
            if (action.ExpectedBoardVersion != boardVersionBefore)
            {
                throw new ArgumentException(
                    $"A ação esperava a versão " +
                    $"{action.ExpectedBoardVersion}, mas a execução começou na " +
                    $"versão {boardVersionBefore}. Uma ação desatualizada deve " +
                    $"produzir um resultado rejeitado.",
                    nameof(boardVersionBefore));
            }

            if (boardVersionAfter < boardVersionBefore)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boardVersionAfter),
                    boardVersionAfter,
                    "A versão posterior não pode ser menor que a anterior.");
            }

            ValidateBoardChanges(
                boardVersionBefore,
                boardVersionAfter,
                boardChanges);

            return new ActionResult(
                action,
                ActionRejectionReason.None,
                boardVersionBefore,
                boardVersionAfter,
                boardChanges);
        }

        /// <summary>
        /// Cria o relatório de uma ação rejeitada.
        ///
        /// Uma rejeição não altera versão e não produz BoardChange.
        /// </summary>
        public static ActionResult Rejected(
            GameAction action,
            ActionRejectionReason reason,
            long boardVersion)
        {
            ValidateAction(action);
            ValidateRejectionReason(reason);

            ValidateBoardVersion(
                boardVersion,
                nameof(boardVersion));

            return new ActionResult(
                action,
                reason,
                boardVersion,
                boardVersion,
                BoardChangeSet.Empty);
        }

        private static void ValidateAction(
            GameAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
        }

        private static void ValidateRejectionReason(
            ActionRejectionReason reason)
        {
            if (reason == ActionRejectionReason.None)
            {
                throw new ArgumentException(
                    "Uma ação rejeitada precisa possuir um motivo.",
                    nameof(reason));
            }

            if (!Enum.IsDefined(
                    typeof(ActionRejectionReason),
                    reason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "O motivo de rejeição informado não é reconhecido.");
            }
        }

        private static void ValidateBoardVersion(
            long boardVersion,
            string parameterName)
        {
            if (boardVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    boardVersion,
                    "A versão do tabuleiro não pode ser negativa.");
            }
        }

        private static void ValidateBoardChanges(
            long boardVersionBefore,
            long boardVersionAfter,
            BoardChangeSet boardChanges)
        {
            bool versionChanged =
                boardVersionAfter > boardVersionBefore;

            /*
             * Se a versão aumentou, alguma casa precisa ter mudado.
             *
             * Isso impede que o relatório diga que o tabuleiro mudou sem
             * informar à apresentação quais casas devem ser atualizadas.
             */
            if (versionChanged && boardChanges.IsEmpty)
            {
                throw new ArgumentException(
                    "Uma alteração de versão precisa possuir ao menos um " +
                    "BoardChange.",
                    nameof(boardChanges));
            }

            /*
             * Se a versão não mudou, não pode existir uma alteração de casa.
             *
             * Ações futuras que modificarem somente pontuação, inventário ou
             * Turnos serão aplicadas com a mesma versão e BoardChangeSet.Empty.
             */
            if (!versionChanged && !boardChanges.IsEmpty)
            {
                throw new ArgumentException(
                    "BoardChangeSet precisa estar vazio quando a versão do " +
                    "tabuleiro não mudou.",
                    nameof(boardChanges));
            }
        }

        /*
         * Integrações futuras:
         *
         * 1. BoardMutationService produzirá BoardChangeSet para alterações
         *    compostas.
         *
         * 2. ActionExecutor converterá o resultado de MoveService em
         *    ActionResult.
         *
         * 3. Somente ActionResult.WasApplied poderá chamar
         *    TurnContext.RegisterAppliedAction.
         *
         * 4. A apresentação poderá percorrer BoardChanges sem recalcular as
         *    regras que produziram as alterações.
         *
         * 5. Mensagens traduzidas para o jogador continuarão pertencendo à
         *    camada de apresentação.
         */
    }
}