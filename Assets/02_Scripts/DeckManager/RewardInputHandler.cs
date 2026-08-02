using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상에서 후보 카드 중 하나를 타이핑으로 고르거나 "넘기기"로 건너뛰는 수신자.
///
/// 매칭 파이프라인(조합 중 평가 / IME 메아리 필터 / 오타 1회 발화)과 안내 문구 조립은 전부
/// <see cref="CommandWordReceiver"/>에 있다. 여기 남은 것은 "지금 무엇을 노리는가"와
/// "골랐을 때 무엇을 하는가"뿐이다.
///
/// 카드 이름을 그대로 매칭 키로 쓴다 - 손패를 치는 것과 같은 방식이라 따로 배울 게 없고,
/// 보상 후보는 정의상 사전에 없는 단어라 손패 카드와 이름이 겹치지 않는다.
/// </summary>
public class RewardInputHandler : CommandWordReceiver
{
    [Header("Reward")]
    [Tooltip("고른 카드를 사전에 확정하는 데 쓴다.")]
    [SerializeField] private WordUnlockManager wordUnlockManager;

    [Tooltip("타이핑 중인 카드를 들어올리고 나머지를 흐리게 하는 뷰. 비워도 선택 자체는 동작한다.")]
    [SerializeField] private RewardCardView rewardCardView;

    [Header("명령 단어")]
    [SerializeField]
    private TypedCommand skipCommand = new TypedCommand(
        "넘기기", "skip",
        "건너뛰려면 \"{0}\"",
        "Type \"{0}\" to skip");

    [Header("안내 문구")]
    [Tooltip("카드를 고르라는 안내. 스킵 안내보다 앞에 붙는다.")]
    [SerializeField, TextArea] private string pickHint = "얻을 카드를 입력하세요";

    [Tooltip("영어 안내. 비워두면 한국어로 대체되고 경고가 남는다.")]
    [SerializeField, TextArea] private string pickHintEn = "Type a card name to claim it";

    [SerializeField] private bool logDebugEvents;

    /// <summary>한 라운드가 끝났을 때(카드를 골랐든 넘겼든) 발생한다. StageManager가 받아
    /// 럭키 라운드가 남았는지 보고 다음을 열거나 결과 화면으로 넘긴다.</summary>
    public event Action OnSelectionFinished;

    /// <summary>지금 보상을 고르는 중인지. BattleManager가 이 동안에는 "다음" 안내를
    /// 띄우지 않으려고 본다.</summary>
    public bool IsSelecting => _active;

    private readonly List<CardBase> _candidates = new List<CardBase>();
    private bool _active;

    // 후보 이름 + 스킵 단어를 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private string[] _targets = Array.Empty<string>();

    public override TypingPriority Priority => TypingPriority.Reward;

    public override bool WantsInput() => _active;

    /// <summary>보상 한 라운드를 연다. 후보가 비어 있으면 아무것도 하지 않는다 -
    /// 부르는 쪽(StageManager)이 그 경우를 미리 걸러내는 게 맞지만 여기서도 막아둔다.</summary>
    public void BeginSelection(IReadOnlyList<CardBase> candidates)
    {
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

        // 후보 수 + 스킵 단어 하나.
        if (_targets.Length != _candidates.Count + 1)
            _targets = new string[_candidates.Count + 1];

        _active = true;

        // 이전 화면에서 치다 만 글자가 후보 이름에 섞이지 않게 비운다.
        if (inputManager != null)
            inputManager.ClearInput();

        RefreshHint();

        if (logDebugEvents)
            Debug.Log($"RewardInput: 보상 선택 시작 - 후보 {_candidates.Count}장", this);
    }

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            // CardName은 언어에 따라 갈리는 프로퍼티라 캐시하지 않고 매번 읽는다.
            for (var i = 0; i < _candidates.Count; i++)
                _targets[i] = _candidates[i].CardName;

            _targets[_candidates.Count] = skipCommand.Word(this, nameof(skipCommand));

            return _targets;
        }
    }

    public override string BuildHint()
    {
        if (!_active)
            return string.Empty;

        return JoinHints(
            LanguageSettings.Pick(pickHint, pickHintEn, this, nameof(pickHintEn)),
            skipCommand.Hint(this, nameof(skipCommand)));
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        // 마지막 칸이 스킵이다. 그 앞은 전부 후보 카드.
        var isSkip = index >= _candidates.Count;

        if (!isSkip && wordUnlockManager != null)
            wordUnlockManager.ConfirmReward(_candidates[index]);
        else if (!isSkip)
            Debug.LogWarning("RewardInputHandler: wordUnlockManager가 연결되지 않아 고른 카드를 얻지 못했습니다.", this);

        if (logDebugEvents)
            Debug.Log(isSkip ? "RewardInput: 보상 넘김" : $"RewardInput: 보상 획득 - {_candidates[index].CardName}", this);

        EndSelection();
    }

    private void EndSelection()
    {
        _active = false;
        _candidates.Clear();

        if (rewardCardView != null)
            rewardCardView.SetTypingCandidate(-1, false);

        RefreshHint();

        OnSelectionFinished?.Invoke();
    }

    // 지금 치고 있는 글자가 어느 후보를 향하는지 뷰에 알려준다. 판정은 손패(CardSlotView)와
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
            for (var i = 0; i < _candidates.Count; i++)
            {
                if (!InputManager.IsValidProgress(committed, composing, _candidates[i].CardName))
                    continue;

                candidate = i;
                break;
            }
        }

        rewardCardView.SetTypingCandidate(candidate, hasInput);
    }
}
