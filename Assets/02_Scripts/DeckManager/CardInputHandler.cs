using System;
using UnityEngine;

public class CardInputHandler : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private MainBufferManager mainBufferManager;
    [SerializeField] private WordChainManager wordChainManager;

    [Tooltip("결과 화면에서는 타이핑이 전투가 아니라 ResultInputHandler로 가야 하므로 승패 여부를 본다.")]
    [SerializeField] private BattleManager battleManager;

    public event Action<CardBase> OnCardMatched;
    public event Action OnTypo;

    // 조합 중 매칭으로 슬롯을 소비하면 OS IME는 그 글자를 여전히 조합 중이라고 알고 있어서,
    // 다음 카드를 이어 치는 순간 뒤늦게 커밋되어 되돌아올 수 있다. 그 메아리를 한 번만 걸러낸다.
    private string _pendingEcho;

    // 지금 입력이 어떤 손패 단어로도 이어지지 않는 상태인지. 입력을 자동으로 지우지 않으므로
    // 그 상태가 여러 글자에 걸쳐 이어지는데, OnTypo는 들어선 순간에만 한 번 쏘려고 들고 있는다.
    private bool _notProgressing;

    private void OnEnable()
    {
        inputManager.OnCharacterEntered += HandleCharacterEntered;
        inputManager.OnCompositionChanged += HandleCompositionChanged;
    }

    private void OnDisable()
    {
        inputManager.OnCharacterEntered -= HandleCharacterEntered;
        inputManager.OnCompositionChanged -= HandleCompositionChanged;
    }

    // 글자가 커밋되는 순간 InputManager.Composition은 아직 방금 커밋된 옛 값을 들고 있을 수 있다.
    // 그걸 CurrentInput에 이어붙이면 "펀펀"처럼 중복되어 오타로 오인되므로, 커밋 경로에서는
    // 조합 문자열을 비워서 평가한다 - 커밋된 글자는 이미 CurrentInput에 들어가 있다.
    private void HandleCharacterEntered(char _)
    {
        Evaluate(string.Empty);
    }

    // 한 음절 단어(퀵/잽/훅 등)는 뒤에 이어질 음절이 없어 IME가 절대 커밋을 안 하므로
    // OnCharacterEntered만으로는 평가 자체가 안 일어난다. 조합 변화에도 반응해야 한다.
    // 이벤트가 넘겨준 값을 그대로 쓴다 - 프로퍼티를 다시 읽으면 시점이 어긋날 수 있다.
    private void HandleCompositionChanged(string composing)
    {
        Evaluate(composing);
    }

    private void Evaluate(string composing)
    {
        // 일시정지 중에는 타이핑이 일시정지 메뉴(PauseManager)의 명령 단어로 가야 한다.
        // 여기서 걸러내지 않으면 "계속"이 손패에 없는 단어라 오타로 처리되고, 그 자리에서
        // ClearInput이 불려 명령 단어를 끝까지 칠 수 없게 된다.
        // PauseManager를 참조하지 않고 timeScale을 보는 이유는 BattleManager.Update와 같다.
        if (Mathf.Approximately(Time.timeScale, 0f))
            return;

        // 결과 화면에서도 같은 이유로 비켜준다. 여기서 걸러내지 않으면 "다음"의 첫 글자가
        // 손패에 없는 단어라 오타로 처리되고 ClearInput이 불려 명령 단어를 끝까지 칠 수 없다.
        if (battleManager != null && battleManager.IsGameOver)
            return;

        var committed = inputManager.CurrentInput;

        if (committed.Length == 0 && composing.Length == 0)
        {
            // 백스페이스로 전부 지웠거나 매칭 직후다. 다음 오타를 다시 알릴 수 있게 되돌린다.
            _notProgressing = false;
            return;
        }

        // 메아리는 커밋되어 CurrentInput에 들어왔을 때만 판정한다. 조합 단계에서 표시를 써버리면
        // 정작 커밋된 메아리를 걸러내지 못한다.
        if (_pendingEcho != null && committed.Length > 0)
        {
            var echo = _pendingEcho;
            _pendingEcho = null;

            if (committed == echo)
            {
                inputManager.ClearInput();
                return;
            }
        }

        var typed = committed + composing;
        var cards = cardSlotManager.CurrentCards;

        for (var i = 0; i < cards.Count; i++)
        {
            // 사전이 아직 비어 있으면 슬롯이 null일 수 있다.
            if (cards[i] == null || cards[i].CardName != typed)
                continue;

            var matched = cards[i];
            var wasComposing = composing.Length > 0;

            _notProgressing = false;
            mainBufferManager.AddCard(matched);
            wordChainManager?.SubmitWord(matched.CardName);
            cardSlotManager.ConsumeSlot(i);
            inputManager.ClearInput();

            // 조합 중이던 글자로 매칭됐다면 OS IME는 아직 그 글자를 붙잡고 있다 - 뒤늦게 커밋되어
            // 돌아올 걸 대비해 한 번만 걸러낼 표시를 남긴다. ClearInput이 OnCompositionChanged를
            // 발생시켜 이 메서드가 재진입하므로, 표시는 반드시 그 뒤에 남겨야 지워지지 않는다.
            //
            // ⚠️ 돌아오는 건 카드 이름 전체가 아니라 **조합 중이던 그 글자**다.
            // "펀치"를 조합 중에 맞히면 커밋된 건 "펀", 조합 중인 건 "치"이고 메아리로 오는 것도 "치"다.
            // 예전엔 여기에 CardName을 넣어 한 번도 걸러지지 않았는데, 뒤따르던 오타 자동 삭제가
            // 대신 치워주고 있어서 드러나지 않았을 뿐이다. 그 삭제를 걷어내자 마지막 글자가 남았다.
            if (wasComposing)
                _pendingEcho = composing;

            OnCardMatched?.Invoke(matched);
            return;
        }

        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && InputManager.IsValidProgress(committed, composing, cards[i].CardName))
            {
                _notProgressing = false;
                return;
            }
        }

        // ⚠️ 여기서 입력을 지우지 않는다. 손패에 없는 글자를 쳤다고 곧바로 비워버리면
        // 플레이어가 자기가 무엇을 잘못 쳤는지 볼 수가 없다. 화면에 그대로 남겨두고
        // 백스페이스로 직접 지우게 한다(길이 상한은 InputManager.maxInputLength가 맡는다).
        //
        // 그래서 오타 뒤에는 이어서 쳐도 매칭되지 않는다 - 매칭은 버퍼 전체와의 정확 일치라
        // 앞의 잘못된 글자가 남아 있는 한 어떤 단어도 완성되지 않는다. 지우는 건 플레이어 몫이다.
        //
        // WordChainManager의 체인도 지우지 않는다 - GDD의 오타 페널티(조합 전부 초기화)는
        // 여기 적용하지 않기로 한 결정이다. 체인은 액션 카드로 완성되어 다른 시스템(SkillResolver)에
        // 넘어간 뒤 그쪽에서 ClearChain을 호출해야 비워진다.
        if (_notProgressing)
            return;

        // 어긋나기 시작한 순간에만 한 번 알린다. 매 글자마다 쏘면 로그와 연출이 폭주한다.
        _notProgressing = true;
        mainBufferManager.ClearBuffer();
        OnTypo?.Invoke();
    }
}
