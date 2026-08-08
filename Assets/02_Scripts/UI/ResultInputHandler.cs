using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 런이 끝난 결과 화면에서 "다시하기"/"카드"/"타이틀"을 타이핑해 고른다. 손패 자리에
// PauseManager의 명령 카드 연출과 같은 방식으로 카드 세 장을 띄운다 - 손패는 DeckManager가
// 패배 시 이미 무너뜨려(PlayCollapse) 비워둔 상태라 자리가 남는다.
//
// 카드가 뜨는 조건은 BattleManager.IsFinalResult다 - 패배와 전체 클리어 둘뿐이고, 일반
// 스테이지 클리어는 보상 선택으로 이어지므로 제외된다. "카드"는 진 판에서 사전이 어땠는지
// 돌아보라고 있는 것이라 두 경우 모두에서 의미가 있다.
//
// ⚠️ 그래도 WantsInput은 결과 화면 전체(IsGameOver)에서 true를 유지한다 - 그래야 우선순위상
// 이 수신자가 입력을 붙들어, 일반 클리어 화면에서 친 글자가 손패(CardInputHandler)로 새어
// 조합이 쌓이는 일이 없다. 그 구간에는 Targets가 전부 빈 문자열이라 아무것도 완성되지 않는다.
//
// PauseManager와 같은 PauseHand(resultCardsLayout)를 쓰는데, 그래도 겹치지 않는다 -
// IsFinalResult인 동안은 PauseManager가 ESC를 받아도 멈추지 않기 때문이다.
public class ResultInputHandler : CommandWordReceiver
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private StageManager stageManager;

    [Header("명령 카드 (CardLocalization.json의 id)")]
    [Tooltip("재도전할 카드의 id.")]
    [SerializeField] private string retryCardId = "retry";

    [Tooltip("보유 카드 목록을 열 카드의 id. PauseManager와 같은 id를 그대로 쓰면 된다.")]
    [SerializeField] private string cardsCardId = "cards";

    [Tooltip("타이틀로 나갈 카드의 id. PauseManager와 같은 id를 그대로 써도 된다 - " +
             "같은 행동이라 단어를 새로 만들 이유가 없다.")]
    [SerializeField] private string titleCardId = "title";

    private CommandCardData retryCard;
    private CommandCardData cardsCard;
    private CommandCardData titleCard;

    [Tooltip("\"카드\"를 쳤을 때 열 보유 카드 목록. PauseManager가 쓰는 것과 같은 인스턴스를 " +
             "연결한다. 비워두면 카드 명령이 통째로 사라진다 - 열 창이 없는데 단어만 남으면 " +
             "쳐도 아무 일이 안 일어난다(PauseManager와 같은 처리).")]
    [SerializeField] private CardCollectionPanel cardCollectionPanel;

    [Tooltip("두 카드를 배치할, 결과 화면 전용 HandFanLayout(PauseManager.commandCardsLayout과 " +
             "같은 성격). Card Slot Manager는 비워둘 것 - 자동 스폰 없이 이 스크립트가 직접 " +
             "두 장만 채운다.")]
    [SerializeField] private HandFanLayout resultCardsLayout;

    [Tooltip("카드로 스폰할 프리팹. 보통 손패와 같은 Card.prefab.")]
    [SerializeField] private CardSlotView commandCardPrefab;

    [Tooltip("카드가 뜨는 방향으로 움직이는 거리(px).")]
    [SerializeField] private float cardTransitionHeight = 80f;

    [Tooltip("카드 목록을 열 때 결과 명령 카드들이 아래로 떨어지는 거리(px).")]
    [SerializeField] private float collectionCardTransitionHeight = 600f;

    [Tooltip("평소 손패를 그리는 HandFanLayout(Card Canvas 쪽). 패배 시 손패가 무너져 다 떨어질 " +
             "때까지 기다렸다가 명령 카드를 띄우는 데 쓴다. 비워두면 기다리지 않고 " +
             "cardsAppearDelay만으로 뜨므로, 떨어지는 카드와 겹칠 수 있다.")]
    [SerializeField] private HandFanLayout handFanLayout;

    [Tooltip("카드가 나타나기 시작하기까지의 지연 시간(초). 패배 시에는 손패가 다 무너진 " +
             "뒤부터 센다 - 무너짐 길이(CardSlotView의 collapseDuration + DeckManager의 " +
             "collapseStagger)를 여기에 더해 어림잡을 필요가 없다.")]
    [SerializeField] private float cardsAppearDelay = 0.8f;

    [Tooltip("카드가 떠오르는 데 걸리는 시간(초). CardSlotView 기본값(0.15초)보다 느리게, " +
             "천천히 뜨도록 여기서 따로 늘려 넘긴다.")]
    [SerializeField] private float cardsAppearDuration = 0.6f;

    [SerializeField] private bool logDebugEvents;

    // 명령 단어를 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    // 순서가 곧 OnCommandMatched의 index이자 화면에 놓이는 카드 순서다.
    private readonly string[] _targets = new string[3];
    private const int RetryIndex = 0;
    private const int CardsIndex = 1;
    private const int TitleIndex = 2;

    private readonly List<CardSlotView> _cardInstances = new List<CardSlotView>();
    private Coroutine _showRoutine;

    /// 결과 화면은 손패보다 먼저 가져간다 - 안 그러면 "다시하기"의 첫 글자가 손패에 없는 단어라
    /// 오타로 처리되어 명령 단어를 끝까지 칠 수 없다.
    public override TypingPriority Priority => TypingPriority.Result;

    public override bool WantsInput() => battleManager != null && battleManager.IsGameOver;

    /// <summary>런이 끝나 명령 카드를 띄워야 하는 상태인가(패배·전체 클리어). 일반 스테이지
    /// 클리어는 보상 선택과 자동 진행이 대신하므로 칠 단어도, 뜨는 카드도 없다.</summary>
    private bool ShowsCards => battleManager != null && battleManager.IsFinalResult;

    private bool HasCardCollection => cardCollectionPanel != null && cardsCard != null;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            // 일반 클리어 구간에서는 빈 문자열을 둔다. 베이스가 빈 항목을 매칭과 진행 판정
            // 양쪽에서 건너뛰므로, 입력은 계속 이쪽이 붙들면서 아무 단어도 완성되지 않는다.
            var active = ShowsCards;

            _targets[RetryIndex] = active ? WordOf(retryCard) : string.Empty;

            // 목록 창이 연결되지 않았으면 빈 문자열로 둔다 - 열 창이 없는데 단어만 살아 있는
            // 상태가 되지 않게 한다(PauseManager와 같은 처리).
            _targets[CardsIndex] = active && HasCardCollection ? WordOf(cardsCard) : string.Empty;

            _targets[TitleIndex] = active ? WordOf(titleCard) : string.Empty;
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
        // ⚠️ "카드"는 카드를 치우지 않는다 - 목록을 닫고 이 화면으로 돌아와야 하므로,
        // 화면을 떠나는 두 명령(재시작·타이틀)만 정리한다.
        // 목록이 열려 있는 동안은 우선순위(TypingPriority.CardCollection)가 그쪽으로 넘어가
        // 여기로 입력이 오지 않고, 닫으면 저절로 돌아온다(PauseManager와 같다).
        if (index == CardsIndex)
        {
            if (logDebugEvents)
                Debug.Log("ResultInput: 명령 단어 입력 - 카드", this);

            cardCollectionPanel.Open();
            return;
        }

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

        // 명령 카드는 여기서 한 번만 꺼낸다(PauseManager와 같은 이유 - Targets는 글자마다 불린다).
        retryCard = CardDatabase.Get<CommandCardData>(retryCardId, this, nameof(retryCardId));
        cardsCard = CardDatabase.Get<CommandCardData>(cardsCardId, this, nameof(cardsCardId));
        titleCard = CardDatabase.Get<CommandCardData>(titleCardId, this, nameof(titleCardId));

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

        if (cardCollectionPanel != null)
        {
            cardCollectionPanel.Opened += HandleCollectionOpened;
            cardCollectionPanel.Closed += HandleCollectionClosed;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (battleManager != null)
            battleManager.OnBattleEnded -= HandleBattleEnded;

        if (cardCollectionPanel != null)
        {
            cardCollectionPanel.Opened -= HandleCollectionOpened;
            cardCollectionPanel.Closed -= HandleCollectionClosed;
        }

        HideCards();
    }

    private void HandleCollectionOpened()
    {
        foreach (var card in _cardInstances)
        {
            if (card != null)
                card.PlayExit(-collectionCardTransitionHeight, () => card.gameObject.SetActive(false));
        }
    }

    private void HandleCollectionClosed()
    {
        if (!ShowsCards)
            return;

        foreach (var card in _cardInstances)
        {
            if (card != null)
                card.PlayEnter(-collectionCardTransitionHeight);
        }
    }

    // OnBattleEnded는 승패와 무관하게 isGameOver가 false -> true로 바뀌는 순간 한 번 온다.
    // 일반 스테이지 클리어에는 카드를 띄우지 않는다 - 보상/자동 진행이 그 화면을 대신한다.
    //
    // ⚠️ BattleManager.ShowResult가 lastResultKind를 세운 <b>뒤에</b> 이 이벤트를 쏘므로
    // 여기서 IsFinalResult를 읽어도 값이 이미 최신이다. 순서를 뒤집으면 패배·전체 클리어에서
    // 카드가 아예 안 뜬다.
    private void HandleBattleEnded()
    {
        if (!ShowsCards)
            return;

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);

        _showRoutine = StartCoroutine(ShowCardsAfterDelay());
    }

    private IEnumerator ShowCardsAfterDelay()
    {
        // ⚠️ 패배 손패 무너짐(DeckManager가 거는 CardSlotView.PlayCollapse)이 다 끝난 뒤에
        // 올라와야 한다. 명령 카드가 같은 아래쪽 자리로 떠오르기 때문에, 겹치면 떨어지는 카드와
        // 올라오는 카드가 한 화면에서 엇갈린다.
        //
        // 고정 지연으로 어림잡지 않는 이유: 무너짐 길이는 "collapseDuration + (장수-1) x
        // collapseStagger"라 카드 수와 두 인스펙터 값에 따라 변한다. 그중 하나만 바뀌어도
        // 여기 적어둔 숫자가 조용히 어긋난다 - 실제로 끝났는지 물어보는 쪽이 맞다.
        //
        // 전체 클리어에는 애초에 무너짐이 없으므로(패배에서만 건다) 이 대기가 곧바로 통과한다.
        if (handFanLayout != null)
            yield return new WaitUntil(() => !handFanLayout.IsLeaving);

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

        // Targets와 같은 순서로 만든다 - 화면에 놓이는 순서가 곧 명령 순서다.
        // 목록 창이 없으면 "카드" 카드도 만들지 않는다(쳐도 아무 일이 안 일어나는 카드를
        // 자리만 차지하게 두지 않는다 - PauseManager.ShowCommandCards와 같다).
        SpawnCard(retryCard);

        if (HasCardCollection)
            SpawnCard(cardsCard);

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
