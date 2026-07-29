using System;
using UnityEngine;

public class CardInputHandler : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private MainBufferManager mainBufferManager;
    [SerializeField] private WordChainManager wordChainManager;

    public event Action<CardBase> OnCardMatched;
    public event Action OnTypo;

    // 조합 중 매칭으로 슬롯을 소비하면 OS IME는 그 글자를 여전히 조합 중이라고 알고 있어서,
    // 다음 카드를 이어 치는 순간 뒤늦게 커밋되어 되돌아올 수 있다. 그 메아리를 한 번만 걸러낸다.
    private string _pendingEcho;

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
        var committed = inputManager.CurrentInput;

        if (committed.Length == 0 && composing.Length == 0)
            return;

        if (_pendingEcho != null)
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

            mainBufferManager.AddCard(matched);
            wordChainManager?.SubmitWord(matched.CardName);
            cardSlotManager.ConsumeSlot(i);
            inputManager.ClearInput();

            // 조합 중이던 글자로 매칭됐다면 OS IME는 아직 그 글자를 붙잡고 있다 - 뒤늦게 커밋되어
            // 돌아올 걸 대비해 한 번만 걸러낼 표시를 남긴다. ClearInput이 OnCompositionChanged를
            // 발생시켜 이 메서드가 재진입하므로, 표시는 반드시 그 뒤에 남겨야 지워지지 않는다.
            if (wasComposing)
                _pendingEcho = matched.CardName;

            OnCardMatched?.Invoke(matched);
            return;
        }

        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && InputManager.IsValidProgress(committed, composing, cards[i].CardName))
                return;
        }

        mainBufferManager.ClearBuffer();

        // 오타가 나도 WordChainManager의 체인은 지우지 않는다 - GDD의 오타 페널티(조합 전부 초기화)는
        // 여기 적용하지 않기로 한 결정이다. 체인은 액션 카드로 완성되어 다른 시스템(SkillResolver)에
        // 넘어간 뒤 그쪽에서 ClearChain을 호출해야 비워지고, 그 전까지는 오타를 내도 계속 이어서 쌓인다.
        inputManager.ClearInput();
        OnTypo?.Invoke();
    }
}
