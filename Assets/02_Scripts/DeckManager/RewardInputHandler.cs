using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상에서 한 줄에 놓인 카드 중 하나를 타이핑으로 고르는 수신자.
///
/// <code>
/// [지우기]  [후보1] [후보2] [후보3]  [넘기기]   ← 마더 드래곤 스테이지
///           [후보1] [후보2] [후보3]  [넘기기]   ← 일반 스테이지
/// </code>
///
/// 매칭 파이프라인(조합 중 평가 / IME 메아리 필터 / 오타 1회 발화)은 전부
/// <see cref="CommandWordReceiver"/>에 있다. 여기 남은 것은 "지금 무엇을 노리는가"와
/// "골랐을 때 무엇을 하는가"뿐이다.
///
/// 후보 카드든 명령 카드든 <b>전부 <see cref="CardBase"/>라 매칭 키가 CardName 하나로 통일된다.</b>
/// 손패를 치는 것과 같은 방식이라 따로 배울 게 없다.
///
/// ⚠️ <b><see cref="_targets"/>의 순서가 곧 화면에 놓이는 카드 순서다.</b> 그래야
/// <see cref="RewardCardView.SetTypingCandidate"/>에 넘기는 인덱스가 그대로 들어맞는다.
/// 예전에는 "마지막 칸이 스킵"이라고 계산했는데, 왼쪽에 카드가 하나 붙는 순간 그 계산이 깨진다.
/// </summary>
public class RewardInputHandler : CommandWordReceiver
{
    [Header("Reward")]
    [Tooltip("고른 카드를 사전에 확정하는 데 쓴다.")]
    [SerializeField] private WordUnlockManager wordUnlockManager;

    [Tooltip("타이핑 중인 카드를 들어올리고 나머지를 흐리게 하는 뷰. 비워도 선택 자체는 동작한다.")]
    [SerializeField] private RewardCardView rewardCardView;

    [Header("명령 카드")]
    [Tooltip("줄 오른쪽 끝에 놓일 건너뛰기 카드. 04_Data/Cards/Commands/Skip")]
    [SerializeField] private CommandCardData skipCard;

    [Tooltip("줄 왼쪽 끝에 놓일 카드 삭제 카드. 04_Data/Cards/Commands/Erase. " +
             "마더 드래곤 스테이지에서만 나온다.")]
    [SerializeField] private CommandCardData eraseCard;

    [Header("카드 삭제")]
    [Tooltip("\"지우기\"를 쳤을 때 열 사전 카드 목록. 비워두면 지우기 카드 자체가 나오지 않는다 - " +
             "열 창이 없는데 카드만 놓이면 쳐도 아무 일이 안 일어난다.")]
    [SerializeField] private CardDeletePanel cardDeletePanel;

    [Tooltip("사전에 남은 카드가 이 수 이하면 지우기 카드를 띄우지 않는다. 덱이 너무 얇아져 " +
             "진행이 막히는 것을 막는 안전장치다.")]
    [SerializeField] private int minWordsToKeep = 4;

    [Tooltip("남은 카드 수를 세는 데 쓴다. 비워두면 지우기 카드가 나오지 않는다.")]
    [SerializeField] private WordDictionary wordDictionary;

    [Header("안내 문구")]
    [Tooltip("카드를 고르라는 안내. 넘기기/지우기는 카드로 보이므로 여기엔 넣지 않는다.")]
    [SerializeField, TextArea] private string pickHint = "얻을 카드를 입력하세요";

    [Tooltip("영어 안내. 비워두면 한국어로 대체되고 경고가 남는다.")]
    [SerializeField, TextArea] private string pickHintEn = "Type a card name to claim it";

    [Header("열려 있는 동안 비켜날 UI")]
    [Tooltip("선택이 열리면 여기 넣은 UI들이 offset만큼 밀려난다. CardCollectionPanel/CardDeletePanel과 " +
             "같은 UIDisplacement 패턴 - 보통 InputFieldDisplay를 넣는다. ⚠️ 밀려난 자리는 카드를 " +
             "고른 뒤(EndSelection)에도 곧바로 돌아오지 않는다 - 다음 스테이지 배너가 뜰 때 " +
             "RestoreHand()가 inputFieldCanvasGroup 페이드인과 함께 되돌린다.")]
    [SerializeField] private DisplacedUI[] displacedUI = new DisplacedUI[0];

