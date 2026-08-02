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
        // 배열만 잡아두고 채우지는 않는다. 손패가 나오는 시점은 스테이지가 시작될 때
        // (StageManager.BeginStageAfterDelay -> RefillAll) 한 곳뿐이다 - 사전이 채워지는
        // 순간 자동으로 뽑으면 스테이지 시작 대기 중에 손패가 미리 보였다가 다시 뽑히게 된다.
        _currentCards = new CardBase[slotCount];

        if (wordDictionary == null)
            Debug.LogWarning("CardSlotManager: Word Dictionary가 연결되지 않아 슬롯을 채울 수 없습니다.", this);
    }

    public void EmptyAllSlots()
    {
        if (_currentCards == null) return;

        for (int i = 0; i < _currentCards.Length; i++)
        {
            _currentCards[i] = null;
            OnSlotChanged?.Invoke(i, null); // UI(CardSlotView)에 null을 보내서 카드를 화면에서 내림
        }
    }

    public void ConsumeSlot(int index)
    {
        // 방금 타이핑으로 쓴 카드가 같은 자리에 곧바로 다시 올라오지 않게 한다.
        // 슬롯 간 중복(다른 칸에 같은 단어)은 의도된 동작이라 그대로 둔다.
        FillSlot(index, excludeCurrent: true);
    }

    /// <summary>차 있는 슬롯까지 전부 새로 뽑는다. 스테이지가 바뀔 때처럼 손패를 통째로
    /// 갈아야 하는 경우에만 쓴다 - 평소 보충은 ConsumeSlot이 담당한다.</summary>
    public void RefillAll()
    {
        if (_currentCards == null)
            return;

        // 사전이 비어 있으면 들고 있던 카드를 null로 지워버리게 되므로 아예 손대지 않는다.
        if (wordDictionary == null || wordDictionary.Words.Count == 0)
        {
            Debug.LogWarning("CardSlotManager: 사전이 비어 있어 손패를 다시 뽑지 않았습니다.", this);
            return;
        }

        // 손패를 통째로 가는 경우엔 제외 없이 완전 랜덤으로 뽑는다.
        for (var i = 0; i < _currentCards.Length; i++)
            FillSlot(i, excludeCurrent: false);
    }

    private void FillSlot(int index, bool excludeCurrent)
    {
        CardBase card = null;
        if (wordDictionary != null)
        {
            // 제외 대상은 슬롯을 덮어쓰기 전에 읽어야 한다.
            card = excludeCurrent
                ? wordDictionary.GetRandomWord(_currentCards[index])
                : wordDictionary.GetRandomWord();
        }

        // 사전이 아직 비어 있다면 빈 슬롯을 그대로 두고 이벤트도 쏘지 않는다.
        if (card == null && _currentCards[index] == null)
            return;

        _currentCards[index] = card;
        OnSlotChanged?.Invoke(index, card);
    }
}
