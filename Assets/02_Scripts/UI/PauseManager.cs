using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ESC로 게임을 멈추고 메뉴를 띄운다. 메뉴 선택은 버튼 클릭이 아니라 타이핑으로 한다 -
// 멈춘 동안 입력창에 "계속"/"타이틀"을 쳐서 고른다.
//
// 매칭 파이프라인은 TypingReceiver가, 안내 문구 조립은 CommandWordReceiver가 맡는다.
// 여기 남은 것은 "언제 내 차례인가"(멈춰 있을 때), "맞혔을 때 무엇을 하는가", 그리고
// 일시정지 중 손패 자리를 "계속"/"카드"/"타이틀" 카드로 바꿔치기하는 연출뿐이다.
//
// "카드"는 보유 카드 목록(CardCollectionPanel)을 여는 것뿐이고, 그 창이 떠 있는 동안은
// 우선순위(TypingPriority.CardCollection)가 그쪽으로 넘어가 여기로 입력이 오지 않는다 -
// "지금 목록이 열려 있나"를 이 클래스가 상태로 들고 갈라줄 필요가 없다는 뜻이다.
//
// Time.timeScale = 0 하나로 턴 전환 대기(DeckManager/StageManager)·말풍선(BattleManager)·
// 타이머 감소(TimerManager)·카드 애니메이션(CardSlotView/HandFanLayout)이 전부 멈춘다 -
// 시간에 의존하는 코드가 모두 deltaTime/WaitForSeconds 기반이라 개별 정지 처리는 하지 않는다.
public class PauseManager : CommandWordReceiver
{
    [Header("UI")]
    [Tooltip("일시정지 창 루트. 평소엔 비활성이어야 한다.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("\"PAUSE\" 제목이 가운데서부터 열리는 연출. pausePanel을 켤 때마다 재생한다. 비워두면 재생하지 않는다.")]
    [SerializeField] private TextGateRevealAnimation titleReveal;

    [Header("퍼즈 Canvas 정렬")]
    [Tooltip("퍼즈가 열릴 때 모든 Canvas보다 위로 올릴 Canvas. 비우면 pausePanel의 부모에서 찾는다.")]
    [SerializeField] private Canvas pauseCanvas;

    [Tooltip("퍼즈 Canvas보다 한 단계 더 위로 올릴 입력창 Canvas. 비우면 InputFieldDisplay를 찾아 자동 연결한다.")]
    [SerializeField] private Canvas inputCanvas;

    [Header("References")]
    [Tooltip("평소 손패를 그리는 HandFanLayout(Card Canvas 쪽). 일시정지 중엔 5장을 전부 숨긴다.")]
    [SerializeField] private HandFanLayout handFanLayout;

    [Tooltip("일시정지가 풀렸을 때 카드를 원래 슬롯 내용으로 되돌리는 데 필요하다.")]
    [SerializeField] private CardSlotManager cardSlotManager;

    [Tooltip("\"계속\"/\"타이틀\" 카드 2장을 배치할, Pause Canvas 아래의 별도 HandFanLayout. " +
             "패널보다 위에 그려져야 하므로 손패(Card Canvas)가 아니라 Pause Canvas 쪽에 둔다. " +
             "Card Slot Manager는 비워둘 것 - 자동 스폰 없이 이 스크립트가 직접 2장만 채운다.")]
    [SerializeField] private HandFanLayout commandCardsLayout;

    [Tooltip("명령 카드로 스폰할 프리팹. 보통 손패와 같은 Card.prefab.")]
    [SerializeField] private CardSlotView commandCardPrefab;

    [Tooltip("전환 애니메이션에서 카드가 움직이는 거리(px). 기존 5장은 이만큼 아래로 가라앉듯 " +
             "사라지고, 새 명령 카드는 그 자리에서 떠오르듯 나타난다(재개할 땐 반대).")]
    [SerializeField] private float commandCardTransitionHeight = 80f;

    [Tooltip("\"카드\"를 쳤을 때 열 보유 카드 목록. 비워두면 카드 명령이 안내에도 뜨지 않고 " +
             "명령 카드도 만들어지지 않는다 - 열 창이 없는데 단어만 남으면 쳐도 아무 일이 안 일어난다.")]
    [SerializeField] private CardCollectionPanel cardCollectionPanel;

