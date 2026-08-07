using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Sequences;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Apresenta visualmente uma resolução de combate que já foi calculada.
///
/// Esta classe substitui o antigo MatchAnimator. O novo nome deixa explícito o
/// contrato: ela anima relatórios prontos e nunca decide pontuação ou dano.
///
/// O fluxo autoritativo acontece antes de esta classe ser chamada:
///
/// 1. ScorePipeline produz os dois ScorePipelineResult;
/// 2. ClashResolver produz o ClashReport;
/// 3. DamageResolver altera o CombatantState correto e produz o DamageReport;
/// 4. CombatReportAnimator apenas reproduz esses fatos na tela.
///
/// Portanto, interromper, acelerar ou até remover esta animação não muda o
/// resultado da partida. Essa propriedade é muito importante para testes,
/// replays, opção de pular animações e futuras simulações da IA.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatReportAnimator : MonoBehaviour
{
    [Header("Dependências")]
    [Tooltip(
        "View do tabuleiro usada somente para localizar e destacar as casas " +
        "presentes em cada SequenceMatch.")]
    [SerializeField] private BoardView _boardView;

    [Header("Textos obrigatórios")]
    [SerializeField] private TextMeshProUGUI _playerScoreText;
    [SerializeField] private TextMeshProUGUI _enemyScoreText;
    [SerializeField] private TextMeshProUGUI _playerHealthText;
    [SerializeField] private TextMeshProUGUI _enemyHealthText;

    [Header("Textos opcionais")]
    [Tooltip("Pode ficar vazio enquanto a interface ainda não exibir escudo.")]
    [SerializeField] private TextMeshProUGUI _playerShieldText;

    [Tooltip("Pode ficar vazio enquanto a interface ainda não exibir escudo.")]
    [SerializeField] private TextMeshProUGUI _enemyShieldText;

    [Header("Sequências e contribuições opcionais")]
    [Tooltip(
        "Prefab de uma linha de UI. Precisa possuir RectTransform; Image é " +
        "opcional e, quando presente, recebe a cor do participante.")]
    [SerializeField] private GameObject _sequenceLinePrefab;

    [Tooltip(
        "Prefab de texto flutuante. O primeiro TextMeshProUGUI encontrado nos " +
        "filhos recebe a descrição já fornecida pelo relatório.")]
    [SerializeField] private GameObject _floatingTextPrefab;

    [Tooltip("Contêiner visual que receberá linhas e textos temporários.")]
    [SerializeField] private Transform _effectsContainer;

    [SerializeField] private RectTransform _playerFloatingTextOrigin;
    [SerializeField] private RectTransform _enemyFloatingTextOrigin;
    
    [Min(1f)]
    [Tooltip(
        "Espessura visual das linhas desenhadas sobre as sequências. " +
        "O MatchAnimator antigo utilizava 30.")]
    [SerializeField]
    private float _sequenceLineThickness = 30f;

    [Header("Cores")]
    [SerializeField] private Color _playerColor =
        new Color(0.18f, 0.55f, 1f, 1f);

    [SerializeField] private Color _enemyColor =
        new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Efeitos opcionais")]
    [SerializeField] private ParticleSystem _clashParticlePrefab;
    [SerializeField] private Transform _shakeTarget;

    [Header("Duração das etapas")]
    [Min(0f)]
    [SerializeField] private float _sequenceDuration = 0.35f;

    [Min(0f)]
    [SerializeField] private float _scoreStepDuration = 0.25f;

    [Min(0f)]
    [SerializeField] private float _clashDuration = 0.65f;

    [Min(0f)]
    [Tooltip(
        "Tempo que os dois placares levam para se encontrar no centro da tela.")]
    [SerializeField] private float _scoreMeetingDuration = 0.45f;

    [Min(0f)]
    [Tooltip(
        "Tempo que o valor restante leva para viajar até a vida do alvo.")]
    [SerializeField] private float _remainingScoreTravelDuration = 0.22f;

    [Min(0f)]
    [SerializeField] private float _damageDuration = 0.55f;

    [Min(0f)]
    [SerializeField] private float _finalPauseDuration = 0.25f;

    [Min(0f)]
    [SerializeField] private float _shakeDuration = 0.15f;

    [Min(0f)]
    [SerializeField] private float _shakeMagnitude = 8f;

    private readonly List<GameObject> _temporaryVisuals =
        new List<GameObject>();

    private Vector3 _shakeTargetRestPosition;
    private bool _hasShakeTargetRestPosition;

    private Vector3 _playerScoreRestWorldPosition;
    private Vector3 _enemyScoreRestWorldPosition;
    private bool _hasScoreRestPositions;

    /// <summary>
    /// Confere somente a configuração visual permanente do componente.
    ///
    /// Prefabs e textos de escudo são opcionais. Os quatro textos principais e a
    /// BoardView são obrigatórios porque formam a apresentação mínima esperada.
    /// </summary>
    public void ValidateConfiguration()
    {
        if (_boardView == null)
        {
            throw new InvalidOperationException(
                "Informe a BoardView no CombatReportAnimator.");
        }

        if (_playerScoreText == null || _enemyScoreText == null)
        {
            throw new InvalidOperationException(
                "Informe os textos de placar do jogador e do inimigo no " +
                "CombatReportAnimator.");
        }

        if (_playerHealthText == null || _enemyHealthText == null)
        {
            throw new InvalidOperationException(
                "Informe os textos de vida do jogador e do inimigo no " +
                "CombatReportAnimator.");
        }

        if ((_sequenceLinePrefab != null || _floatingTextPrefab != null) &&
            _effectsContainer == null)
        {
            throw new InvalidOperationException(
                "Informe Effects Container quando houver prefab de linha ou " +
                "texto flutuante no CombatReportAnimator.");
        }

        ValidateNonNegativeDuration(_sequenceDuration, "Sequence Duration");
        ValidateNonNegativeDuration(_scoreStepDuration, "Score Step Duration");
        ValidateNonNegativeDuration(_clashDuration, "Clash Duration");
        ValidateNonNegativeDuration(
            _scoreMeetingDuration,
            "Score Meeting Duration");
        ValidateNonNegativeDuration(
            _remainingScoreTravelDuration,
            "Remaining Score Travel Duration");
        ValidateNonNegativeDuration(_damageDuration, "Damage Duration");
        ValidateNonNegativeDuration(_finalPauseDuration, "Final Pause Duration");
        ValidateNonNegativeDuration(_shakeDuration, "Shake Duration");
        ValidateNonNegativeDuration(_shakeMagnitude, "Shake Magnitude");
    }

    /// <summary>
    /// Atualiza a interface diretamente a partir dos estados autoritativos.
    ///
    /// Este método não mantém uma cópia de vida. Sempre que for chamado, apenas
    /// transforma os valores recebidos em texto.
    /// </summary>
    public void SynchronizeCombatants(
        CombatantState playerState,
        CombatantState enemyState)
    {
        ValidateCombatantStates(playerState, enemyState);
        ValidateConfiguration();

        SetHealthText(ScoreActor.Player, playerState.CurrentHealth);
        SetHealthText(ScoreActor.Enemy, enemyState.CurrentHealth);
        SetShieldText(ScoreActor.Player, playerState.Shield);
        SetShieldText(ScoreActor.Enemy, enemyState.Shield);
    }

    /// <summary>
    /// Confere se os relatórios formam uma única resolução coerente.
    ///
    /// O EncounterController chama esta validação antes de iniciar a coroutine.
    /// Isso é útil porque o corpo de um IEnumerator só começa a executar depois
    /// que a Unity avança a coroutine; validar antes permite transformar erros de
    /// integração em uma falha clara do encontro.
    /// </summary>
    public void ValidateResolution(
        ScorePipelineResult playerScore,
        ScorePipelineResult enemyScore,
        ClashReport clash,
        DamageReport damage,
        CombatantState playerState,
        CombatantState enemyState)
    {
        ValidateConfiguration();
        ValidateCombatantStates(playerState, enemyState);

        if (playerScore == null)
        {
            throw new ArgumentNullException(nameof(playerScore));
        }

        if (enemyScore == null)
        {
            throw new ArgumentNullException(nameof(enemyScore));
        }

        if (clash == null)
        {
            throw new ArgumentNullException(nameof(clash));
        }

        if (damage == null)
        {
            throw new ArgumentNullException(nameof(damage));
        }

        if (!ReferenceEquals(clash.PlayerScoreResult, playerScore) ||
            !ReferenceEquals(clash.EnemyScoreResult, enemyScore))
        {
            throw new ArgumentException(
                "O ClashReport precisa pertencer exatamente aos dois resultados " +
                "de pontuação recebidos pelo animador.");
        }

        if (!ReferenceEquals(damage.Clash, clash))
        {
            throw new ArgumentException(
                "O DamageReport precisa ter sido originado pelo ClashReport " +
                "recebido pelo animador.");
        }

        if (_boardView.DisplayedBoard == null)
        {
            throw new InvalidOperationException(
                "A BoardView precisa estar exibindo o tabuleiro da resolução.");
        }

        if (_boardView.DisplayedBoard.Version != clash.BoardVersion)
        {
            throw new InvalidOperationException(
                "A BoardView exibe uma versão diferente da versão registrada " +
                "nos relatórios de combate.");
        }

        if (damage.HasTarget)
        {
            CombatantState target =
                damage.TargetActor == ScoreActor.Player
                    ? playerState
                    : enemyState;

            if (target.CombatantId != damage.TargetAfter.CombatantId ||
                target.Version != damage.TargetAfter.Version ||
                target.CurrentHealth != damage.TargetAfter.CurrentHealth ||
                target.Shield != damage.TargetAfter.Shield)
            {
                throw new InvalidOperationException(
                    "O estado atual do alvo não corresponde ao snapshot posterior " +
                    "do DamageReport. A animação não pode apresentar dois fatos " +
                    "diferentes.");
            }
        }
    }

    /// <summary>
    /// Reproduz uma resolução completa sem calcular nenhum valor de gameplay.
    ///
    /// O método devolve IEnumerator para que o EncounterController seja o dono da
    /// coroutine. Assim, o controlador também controla cancelamento, troca de
    /// rodada e a transição para o próximo estado do encontro.
    /// </summary>
    public IEnumerator PlayResolution(
        ScorePipelineResult playerScore,
        ScorePipelineResult enemyScore,
        ClashReport clash,
        DamageReport damage,
        CombatantState playerState,
        CombatantState enemyState)
    {
        /*
         * ValidateResolution deve ser chamada pelo controlador antes de iniciar
         * a coroutine. Repetimos a validação para que o método continue seguro se
         * for utilizado diretamente por outra tela no futuro.
         */
        ValidateResolution(
            playerScore,
            enemyScore,
            clash,
            damage,
            playerState,
            enemyState);

        ResetTemporaryPresentation();
        CaptureScoreRestPositions();
        PrepareHealthTextsBeforeDamage(damage, playerState, enemyState);
        SetScoreText(ScoreActor.Player, 0);
        SetScoreText(ScoreActor.Enemy, 0);

        /*
         * Primeiro apresentamos somente as sequências e seus pontos básicos.
         * Cada linha é imediatamente seguida pela contribuição que ela gerou.
         */
        yield return PlayParticipantSequencesAndBaseScore(
            playerScore,
            _playerColor,
            _playerFloatingTextOrigin);

        yield return PlayParticipantSequencesAndBaseScore(
            enemyScore,
            _enemyColor,
            _enemyFloatingTextOrigin);

        /*
         * Multiplicadores são deixados para depois que os dois participantes
         * terminaram de apresentar suas sequências.
         */
        yield return PlayParticipantVictoryMultiplier(
            playerScore,
            _playerColor,
            _playerFloatingTextOrigin);

        yield return PlayParticipantVictoryMultiplier(
            enemyScore,
            _enemyColor,
            _enemyFloatingTextOrigin);

        yield return PlayClash(clash);
        yield return PlayDamage(damage);

        SetHealthText(ScoreActor.Player, playerState.CurrentHealth);
        SetHealthText(ScoreActor.Enemy, enemyState.CurrentHealth);
        SetShieldText(ScoreActor.Player, playerState.Shield);
        SetShieldText(ScoreActor.Enemy, enemyState.Shield);

        if (_finalPauseDuration > 0f)
        {
            yield return new WaitForSeconds(_finalPauseDuration);
        }

        SetScoreTextOrEmpty(ScoreActor.Player, 0);
        SetScoreTextOrEmpty(ScoreActor.Enemy, 0);
        ResetTemporaryPresentation();
    }

    /// <summary>
    /// Remove apenas objetos e alterações visuais temporárias.
    /// Não toca no BoardState nem nos CombatantState.
    /// </summary>
    public void ResetTemporaryPresentation()
    {
        if (_boardView != null &&
            _boardView.DisplayedBoard != null)
        {
            _boardView.ClearAllHighlights();
        }

        for (int index = _temporaryVisuals.Count - 1;
             index >= 0;
             index--)
        {
            GameObject temporaryVisual = _temporaryVisuals[index];

            if (temporaryVisual != null)
            {
                temporaryVisual.SetActive(false);
                Destroy(temporaryVisual);
            }
        }

        _temporaryVisuals.Clear();

        if (_shakeTarget != null && _hasShakeTargetRestPosition)
        {
            _shakeTarget.localPosition = _shakeTargetRestPosition;
            _hasShakeTargetRestPosition = false;
        }

        RestoreScoreRestPositions();
    }

    private IEnumerator PlayParticipantSequencesAndBaseScore(
        ScorePipelineResult scoreResult,
        Color participantColor,
        RectTransform floatingTextOrigin)
    {
        /*
         * Na pontuação atual, cada sequência produz uma contribuição AddPoints
         * na mesma ordem em que aparece em ScoringSequences. O animador apenas
         * percorre os dois históricos; ele não calcula o valor da sequência.
         */
        int sequenceIndex = 0;

        for (int stepIndex = 0;
             stepIndex < scoreResult.Breakdown.Steps.Count;
             stepIndex++)
        {
            ScoreStep step =
                scoreResult.Breakdown.Steps[stepIndex];

            if (IsVictoryMultiplierStep(step))
            {
                continue;
            }

            SequenceMatch sequence = null;
            GameObject lineObject = null;

            if (step.Contribution.Phase == ScorePhase.BasePoints)
            {
                if (sequenceIndex >=
                    scoreResult.ScoringSequences.Count)
                {
                    throw new InvalidOperationException(
                        "Existe uma etapa BasePoints sem a SequenceMatch " +
                        "correspondente no ScorePipelineResult.");
                }

                sequence =
                    scoreResult.ScoringSequences[sequenceIndex];

                sequenceIndex++;

                lineObject =
                    ShowSequence(
                        sequence,
                        participantColor);

                yield return WaitForPresentationDuration(
                    _sequenceDuration);
            }

            SetScoreText(
                scoreResult.Participant,
                step.ScoreAfter);

            GameObject floatingText =
                SpawnFloatingText(
                    floatingTextOrigin,
                    BuildScoreStepText(step),
                    participantColor);

            yield return WaitForPresentationDuration(
                _scoreStepDuration);

            DestroyTemporaryVisual(floatingText);
            DestroyTemporaryVisual(lineObject);

            if (sequence != null)
            {
                ClearSequenceHighlight(sequence);
            }
        }

        if (sequenceIndex != scoreResult.ScoringSequences.Count)
        {
            throw new InvalidOperationException(
                "A quantidade de etapas BasePoints não corresponde às " +
                "ScoringSequences do ScorePipelineResult.");
        }
    }

    private IEnumerator PlayParticipantVictoryMultiplier(
        ScorePipelineResult scoreResult,
        Color participantColor,
        RectTransform floatingTextOrigin)
    {
        /*
         * Esta etapa acontece somente depois que jogador e inimigo terminaram
         * suas linhas. ScoreAfter continua sendo o valor oficial do relatório.
         */
        for (int index = 0;
             index < scoreResult.Breakdown.Steps.Count;
             index++)
        {
            ScoreStep step = scoreResult.Breakdown.Steps[index];

            if (!IsVictoryMultiplierStep(step))
            {
                continue;
            }

            SetScoreText(
                scoreResult.Participant,
                step.ScoreAfter);

            GameObject floatingText =
                SpawnFloatingText(
                    floatingTextOrigin,
                    BuildScoreStepText(step),
                    participantColor);

            yield return WaitForPresentationDuration(
                _scoreStepDuration);

            DestroyTemporaryVisual(floatingText);
        }

        SetScoreText(
            scoreResult.Participant,
            scoreResult.Breakdown.FinalScore);
    }

    private IEnumerator PlayClash(ClashReport clash)
    {
        int playerStart = clash.PlayerScore;
        int enemyStart = clash.EnemyScore;

        RectTransform playerRect =
            _playerScoreText.rectTransform;

        RectTransform enemyRect =
            _enemyScoreText.rectTransform;

        /*
         * Garante que largura e posição dos textos reflitam os valores que
         * acabaram de ser atribuídos antes de calcular o ponto de contato.
         */
        Canvas.ForceUpdateCanvases();

        Vector3 playerStartPosition =
            playerRect.position;

        Vector3 enemyStartPosition =
            enemyRect.position;

        Vector3 center =
            (playerStartPosition + enemyStartPosition) * 0.5f;

        Vector3 direction =
            enemyStartPosition - playerStartPosition;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = Vector3.right;
        }
        else
        {
            direction.Normalize();
        }

        /*
         * Os centros não terminam sobrepostos. Cada texto para a meia largura
         * correspondente antes do centro, fazendo as bordas se encostarem.
         */
        float playerHalfWidth =
            GetWorldHalfWidth(playerRect);

        float enemyHalfWidth =
            GetWorldHalfWidth(enemyRect);

        Vector3 playerMeetingPosition =
            center - direction * playerHalfWidth;

        Vector3 enemyMeetingPosition =
            center + direction * enemyHalfWidth;

        yield return MoveTwoRects(
            playerRect,
            playerStartPosition,
            playerMeetingPosition,
            enemyRect,
            enemyStartPosition,
            enemyMeetingPosition,
            _scoreMeetingDuration);

        /*
         * Partículas e tremor marcam o instante do contato. A subtração só
         * começa depois que os dois placares já chegaram ao centro.
         */
        SpawnClashParticles(center);

        if (_shakeTarget != null &&
            _shakeDuration > 0f &&
            _shakeMagnitude > 0f)
        {
            yield return ShakeRoutine();
        }

        int cancelledPoints =
            Math.Min(playerStart, enemyStart);

        if (cancelledPoints <= 0 ||
            _clashDuration <= 0f)
        {
            SetScoreTextOrEmpty(
                ScoreActor.Player,
                clash.PlayerRemainingScore);

            SetScoreTextOrEmpty(
                ScoreActor.Enemy,
                clash.EnemyRemainingScore);

            yield break;
        }

        float pointInterval =
            _clashDuration / cancelledPoints;

        for (int removed = 1;
             removed <= cancelledPoints;
             removed++)
        {
            /*
             * Ambos caem exatamente um ponto por passo. Quando um chega a zero,
             * SetScoreTextOrEmpty remove o número em vez de mostrar "0".
             */
            SetScoreTextOrEmpty(
                ScoreActor.Player,
                playerStart - removed);

            SetScoreTextOrEmpty(
                ScoreActor.Enemy,
                enemyStart - removed);

            yield return WaitForPresentationDuration(
                pointInterval);
        }

        SetScoreTextOrEmpty(
            ScoreActor.Player,
            clash.PlayerRemainingScore);

        SetScoreTextOrEmpty(
            ScoreActor.Enemy,
            clash.EnemyRemainingScore);
    }

    private IEnumerator PlayDamage(DamageReport damage)
    {
        if (!damage.HasTarget)
        {
            /*
             * Empate é um resultado explícito. Não existe alvo, mas conservar um
             * frame aqui deixa a transição visual perceptível quando todas as
             * durações estão configuradas como zero.
             */
            yield return null;
            yield break;
        }

        ScoreActor attacker =
            damage.TargetActor == ScoreActor.Player
                ? ScoreActor.Enemy
                : ScoreActor.Player;

        RectTransform attackingScoreRect =
            GetScoreText(attacker).rectTransform;

        RectTransform targetHealthRect =
            GetHealthText(damage.TargetActor).rectTransform;

        /*
         * O próprio número que sobreviveu ao embate viaja até a vida do alvo.
         * Ele desaparece no impacto; não criamos outro texto de dano por cima.
         */
        yield return MoveRect(
            attackingScoreRect,
            attackingScoreRect.position,
            targetHealthRect.position,
            _remainingScoreTravelDuration);

        SetScoreTextOrEmpty(attacker, 0);

        if (_shakeTarget != null &&
            _shakeDuration > 0f &&
            _shakeMagnitude > 0f)
        {
            yield return ShakeRoutine();
        }

        if (_damageDuration <= 0f)
        {
            SetHealthText(
                damage.TargetActor,
                damage.TargetAfter.CurrentHealth);

            SetShieldText(
                damage.TargetActor,
                damage.TargetAfter.Shield);

            yield break;
        }

        int visibleShield =
            damage.TargetBefore.Shield;

        int visibleHealth =
            damage.TargetBefore.CurrentHealth;

        int shieldSteps =
            Math.Max(
                0,
                visibleShield - damage.TargetAfter.Shield);

        int healthSteps =
            Math.Max(
                0,
                visibleHealth - damage.TargetAfter.CurrentHealth);

        int totalSteps = shieldSteps + healthSteps;

        if (totalSteps <= 0)
        {
            yield return null;
            yield break;
        }

        float pointInterval =
            _damageDuration / totalSteps;

        /*
         * O escudo é consumido primeiro, respeitando o DamageReport.
         */
        while (visibleShield > damage.TargetAfter.Shield)
        {
            visibleShield--;

            SetShieldText(
                damage.TargetActor,
                visibleShield);

            yield return WaitForPresentationDuration(
                pointInterval);
        }

        /*
         * Depois a vida cai um ponto por vez até o snapshot final autoritativo.
         */
        while (visibleHealth > damage.TargetAfter.CurrentHealth)
        {
            visibleHealth--;

            SetHealthText(
                damage.TargetActor,
                visibleHealth);

            yield return WaitForPresentationDuration(
                pointInterval);
        }

        SetHealthText(
            damage.TargetActor,
            damage.TargetAfter.CurrentHealth);

        SetShieldText(
            damage.TargetActor,
            damage.TargetAfter.Shield);
    }

    private GameObject ShowSequence(
        SequenceMatch sequence,
        Color color)
    {
        for (int index = 0;
             index < sequence.Cells.Count;
             index++)
        {
            _boardView.SetCellHighlight(
                sequence.Cells[index],
                color);
        }

        if (_sequenceLinePrefab == null ||
            _effectsContainer == null)
        {
            return null;
        }

        if (!_boardView.TryGetCellView(
                sequence.Start,
                out BoardCellView startCell) ||
            !_boardView.TryGetCellView(
                sequence.End,
                out BoardCellView endCell))
        {
            return null;
        }

        /*
         * false faz o objeto usar o espaço local do contêiner de UI.
         * Isso evita que a escala do Canvas seja aplicada incorretamente.
         */
        GameObject lineObject = Instantiate(
            _sequenceLinePrefab,
            _effectsContainer,
            false);

        RectTransform lineRect =
            lineObject.GetComponent<RectTransform>();

        if (lineRect == null)
        {
            Destroy(lineObject);

            throw new InvalidOperationException(
                "O prefab de linha precisa possuir RectTransform.");
        }

        Image lineImage =
            lineObject.GetComponent<Image>();

        if (lineImage != null)
        {
            lineImage.color = color;
        }

        /*
         * Âncoras esticadas fazem sizeDelta.y deixar de representar a altura
         * verdadeira. Centralizá-las garante que a espessura configurada seja
         * exatamente a espessura mostrada.
         */
        Vector2 centerAnchor =
            new Vector2(0.5f, 0.5f);

        lineRect.anchorMin = centerAnchor;
        lineRect.anchorMax = centerAnchor;
        lineRect.pivot = centerAnchor;
        lineRect.localScale = Vector3.one;

        /*
         * Convertemos as posições das casas para o espaço local do contêiner.
         * Assim, comprimento e posição continuam corretos mesmo quando o Canvas
         * possui Canvas Scaler ou outra escala.
         */
        Vector3 startPosition =
            _effectsContainer.InverseTransformPoint(
                startCell.transform.position);

        Vector3 endPosition =
            _effectsContainer.InverseTransformPoint(
                endCell.transform.position);

        Vector3 direction =
            endPosition - startPosition;

        lineRect.localPosition =
            (startPosition + endPosition) * 0.5f;

        lineRect.sizeDelta =
            new Vector2(
                direction.magnitude,
                _sequenceLineThickness);

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        lineRect.localRotation =
            Quaternion.Euler(0f, 0f, angle);

        _temporaryVisuals.Add(lineObject);

        return lineObject;
    }

    private void ClearSequenceHighlight(SequenceMatch sequence)
    {
        for (int index = 0;
             index < sequence.Cells.Count;
             index++)
        {
            _boardView.ClearCellHighlight(sequence.Cells[index]);
        }
    }

    private GameObject SpawnFloatingText(
        RectTransform origin,
        string content,
        Color color)
    {
        if (_floatingTextPrefab == null ||
            _effectsContainer == null ||
            origin == null)
        {
            return null;
        }

        GameObject textObject = Instantiate(
            _floatingTextPrefab,
            _effectsContainer,
            false);

        /*
         * Normaliza a transformação do objeto criado para que a escala do
         * contêiner não transforme o texto em algo gigantesco.
         */
        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        if (textRect != null)
        {
            textRect.position = origin.position;
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.identity;
        }
        else
        {
            textObject.transform.position = origin.position;
            textObject.transform.localScale = Vector3.one;
            textObject.transform.localRotation =
                Quaternion.identity;
        }

        TextMeshProUGUI text =
            textObject.GetComponentInChildren<TextMeshProUGUI>();

        if (text != null)
        {
            text.text = content;

            color.a = 1f;
            text.color = color;
        }

        _temporaryVisuals.Add(textObject);

        return textObject;
    }

    private void SpawnClashParticles(Vector3 center)
    {
        if (_clashParticlePrefab == null)
        {
            return;
        }

        ParticleSystem particles = Instantiate(
            _clashParticlePrefab,
            center,
            Quaternion.identity);

        _temporaryVisuals.Add(particles.gameObject);
    }

    private IEnumerator ShakeRoutine()
    {
        _shakeTargetRestPosition = _shakeTarget.localPosition;
        _hasShakeTargetRestPosition = true;

        float elapsed = 0f;

        while (elapsed < _shakeDuration)
        {
            float horizontal =
                UnityEngine.Random.Range(-1f, 1f) *
                _shakeMagnitude;

            float vertical =
                UnityEngine.Random.Range(-1f, 1f) *
                _shakeMagnitude;

            _shakeTarget.localPosition =
                _shakeTargetRestPosition +
                new Vector3(horizontal, vertical, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _shakeTarget.localPosition = _shakeTargetRestPosition;
    }

    private void PrepareHealthTextsBeforeDamage(
        DamageReport damage,
        CombatantState playerState,
        CombatantState enemyState)
    {
        int playerHealth = playerState.CurrentHealth;
        int enemyHealth = enemyState.CurrentHealth;
        int playerShield = playerState.Shield;
        int enemyShield = enemyState.Shield;

        /*
         * DamageResolver já alterou o alvo. Para animar a transição, o primeiro
         * frame usa o snapshot anterior do relatório; o participante não atingido
         * usa diretamente seu estado atual.
         */
        if (damage.HasTarget &&
            damage.TargetActor == ScoreActor.Player)
        {
            playerHealth = damage.TargetBefore.CurrentHealth;
            playerShield = damage.TargetBefore.Shield;
        }
        else if (damage.HasTarget &&
                 damage.TargetActor == ScoreActor.Enemy)
        {
            enemyHealth = damage.TargetBefore.CurrentHealth;
            enemyShield = damage.TargetBefore.Shield;
        }

        SetHealthText(ScoreActor.Player, playerHealth);
        SetHealthText(ScoreActor.Enemy, enemyHealth);
        SetShieldText(ScoreActor.Player, playerShield);
        SetShieldText(ScoreActor.Enemy, enemyShield);
    }

    private void CaptureScoreRestPositions()
    {
        _playerScoreRestWorldPosition =
            _playerScoreText.rectTransform.position;

        _enemyScoreRestWorldPosition =
            _enemyScoreText.rectTransform.position;

        _hasScoreRestPositions = true;
    }

    private void RestoreScoreRestPositions()
    {
        if (!_hasScoreRestPositions ||
            _playerScoreText == null ||
            _enemyScoreText == null)
        {
            return;
        }

        _playerScoreText.rectTransform.position =
            _playerScoreRestWorldPosition;

        _enemyScoreText.rectTransform.position =
            _enemyScoreRestWorldPosition;

        _hasScoreRestPositions = false;
    }

    private static IEnumerator MoveTwoRects(
        RectTransform first,
        Vector3 firstStart,
        Vector3 firstEnd,
        RectTransform second,
        Vector3 secondStart,
        Vector3 secondEnd,
        float duration)
    {
        if (duration <= 0f)
        {
            first.position = firstEnd;
            second.position = secondEnd;
            yield return null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration));

            first.position =
                Vector3.LerpUnclamped(
                    firstStart,
                    firstEnd,
                    progress);

            second.position =
                Vector3.LerpUnclamped(
                    secondStart,
                    secondEnd,
                    progress);

            yield return null;
        }

        first.position = firstEnd;
        second.position = secondEnd;
    }

    private static IEnumerator MoveRect(
        RectTransform target,
        Vector3 start,
        Vector3 end,
        float duration)
    {
        if (duration <= 0f)
        {
            target.position = end;
            yield return null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration));

            target.position =
                Vector3.LerpUnclamped(
                    start,
                    end,
                    progress);

            yield return null;
        }

        target.position = end;
    }

    private static IEnumerator WaitForPresentationDuration(
        float duration)
    {
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }
        else
        {
            yield return null;
        }
    }

    private void DestroyTemporaryVisual(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        _temporaryVisuals.Remove(visual);
        visual.SetActive(false);
        Destroy(visual);
    }

    private static float GetWorldHalfWidth(RectTransform rect)
    {
        return rect.rect.width *
               Mathf.Abs(rect.lossyScale.x) *
               0.5f;
    }

    private static bool IsVictoryMultiplierStep(ScoreStep step)
    {
        return step.Contribution.Phase ==
               ScorePhase.VictoryMultiplier;
    }

    private void SetScoreText(ScoreActor actor, int value)
    {
        SetScoreText(actor, (decimal)value);
    }

    private void SetScoreText(ScoreActor actor, decimal value)
    {
        TextMeshProUGUI target = GetScoreText(actor);

        target.text = value.ToString(
            "0.##",
            CultureInfo.CurrentCulture);
    }

    private void SetScoreTextOrEmpty(
        ScoreActor actor,
        int value)
    {
        TextMeshProUGUI target = GetScoreText(actor);

        target.text = value <= 0
            ? string.Empty
            : value.ToString(
                CultureInfo.CurrentCulture);
    }

    private void SetHealthText(ScoreActor actor, int value)
    {
        TextMeshProUGUI target = GetHealthText(actor);

        target.text = value.ToString(
            CultureInfo.CurrentCulture);
    }

    private void SetShieldText(ScoreActor actor, int value)
    {
        TextMeshProUGUI target =
            actor == ScoreActor.Player
                ? _playerShieldText
                : _enemyShieldText;

        if (target != null)
        {
            target.text = value.ToString(
                CultureInfo.CurrentCulture);
        }
    }

    private TextMeshProUGUI GetScoreText(ScoreActor actor)
    {
        ValidateDisplayActor(actor);

        return actor == ScoreActor.Player
            ? _playerScoreText
            : _enemyScoreText;
    }

    private TextMeshProUGUI GetHealthText(ScoreActor actor)
    {
        ValidateDisplayActor(actor);

        return actor == ScoreActor.Player
            ? _playerHealthText
            : _enemyHealthText;
    }

    private static string BuildScoreStepText(
        ScoreStep step)
    {
        /*
         * DisplayText permanece disponível no relatório para logs, debug e
         * futuras telas detalhadas. O efeito flutuante mostra somente a
         * operação curta que interessa durante a animação.
         */
        switch (step.Contribution.Operation)
        {
            case ScoreOperation.AddPoints:
                return BuildSignedValue(
                    step.Contribution.Amount);

            case ScoreOperation.AddToMultiplier:
                /*
                 * Em multiplicadores aditivos, EffectiveFactor representa o
                 * fator acumulado real. Duas contribuições de +0,5 aparecem
                 * primeiro como x1,5 e depois como x2.
                 */
                return "x" +
                       FormatShortNumber(
                           step.EffectiveFactor);

            case ScoreOperation.Multiply:
                return "x" +
                       FormatShortNumber(
                           step.EffectiveFactor);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(step),
                    step.Contribution.Operation,
                    "A operação não possui um formato visual.");
        }
    }

    private static string BuildSignedValue(
        decimal value)
    {
        string formattedValue =
            FormatShortNumber(
                Math.Abs(value));

        return value >= 0m
            ? "+" + formattedValue
            : "-" + formattedValue;
    }

    private static string FormatShortNumber(
        decimal value)
    {
        return value.ToString(
            "0.##",
            CultureInfo.CurrentCulture);
    }

    private static void ValidateCombatantStates(
        CombatantState playerState,
        CombatantState enemyState)
    {
        if (playerState == null)
        {
            throw new ArgumentNullException(nameof(playerState));
        }

        if (enemyState == null)
        {
            throw new ArgumentNullException(nameof(enemyState));
        }

        if (playerState.Actor != ScoreActor.Player)
        {
            throw new ArgumentException(
                "playerState precisa pertencer a ScoreActor.Player.",
                nameof(playerState));
        }

        if (enemyState.Actor != ScoreActor.Enemy)
        {
            throw new ArgumentException(
                "enemyState precisa pertencer a ScoreActor.Enemy.",
                nameof(enemyState));
        }
    }

    private static void ValidateDisplayActor(ScoreActor actor)
    {
        if (actor != ScoreActor.Player &&
            actor != ScoreActor.Enemy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actor),
                actor,
                "A apresentação aceita somente Player ou Enemy.");
        }
    }

    private static void ValidateNonNegativeDuration(
        float value,
        string fieldName)
    {
        if (value < 0f)
        {
            throw new InvalidOperationException(
                fieldName + " não pode ser negativo.");
        }
    }

    private void OnDisable()
    {
        ResetTemporaryPresentation();
    }

    private void Reset()
    {
        _boardView = GetComponent<BoardView>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_boardView == null)
        {
            Debug.LogWarning(
                "Informe a BoardView no CombatReportAnimator.",
                this);
        }

        if (_playerScoreText == null ||
            _enemyScoreText == null ||
            _playerHealthText == null ||
            _enemyHealthText == null)
        {
            Debug.LogWarning(
                "Informe os quatro textos principais no " +
                "CombatReportAnimator.",
                this);
        }
    }
