using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 손패 카드를 타이핑으로 맞히는 수신자.
///
/// 파이프라인(조합 중 평가 / 커밋 시 조합 문자열 비우기 / IME 메아리 필터 / 오타 1회 발화)은
/// 전부 <see cref="TypingReceiver"/>에 있다. 여기 남은 것은 "무엇을 노리는가"와
/// "맞혔을 때 무엇을 하는가"뿐이다.
/// </summary>
public class CardInputHandler : TypingReceiver
{
    [Header("Deck")]
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private MainBufferManager mainBufferManager;
    [SerializeField] private WordChainManager wordChainManager;

    public event Action<CardBase> OnCardMatched;
    public event Action OnTypo;

    // 손패 이름을 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새 목록을 만들지 않는다.
    private string[] _targets = Array.Empty<string>();

    /// 손패는 최하위 폴백이다 - 일시정지·결과 화면이 자기 차례라고 하면 그쪽이 먼저 가져간다.
    public override TypingPriority Priority => TypingPriority.Battle;

    /// <summary>
    /// 우선순위상 일시정지(PauseManager)가 먼저 가져가므로 사실 여기서 timeScale을 볼 이유는 없다.
    /// 그래도 남겨두는 건 <b>PauseManager가 씬에서 빠졌을 때의 보험</b>이다 - 그 경우 멈춘 화면에서
    /// 손패가 계속 입력을 먹는 것보다는 아무도 안 먹는 쪽이 안전하다.
    ///
    /// 결과 화면 가드(IsGameOver)는 없앴다. ResultInputHandler가 더 높은 우선순위로 가져가므로
    /// 여기까지 내려오지 않는다.
    /// </summary>
    public override bool WantsInput()
    {
        return !Mathf.Approximately(Time.timeScale, 0f);
    }

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            var cards = cardSlotManager != null ? cardSlotManager.CurrentCards : null;
            var count = cards != null ? cards.Count : 0;

            if (_targets.Length != count)
                _targets = new string[count];

            for (var i = 0; i < count; i++)
            {
                // 빈 슬롯은 빈 문자열로 둔다. 베이스가 빈 항목을 매칭과 진행 판정 양쪽에서 건너뛴다.
                _targets[i] = cards[i] != null ? cards[i].CardName : string.Empty;
            }

            return _targets;
        }
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        var cards = cardSlotManager.CurrentCards;
        if (index < 0 || index >= cards.Count || cards[index] == null)
            return;

        var matched = cards[index];

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayWordComplete();

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.AddValidWord(matched.CardName);

        if (mainBufferManager != null)
            mainBufferManager.AddCard(matched);

        wordChainManager?.SubmitWord(matched.CardName);
        cardSlotManager.ConsumeSlot(index);

        OnCardMatched?.Invoke(matched);
    }

    /// <summary>
    /// 입력창을 비우지 않는 것은 베이스의 공통 정책이라 여기서 다시 하지 않는다
    /// (<see cref="TypingReceiver.HandleTypo"/> 참조). 여기서는 손패 전용 뒷정리만 한다.
    ///
    /// ⚠️ WordChainManager의 체인은 지우지 않는다 - GDD의 오타 페널티(조합 전부 초기화)는
    /// 여기 적용하지 않기로 한 결정이다. 체인은 액션 카드로 완성되어 SkillResolver로 넘어간 뒤
    /// 그쪽에서 ClearChain을 호출해야 비워진다. 지우는 건 매칭된 카드 목록(MainBufferManager)뿐이다.
    /// </summary>
    protected override void HandleTypo()
    {
        if (mainBufferManager != null)
            mainBufferManager.ClearBuffer();

        OnTypo?.Invoke();
    }
}
