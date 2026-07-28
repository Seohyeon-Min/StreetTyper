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

    private void OnEnable()
    {
        inputManager.OnCharacterEntered += HandleCharacterEntered;
    }

    private void OnDisable()
    {
        inputManager.OnCharacterEntered -= HandleCharacterEntered;
    }

    private void HandleCharacterEntered(char _)
    {
        var typed = inputManager.CurrentInput;
        var cards = cardSlotManager.CurrentCards;

        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].CardName != typed)
                continue;

            var matched = cards[i];
            mainBufferManager.AddCard(matched);
            wordChainManager?.SubmitWord(matched.CardName);
            cardSlotManager.ConsumeSlot(i);
            inputManager.ClearInput();
            OnCardMatched?.Invoke(matched);
            return;
        }

        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].CardName.StartsWith(typed, StringComparison.Ordinal))
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