    [Tooltip("비켜나고 돌아오는 데 걸리는 시간(초). 0이면 즉시 이동한다.")]
    [SerializeField] private float displaceDuration = 0.15f;

    [Tooltip("진행도(0~1)에 따른 밀림 비율. 기본은 EaseInOut.")]
    [SerializeField] private AnimationCurve displaceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("켜면(기본) 카드를 고른 순간(EndSelection) 위치는 밀려난 채로 두고 알파만 " +
             "페이드아웃했다가 다음 스테이지 배너가 뜰 때 페이드인한다(inputFieldCanvasGroup 필요). " +
             "끄면 페이드 없이, 라운드가 끝날 때마다 곧바로 원래 자리로 올라갔다가 다음 라운드가 " +
             "열리면 다시 내려가는 단순한 방식으로 동작한다.")]
    [SerializeField] private bool fadeInputFieldOnSelect = true;

    [Tooltip("페이드 모드(fadeInputFieldOnSelect)에서 페이드인/아웃할 대상(보통 InputFieldDisplay에 " +
             "붙인 CanvasGroup). 위치는 displacedUI가 따로 맡으므로 알파만 바뀐다. 비워두면 " +
             "페이드 모드라도 경고만 남기고 아무 일도 하지 않는다.")]
    [SerializeField] private CanvasGroup inputFieldCanvasGroup;

    [Tooltip("입력창 페이드인/아웃에 걸리는 시간(초). 페이드 모드에서만 쓰인다.")]
    [SerializeField] private float inputFieldFadeDuration = 0.2f;

    [Header("타이머")]
    [Tooltip("선택 중 페이드아웃시킬 타이머 바. 보상을 고르는 동안은 시간 제한이 의미가 없다.")]
    [SerializeField] private TimerView timerView;

    [Header("손패")]
    [Tooltip("선택이 열리는 동안 잠시 가라앉혀 치울 손패. 끝나면 원래 자리로 되돌아온다. " +
             "PauseManager가 명령 카드로 바꿀 때 쓰는 것과 같은 CardSlotView.PlayExit/PlayEnter " +
             "패턴이다.")]
    [SerializeField] private HandFanLayout handFanLayout;

    [Tooltip("손패를 되돌릴 때 다시 Bind하는 데 쓴다. 비우면 손패가 안 돌아온다.")]
    [SerializeField] private CardSlotManager cardSlotManager;

    [Tooltip("손패가 가라앉았다 돌아오는 높이(px).")]
    [SerializeField] private float handTransitionHeight = 80f;

    [SerializeField] private bool logDebugEvents;

    /// <summary>한 라운드가 끝났을 때(카드를 골랐든 넘겼든 지웠든) 발생한다. StageManager가 받아
    /// 럭키 라운드가 남았는지 보고 다음을 열거나 결과 화면으로 넘긴다.</summary>
    public event Action OnSelectionFinished;

    /// <summary>지금 보상을 고르는 중인지. BattleManager가 이 동안에는 결과 화면 안내를
    /// 띄우지 않으려고 본다.</summary>
    public bool IsSelecting => _active;

    /// <summary>방금 라운드에서 실제로 카드를 골랐다면 그 카드가 화면 줄(RewardCardView가 그리는
    /// 순서와 같다)의 몇 번째였는지. 넘겼거나 지웠다면(고른 카드가 없다면) -1. StageManager가
    /// 퇴장 연출에서 "이 카드만 남기고 나머지를 떨어뜨리는" 대상을 정하는 데 쓴다.</summary>
    public int LastPickedRowIndex { get; private set; } = -1;

    private readonly List<CardBase> _candidates = new List<CardBase>();

    // 화면에 놓이는 순서 그대로의 줄. 그리기(RewardCardView)와 매칭(Targets)이 같은 목록을 본다.
    private readonly List<CardBase> _row = new List<CardBase>();

    // 비켜나기 진행 상태 + 로직. CardCollectionPanel/CardDeletePanel과 같은 것을 쓴다.
    private readonly UIDisplacement _displacement = new UIDisplacement();

    // 선택이 열리는 동안 가라앉혀 치운 손패 카드들의 스냅샷. 비활성화하고 나면
    // HandFanLayout.Cards에서 빠져 다시는 참조를 못 얻으므로 여기 따로 들고 있는다.
    private readonly List<CardSlotView> _hiddenHandCards = new List<CardSlotView>();

    // displacedUI가 지금 밀려나 있어야 하는가. _active와 분리해뒀다 - 카드를 고른 뒤(_active가
    // 꺼진 뒤)에도 다음 스테이지 배너가 뜰 때까지는 계속 밀려난 채(+ 페이드아웃)로 있어야 한다.
    private bool _displaced;

    private Coroutine _inputFieldFadeRoutine;

    private bool _active;

    // 이번 라운드에 지우기 카드가 놓였는지. 인덱스 경계가 여기서 갈린다.
    private bool _hasErase;

    // 카드 이름을 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private string[] _targets = Array.Empty<string>();

    public override TypingPriority Priority => TypingPriority.Reward;

    public override bool WantsInput() => _active;

    // 줄에서의 자리 번호. 숫자를 코드 곳곳에 흩뿌리지 않으려고 여기 모아둔다.
    private int EraseIndex => _hasErase ? 0 : -1;
    private int FirstCandidateIndex => _hasErase ? 1 : 0;
    private int SkipIndex => _row.Count - 1;

    private void Awake()
    {
        _displacement.CaptureOrigins(displacedUI, this, nameof(displacedUI));
    }

    // ⚠️ 반드시 LateUpdate에서 부른다 - HandFanLayout이 손패(자식)를 배치한 뒤에 밀려나는
    // UI(부모)를 옮겨야 한다(CardCollectionPanel/CardDeletePanel과 같은 이유). timeScale이 1인
    // 보상 구간에서만 뜨므로 useUnscaledTime은 false다.
    private void LateUpdate()
    {
        _displacement.Tick(displacedUI, _displaced, displaceDuration, useUnscaledTime: false, displaceCurve);
    }

    // offset은 배치를 눈으로 보며 맞추는 값이라 Play 중에 조정하게 된다. 도착해서 좌표 쓰기를
    // 멈춘 상태에서는 다음 여닫이까지 반영이 안 보이므로 여기서 다시 움직이게 한다.
    private void OnValidate()
    {
        _displacement.MarkDirty();
    }

    /// <summary>보상 한 라운드를 연다. 후보가 비어 있으면 아무것도 하지 않는다 -
    /// 부르는 쪽(StageManager)이 그 경우를 미리 걸러내는 게 맞지만 여기서도 막아둔다.</summary>
    /// <param name="allowErase">마더 드래곤 스테이지인가. 실제로 지우기 카드를 놓을지는
    /// 배선과 남은 카드 수까지 함께 보고 정한다.</param>
    /// <param name="isBonusRound">럭키로 얻은 보너스 라운드인가. rewardCardView.Show에 그대로
    /// 넘겨 "럭키 보상!" 배너를 켤지 정하는 데만 쓴다.</param>
    public void BeginSelection(IReadOnlyList<CardBase> candidates, bool allowErase, bool isBonusRound = false)
    {
        // 라운드마다 새로 정해진다 - 이전 라운드에서 골랐던 자리가 이번 라운드까지 남아
        // 엉뚱한 카드가 퇴장 연출에서 남겨지지 않게 한다.
        LastPickedRowIndex = -1;

        _candidates.Clear();

        if (candidates != null)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null)
                    _candidates.Add(candidates[i]);
            }
        }

        if (_candidates.Count == 0)
        {
            Debug.LogWarning("RewardInputHandler: 후보가 비어 있어 선택을 열지 않습니다.", this);
            return;
        }

        if (skipCard == null)
            Debug.LogWarning("RewardInputHandler: skipCard가 연결되지 않아 건너뛸 방법이 없습니다.", this);

        // 지우기는 조건이 셋 다 맞을 때만 놓는다. 창도 사전도 없는데 카드만 놓이면
        // 쳐도 아무 일이 일어나지 않고, 덱이 너무 얇으면 지울수록 진행이 막힌다.
        _hasErase = allowErase
                    && eraseCard != null
                    && cardDeletePanel != null
                    && wordDictionary != null
                    && wordDictionary.Words.Count > minWordsToKeep;

        BuildRow();

        _active = true;

        // 가리는 UI를 비켜나게 한다(실제 이동은 LateUpdate가 이어서 한다).
        // 페이드 모드(fadeInputFieldOnSelect)면 이 위치는 라운드가 끝나도 곧바로 돌아오지
        // 않는다 - RestoreHand() 참조. 아니면(단순 모드) EndSelection이 라운드마다 곧바로
        // 되돌리므로, 여기서 다시 밀어내는 게 매 라운드 반복된다.
        _displaced = true;
        _displacement.MarkDirty();

        // 페이드 모드에서만 알파를 쓴다 - 위치는 세션 내내 밀려나 있고 알파만 라운드마다
        // (_active) 오간다. 럭키로 보상 라운드가 연달아 열리면(TryConsumeBonusRound) 그
        // 라운드에도 타이핑이 보여야 하기 때문이다. 단순 모드는 알파를 아예 건드리지 않는다 -
        // 위치가 오르내리는 것 자체가 "숨김"을 대신한다.
        if (fadeInputFieldOnSelect)
            FadeInputField(true);

        // 보상을 고르는 동안엔 시간 제한이 의미가 없으니 타이머를 지운다.
        if (timerView != null)
            timerView.SetVisible(false);

        // 손패를 잠시 가라앉혀 치운다. 여기서는 되돌리지 않는다 - 다음 스테이지의 "STAGE
        // START" 배너가 뜰 때(StageManager.LoadStage)까지 내려가 있다가 그때 RestoreHand()로
        // 올라온다. 럭키로 보상 라운드가 연달아 열릴 수 있어(TryConsumeBonusRound), 이미
        // 손패가 내려가 있으면(_hiddenHandCards가 비어 있지 않으면) 다시 캡처하지 않는다 -
        // 다시 캡처하면 handFanLayout.Cards가 "활성 자식만" 모으는 목록이라 이미 꺼진 카드는
        // 안 잡혀서 빈 목록으로 스냅샷을 덮어써버리고, 그러면 RestoreHand()가 아무것도
        // 되돌리지 못한다.
        if (handFanLayout != null && _hiddenHandCards.Count == 0)
        {
            _hiddenHandCards.AddRange(handFanLayout.Cards);

            foreach (var card in _hiddenHandCards)
            {
                if (card != null)
                    card.PlayExit(-handTransitionHeight, () => card.gameObject.SetActive(false));
            }
        }

        // 이전 화면에서 치다 만 글자가 카드 이름에 섞이지 않게 비운다.
        if (inputManager != null)
            inputManager.ClearInput();

        RefreshHint();

        // 표시도 여기서 지시한다 - 단어를 가진 쪽이 화면에 놓이는 순서까지 정해야
        // 매칭 인덱스와 카드 위치가 어긋나지 않는다.
        if (rewardCardView != null)
            rewardCardView.Show(_row, isBonusRound);

        if (logDebugEvents)
            Debug.Log($"RewardInput: 보상 선택 시작 - 후보 {_candidates.Count}장" +
                      $"{(_hasErase ? " (+지우기)" : string.Empty)}", this);
    }

    // [지우기?] + 후보들 + [넘기기]. Targets와 화면 배치가 이 하나를 같이 본다.
    private void BuildRow()
    {
        _row.Clear();

        if (_hasErase)
            _row.Add(eraseCard);

        for (var i = 0; i < _candidates.Count; i++)
            _row.Add(_candidates[i]);

        if (skipCard != null)
            _row.Add(skipCard);

        if (_targets.Length != _row.Count)
            _targets = new string[_row.Count];
    }

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            // CardName은 언어에 따라 갈리는 프로퍼티라 캐시하지 않고 매번 읽는다.
            for (var i = 0; i < _row.Count; i++)
                _targets[i] = _row[i].CardName;

            return _targets;
        }
    }

    public override string BuildHint()
    {
        if (!_active)
            return string.Empty;

        // 넘기기·지우기는 카드로 보이므로 안내에 넣지 않는다.
        return JoinHints(LanguageSettings.Pick(pickHint, pickHintEn, this, nameof(pickHintEn)));
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        if (index == EraseIndex)
        {
            // 삭제 창이 라운드를 대신 끝낸다(OnDeleteFinished -> EndSelection). 여기서 끝내면
            // 아직 지우지도 않았는데 다음 스테이지로 넘어간다.
            _active = false;

            if (rewardCardView != null)
                rewardCardView.SetTypingCandidate(-1, false);

            if (logDebugEvents)
                Debug.Log("RewardInput: 카드 삭제 창 열기", this);

            cardDeletePanel.Open();
            return;
        }

        if (index == SkipIndex)
        {
            if (logDebugEvents)
                Debug.Log("RewardInput: 보상 넘김", this);

            EndSelection();
            return;
        }

        var picked = _candidates[index - FirstCandidateIndex];

        if (wordUnlockManager != null)
            wordUnlockManager.ConfirmReward(picked);
        else
            Debug.LogWarning("RewardInputHandler: wordUnlockManager가 연결되지 않아 고른 카드를 얻지 못했습니다.", this);

        if (logDebugEvents)
            Debug.Log($"RewardInput: 보상 획득 - {picked.CardName}", this);

        // 실제로 카드를 고른 경우에만 세운다 - 퇴장 연출에서 이 카드만 남기고 나머지를 떨어뜨린다.
        // 뽀잉+플래시는 여기서 곧바로 재생하지 않는다 - RewardCardView.ExitRoutine이 이 인덱스를
        // keepIndex로 받아서 나머지가 떨어지는 동안 재생한다(이유는 RewardCardView 참조).
        LastPickedRowIndex = index;

        EndSelection();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (cardDeletePanel != null)
            cardDeletePanel.OnDeleteFinished += HandleDeleteFinished;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (cardDeletePanel != null)
            cardDeletePanel.OnDeleteFinished -= HandleDeleteFinished;
    }

    // 한 장을 지우고 창이 닫혔다. 이 라운드는 보상 없이 여기서 끝난다.
    private void HandleDeleteFinished()
    {
        EndSelection();
    }

    private void EndSelection()
    {
        _active = false;
        _candidates.Clear();
        _row.Clear();
        _hasErase = false;

        if (rewardCardView != null)
            rewardCardView.SetTypingCandidate(-1, false);

        if (fadeInputFieldOnSelect)
        {
            // 카드를 고른 순간 입력창을 페이드아웃한다(위치는 그대로 밀려나 있다). 럭키 보너스
            // 라운드가 남아 있으면 곧이어 BeginSelection이 다시 열리며 도로 페이드인된다.
            FadeInputField(false);
        }
        else
        {
            // 단순 모드 - 페이드 없이 곧바로 원래 자리로 올라간다. 럭키 보너스 라운드가 남아
            // 있으면 BeginSelection이 다시 밀어낸다.
            _displaced = false;
            _displacement.MarkDirty();
        }

        if (timerView != null)
            timerView.SetVisible(true);

        // ⚠️ 손패(그리고 페이드 모드일 때 입력창 위치)는 여기서 되돌리지 않는다. RestoreHand()
        // 참조 - 다음 스테이지 배너가 뜰 때 StageManager가 부른다. 럭키 보너스 라운드가 남아 있으면
        // 이 직후 BeginSelection이 다시 열리는데, 그때도 손패와 입력창 위치는 계속 밀려나
        // 있어야 한다(입력창 알파만 위에서 다시 켜진다).

        RefreshHint();

        OnSelectionFinished?.Invoke();
    }

    /// <summary>보상 중 가라앉혀뒀던 손패와, 밀려난 채 페이드아웃돼 있던 displacedUI(입력창 등)를
    /// 원래 상태로 되돌린다. StageManager.LoadStage가 다음 스테이지의 "STAGE START" 배너를
    /// 띄우는 순간 부른다 - 배너가 화면을 가리고 있는 동안 다시 떠올라야, 보상 카드가 채
    /// 정리되기도 전에 손패/입력창이 불쑥 나타나는 것처럼 안 보인다.
    ///
    /// 손패는 리스트 순서가 아니라 카드 자신이 기억하는 원래 슬롯 인덱스로 되돌린다 -
    /// HandFanLayout이 가운데 카드를 맨 위로 그리려고(centerOnTop) 형제 순서를 바꾸면 스냅샷
    /// 순서와 슬롯 인덱스가 더 이상 일치하지 않을 수 있다(PauseManager.HideCommandCards와
    /// 같은 이유). 숨긴 게 없으면(_hiddenHandCards가 비어 있으면) 손패 쪽은 아무 일도 하지
    /// 않는다 - 보상을 거치지 않고 재시작한 경우 등.</summary>
    public void RestoreHand()
    {
        foreach (var card in _hiddenHandCards)
        {
            if (card == null)
                continue;

            var slotIndex = card.SlotIndex;
            card.PlayEnter(-handTransitionHeight, () => card.Bind(cardSlotManager, slotIndex, inputManager));
        }

        _hiddenHandCards.Clear();

        // 단순 모드에서는 EndSelection이 매 라운드 이미 되돌려놨을 것이므로 여기서 다시
        // MarkDirty해도 이미 목표(0)에 도달해 있어 아무 일도 일어나지 않는다 - 그래도
        // 명시적으로 불러두는 게 "이 시점엔 무조건 제자리"라는 불변식을 지키기 쉽다.
        _displaced = false;
        _displacement.MarkDirty();

        if (fadeInputFieldOnSelect)
            FadeInputField(true);
    }

    // 호출부가 전부 fadeInputFieldOnSelect일 때만 부르므로, 여기 도달했는데 대상이 없으면
    // 실제 배선 누락이다.
    private void FadeInputField(bool visible)
    {
        if (inputFieldCanvasGroup == null)
        {
            Debug.LogWarning("RewardInputHandler: fadeInputFieldOnSelect가 켜져 있는데 " +
                             "inputFieldCanvasGroup이 연결되지 않아 페이드가 재생되지 않습니다.", this);
            return;
        }

        if (_inputFieldFadeRoutine != null)
            StopCoroutine(_inputFieldFadeRoutine);

        _inputFieldFadeRoutine = StartCoroutine(FadeInputFieldRoutine(visible ? 1f : 0f));
    }

    private IEnumerator FadeInputFieldRoutine(float target)
    {
        var start = inputFieldCanvasGroup.alpha;
        var elapsed = 0f;

        while (elapsed < inputFieldFadeDuration)
        {
            elapsed += Time.deltaTime;
            inputFieldCanvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / inputFieldFadeDuration));
            yield return null;
        }

        inputFieldCanvasGroup.alpha = target;
        _inputFieldFadeRoutine = null;
    }

    // 지금 치고 있는 글자가 줄의 어느 카드를 향하는지 뷰에 알려준다. 판정은 손패(CardSlotView)와
    // 같은 InputManager.IsValidProgress를 쓰므로 두 화면의 들림 기준이 어긋나지 않는다.
    private void Update()
    {
        if (!_active || rewardCardView == null || inputManager == null)
            return;

        // ⚠️ _active만으로는 부족하다. 보상은 카드를 고를 때까지 계속 활성인데 그 위로 일시정지나
        // 지우기 목록이 열리면 입력은 그쪽이 가져간다 - 그때도 계속 읽으면 플레이어가 치는
        // "계속"이 후보 이름과 접두사가 겹치는 순간 엉뚱한 보상 카드가 떠오른다.
        // 후보 없음(-1) + 입력 없음(false)으로 되돌려 카드를 평상 상태로 가라앉힌다.
        if (!HasTypingFocus)
        {
            rewardCardView.SetTypingCandidate(-1, false);
            return;
        }

        var committed = inputManager.CurrentInput;
        var composing = inputManager.Composition;
        var hasInput = committed.Length > 0 || composing.Length > 0;

        var candidate = -1;

        if (hasInput)
        {
            for (var i = 0; i < _row.Count; i++)
            {
                if (!InputManager.IsValidProgress(committed, composing, _row[i].CardName))
                    continue;

                candidate = i;
                break;
            }
        }

        rewardCardView.SetTypingCandidate(candidate, hasInput);
    }
}
