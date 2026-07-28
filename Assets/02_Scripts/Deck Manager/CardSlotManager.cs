using System;
using System.Collections.Generic;
using UnityEngine;

public class CardSlotManager : MonoBehaviour
{
    [SerializeField] private List<CardBase> availableCards;
    [SerializeField] private int slotCount = 5;

    private CardBase[] _currentCards;

    public IReadOnlyList<CardBase> CurrentCards => _currentCards;

    public int SlotCount => slotCount;

    public event Action<int, CardBase> OnSlotChanged;

    private void Awake()
    {
        if (availableCards == null || availableCards.Count == 0)
        {
            Debug.LogError("CardSlotManager: availableCards is empty, cannot fill slots.", this);
            return;
        }

        _currentCards = new CardBase[slotCount];
        for (var i = 0; i < slotCount; i++)
            FillSlot(i);
    }

    public void ConsumeSlot(int index)
    {
        FillSlot(index);
    }

    private void FillSlot(int index)
    {
        var card = availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
        _currentCards[index] = card;
        OnSlotChanged?.Invoke(index, card);
    }
}