    [Tooltip("런이 끝났는지(패배·전체 클리어) 물어보려고 참조한다. 그 상태에서는 ESC로 멈출 수 " +
             "없다 - 결과 화면이 이미 \"다시하기\"/\"카드\"/\"타이틀\"을 명령 카드로 띄우고 " +
             "있어서, 그 위에 같은 자리를 쓰는 일시정지 메뉴가 겹치면 안 된다.")]
    [SerializeField] private BattleManager battleManager;

    // 명령 단어는 CommandCardData 에셋이다 - 단어(한/영)와 카드 겉모습이 한 곳에 모여 있고,
    // 화면에 카드로 그대로 뜨므로 안내 문구가 따로 필요 없다. 씬 오브젝트가 아니라 에셋 참조라
    // 프리팹에 그대로 저장된다(이 프로젝트에서 드문 경우다).
    [Header("명령 카드")]
    [Tooltip("일시정지를 풀 카드. 04_Data/Cards/Commands/Resume")]
    [SerializeField] private CommandCardData resumeCard;

    [Tooltip("보유 카드 목록을 열 카드. 04_Data/Cards/Commands/Cards")]
    [SerializeField] private CommandCardData cardsCard;

    [Tooltip("타이틀로 나갈 카드. 04_Data/Cards/Commands/Title")]
    [SerializeField] private CommandCardData titleCard;

    private bool _isPaused;
    private bool _inputWasEnabled;
    private bool _canvasOrderRaised;
    private int _pauseCanvasOriginalOrder;
    private int _inputCanvasOriginalOrder;
    private bool _pauseCanvasOriginalOverrideSorting;
    private bool _inputCanvasOriginalOverrideSorting;

    // 명령 단어를 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    // 순서가 곧 OnCommandMatched의 index이자 화면에 놓이는 카드 순서다.
    private readonly string[] _targets = new string[3];

    // Targets/명령 카드의 자리 번호. 숫자를 코드 곳곳에 흩뿌리지 않으려고 여기 모아둔다.
    private const int ResumeIndex = 0;
    private const int CardsIndex = 1;
    private const int TitleIndex = 2;

    // HandFanLayout.Cards는 매 프레임 "활성 자식만" 다시 모아서 채워지는 리스트라, 카드를
    // 꺼버리고 나면 그 순간부터 목록에서 빠져 참조를 잃는다. 그래서 끄기 전에 5장 전부를
    // 여기 스냅샷으로 저장해뒀다가, 복원할 때 이걸 쓴다.
    private readonly List<CardSlotView> _pausedHandCards = new List<CardSlotView>();

    // ShowCommandCards가 commandCardsLayout 밑에 스폰한 명령 카드("계속"/"카드"/"타이틀").
    // Hide 때 파괴한다.
    private readonly List<CardSlotView> _commandCardInstances = new List<CardSlotView>();

    public bool IsPaused => _isPaused;

    /// 일시정지는 무엇보다 우선한다 - 멈춘 화면에서 명령 단어의 첫 글자가 손패 쪽으로 새면
    /// 그 자리에서 오타 처리되어 명령 단어를 끝까지 칠 수 없다.
    public override TypingPriority Priority => TypingPriority.Pause;

    public override bool WantsInput() => _isPaused;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            _targets[ResumeIndex] = WordOf(resumeCard);

            // 목록 창이 연결되지 않았으면 빈 문자열로 둔다 - 베이스가 빈 항목을 매칭과 진행
            // 판정 양쪽에서 건너뛰므로, 열 창이 없는데 단어만 살아 있는 상태가 되지 않는다.
            _targets[CardsIndex] = HasCardCollection ? WordOf(cardsCard) : string.Empty;

