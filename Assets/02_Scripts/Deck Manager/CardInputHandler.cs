using System;
using UnityEngine;

public class CardInputHandler : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private MainBufferManager mainBufferManager;

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
        inputManager.ClearInput();
        OnTypo?.Invoke();
    }
}
