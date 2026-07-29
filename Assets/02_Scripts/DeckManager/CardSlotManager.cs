using System;
using System.Collections.Generic;
using UnityEngine;

public class CardSlotManager : MonoBehaviour
{
    [SerializeField] private WordDictionary wordDictionary;
    [SerializeField] private int slotCount = 5;

    private CardBase[] _currentCards;

    public IReadOnlyList<CardBase> CurrentCards => _currentCards;

    public int SlotCount => slotCount;

    public event Action<int, CardBase> OnSlotChanged;

    private void Awake()
    {
        // 사전은 StageManager가 Start()에서 채운다. Unity는 모든 Awake를 모든 Start보다 먼저
        // 실행하므로 여기서 슬롯을 채우면 반드시 빈 사전을 보게 된다 - 배열만 잡아두고,
        // 실제로 채우는 건 OnWordsChanged를 받은 뒤로 미룬다.
        _currentCards = new CardBase[slotCount];
    }

    private void OnEnable()
    {
        if (wordDictionary == null)
        {
            Debug.LogWarning("CardSlotManager: Word Dictionary가 연결되지 않아 슬롯을 채울 수 없습니다.", this);
            return;
        }

        wordDictionary.OnWordsChanged += HandleWordsChanged;
        FillEmptySlots();
    }

    private void OnDisable()
    {
        if (wordDictionary != null)
            wordDictionary.OnWordsChanged -= HandleWordsChanged;
    }

    public void ConsumeSlot(int index)
    {
        FillSlot(index);
    }

    private void HandleWordsChanged()
    {
        // 이미 카드가 들어있는 슬롯은 건드리지 않는다 - 스테이지 보상으로 단어가 늘어도 손패가
        // 갑자기 뒤바뀌지 않고, 새 단어는 카드가 소모되면서 자연스럽게 등장한다.
        FillEmptySlots();
    }

    private void FillEmptySlots()
    {
        if (_currentCards == null)
            return;

        for (var i = 0; i < _currentCards.Length; i++)
        {
            if (_currentCards[i] == null)
                FillSlot(i);
        }
    }

    private void FillSlot(int index)
    {
        var card = wordDictionary != null ? wordDictionary.GetRandomWord() : null;

        // 사전이 아직 비어 있다면 빈 슬롯을 그대로 두고 이벤트도 쏘지 않는다.
        if (card == null && _currentCards[index] == null)
            return;

        _currentCards[index] = card;
        OnSlotChanged?.Invoke(index, card);
    }
}
