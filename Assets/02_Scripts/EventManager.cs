using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;
    public CharacterStats player;

    [Tooltip("마더 드래곤 아웃로에서 회복 대사가 뜨기 전 데미의 웃는 애니메이션을 재생하기 위해 참조한다.")]
    public PlayerBattleVisuals playerVisuals;

    [Tooltip("대사를 넘기는 스페이스 입력을 받기 위해 참조한다. 키보드를 직접 읽지 않는다.")]
    public InputManager inputManager;

    [Header("Mother Dragon Setup")]
    [Tooltip("⚠️ 지금 이 두 칸은 MotherDragon.prefab 안의 없는 fileID를 가리키고 있어 런타임에 " +
             "null이다(머지 잔재). 대사 말풍선은 이제 씬에 실제로 스폰된 적을 따라가므로 비어 " +
             "있어도 정상 동작하며, 여기에 값이 있으면 그쪽을 우선한다.")]
    public GameObject motherDragonVisual;
    public Transform motherDragonTransform;

    [Header("Speech Bubble Settings")]
    public GameObject speechBubblePrefab;
    [Tooltip("마더 드래곤 이벤트 전용 말풍선 프리팹. 비워두면 위의 일반 말풍선을 사용한다. " +
             "마더 드래곤 본인의 대사에만 사용한다.")]
    public GameObject motherDragonSpeechBubblePrefab;
    [Tooltip("마더 드래곤 이벤트에서 플레이어가 말할 때 사용할 전용 프리팹. 비워두면 " +
             "SpeechBubbleManager의 플레이어 전용 프리팹, 그것도 비어 있으면 일반 말풍선을 사용한다.")]
    public GameObject motherDragonPlayerSpeechBubblePrefab;
    public Transform canvasTransform;
    [Tooltip("적이 말할 때의 미세 조정. 기본 위치는 SpeechBubbleManager가 다른 말풍선과 같은 " +
             "규칙으로 잡고, 이 값은 거기서 더 밀어내는 양이다.\n" +
             "⚠️ 단위가 참조 해상도(1920x1080) 픽셀이다 - 예전의 월드 단위가 아니다(월드 1은 " +
             "1080p에서 100픽셀이 넘어 미세 조정이 불가능했다).")]
    public Vector2 bubbleScreenOffset = Vector2.zero;

    [Tooltip("회복 대사처럼 플레이어(데미)가 말할 때의 미세 조정. 적보다 스프라이트가 작아 " +
             "따로 둔다. 단위는 위와 같은 참조 해상도 픽셀이다.")]
    public Vector2 playerBubbleScreenOffset = Vector2.zero;

    [Tooltip("마더 드래곤 아웃로 전용: 0번 대사(\"여기까지 하자꾸나!\") 다음, 회복 대사가 " +
             "뜨기 전에 웃는 애니메이션 + 회복 이펙트를 재생하며 두는 대기 시간(초).")]
    public float mdSmileDelay = 0.8f;

    [Tooltip("마더 드래곤 아웃로 대사 한 줄이 화면에 떠 있다가 저절로 다음으로 넘어가는 시간(초). " +
             "이 아웃로는 스페이스로 넘기지 않고 시간이 지나면 자동으로 진행된다.")]
    public float mdLineAutoAdvanceDelay = 1.8f;

    [Header("Ending Exit")]
    [Min(0.1f)] public float endingExitDuration = 2.2f;
    [Min(0f)] public float endingExitScreenMargin = 0.18f;
    [Min(0f)] public float endingBackgroundScrollSpeed = 0.8f;
    public AnimationCurve endingExitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Dialogues (Inspector에서 변경 가능)")]
    public string[] normalEventLines = { "새로운 단어 카드를 획득했다!", "placeholder1", "placeholder2" };
    public string[] dragonEventLines = { "placeholder0", "placeholder1", "placeholder2" };

    [Header("Dialogues - Ending")]
    public string[] endingEventLines = { "플레이스홀더텍스트0", "플레이스홀더텍스트1" };
    [Tooltip("영어 모드에서 사용할 엔딩 대사. 비어 있으면 한국어 엔딩 대사를 사용합니다.")]
    public string[] endingEventLinesEn = { "Placeholder text 0", "Placeholder text 1" };
    private bool _isEndingEvent = false;

    // ⚠️ 기본값을 비워 둔다. EventManager.prefab의 한국어 배열이 둘 다 빈 배열이라서,
    // 여기에 대사를 채우면 영어 모드에서만 대사가 생겨 스페이스를 여러 번 눌러야 넘어가게 된다.
    // 그 사이 ShowResult가 불리지 않아 결과 화면도 클리어 보상도 나오지 않는다 - 실제로 겪은 회귀다.
    // 한국어 대사를 채우게 되면 이쪽도 같은 개수로 함께 채울 것.
    [Header("Dialogues - English")]
    public string[] normalEventLinesEn = { };
    public string[] dragonEventLinesEn = { };

    private GameObject dialogueBubbleObj;
    private SpeechBubble dialogueBubbleScript;
    private GameObject currentDialogueBubblePrefab;

    // 매 프레임 GetComponent를 부르지 않으려고 캐시한다.
    private RectTransform _dialogueBubbleRect;
    private Canvas _dialogueCanvas;

    private int currentLineIndex = 0;
    private List<string> activeDialogueLines = new List<string>();

    private bool isEventActive = false;

    /// <summary>대화가 열려 있는가. 이벤트 스테이지는 대사가 끝날 때까지
    /// <c>BattleManager.IsGameOver</c>가 false로 남으므로, "지금 대사 중이라 전투를 진행시키면
    /// 안 된다"는 판단은 이 값으로 해야 한다(BattleManager.IsEventActive가 중계한다).</summary>
    public bool IsEventActive => isEventActive;

    private int pendingHealAmount = 0;
    private bool wasMotherDragon = false;

    // 이번 이벤트에서 말풍선이 따라갈 대상. 인스펙터의 motherDragonTransform은 프리팹 에셋
    // 안쪽을 가리키는 깨진 참조라 런타임에 null이고, 애초에 화면에 보이는 건 씬에 스폰된
    // 인스턴스다 - 그래서 이벤트가 열릴 때 살아있는 적에서 다시 잡는다.
    private Transform bubbleAnchor;

    // 대사에 맞춰 말하는 모션을 재생할 적. 위와 같은 이유로 런타임에 잡는다.
    private EnemyBase speakingEnemy;

    // 지금 화자의 CharacterStats. bubbleAnchor(Transform)만으로는 "그 캐릭터의 어느 지점"인지
    // 알 수 없어서(넓은 스프라이트는 원점과 머리 위 지점이 다르다) 대신 CharacterStats.BubblePosition을
    // 쓴다 - 각 캐릭터가 자기 자식 "Pos" Transform으로 정확한 지점을 직접 들고 있다.
    // null이면(motherDragonTransform 같은 옛 배선) bubbleAnchor.position으로 그냥 폴백한다.
    private CharacterStats speakerCharacter;

    // 지금 말하는 쪽이 플레이어인가. 말풍선을 누구 머리 위에 띄울지와 어느 오프셋을 쓸지가 갈린다.
    private bool speakingIsPlayer;

    private void OnEnable()
    {
        if (inputManager != null)
            inputManager.OnAdvance += HandleAdvance;
        else
            Debug.LogWarning("EventManager: inputManager가 연결되지 않았습니다. normalEventLines(스페이스로 " +
                             "넘기는 경로)가 막힙니다 - 마더 드래곤 아웃로는 시간 기반이라 영향받지 않습니다. " +
                             "씬 인스턴스에서 연결하세요.", this);
    }

    private void OnDisable()
    {
        if (inputManager != null)
            inputManager.OnAdvance -= HandleAdvance;
    }

    void Start()
    {
        if (motherDragonVisual != null) motherDragonVisual.SetActive(false);

        // EventManager에 연결한 두 전용 프리팹을 보스 등장 대화/인텐트 경로도 같이 사용한다.
        if (SpeechBubbleManager.Instance != null)
        {
            if (motherDragonSpeechBubblePrefab != null)
                SpeechBubbleManager.Instance.motherDragonSpeechBubblePrefab = motherDragonSpeechBubblePrefab;
            if (motherDragonPlayerSpeechBubblePrefab != null)
                SpeechBubbleManager.Instance.motherDragonPlayerSpeechBubblePrefab = motherDragonPlayerSpeechBubblePrefab;
        }
    }

    private string[] GetEndingLines()
    {
        if (!LanguageSettings.IsEnglish || endingEventLinesEn == null || endingEventLinesEn.Length == 0)
            return endingEventLines;

        int koreanCount = endingEventLines != null ? endingEventLines.Length : 0;
        int count = Mathf.Max(koreanCount, endingEventLinesEn.Length);
        string[] localized = new string[count];

        for (int i = 0; i < count; i++)
        {
            bool hasEnglish = i < endingEventLinesEn.Length && !string.IsNullOrEmpty(endingEventLinesEn[i]);
            localized[i] = hasEnglish
                ? endingEventLinesEn[i]
                : (i < koreanCount ? endingEventLines[i] : string.Empty);
        }

        return localized;
    }

    public void StartEvent(bool isMotherDragon, int healAmount = 0, bool isEnding = false) // [수정] isEnding 매개변수 추가
    {
        isEventActive = true;
        currentLineIndex = 0;
        pendingHealAmount = healAmount;
        wasMotherDragon = isMotherDragon;
        _isEndingEvent = isEnding; // [추가]

        CreateDialogueBubble(isMotherDragon, false);

        ResolveBubbleAnchor();
        activeDialogueLines.Clear();

        string[] linesToUse;
        if (isEnding)
        {
            linesToUse = GetEndingLines();
        }
        else if (LanguageSettings.IsEnglish)
        {
            var en = isMotherDragon ? dragonEventLinesEn : normalEventLinesEn;
            linesToUse = en != null && en.Length > 0 ? en : (isMotherDragon ? dragonEventLines : normalEventLines);
        }
        else
        {
            linesToUse = isMotherDragon ? dragonEventLines : normalEventLines;
        }

        foreach (string line in linesToUse)
        {
            activeDialogueLines.Add(string.Format(line, healAmount));
        }

        if (motherDragonVisual != null) motherDragonVisual.SetActive(true);
        if (dialogueBubbleObj != null) dialogueBubbleObj.SetActive(true);

        // [수정] 엔딩 전용 코루틴 분기 추가
        if (isEnding)
            StartCoroutine(PlayEndingRoutine());
        else if (isMotherDragon)
            StartCoroutine(PlayMotherDragonOutroRoutine());
        else
            ShowNextDialogue();
    }

    private void CreateDialogueBubble(bool isMotherDragon, bool fromPlayer)
    {
        var prefab = ResolveDialogueBubblePrefab(isMotherDragon, fromPlayer);
        if (dialogueBubbleObj != null && currentDialogueBubblePrefab == prefab)
            return;

        if (dialogueBubbleObj != null)
        {
            dialogueBubbleObj.SetActive(false);
            Destroy(dialogueBubbleObj);
        }

        dialogueBubbleObj = null;
        dialogueBubbleScript = null;
        _dialogueBubbleRect = null;
        _dialogueCanvas = null;

        if (canvasTransform == null)
            return;

        if (prefab == null)
            return;

        dialogueBubbleObj = Instantiate(prefab, canvasTransform);
        currentDialogueBubblePrefab = prefab;
        dialogueBubbleScript = dialogueBubbleObj.GetComponent<SpeechBubble>();
        dialogueBubbleObj.SetActive(false);
    }

    private GameObject ResolveDialogueBubblePrefab(bool isMotherDragon, bool fromPlayer)
    {
        if (!isMotherDragon)
            return speechBubblePrefab;
        if (fromPlayer)
        {
            var playerPrefab = motherDragonPlayerSpeechBubblePrefab;
            if (playerPrefab == null && SpeechBubbleManager.Instance != null)
                playerPrefab = SpeechBubbleManager.Instance.motherDragonPlayerSpeechBubblePrefab;
            return playerPrefab != null ? playerPrefab : speechBubblePrefab;
        }
        return motherDragonSpeechBubblePrefab != null ? motherDragonSpeechBubblePrefab : speechBubblePrefab;
    }

    private IEnumerator PlayEndingRoutine()
    {
        // 체력 회복 없이 입력된 대사만 차례대로 출력합니다.
        for (int i = 0; i < activeDialogueLines.Count; i++)
        {
            SpeakLine(i, fromPlayer: false); // 마더 드래곤이 말함
            yield return new WaitForSeconds(mdLineAutoAdvanceDelay);
        }

        if (dialogueBubbleObj != null)
            dialogueBubbleObj.SetActive(false);

        yield return PlayEndingExitRoutine();
        EndEvent();
    }

    private IEnumerator PlayEndingExitRoutine()
    {
        Transform playerTransform = player != null ? player.transform : null;
        Transform motherTransform = speakingEnemy != null ? speakingEnemy.transform : bubbleAnchor;

        if (battleManager != null && battleManager.stageManager != null &&
            battleManager.stageManager.backgroundScrollers != null)
        {
            foreach (var scroller in battleManager.stageManager.backgroundScrollers)
            {
                if (scroller != null)
                    scroller.StartConstantScroll(endingBackgroundScrollSpeed);
            }
        }

        Vector3 playerStart = playerTransform != null ? playerTransform.position : Vector3.zero;
        Vector3 motherStart = motherTransform != null ? motherTransform.position : Vector3.zero;
        Vector3 playerTarget = OffscreenTarget(playerTransform, playerStart, false);
        Vector3 motherTarget = OffscreenTarget(motherTransform, motherStart, true);

        float duration = Mathf.Max(0.1f, endingExitDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = endingExitCurve != null ? endingExitCurve.Evaluate(t) : t;

            if (playerTransform != null)
                playerTransform.position = Vector3.LerpUnclamped(playerStart, playerTarget, eased);
            if (motherTransform != null)
                motherTransform.position = Vector3.LerpUnclamped(motherStart, motherTarget, eased);

            yield return null;
        }

        if (playerTransform != null) playerTransform.position = playerTarget;
        if (motherTransform != null) motherTransform.position = motherTarget;
    }

    private Vector3 OffscreenTarget(Transform subject, Vector3 start, bool exitRight)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return start + Vector3.right * (exitRight ? 14f : -14f);

        Vector3 viewport = camera.WorldToViewportPoint(start);
        viewport.x = exitRight ? 1f + endingExitScreenMargin : -endingExitScreenMargin;
        Vector3 target = camera.ViewportToWorldPoint(viewport);
        SpriteRenderer renderer = subject != null ? subject.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer != null)
            target.x += renderer.bounds.extents.x * (exitRight ? 1f : -1f);
        target.y = start.y;
        target.z = start.z;
        return target;
    }

    // 말풍선이 따라갈 대상과 말하는 모션을 재생할 적을 이벤트가 열릴 때 잡는다.
    // 인스펙터의 motherDragonTransform이 살아 있으면 그쪽을 우선하고(옛 배선 존중),
    // 없으면 씬에 실제로 스폰된 적에서 가져온다. 적은 이 시점에 SetActive(false)로 꺼져 있지만
    // Transform은 그대로라 위치를 읽는 데는 문제가 없다(BattleManager.CheckGameState 참조).
    private void ResolveBubbleAnchor()
    {
        speakingEnemy = battleManager != null && battleManager.enemyManager != null
            ? battleManager.enemyManager.currentEnemy
            : null;

        bubbleAnchor = motherDragonTransform;
        speakerCharacter = null;

        if (bubbleAnchor == null && speakingEnemy != null)
        {
            bubbleAnchor = speakingEnemy.transform;
            speakerCharacter = speakingEnemy;
        }

        if (bubbleAnchor == null)
            Debug.LogWarning("EventManager: 말풍선을 붙일 대상을 찾지 못했습니다 - 대사가 화면 " +
                             "엉뚱한 곳에 뜹니다. motherDragonTransform을 연결하거나 " +
                             "battleManager 연결을 확인하세요.", this);
    }

    // 스페이스는 InputManager가 준다. 마더 드래곤 아웃로는 PlayMotherDragonOutroRoutine이
    // 시간으로 진행시키므로 이 경로를 타지 않는다.
    private void HandleAdvance()
    {
        if (isEventActive && !wasMotherDragon)
            ShowNextDialogue();
    }

    void Update()
    {
        if (!isEventActive) return;

        PositionDialogueBubble();
    }

    // 말풍선 위치는 SpeechBubbleManager의 계산 하나로 통일한다 - 전투 말풍선·적 인텐트와 같은
    // 규칙이라 화자 기준 위치가 화면마다 어긋나지 않는다.
    //
    // ⚠️ 예전에는 여기서 WorldToScreenPoint를 직접 불렀는데 문제가 셋이었다:
    //   ① z를 0으로 지우지 않아(WorldToScreenPoint의 z는 카메라와의 거리다) Overlay 캔버스
    //      평면을 벗어났다.
    //   ② 오프셋이 월드 단위라 1이 1080p에서 100픽셀을 넘어 미세 조정이 사실상 불가능했다.
    //   ③ 캔버스 scaleFactor를 곱하지 않아 해상도가 바뀌면 위치가 밀렸다.
    private void PositionDialogueBubble()
    {
        if (dialogueBubbleObj == null || bubbleAnchor == null || !dialogueBubbleObj.activeSelf)
            return;

        if (_dialogueBubbleRect == null)
            _dialogueBubbleRect = dialogueBubbleObj.GetComponent<RectTransform>();

        if (_dialogueBubbleRect == null || SpeechBubbleManager.Instance == null)
            return;

        // speakerCharacter가 있으면(플레이어·씬에 스폰된 적) BubblePosition을 쓴다 - 그 캐릭터의
        // 자식 "Pos" Transform으로 정확한 지점을 잡는다. motherDragonTransform 같은 옛 Transform
        // 배선만 있으면 그 위치를 그대로 쓴다.
        Vector3 anchorPosition = speakerCharacter != null ? speakerCharacter.BubblePosition : bubbleAnchor.position;
        Vector3 screenPos = SpeechBubbleManager.Instance.GetBubbleScreenPosition(anchorPosition, speakingIsPlayer);

        // 그 위에 이 화면만의 미세 조정을 얹는다. 참조 해상도 기준 값이라 실제 픽셀로 바꿀 때
        // 캔버스 scaleFactor를 곱한다(SpeechBubbleManager가 자기 오프셋에 하는 것과 같다).
        Vector2 offset = speakingIsPlayer ? playerBubbleScreenOffset : bubbleScreenOffset;

        if (offset != Vector2.zero)
        {
            float scale = DialogueCanvasScale;
            screenPos.x += offset.x * scale;
            screenPos.y += offset.y * scale;
        }

        _dialogueBubbleRect.position = screenPos;
    }

    private float DialogueCanvasScale
    {
        get
        {
            if (_dialogueCanvas == null && canvasTransform != null)
                _dialogueCanvas = canvasTransform.GetComponentInParent<Canvas>();

            return _dialogueCanvas != null ? _dialogueCanvas.scaleFactor : 1f;
        }
    }

    // normalEventLines 전용 경로(스페이스로 한 줄씩 넘긴다). 지금은 실제로 호출하는 곳이
    // 없지만(StartEvent가 isMotherDragon:false로 불리는 곳이 없다), 나중에 쓰이게 되어도
    // 마더 드래곤 아웃로의 시간 기반 진행과 섞이지 않도록 구조를 남겨 둔다.
    private void ShowNextDialogue()
    {
        if (currentLineIndex < activeDialogueLines.Count)
        {
            SpeakLine(currentLineIndex);
            currentLineIndex++;
        }
        else
        {
            EndEvent();
        }
    }

    // 대사 한 줄을 말풍선에 채우고 말하기 애니메이션을 재생한다 - 스페이스 기반 경로와
    // 시간 기반 경로(PlayMotherDragonOutroRoutine)가 같이 쓴다.
    /// <summary>대사 한 줄을 말풍선에 채운다. fromPlayer면 말풍선이 플레이어(데미) 머리 위로
    /// 옮겨가고 적의 말하는 모션도 재생하지 않는다 - 회복 대사가 그렇다.</summary>
    private void SpeakLine(int index, bool fromPlayer = false)
    {
        speakingIsPlayer = fromPlayer;

        if (wasMotherDragon)
        {
            CreateDialogueBubble(true, fromPlayer);
            if (dialogueBubbleObj != null)
                dialogueBubbleObj.SetActive(true);
        }

        // 말풍선이 따라갈 대상을 화자에 맞춰 바꾼다. 플레이어 참조가 없으면 원래 대상에 그대로 둔다.
        if (fromPlayer && player != null)
        {
            bubbleAnchor = player.transform;
            speakerCharacter = player;
        }

        if (dialogueBubbleScript != null)
        {
            // 같은 말풍선 오브젝트를 화자만 바꿔 가며 쓰므로 꼬리 방향도 같이 뒤집어야 한다 -
            // 플레이어는 화면 왼쪽, 적은 오른쪽이라 꼬리가 서로 반대를 향한다.
            dialogueBubbleScript.SetMirrored(fromPlayer);
            dialogueBubbleScript.Setup(activeDialogueLines[index]);
        }

        if (fromPlayer)
            return; // 플레이어가 말하는 줄에서 적의 말하는 모션을 재생하면 화자가 둘로 보인다.

        // 씬에 스폰된 적이 있으면 그쪽 모션을 재생한다(BattleManager가 턴 대사에서 쓰는 것과
        // 같은 경로). 옛 motherDragonVisual 배선이 살아 있는 경우에만 그쪽으로 폴백한다.
        // ⚠️ 적이 이미 꺼져 있으면 Animator 트리거가 경고만 쌓이므로 켜져 있을 때만 재생한다.
        if (speakingEnemy != null && speakingEnemy.gameObject.activeInHierarchy)
        {
            speakingEnemy.PlaySpeakAnimation();
        }
        else if (motherDragonVisual != null)
        {
            Animator anim = motherDragonVisual.GetComponent<Animator>();
            if (anim != null) anim.SetTrigger("Speak");
        }
    }

    // 마더 드래곤 아웃로 전용: 스페이스 없이 시간이 지나면 저절로 넘어간다.
    // 0번 대사("여기까지 하자꾸나!") 표시 -> 대기 -> 웃는 애니메이션 + 회복 이펙트 + 실제
    // 회복 -> 회복 대사 표시 -> 대기 -> 결과 화면. 대사가 하나도 없거나 한 줄뿐이어도(인스펙터
    // 설정 누락) 회복과 결과 화면까지는 계속 진행되도록 각 단계를 개수만큼만 조건부로 밟는다.
    private IEnumerator PlayMotherDragonOutroRoutine()
    {
        // 조용히 건너뛰면 "회복 이펙트만 뜨고 곧바로 보상 화면"처럼 보여서 원인을 찾기 어렵다 -
        // 대사가 모자란 건 배선 실수이므로 시끄럽게 알린다(실제로 겪은 증상이다).
        if (activeDialogueLines.Count < 2)
            Debug.LogWarning($"EventManager: dragonEventLines가 {activeDialogueLines.Count}줄뿐입니다. " +
                             "마더 드래곤 아웃로는 0번=작별 인사, 1번=회복 대사 두 줄을 기대합니다 - " +
                             "모자란 줄은 건너뛰고 진행합니다.", this);

        if (activeDialogueLines.Count > 0)
        {
            SpeakLine(0);
            yield return new WaitForSeconds(mdLineAutoAdvanceDelay);
        }

        yield return new WaitForSeconds(mdSmileDelay);

        if (playerVisuals != null)
            playerVisuals.PlaySmileAnimation();

        if (HealEffectManager.Instance != null && player != null)
            HealEffectManager.Instance.PlayHealEffect(player.GetComponent<SpriteRenderer>());

        if (player != null)
            player.Heal(pendingHealAmount);
        pendingHealAmount = 0;

        // 회복 대사는 데미가 한다 - 말풍선이 플레이어 머리 위로 옮겨간다.
        if (activeDialogueLines.Count > 1)
        {
            SpeakLine(1, fromPlayer: true);
            yield return new WaitForSeconds(mdLineAutoAdvanceDelay);
        }

        EndEvent();
    }

    private void EndEvent()
    {
        isEventActive = false;
        speakingIsPlayer = false;

        // [수정] 엔딩 이벤트가 아닐 때만 적을 숨김 (엔딩 땐 화면에 남겨둠)
        if (!_isEndingEvent && speakingEnemy != null && speakingEnemy.gameObject.activeSelf)
            speakingEnemy.gameObject.SetActive(false);

        if (motherDragonVisual != null) motherDragonVisual.SetActive(false);
        if (dialogueBubbleObj != null) dialogueBubbleObj.SetActive(false);

        if (battleManager != null)
        {
            battleManager.UpdateUI();

            // [수정] 엔딩 이벤트면 보상(Victory) 없이 바로 전체 클리어 화면 호출
            if (_isEndingEvent)
            {
                battleManager.ShowGameClear();
            }
            else
            {
                battleManager.ShowResult(ResultKind.Victory);
            }
        }
    }
}