#endif

    /*
     * ADIÇÕES FUTURAS POSSÍVEIS
     *
     * 1. Em vez de um único texto flutuante, uma futura ScoreStepView poderá
     *    escolher ícone, tradução, som e cor a partir de SourceId e Phase. O
     *    domínio continuará entregando ScoreStep pronto.
     *
     * 2. Um botão de "pular" poderá simplesmente encerrar a coroutine e chamar
     *    SynchronizeCombatants. Como o dano já foi aplicado, pular a animação não
     *    exigirá executar qualquer regra adicional.
     *
     * 3. Sequências poderão usar pooling de linhas e textos para reduzir
     *    Instantiate/Destroy em tabuleiros grandes. O pool continuará sendo um
     *    detalhe da apresentação.
     *
     * 4. DamageContribution e DamageStep poderão ser percorridos da mesma forma
     *    que ScoreBreakdown.Steps, exibindo defesa, vulnerabilidade, imunidade e
     *    reflexão uma etapa por vez sem duplicar suas fórmulas.
     *
     * 5. Animações especiais de runas deverão observar SourceId ou um futuro
     *    PresentationCue produzido pelo motor de efeitos. Elas não devem procurar
     *    componentes de runa para decidir novamente o que aconteceu.
     *
     * 6. Para replay, PlayResolution poderá receber uma velocidade ou um relógio
     *    de apresentação. Os relatórios não mudam, apenas o tempo visual.
     */
}