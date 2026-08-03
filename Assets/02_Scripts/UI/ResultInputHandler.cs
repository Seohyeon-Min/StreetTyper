using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 결과 화면에서 "다시하기"/"타이틀로"를 타이핑해 고른다. 패배했을 때만 뜨고, 손패 자리에
// PauseManager의 명령 카드 연출과 같은 방식으로 카드 두 장을 띄운다 - 손패는 DeckManager가
// 패배 시 이미 무너뜨려(PlayCollapse) 비워둔 상태라 자리가 남는다.
//
// ⚠️ 이긴 판에는 카드가 없다. 예전엔 "다음"이 있었지만 승리는 보상 선택이 끝나면
// StageManager가 자동으로 넘기므로 쓰이지 않아 삭제했다. 그래도 WantsInput은 결과 화면
// 전체에서 true를 유지한다 - 그래야 우선순위상 이 수신자가 입력을 붙들어, 승리 화면에서
// 친 글자가 손패(CardInputHandler)로 새어 조합이 쌓이는 일이 없다.
public class ResultInputHandler : CommandWordReceiver
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private StageManager stageManager;

    [Tooltip("승리인지 패배인지 판별하는 데 쓴다.")]
    [SerializeField] private CharacterStats player;

    [Header("명령 카드")]
    [Tooltip("재도전할 카드. 04_Data/Cards/Commands/Retry")]
    [SerializeField] private CommandCardData retryCard;

    [Tooltip("타이틀로 나갈 카드. PauseManager.titleCard와 같은 04_Data/Cards/Commands/Title " +
             "에셋을 그대로 연결해도 된다 - 같은 행동이라 단어를 새로 만들 이유가 없다.")]
    [SerializeField] private CommandCardData titleCard;

    [Tooltip("두 카드를 배치할, 결과 화면 전용 HandFanLayout(PauseManager.commandCardsLayout과 " +
             "같은 성격). Card Slot Manager는 비워둘 것 - 자동 스폰 없이 이 스크립트가 직접 " +
             "두 장만 채운다.")]
    [SerializeField] private HandFanLayout resultCardsLayout;

    [Tooltip("카드로 스폰할 프리팹. 보통 손패와 같은 Card.prefab.")]
    [SerializeField] private CardSlotView commandCardPrefab;

    [Tooltip("카드가 뜨는 방향으로 움직이는 거리(px).")]
    [SerializeField] private float cardTransitionHeight = 80f;

    [Tooltip("패배 화면이 뜨고 나서 카드가 나타나기 시작하기까지의 지연 시간(초).")]
    [SerializeField] private float cardsAppearDelay = 0.8f;

    [Tooltip("카드가 떠오르는 데 걸리는 시간(초). CardSlotView 기본값(0.15초)보다 느리게, " +
             "천천히 뜨도록 여기서 따로 늘려 넘긴다.")]
    [SerializeField] private float cardsAppearDuration = 0.6f;

    [SerializeField] private bool logDebugEvents;

    // 명령 단어를 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    // 순서가 곧 OnCommandMatched의 index이자 화면에 놓이는 카드 순서다.
    private readonly string[] _targets = new string[2];
    private const int RetryIndex = 0;
    private const int TitleIndex = 1;

    private readonly List<CardSlotView> _cardInstances = new List<CardSlotView>();
    private Coroutine _showRoutine;

    /// 결과 화면은 손패보다 먼저 가져간다 - 안 그러면 "다시하기"의 첫 글자가 손패에 없는 단어라
    /// 오타로 처리되어 명령 단어를 끝까지 칠 수 없다.
    public override TypingPriority Priority => TypingPriority.Result;

    public override bool WantsInput() => battleManager != null && battleManager.IsGameOver;

    /// <summary>이번 판을 이겼는지. 이긴 판에는 칠 단어도, 뜨는 카드도 없다.</summary>
    private bool IsVictory => player != null && player.currentHP > 0;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            // 이긴 판에서는 빈 문자열을 둔다. 베이스가 빈 항목을 매칭과 진행 판정 양쪽에서
            // 건너뛰므로, 입력은 계속 이쪽이 붙들면서 아무 단어도 완성되지 않는다.
            _targets[RetryIndex] = IsVictory ? string.Empty : WordOf(retryCard);
            _targets[TitleIndex] = IsVictory ? string.Empty : WordOf(titleCard);
            return _targets;
        }
    }

    // 카드가 연결되지 않았으면 빈 문자열이다. 베이스가 빈 항목을 건너뛰므로 그 명령만 조용히
    // 사라지고 나머지는 그대로 동작한다(연결 누락은 Awake에서 따로 경고한다).
    private static string WordOf(CommandCardData card) => card != null ? card.CardName : string.Empty;

    // 명령 단어가 카드로 화면에 그대로 뜨므로 안내 문구를 따로 쓰지 않는다(PauseManager와 같다).
    public override string BuildHint() => string.Empty;

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        // 고른 순간 카드는 더 이상 필요 없다 - 화면이 곧바로 재시작되거나 타이틀로 넘어간다.
        HideCards();

        switch (index)
        {
            case RetryIndex:
                if (logDebugEvents)
                    Debug.Log("ResultInput: 명령 단어 입력 - 다시하기", this);

                if (stageManager == null)
                {
                    Debug.LogWarning("ResultInputHandler: stageManager가 연결되지 않아 재시작할 수 없습니다.", this);
                    return;
                }

                stageManager.RestartStage();
                break;

            case TitleIndex:
                if (logDebugEvents)
                    Debug.Log("ResultInput: 명령 단어 입력 - 타이틀로", this);

                ReturnToTitle();
                break;
        }
    }

    private void Awake()
    {
        if (battleManager == null)
            Debug.LogWarning("ResultInputHandler: battleManager가 연결되지 않아 결과 화면인지 알 수 없습니다.", this);

        if (stageManager == null)
            Debug.LogWarning("ResultInputHandler: stageManager가 연결되지 않아 스테이지를 넘길 수 없습니다.", this);

        if (player == null)
            Debug.LogWarning("ResultInputHandler: player가 연결되지 않아 승패를 판별할 수 없습니다.", this);

        if (retryCard == null)
            Debug.LogWarning("ResultInputHandler: retryCard가 연결되지 않아 재도전할 단어가 없습니다.", this);

        if (titleCard == null)
            Debug.LogWarning("ResultInputHandler: titleCard가 연결되지 않아 타이틀로 나갈 단어가 없습니다.", this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (battleManager != null)
            battleManager.OnBattleEnded += HandleBattleEnded;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (battleManager != null)
            battleManager.OnBattleEnded -= HandleBattleEnded;

        HideCards();
    }

    // OnBattleEnded는 승패와 무관하게 isGameOver가 false -> true로 바뀌는 순간 한 번 온다.
    // 이긴 판(승리·전체 클리어)에는 카드를 띄우지 않는다 - 보상/자동 진행이 그 화면을 대신한다.
    private void HandleBattleEnded()
    {
        if (IsVictory)
            return;

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);

        _showRoutine = StartCoroutine(ShowCardsAfterDelay());
    }

    private IEnumerator ShowCardsAfterDelay()
    {
        if (cardsAppearDelay > 0f)
            yield return new WaitForSeconds(cardsAppearDelay);

        _showRoutine = null;
        ShowCards();
    }

    private void ShowCards()
    {
        if (resultCardsLayout == null || commandCardPrefab == null)
            return;

        // 씬에 비활성으로 남아 있는 습관을 대비한다(PauseManager.ShowCommandCards와 같은 이유) -
        // 부모가 꺼져 있으면 자식만 켜도 activeInHierarchy가 false라 코루틴이 실패한다.
        resultCardsLayout.gameObject.SetActive(true);

        SpawnCard(retryCard);
        SpawnCard(titleCard);
    }

    private void SpawnCard(CommandCardData card)
    {
        // 연결이 빠진 명령은 아예 만들지 않는다 - 빈 카드가 자리만 차지하면 부채꼴 배치가
        // 어긋나고 플레이어는 칠 수 없는 카드를 보게 된다(Targets 쪽도 같은 이유로 비운다).
        if (card == null)
            return;

        var instance = Instantiate(commandCardPrefab, resultCardsLayout.transform, false);
        _cardInstances.Add(instance);

        // duration을 따로 넘겨 CardSlotView 기본값보다 천천히 떠오르게 한다.
        instance.PlayEnter(-cardTransitionHeight, () => instance.BindStatic(card, inputManager), cardsAppearDuration);
    }

    private void HideCards()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        foreach (var instance in _cardInstances)
        {
            if (instance != null)
                Destroy(instance.gameObject);
        }

        _cardInstances.Clear();
    }

    public void ReturnToTitle()
    {
        if (inputManager != null)
            inputManager.ClearInput();

        // 씬을 넘어가도 timeScale은 유지된다 - 여기서 되돌리지 않으면 타이틀이 멈춘 채로 뜬다.
        Time.timeScale = 1f;

        if (SoundManager.Instance != null)
            SoundManager.Instance.StopBGM();

        SceneManager.LoadScene(GameScenes.Title);
    }
}
