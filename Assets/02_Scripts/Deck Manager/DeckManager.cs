using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private CardInputHandler cardInputHandler;
    [SerializeField] private MainBufferManager mainBufferManager;
    [SerializeField] private InputManager inputManager;

    [SerializeField] private bool logDebugEvents;

    public CardSlotManager Slots => cardSlotManager;
    public CardInputHandler Input => cardInputHandler;
    public MainBufferManager Buffer => mainBufferManager;

    private void OnEnable()
    {
        if (!logDebugEvents)
            return;

        cardInputHandler.OnCardMatched += HandleCardMatched;
        cardInputHandler.OnTypo += HandleTypo;
        mainBufferManager.OnCardAdded += HandleCardAdded;
        mainBufferManager.OnBufferCleared += HandleBufferCleared;
    }

    private void OnDisable()
    {
        if (!logDebugEvents)
            return;

        cardInputHandler.OnCardMatched -= HandleCardMatched;
        cardInputHandler.OnTypo -= HandleTypo;
        mainBufferManager.OnCardAdded -= HandleCardAdded;
        mainBufferManager.OnBufferCleared -= HandleBufferCleared;
    }

    private void HandleCardMatched(CardBase card)
    {
        Debug.Log($"Matched: {card.CardName}");
    }

    private void HandleTypo()
    {
        Debug.Log("Typo");
    }

    private void HandleCardAdded(CardBase card)
    {
        Debug.Log($"Buffer += {card.CardName} (count: {mainBufferManager.Buffer.Count})");
    }

    private void HandleBufferCleared()
    {
        Debug.Log("Buffer cleared");
    }
}
