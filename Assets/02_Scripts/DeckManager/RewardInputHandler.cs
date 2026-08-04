using System;
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

    /// <summary>보상 한 라운드를 연다. 후보가 비어 있으면 아무것도 하지 않는다 -
    /// 부르는 쪽(StageManager)이 그 경우를 미리 걸러내는 게 맞지만 여기서도 막아둔다.</summary>
    /// <param name="allowErase">마더 드래곤 스테이지인가. 실제로 지우기 카드를 놓을지는
    /// 배선과 남은 카드 수까지 함께 보고 정한다.</param>
    public void BeginSelection(IReadOnlyList<CardBase> candidates, bool allowErase)
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

        // 이전 화면에서 치다 만 글자가 카드 이름에 섞이지 않게 비운다.
        if (inputManager != null)
            inputManager.ClearInput();

        RefreshHint();

        // 표시도 여기서 지시한다 - 단어를 가진 쪽이 화면에 놓이는 순서까지 정해야
        // 매칭 인덱스와 카드 위치가 어긋나지 않는다.
        if (rewardCardView != null)
            rewardCardView.Show(_row);

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

        RefreshHint();

        OnSelectionFinished?.Invoke();
    }

    // 지금 치고 있는 글자가 줄의 어느 카드를 향하는지 뷰에 알려준다. 판정은 손패(CardSlotView)와
    // 같은 InputManager.IsValidProgress를 쓰므로 두 화면의 들림 기준이 어긋나지 않는다.
    private void Update()
    {
        if (!_active || rewardCardView == null || inputManager == null)
            return;

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