            _targets[TitleIndex] = WordOf(titleCard);
            return _targets;
        }
    }

    private bool HasCardCollection => cardCollectionPanel != null && cardsCard != null;

    // 카드가 연결되지 않았으면 빈 문자열이다. 베이스가 빈 항목을 건너뛰므로 그 명령만 조용히
    // 사라지고 나머지는 그대로 동작한다(연결 누락은 Awake에서 따로 경고한다).
    private static string WordOf(CommandCardData card) => card != null ? card.CardName : string.Empty;

    // 명령 단어가 카드로 화면에 그대로 뜨므로 안내 문구를 따로 쓰지 않는다.
    public override string BuildHint() => string.Empty;

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        switch (index)
        {
            case ResumeIndex:
                Resume();
                break;

            // 목록이 열려 있는 동안은 우선순위가 그쪽(TypingPriority.CardCollection)으로 넘어가
            // 여기로 입력이 오지 않는다. 닫으면 저절로 돌아오므로 따로 상태를 들 필요가 없다.
            case CardsIndex:
                cardCollectionPanel.Open();
                break;

            case TitleIndex:
                ReturnToTitle();
                break;
        }
    }

    private void Awake()
    {
        // 이전 플레이에서 멈춘 채로 씬을 다시 불러왔을 수 있다.
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        else
            Debug.LogWarning("PauseManager: pausePanel이 연결되지 않았습니다.", this);

        // 명령 카드가 비어 있으면 그 단어는 조용히 사라진다. 멈춘 화면에서 나갈 방법이 없어지는
        // 종류의 누락이라 화면에 나오기 전에 알린다.
        if (resumeCard == null)
            Debug.LogWarning("PauseManager: resumeCard가 연결되지 않아 일시정지를 풀 단어가 없습니다.", this);

        if (titleCard == null)
            Debug.LogWarning("PauseManager: titleCard가 연결되지 않아 타이틀로 나갈 단어가 없습니다.", this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (inputManager != null)
            inputManager.OnCancel += HandleCancel;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (inputManager != null)
            inputManager.OnCancel -= HandleCancel;

        RestorePauseCanvases();
    }

    // ESC는 InputManager가 준다. 그쪽에서 입력 잠금(_inputEnabled)보다 위에서 읽으므로
    // 턴 전환 대기처럼 타이핑이 잠긴 구간에서도 일시정지가 걸린다.
    private void HandleCancel()
    {
        // 카드 목록이 떠 있으면 ESC는 "한 단계 뒤로" - 목록만 닫고 일시정지 메뉴로 돌아간다.
        // (타이핑 디스패치는 우선순위가 알아서 갈라주지만 OnCancel은 구독자 전부에게 가므로,
        // 여기서 갈라주지 않으면 목록을 닫으려던 ESC가 게임까지 재개해버린다.)
        if (HasCardCollection && cardCollectionPanel.IsOpen)
        {
            cardCollectionPanel.Close();
            return;
        }

        // 멈춰 있었다면 게임 상태와 무관하게 풀 수 있어야 한다 - 이 검사가 아래 결과 화면
        // 가드보다 먼저 와야 "멈춘 채로 갇히는" 경우가 생기지 않는다.
        if (_isPaused)
        {
            Resume();
            return;
        }

        // ⚠️ 런이 끝난 뒤(패배·전체 클리어)에는 멈출 수 없다. 결과 화면이 이미 손패 자리에
        // 명령 카드를 띄우고 있는데(ResultInputHandler), 일시정지가 같은 PauseHand를 쓰므로
        // 겹치면 두 화면의 카드가 서로를 밀어낸다. 애초에 멈출 게임도 남아 있지 않다.
        //
        // 일반 스테이지 클리어(보상 선택 중)는 여기 걸리지 않는다 - 그때는 게임이 이어지고
        // 있어서 멈출 수 있어야 한다(IsGameOver가 아니라 IsFinalResult를 보는 이유다).
        if (battleManager != null && battleManager.IsFinalResult)
            return;

        Pause();
    }

    public void Pause()
    {
        if (_isPaused)
            return;

        _isPaused = true;

        RaisePauseCanvases();

        // 멈추기 전 입력 상태를 기억해 뒀다가 재개할 때 그대로 되돌린다.
        _inputWasEnabled = inputManager != null && inputManager.IsInputEnabled;

        if (inputManager != null)
        {
            // 메뉴를 타이핑으로 고르므로 입력을 끄지 않는다 - 오히려 꺼져 있었다면 켠다.
            // (턴 전환 대기나 결과 화면에서 멈췄다면 원래 잠겨 있다.)
            // EnableInput은 IME 모드까지 다시 맞춰주므로 명령 단어를 바로 칠 수 있다.
            inputManager.EnableInput();

            // ⚠️ ClearInput을 따로 불러야 한다. EnableInput은 이미 켜져 있으면 곧바로 리턴해서
            // 내부의 ClearInput까지 건너뛰는데, 일시정지는 보통 입력이 켜진 플레이어 턴 중에
            // 걸린다 - 그때 치다 만 글자가 남아 명령 단어에 섞인다.
            inputManager.ClearInput();
        }

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (titleReveal != null)
            titleReveal.Play();
        else
            Debug.LogWarning("PauseManager: titleReveal이 연결되지 않아 PAUSE 열림 연출이 재생되지 않습니다.", this);

        ShowCommandCards();

        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        Time.timeScale = 1f;

        // 목록을 띄운 채로 재개(또는 목록 위에서 ESC 두 번)했을 수 있다. 남겨두면 전투 화면
        // 위에 카드 목록이 그대로 덮인다.
        if (HasCardCollection)
            cardCollectionPanel.Close();

        // pausePanel을 먼저 끄면 그 아래 있는 명령 카드(commandCardsLayout의 자식)도
        // activeInHierarchy가 함께 false가 되어, 뒤이은 HideCommandCards()의 PlayExit이
        // 코루틴을 새로 못 띄우고 "game object is inactive" 에러를 낸다.
        // 카드가 아직 활성 상태일 때 퇴장 애니메이션을 먼저 걸어두고, 패널은 그 뒤에 끈다.
        HideCommandCards();

        if (pausePanel != null)
        {
            var background = pausePanel.GetComponentInChildren<FadeInBackground>(true);
            if (background != null)
                background.FadeOut(() =>
                {
                    pausePanel.SetActive(false);
                    RestorePauseCanvases();
                });
            else
            {
                pausePanel.SetActive(false);
                RestorePauseCanvases();
            }
        }
        else
        {
            RestorePauseCanvases();
        }

        if (inputManager != null)
        {
            // 명령 단어가 입력창에 남아 전투 입력으로 흘러가지 않게 비운다.
            inputManager.ClearInput();

            // 멈추기 전에 잠겨 있었다면(턴 전환 대기 등) 도로 잠근다.
            if (!_inputWasEnabled)
                inputManager.DisableInput();
        }
    }

    // 기존 손패 5장은 아래로 가라앉듯 사라지고(PlayExit, 음수 offset), commandCardsLayout에
    // 새로 스폰한 "계속"/"타이틀" 2장은 그 자리에서 떠오르듯 나타난다(PlayEnter, 음수 offset에서
    // 0으로). CardSlotView.PlayExit/PlayEnter는 일반적인 범용 API라 다른 메뉴에서 카드 자리를
    // 다른 용도로 바꿀 때도 같은 방식으로 재사용할 수 있다.
    private void ShowCommandCards()
    {
        if (handFanLayout == null)
            return;

        // 끄기 전에 지금 활성 상태인 카드 전부를 스냅샷으로 저장해둔다 - 비활성화하고 나면
        // HandFanLayout.Cards에서 빠져서 다시는 참조를 못 얻는다.
        _pausedHandCards.Clear();
        _pausedHandCards.AddRange(handFanLayout.Cards);

        foreach (var card in _pausedHandCards)
        {
            if (card != null)
                card.PlayExit(-commandCardTransitionHeight, () => card.gameObject.SetActive(false));
        }

        if (commandCardsLayout == null || commandCardPrefab == null)
            return;

        // commandCardsLayout 자신이 꺼진 채로 시작할 수 있다(pausePanel과 같은 습관으로 씬에
        // 비활성으로 남아 있는 경우). 자식(새 카드)만 SetActive(true)해도 부모가 꺼져 있으면
        // activeInHierarchy가 false라 StartCoroutine이 "game object is inactive" 에러로 실패한다.
        commandCardsLayout.gameObject.SetActive(true);

        // Targets와 같은 순서로 만든다 - 화면에 놓이는 순서가 곧 명령 순서이고,
        // 목록 창이 없으면 "카드" 카드도 만들지 않는다(쳐도 아무 일이 안 일어나는 카드를 띄우지 않는다).
        SpawnCommandCard(resumeCard);

        if (HasCardCollection)
            SpawnCommandCard(cardsCard);

        SpawnCommandCard(titleCard);
    }

    private void SpawnCommandCard(CommandCardData card)
    {
        // 연결이 빠진 명령은 아예 만들지 않는다 - 빈 카드가 자리만 차지하면 부채꼴 배치가
        // 어긋나고 플레이어는 칠 수 없는 카드를 보게 된다(Targets 쪽도 같은 이유로 비운다).
        if (card == null)
            return;

        var instance = Instantiate(commandCardPrefab, commandCardsLayout.transform, false);
        _commandCardInstances.Add(instance);
        instance.PlayEnter(-commandCardTransitionHeight, () => instance.BindStatic(card, inputManager));
    }

    private void HideCommandCards()
    {
        // 명령 카드는 애니메이션 없이 바로 파괴한다. commandCardsLayout(PauseHand)이 pausePanel의
        // 자식이라 이 직후 pausePanel이 꺼지면 카드도 같이 화면에서 사라지므로, 퇴장 애니메이션을
        // 걸어도 어차피 안 보인다 - 그리고 걸어봤자 코루틴이 시작되자마자 부모가 꺼지면서 잘려
        // Destroy가 끝내 호출되지 않고 비활성 상태로 계속 쌓이기만 한다(이유는 ShowCommandCards
        // 주석 참조 - 같은 activeInHierarchy 문제의 반대 방향).
        foreach (var instance in _commandCardInstances)
        {
            if (instance != null)
                Destroy(instance.gameObject);
        }
        _commandCardInstances.Clear();

        // 기존 손패는 반대로 떠오르며 되돌아온다.
        foreach (var card in _pausedHandCards)
        {
            if (card == null)
                continue;

            // 리스트 순서가 아니라 카드 자신이 기억하고 있는 원래 슬롯 인덱스로 되돌린다 -
            // HandFanLayout이 가운데 카드를 맨 위로 그리려고(centerOnTop) 형제 순서를 바꾸면
            // 스냅샷 순서와 슬롯 인덱스가 더 이상 일치하지 않을 수 있기 때문이다.
            var slotIndex = card.SlotIndex;
            card.PlayEnter(-commandCardTransitionHeight, () => card.Bind(cardSlotManager, slotIndex, inputManager));
        }

        _pausedHandCards.Clear();
    }

    public void ReturnToTitle()
    {
        if (inputManager != null)
            inputManager.ClearInput();

        // 씬을 넘어가도 timeScale은 유지된다 - 여기서 되돌리지 않으면 타이틀이 멈춘 채로 뜬다.
        Time.timeScale = 1f;
        RestorePauseCanvases();

        if (SoundManager.Instance != null)
            SoundManager.Instance.StopBGM();

        SceneManager.LoadScene(GameScenes.Title);
    }

    private void RaisePauseCanvases()
    {
        if (_canvasOrderRaised)
            return;

        ResolvePauseCanvases();
        if (pauseCanvas == null || inputCanvas == null || pauseCanvas == inputCanvas)
        {
            Debug.LogWarning("PauseManager: Pause Canvas/Input Canvas를 찾지 못해 최상단 정렬을 적용할 수 없습니다.", this);
            return;
        }

        _pauseCanvasOriginalOrder = pauseCanvas.sortingOrder;
        _inputCanvasOriginalOrder = inputCanvas.sortingOrder;
        _pauseCanvasOriginalOverrideSorting = pauseCanvas.overrideSorting;
        _inputCanvasOriginalOverrideSorting = inputCanvas.overrideSorting;

        var highestOrder = int.MinValue;
        var canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases)
        {
            if (canvas == null || canvas == pauseCanvas || canvas == inputCanvas)
                continue;
            highestOrder = Mathf.Max(highestOrder, canvas.sortingOrder);
        }

        if (highestOrder == int.MinValue)
            highestOrder = 0;

        pauseCanvas.overrideSorting = true;
        inputCanvas.overrideSorting = true;
        pauseCanvas.sortingOrder = highestOrder + 1;
        inputCanvas.sortingOrder = highestOrder + 2;
        _canvasOrderRaised = true;
    }

    private void RestorePauseCanvases()
    {
        if (!_canvasOrderRaised)
            return;

        if (pauseCanvas != null)
        {
            pauseCanvas.sortingOrder = _pauseCanvasOriginalOrder;
            pauseCanvas.overrideSorting = _pauseCanvasOriginalOverrideSorting;
        }

        if (inputCanvas != null)
        {
            inputCanvas.sortingOrder = _inputCanvasOriginalOrder;
            inputCanvas.overrideSorting = _inputCanvasOriginalOverrideSorting;
        }

        _canvasOrderRaised = false;
    }

    private void ResolvePauseCanvases()
    {
        if (pauseCanvas == null && pausePanel != null)
            pauseCanvas = pausePanel.GetComponentInParent<Canvas>();

        if (inputCanvas != null)
            return;

        var canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases)
        {
            var transforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
            {
                if (child.name != "InputFieldDisplay")
                    continue;

                inputCanvas = canvas;
                return;
            }
        }
    }
}
