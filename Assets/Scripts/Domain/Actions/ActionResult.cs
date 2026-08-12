using System;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Explica por que uma GameAction não foi aplicada.
    ///
    /// O motivo é representado por enum, em vez de uma mensagem textual, para
    /// permitir que:
    ///
    /// - testes verifiquem a regra exata;
    /// - a interface escolha uma mensagem traduzida;
    /// - a IA reconheça decisões desatualizadas;
    /// - logs e replays armazenem um valor estável;
    /// - diferentes tipos de ação usem o mesmo relatório.
    ///
    /// Uma rejeição representa uma situação prevista durante o jogo.
    /// Inconsistências internas de programação devem lançar exceções.
    /// </summary>
    public enum ActionRejectionReason
    {
        /// <summary>
        /// Nenhuma regra rejeitou a ação.
        ///
        /// Este valor aparece somente em ActionResult aplicado.
        /// </summary>
        None = 0,

        /// <summary>
        /// O encontro está em uma fase que não aceita ações.
        ///
        /// Por exemplo: apresentação de pontuação, resolução de dano,
        /// encerramento da rodada ou escolha de recompensa.
        /// </summary>
        EncounterDoesNotAcceptActions = 1,

        /// <summary>
        /// Não existe um Turno aberto capaz de receber a ação.
        ///
        /// Isso cobre um TurnContext ausente, concluído, pulado ou cancelado.
        /// </summary>
        TurnIsNotOpen = 2,

        /// <summary>
        /// A ação foi criada para um Turno diferente do Turno atual.
        ///
        /// Mesmo que os dois Turnos pertençam ao mesmo ator, eles representam
        /// oportunidades diferentes e possuem TurnId distintos.
        /// </summary>
        TurnIdDoesNotMatch = 3,

        /// <summary>
        /// O ator da ação não é o participante que possui o Turno atual.
        /// </summary>
        ActorDoesNotOwnTurn = 4,

        /// <summary>
        /// O tabuleiro mudou depois que a intenção foi criada.
        ///
        /// A ação deve ser descartada e, se necessário, calculada novamente
        /// usando o estado atual.
        /// </summary>
        BoardVersionDoesNotMatch = 5,

        /// <summary>
        /// O executor ainda não reconhece a categoria da ação recebida.
        /// </summary>
        ActionTypeNotSupported = 6,

        /// <summary>
        /// A marca solicitada não pertence ao ator da ação.
        ///
        /// PlaceMarkAction garante apenas que a marca é X ou O. A relação entre
        /// participante e marca depende da configuração do encontro e será
        /// validada pelo ActionExecutor.
        /// </summary>
        MarkDoesNotBelongToActor = 7,

        /// <summary>
        /// A coordenada informada não corresponde a uma casa existente.
        ///
        /// Esse motivo corresponde a MoveRejectionReason.CellDoesNotExist.
        /// </summary>
        CellDoesNotExist = 8,

        /// <summary>
        /// A casa já contém a marca solicitada.
        ///
        /// Esse motivo corresponde a MoveRejectionReason.MarkAlreadyPresent.
        /// </summary>
        MarkAlreadyPresent = 9,

        /// <summary>
        /// A casa está ocupada e não permite sobreposição.
        ///
        /// Esse motivo corresponde a
        /// MoveRejectionReason.OccupiedCellDoesNotAllowOverlap.
        /// </summary>
        OccupiedCellDoesNotAllowOverlap = 10,

        /// <summary>
        /// A ação existe, mas deixou de estar disponível no estado atual.
        ///
        /// Este motivo poderá ser usado por futuras ações de runa quando seus
        /// requisitos deixarem de ser atendidos antes da execução.
        /// </summary>
        ActionNotAvailable = 11
    }

    /// <summary>
    /// Relatório imutável da tentativa de executar uma GameAction.
    ///
    /// GameAction representa uma intenção. ActionResult registra o que realmente
    /// aconteceu quando essa intenção foi entregue ao futuro ActionExecutor.
    ///
    /// Um resultado possui exatamente uma das situações:
    ///
    /// - aplicado: RejectionReason é None;
    /// - rejeitado: RejectionReason possui um motivo específico.
    ///
    /// Uma ação rejeitada conserva a mesma versão antes e depois. Assim, ela não
    /// pode esconder uma alteração parcial no tabuleiro.
    ///
    /// Esta classe não modifica BoardState, não consome o TurnContext, não escolhe
    /// o próximo participante e não avalia reação.
    /// </summary>
    public sealed class ActionResult
    {
        /// <summary>
        /// Intenção que produziu este resultado.
        ///
        /// GameAction é imutável, portanto pode ser armazenada com segurança no
        /// relatório.
        /// </summary>
        public GameAction Action { get; }

        /// <summary>
        /// Informa que a ação foi aceita e aplicada pelo serviço autorizado.
        ///
        /// Uma ação aplicada poderá ou não alterar o tabuleiro. Futuras ações
        /// poderão modificar pontuação, runas ou outro estado sem aumentar a
        /// BoardState.Version.
        /// </summary>
        public bool WasApplied =>
            RejectionReason == ActionRejectionReason.None;

        /// <summary>
        /// Informa que a ação não foi aplicada.
        ///
        /// Resultados rejeitados não devem consumir o orçamento do TurnContext.
        /// </summary>
        public bool WasRejected =>
            !WasApplied;

        /// <summary>
        /// Motivo da rejeição.
        ///
        /// Quando WasApplied for true, este valor será None.
        /// </summary>
        public ActionRejectionReason RejectionReason { get; }

        /// <summary>
        /// Versão real do tabuleiro imediatamente antes da tentativa.
        ///
        /// Ela não é necessariamente igual a Action.ExpectedBoardVersion. Quando
        /// for diferente, a ação estará desatualizada e deverá ser rejeitada.
        /// </summary>
        public long BoardVersionBefore { get; }

        /// <summary>
        /// Versão real do tabuleiro depois da tentativa.
        ///
        /// Em uma rejeição, este número será obrigatoriamente igual a
        /// BoardVersionBefore.
        ///
        /// Em uma ação aplicada, ele pode ser maior ou permanecer igual, mas nunca
        /// pode ser menor.
        /// </summary>
        public long BoardVersionAfter { get; }

        /// <summary>
        /// Informa se a versão do tabuleiro mudou durante a execução.
        ///
        /// WasApplied pode ser true enquanto DidChangeBoard é false, pois uma ação
        /// futura poderá modificar outro estado do encontro.
        /// </summary>
        public bool DidChangeBoard =>
            BoardVersionAfter > BoardVersionBefore;

        private ActionResult(
            GameAction action,
            ActionRejectionReason rejectionReason,
            long boardVersionBefore,
            long boardVersionAfter)
        {
            Action = action;
            RejectionReason = rejectionReason;
            BoardVersionBefore = boardVersionBefore;
            BoardVersionAfter = boardVersionAfter;
        }

        /// <summary>
        /// Cria o relatório de uma ação aplicada.
        ///
        /// A versão esperada pela ação precisa ser igual à versão observada antes
        /// da execução. Se forem diferentes, o executor deve produzir um resultado
        /// rejeitado com BoardVersionDoesNotMatch.
        ///
        /// boardVersionAfter pode ser igual a boardVersionBefore. Isso permite
        /// representar ações que alteram pontuação, runas ou outro estado sem
        /// modificar o tabuleiro.
        /// </summary>
        /// <param name="action">
        /// Ação que foi aplicada.
        /// </param>
        /// <param name="boardVersionBefore">
        /// Versão real antes da aplicação.
        /// </param>
        /// <param name="boardVersionAfter">
        /// Versão real depois da aplicação.
        /// </param>
        public static ActionResult Applied(
            GameAction action,
            long boardVersionBefore,
            long boardVersionAfter)
        {
            ValidateAction(action);

            ValidateBoardVersion(
                boardVersionBefore,
                nameof(boardVersionBefore));

            ValidateBoardVersion(
                boardVersionAfter,
                nameof(boardVersionAfter));

            /*
             * Uma ação só pode ser aplicada sobre o estado para o qual foi criada.
             *
             * Esta proteção torna impossível construir acidentalmente um resultado
             * de sucesso para uma intenção desatualizada.
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

            /*
             * A versão nunca pode voltar no tempo.
             *
             * Igualdade é permitida porque nem toda ação futura modificará o
             * BoardState.
             */
            if (boardVersionAfter < boardVersionBefore)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boardVersionAfter),
                    boardVersionAfter,
                    "A versão posterior não pode ser menor que a versão anterior.");
            }

            return new ActionResult(
                action,
                ActionRejectionReason.None,
                boardVersionBefore,
                boardVersionAfter);
        }

        /// <summary>
        /// Cria o relatório de uma ação rejeitada.
        ///
        /// Uma única versão é recebida porque uma rejeição não pode modificar o
        /// tabuleiro. O mesmo valor será armazenado como versão anterior e
        /// posterior.
        ///
        /// A versão pode ser diferente de Action.ExpectedBoardVersion. Essa
        /// diferença é justamente o que ocorrerá em uma rejeição causada por
        /// BoardVersionDoesNotMatch.
        /// </summary>
        /// <param name="action">
        /// Ação que foi rejeitada.
        /// </param>
        /// <param name="reason">
        /// Regra que impediu sua aplicação.
        /// </param>
        /// <param name="boardVersion">
        /// Versão real que permaneceu inalterada.
        /// </param>
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

            /*
             * A versão é usada duas vezes intencionalmente.
             *
             * Dessa forma, o próprio construtor público da rejeição impede que um
             * chamador registre uma alteração parcial como se nada tivesse sido
             * aplicado.
             */
            return new ActionResult(
                action,
                reason,
                boardVersion,
                boardVersion);
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
            /*
             * None significa sucesso e, portanto, não pode criar um resultado
             * rejeitado.
             */
            if (reason == ActionRejectionReason.None)
            {
                throw new ArgumentException(
                    "Uma ação rejeitada precisa possuir um motivo.",
                    nameof(reason));
            }

            /*
             * Enums aceitam conversões numéricas. Esta verificação impede valores
             * que não foram declarados em ActionRejectionReason.
             */
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

        /*
         * Integrações futuras:
         *
         * 1. Na sessão 4.3, ActionResult receberá um BoardChangeSet imutável.
         *    Resultados rejeitados utilizarão BoardChangeSet.Empty.
         *
         * 2. ActionExecutor criará Rejected quando fase, Turno, ator ou versão não
         *    corresponderem ao estado atual.
         *
         * 3. As rejeições do MoveService serão convertidas explicitamente:
         *
         *    CellDoesNotExist
         *        -> ActionRejectionReason.CellDoesNotExist
         *
         *    MarkAlreadyPresent
         *        -> ActionRejectionReason.MarkAlreadyPresent
         *
         *    OccupiedCellDoesNotAllowOverlap
         *        -> ActionRejectionReason.OccupiedCellDoesNotAllowOverlap
         *
         * 4. Somente um ActionResult com WasApplied igual a true poderá chamar
         *    TurnContext.RegisterAppliedAction.
         *
         * 5. Mensagens para o jogador não devem ser armazenadas neste objeto.
         *    A apresentação poderá traduzir RejectionReason para o texto adequado.
         *
         * 6. Erros inesperados de programação não deverão ser convertidos em
         *    rejeições genéricas. Eles precisam continuar visíveis por exceções e
         *    logs para que a inconsistência seja corrigida.
         */
    }
}