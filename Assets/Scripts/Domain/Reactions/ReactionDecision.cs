using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Reactions
{
    /// <summary>
    /// Resultado lógico produzido pela futura ReactionRule ao final de uma
    /// turno de ações.
    ///
    /// É importante diferenciar uma ação de uma turno de ações:
    ///
    /// - uma ação é um lance comum ou o uso de uma runa;
    /// - uma turno de ações pode conter uma ou mais ações do mesmo participante;
    /// - a ReactionRule somente decide falha ou sucesso da reação quando essa
    ///   turno de ações realmente termina.
    ///
    /// Por isso, duas ações concedidas pela mesma runa não produzem duas decisões
    /// separadas. Já duas turnos consecutivas produzem uma decisão ao final de
    /// cada turno, mesmo quando continuam pertencendo ao mesmo participante.
    /// </summary>
    public enum ReactionDecisionKind
    {
        /// <summary>
        /// O placar estava empatado, continuou empatado e ainda existem ações.
        /// A partida/tabuleiro prossegue sem uma reação pendente.
        /// </summary>
        ContinueNormal,

        /// <summary>
        /// O placar deixou de estar empatado. O participante que ficou atrás
        /// passa a possuir o direito e o dever de reagir na sua próxima turno.
        /// </summary>
        ReactionStarted,

        /// <summary>
        /// A mesma liderança e a mesma reação pendente foram preservadas.
        ///
        /// O caso mais importante é uma turno extra do líder: ele pode ampliar
        /// a vantagem, mas isso não conta como tentativa fracassada de quem ainda
        /// não recebeu sua oportunidade de reação.
        /// </summary>
        ReactionMaintained,

        /// <summary>
        /// O participante que estava atrás virou o placar. Agora o antigo líder
        /// ficou atrás e passa a ser o novo responsável pela reação.
        /// </summary>
        ReactionTransferred,

        /// <summary>
        /// O participante que estava atrás igualou o placar. A reação terminou e
        /// a partida/tabuleiro volta ao fluxo normal.
        /// </summary>
        ReactionResolvedByTie,

        /// <summary>
        /// A partida/tabuleiro terminou com um participante à frente.
        ///
        /// Isso pode ocorrer porque o responsável pela reação terminou sua turno
        /// ainda atrás ou porque não existem ações disponíveis para continuar.
        /// O ReactionEndReason informa qual dessas situações aconteceu.
        /// </summary>
        Victory,

        /// <summary>
        /// A partida/tabuleiro terminou empatada porque nenhuma ação capaz de
        /// continuar a disputa está disponível.
        /// </summary>
        DrawNoActions
    }

    /// <summary>
    /// Explica por que uma ReactionDecision encerrou a partida/tabuleiro.
    ///
    /// Guardar esse motivo no domínio evita que o EncounterController tente
    /// deduzi-lo novamente a partir do tabuleiro, do placar ou da interface.
    /// </summary>
    public enum ReactionEndReason
    {
        /// <summary>
        /// A decisão não encerrou a partida/tabuleiro.
        /// </summary>
        None,

        /// <summary>
        /// O participante que precisava reagir concluiu sua turno de ações e
        /// permaneceu atrás no placar de sequências.
        /// </summary>
        ResponderRemainedBehind,

        /// <summary>
        /// Não existe nenhuma ação atualmente disponível capaz de continuar a
        /// partida/tabuleiro.
        ///
        /// Hoje isso normalmente significa que não existem casas válidas vazias.
        /// Futuramente, a consulta de disponibilidade também considerará runas
        /// que limpem casas, troquem símbolos ou quebrem sequências.
        /// </summary>
        NoAvailableActions
    }

    /// <summary>
    /// Relatório imutável da decisão tomada pela regra de reação.
    ///
    /// ReactionDecision não calcula sequências, não escolhe o próximo turno, não
    /// concede multiplicadores e não inicia animações. Ele apenas conserva o fato
    /// que a futura ReactionRule já decidiu.
    ///
    /// O estado anterior e o estado atual são mantidos juntos para que testes,
    /// logs, IA e apresentação consigam entender a transição sem consultar um
    /// objeto mutável que talvez já tenha avançado para outro turno.
    /// </summary>
    public sealed class ReactionDecision
    {
        /// <summary>
        /// Tipo de transição decidida pela ReactionRule.
        /// </summary>
        public ReactionDecisionKind Kind { get; }

        /// <summary>
        /// Motivo do encerramento da partida/tabuleiro.
        ///
        /// Vale ReactionEndReason.None em toda decisão que permite continuar.
        /// </summary>
        public ReactionEndReason EndReason { get; }

        /// <summary>
        /// Placar de reação válido antes da turno de ações avaliada.
        /// </summary>
        public ReactionState PreviousState { get; }

        /// <summary>
        /// Placar obtido pela recontagem completa depois que todas as ações da
        /// turno foram aplicadas ao tabuleiro.
        /// </summary>
        public ReactionState CurrentState { get; }

        /// <summary>
        /// Participante que acabou de concluir a turno de ações avaliada.
        ///
        /// Esta informação é indispensável para não confundir:
        ///
        /// - o responsável pela reação que recebeu sua oportunidade e falhou;
        /// - o líder que ganhou uma turno extra e apenas manteve a vantagem;
        /// - um turno pulado, que não deve ser registrado como tentativa.
        /// </summary>
        public ScoreActor CompletedRoundActor { get; }

        /// <summary>
        /// Informa se, depois da recontagem, ainda existia ao menos uma ação
        /// disponível para continuar a disputa.
        ///
        /// Uma vitória por reação fracassada pode conservar este valor como true:
        /// nesse caso a partida termina não por falta de casas, mas porque a única
        /// oportunidade padrão de reação já foi utilizada.
        /// </summary>
        public bool HasAvailableActions { get; }

        /// <summary>
        /// Participante vencedor da partida/tabuleiro.
        ///
        /// Vale ScoreActor.None quando a decisão permite continuar ou quando a
        /// partida terminou empatada.
        /// </summary>
        public ScoreActor Winner { get; }

        /// <summary>
        /// Indica que a partida/tabuleiro deve continuar recebendo turnos.
        /// </summary>
        public bool ShouldContinue => !EndsBoardMatch;

        /// <summary>
        /// Indica que a partida disputada no tabuleiro terminou e deve seguir
        /// para pontuação, confronto e dano.
        ///
        /// Isso não significa o fim do encontro. O encontro somente termina
        /// quando a vida de um dos combatentes chega a zero.
        /// </summary>
        public bool EndsBoardMatch =>
            Kind == ReactionDecisionKind.Victory ||
            Kind == ReactionDecisionKind.DrawNoActions;

        /// <summary>
        /// Verdadeiro somente para um encerramento empatado e sem vencedor.
        /// </summary>
        public bool IsDraw =>
            Kind == ReactionDecisionKind.DrawNoActions;

        /// <summary>
        /// Indica que existe uma reação pendente depois desta decisão e que a
        /// partida/tabuleiro ainda não terminou.
        ///
        /// A apresentação poderá usar esta propriedade para ativar o estado
        /// visual de tensão. Ela não deve inferir a reação apenas comparando os
        /// números exibidos na tela.
        /// </summary>
        public bool HasActiveReaction =>
            ShouldContinue && CurrentState.IsReactionActive;

        /// <summary>
        /// Participante que deverá reagir quando HasActiveReaction for true.
        ///
        /// Em empate ou depois do encerramento, retorna ScoreActor.None. Dessa
        /// forma, um placar desigual que já terminou em vitória não continua
        /// parecendo uma reação ativa para o controlador ou para a apresentação.
        /// </summary>
        public ScoreActor PendingResponder =>
            HasActiveReaction
                ? CurrentState.PendingResponder
                : ScoreActor.None;

        /// <summary>
        /// Participante que deverá receber o multiplicador de vitória durante o
        /// futuro ScorePipeline.
        ///
        /// Em qualquer decisão sem vitória retorna ScoreActor.None. O objeto não
        /// aplica o multiplicador; ele apenas informa quem conquistou o direito.
        /// </summary>
        public ScoreActor VictoryMultiplierRecipient => Winner;

        /// <summary>
        /// Indica que existe um vencedor autorizado a receber o multiplicador de
        /// vitória.
        /// </summary>
        public bool GrantsVictoryMultiplier =>
            VictoryMultiplierRecipient != ScoreActor.None;

        /// <summary>
        /// Cria o relatório a partir dos valores já determinados pela futura
        /// ReactionRule.
        ///
        /// O construtor é internal para impedir que componentes de apresentação
        /// fabriquem decisões. Classes do mesmo domínio poderão criá-las, mas
        /// qualquer combinação incoerente será rejeitada pelas validações abaixo.
        /// </summary>
        internal ReactionDecision(
            ReactionDecisionKind kind,
            ReactionEndReason endReason,
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor,
            bool hasAvailableActions)
        {
            PreviousState = previousState ??
                throw new ArgumentNullException(nameof(previousState));

            CurrentState = currentState ??
                throw new ArgumentNullException(nameof(currentState));

            ValidateEnumValues(kind, endReason);
            ValidateCompletedRoundActor(completedRoundActor);
            ValidateBoardVersions(previousState, currentState);
            ValidateDecisionShape(
                kind,
                endReason,
                previousState,
                currentState,
                completedRoundActor,
                hasAvailableActions);

            Kind = kind;
            EndReason = endReason;
            CompletedRoundActor = completedRoundActor;
            HasAvailableActions = hasAvailableActions;

            Winner = kind == ReactionDecisionKind.Victory
                ? currentState.Leader
                : ScoreActor.None;
        }

        private static void ValidateEnumValues(
            ReactionDecisionKind kind,
            ReactionEndReason endReason)
        {
            if (!Enum.IsDefined(typeof(ReactionDecisionKind), kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "O tipo de decisão de reação informado não existe.");
            }

            if (!Enum.IsDefined(typeof(ReactionEndReason), endReason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endReason),
                    endReason,
                    "O motivo de encerramento informado não existe.");
            }
        }

        private static void ValidateCompletedRoundActor(
            ScoreActor completedRoundActor)
        {
            if (completedRoundActor != ScoreActor.Player &&
                completedRoundActor != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedRoundActor),
                    completedRoundActor,
                    "Uma turno de ações deve ter sido concluída por Player " +
                    "ou Enemy.");
            }
        }

        private static void ValidateBoardVersions(
            ReactionState previousState,
            ReactionState currentState)
        {
            if (currentState.BoardVersion < previousState.BoardVersion)
            {
                throw new ArgumentException(
                    "O estado atual da reação não pode pertencer a uma versão " +
                    "do tabuleiro anterior à versão do estado precedente.",
                    nameof(currentState));
            }
        }

        private static void ValidateDecisionShape(
            ReactionDecisionKind kind,
            ReactionEndReason endReason,
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor,
            bool hasAvailableActions)
        {
            bool endsBoardMatch =
                kind == ReactionDecisionKind.Victory ||
                kind == ReactionDecisionKind.DrawNoActions;

            if (!endsBoardMatch && endReason != ReactionEndReason.None)
            {
                throw new ArgumentException(
                    "Uma decisão que permite continuar não pode possuir motivo " +
                    "de encerramento.",
                    nameof(endReason));
            }

            if (!endsBoardMatch && !hasAvailableActions)
            {
                throw new ArgumentException(
                    "A partida não pode continuar quando nenhuma ação está " +
                    "disponível.",
                    nameof(hasAvailableActions));
            }

            switch (kind)
            {
                case ReactionDecisionKind.ContinueNormal:
                    RequireTied(previousState, nameof(previousState));
                    RequireTied(currentState, nameof(currentState));
                    break;

                case ReactionDecisionKind.ReactionStarted:
                    RequireTied(previousState, nameof(previousState));
                    RequireReactionActive(currentState, nameof(currentState));
                    break;

                case ReactionDecisionKind.ReactionMaintained:
                    RequireReactionActive(previousState, nameof(previousState));
                    RequireReactionActive(currentState, nameof(currentState));

                    if (previousState.Leader != currentState.Leader)
                    {
                        throw new ArgumentException(
                            "ReactionMaintained exige que o mesmo participante " +
                            "permaneça na liderança.",
                            nameof(currentState));
                    }

                    if (completedRoundActor ==
                        previousState.PendingResponder)
                    {
                        throw new ArgumentException(
                            "O participante responsável pela reação não pode " +
                            "apenas mantê-la: se ele concluiu sua turno ainda " +
                            "atrás, a reação fracassou e a decisão deve ser " +
                            "Victory.",
                            nameof(completedRoundActor));
                    }

                    break;

                case ReactionDecisionKind.ReactionTransferred:
                    RequireReactionActive(previousState, nameof(previousState));
                    RequireReactionActive(currentState, nameof(currentState));

                    if (previousState.Leader == currentState.Leader)
                    {
                        throw new ArgumentException(
                            "ReactionTransferred exige uma troca de liderança.",
                            nameof(currentState));
                    }

                    break;

                case ReactionDecisionKind.ReactionResolvedByTie:
                    RequireReactionActive(previousState, nameof(previousState));
                    RequireTied(currentState, nameof(currentState));
                    break;

                case ReactionDecisionKind.Victory:
                    RequireReactionActive(currentState, nameof(currentState));
                    ValidateVictoryEndReason(
                        endReason,
                        previousState,
                        currentState,
                        completedRoundActor,
                        hasAvailableActions);
                    break;

                case ReactionDecisionKind.DrawNoActions:
                    RequireTied(currentState, nameof(currentState));

                    if (endReason != ReactionEndReason.NoAvailableActions)
                    {
                        throw new ArgumentException(
                            "DrawNoActions exige o motivo NoAvailableActions.",
                            nameof(endReason));
                    }

                    if (hasAvailableActions)
                    {
                        throw new ArgumentException(
                            "DrawNoActions não pode conservar ações disponíveis.",
                            nameof(hasAvailableActions));
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "O tipo de decisão de reação informado não existe.");
            }
        }

        private static void ValidateVictoryEndReason(
            ReactionEndReason endReason,
            ReactionState previousState,
            ReactionState currentState,
            ScoreActor completedRoundActor,
            bool hasAvailableActions)
        {
            switch (endReason)
            {
                case ReactionEndReason.ResponderRemainedBehind:
                    RequireReactionActive(
                        previousState,
                        nameof(previousState));

                    if (completedRoundActor !=
                        previousState.PendingResponder)
                    {
                        throw new ArgumentException(
                            "Uma reação somente falha quando o participante que " +
                            "precisava reagir conclui sua própria turno.",
                            nameof(completedRoundActor));
                    }

                    if (currentState.Leader != previousState.Leader)
                    {
                        throw new ArgumentException(
                            "O líder anterior deve continuar à frente em uma " +
                            "vitória causada por reação fracassada.",
                            nameof(currentState));
                    }

                    break;

                case ReactionEndReason.NoAvailableActions:
                    if (hasAvailableActions)
                    {
                        throw new ArgumentException(
                            "Uma vitória por falta de ações não pode conservar " +
                            "ações disponíveis.",
                            nameof(hasAvailableActions));
                    }

                    break;

                case ReactionEndReason.None:
                default:
                    throw new ArgumentException(
                        "Uma vitória precisa informar se ocorreu por reação " +
                        "fracassada ou por ausência de ações.",
                        nameof(endReason));
            }
        }

        private static void RequireTied(
            ReactionState state,
            string parameterName)
        {
            if (!state.IsTied)
            {
                throw new ArgumentException(
                    "A decisão exige um placar de reação empatado.",
                    parameterName);
            }
        }

        private static void RequireReactionActive(
            ReactionState state,
            string parameterName)
        {
            if (!state.IsReactionActive)
            {
                throw new ArgumentException(
                    "A decisão exige um placar com liderança e reação pendente.",
                    parameterName);
            }
        }

        /*
         * ADIÇÕES FUTURAS POSSÍVEIS
         *
         * 1. ReactionEndReason poderá receber motivos específicos de efeitos,
         *    como rendição, limite especial de um boss ou encerramento causado
         *    diretamente por uma runa. Esses motivos deverão continuar sendo
         *    fatos produzidos pelo domínio, nunca pela interface.
         *
         * 2. Quando existirem replays, um identificador da turno de ações poderá
         *    ser adicionado ao relatório. A BoardVersion já protege a recontagem
         *    do tabuleiro, mas o identificador permitirá relacionar a decisão aos
         *    comandos e efeitos que a originaram.
         *
         * 3. Caso uma regra permita mais de uma oportunidade de reação, a futura
         *    ReactionState poderá guardar um orçamento de oportunidades. A
         *    ReactionDecision continuará relatando somente a transição já
         *    resolvida, sem executar contadores por conta própria.
         */
    }
}