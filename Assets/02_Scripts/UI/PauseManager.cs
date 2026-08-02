using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ESC로 게임을 멈추고 메뉴를 띄운다. 메뉴 선택은 버튼 클릭이 아니라 타이핑으로 한다 -
// 멈춘 동안 입력창에 "계속"/"타이틀"을 쳐서 고른다.
//
// 전투의 단어 조합(WordChainManager/CardInputHandler)과는 완전히 별개의 경로다. 명령 단어는
// 카드도 사전 단어도 아니므로 InputManager의 이벤트를 여기서 직접 받아 처리한다.
//
// Time.timeScale = 0 하나로 턴 전환 대기(DeckManager/StageManager)·말풍선(BattleManager)·
// 타이머 감소(TimerManager)·카드 애니메이션(CardSlotView/HandFanLayout)이 전부 멈춘다 -
// 시간에 의존하는 코드가 모두 deltaTime/WaitForSeconds 기반이라 개별 정지 처리는 하지 않는다.
public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("일시정지 창 루트. 평소엔 비활성이어야 한다.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("안내 문구 라벨. 비워두면 씬에 적어둔 글자가 그대로 남는다 - 그 경우 " +
             "명령 단어나 언어를 바꿔도 안내가 따라오지 않는다.")]
    [SerializeField] private TMP_Text hintText;

    [Tooltip("\"PAUSE\" 제목이 가운데서부터 열리는 연출. pausePanel을 켤 때마다 재생한다. 비워두면 재생하지 않는다.")]
    [SerializeField] private TextGateRevealAnimation titleReveal;

    [Header("References")]
    [SerializeField] private InputManager inputManager;

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
             "사라지고, 새 명령 카드 2장은 그 자리에서 떠오르듯 나타난다(재개할 땐 반대).")]
    [SerializeField] private float commandCardTransitionHeight = 80f;

    [Header("명령 단어 - 한국어")]
    [Tooltip("치면 일시정지가 풀리는 단어")]
    [SerializeField] private string resumeWord = "계속";

    [Tooltip("치면 타이틀 씬으로 돌아가는 단어")]
    [SerializeField] private string titleWord = "타이틀";

    [Header("명령 단어 - 영어")]
    [Tooltip("소문자로 적을 것. 입력이 소문자로 정규화되어 들어옵니다.")]
    [SerializeField] private string resumeWordEn = "resume";

    [SerializeField] private string titleWordEn = "title";

    [Header("안내 문구")]
    [Tooltip("{0}=계속 단어, {1}=타이틀 단어")]
    [SerializeField, TextArea] private string hintFormat = "계속 진행을 원한다면 \"{0}\"!\n타이틀로 돌아가길 원한다면 \"{1}\"!\n을 입력해주세요!";

    [SerializeField, TextArea] private string hintFormatEn = "Type \"{0}\" to keep playing!\nType \"{1}\" to return to the title!";

    private bool _isPaused;
    private bool _inputWasEnabled;

    // HandFanLayout.Cards는 매 프레임 "활성 자식만" 다시 모아서 채워지는 리스트라, 카드를
    // 꺼버리고 나면 그 순간부터 목록에서 빠져 참조를 잃는다. 그래서 끄기 전에 5장 전부를
    // 여기 스냅샷으로 저장해뒀다가, 복원할 때 이걸 쓴다.
    private readonly List<CardSlotView> _pausedHandCards = new List<CardSlotView>();

    // ShowCommandCards가 commandCardsLayout 밑에 스폰한 "계속"/"타이틀" 카드. Hide 때 파괴한다.
    private readonly List<CardSlotView> _commandCardInstances = new List<CardSlotView>();

    public bool IsPaused => _isPaused;

    private string ResumeWord => LanguageSettings.Pick(resumeWord, resumeWordEn, this, "resumeWordEn");
    private string TitleWord => LanguageSettings.Pick(titleWord, titleWordEn, this, "titleWordEn");

    private void Awake()
    {
        // 이전 플레이에서 멈춘 채로 씬을 다시 불러왔을 수 있다.
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        else
            Debug.LogWarning("PauseManager: pausePanel이 연결되지 않았습니다.", this);

        if (inputManager == null)
            Debug.LogWarning("PauseManager: inputManager가 연결되지 않아 명령 단어를 입력받을 수 없습니다.", this);
    }

    private void OnEnable()
    {
        if (inputManager == null)
            return;

        // 조합 중에도 평가해야 하므로 두 이벤트를 모두 구독한다(이유는 Evaluate 주석 참조).
        inputManager.OnCharacterEntered += HandleCharacterEntered;
        inputManager.OnCompositionChanged += HandleCompositionChanged;
    }

    private void OnDisable()
    {
        if (inputManager == null)
            return;

        inputManager.OnCharacterEntered -= HandleCharacterEntered;
        inputManager.OnCompositionChanged -= HandleCompositionChanged;
    }

    // Update는 timeScale 0에서도 계속 돌기 때문에 멈춘 상태에서도 ESC를 받을 수 있다.
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (_isPaused)
            Resume();
        else
            Pause();
    }

    // 글자가 커밋되는 순간 Composition은 아직 방금 커밋된 옛 값을 들고 있을 수 있다.
    // 그걸 이어붙이면 "계계"처럼 중복되므로, 커밋 경로에서는 조합 문자열을 비워서 평가한다.
    private void HandleCharacterEntered(char _)
    {
        Evaluate(string.Empty);
    }

    private void HandleCompositionChanged(string composing)
    {
        Evaluate(composing);
    }

    // 조합 중에도 평가하는 이유는 CardInputHandler와 같다 - "계속"의 마지막 음절 "속"은
    // 뒤에 이어질 글자가 없어 IME가 영영 커밋하지 않는다. 커밋만 기다리면 명령 단어가
    // 절대 완성되지 않는다.
    private void Evaluate(string composing)
    {
        if (!_isPaused || inputManager == null)
            return;

        var committed = inputManager.CurrentInput;
        var typed = committed + composing;

        // ClearInput()이 OnCompositionChanged를 발생시켜 이 메서드가 재진입한다.
        // 그때는 입력이 비어 있으므로 여기서 빠져나가 무한 재귀가 되지 않는다.
        if (typed.Length == 0)
            return;

        var resume = ResumeWord;
        var title = TitleWord;

        if (typed == resume)
        {
            inputManager.ClearInput();
            Resume();
            return;
        }

        if (typed == title)
        {
            inputManager.ClearInput();
            ReturnToTitle();
            return;
        }

        // 두 단어 중 어느 쪽으로도 진행 중이 아니면 오타다. 입력창을 비워 처음부터 다시 치게 한다.
        // 매칭 판정은 전투 쪽과 같은 기준(InputManager.IsValidProgress)을 쓴다.
        if (InputManager.IsValidProgress(committed, composing, resume) ||
            InputManager.IsValidProgress(committed, composing, title))
            return;

        inputManager.ClearInput();
    }

    // 안내 문구를 명령 단어에서 만들어 넣는다. 씬에 글자를 박아두면 단어를 바꾸거나 언어를
    // 바꿔도 안내만 옛 상태로 남는다(결과 화면의 ResultInputHandler.GetHintText와 같은 이유).
    private void RefreshHint()
    {
        if (hintText == null)
            return;

        var format = LanguageSettings.IsEnglish ? hintFormatEn : hintFormat;
        if (string.IsNullOrEmpty(format))
            return;

        hintText.text = string.Format(format, ResumeWord, TitleWord);
    }

    public void Pause()
    {
        if (_isPaused)
            return;

        _isPaused = true;

        // 멈추기 전 입력 상태를 기억해 뒀다가 재개할 때 그대로 되돌린다.
        _inputWasEnabled = inputManager != null && inputManager.IsInputEnabled;

        if (inputManager != null)
        {
            // 메뉴를 타이핑으로 고르므로 입력을 끄지 않는다 - 오히려 꺼져 있었다면 켠다.
            // (턴 전환 대기나 결과 화면에서 멈췄다면 원래 잠겨 있다.)
            // EnableInput은 한글 모드까지 다시 강제하므로 명령 단어를 바로 칠 수 있다.
            inputManager.EnableInput();

            // 치다 만 글자가 명령 단어와 섞이지 않게 비운다.
            inputManager.ClearInput();
        }

        RefreshHint();

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

        // pausePanel을 먼저 끄면 그 아래 있는 명령 카드(commandCardsLayout의 자식)도
        // activeInHierarchy가 함께 false가 되어, 뒤이은 HideCommandCards()의 PlayExit이
        // 코루틴을 새로 못 띄우고 "game object is inactive" 에러를 낸다.
        // 카드가 아직 활성 상태일 때 퇴장 애니메이션을 먼저 걸어두고, 패널은 그 뒤에 끈다.
        HideCommandCards();

        if (pausePanel != null)
            pausePanel.SetActive(false);

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

        SpawnCommandCard(ResumeWord);
        SpawnCommandCard(TitleWord);
    }

    private void SpawnCommandCard(string label)
    {
        var instance = Instantiate(commandCardPrefab, commandCardsLayout.transform, false);
        _commandCardInstances.Add(instance);
        instance.PlayEnter(-commandCardTransitionHeight, () => instance.BindStatic(label, inputManager));
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

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
        SceneManager.LoadScene(GameScenes.Title);
    }
}
