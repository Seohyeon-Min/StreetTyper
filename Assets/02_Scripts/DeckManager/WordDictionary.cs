using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 "지금" 사용할 수 있는 단어 모음. 직렬화 필드가 없는 순수 런타임 상태이고,
// 내용물은 전부 WordUnlockManager가 채운다.
// 슬롯 뽑기(CardSlotManager)와 타이핑 검증(WordChainManager)이 둘 다 여기 하나만 바라보므로,
// 예전처럼 두 목록이 어긋나 "슬롯엔 뜨는데 입력은 안 되는" 상황이 생길 수 없다.
public class WordDictionary : MonoBehaviour
{
    private readonly List<CardBase> _words = new List<CardBase>();

    public IReadOnlyList<CardBase> Words => _words;

    public event Action OnWordsChanged;

    public bool Contains(CardBase card)
    {
        return card != null && _words.Contains(card);
    }

    /// <summary>타이핑한 문자열이 보유 단어인지 찾는다. CardName이 매칭 키다.</summary>
    public bool TryGetWord(string cardName, out CardBase card)
    {
        for (var i = 0; i < _words.Count; i++)
        {
            if (_words[i].CardName == cardName)
            {
                card = _words[i];
                return true;
            }
        }

        card = null;
        return false;
    }

    /// <summary>슬롯을 채울 때 쓴다. GDD "동일 확률 등장" - 균등 랜덤.
    /// 슬롯 간 같은 단어가 겹치는 건 기존 설계대로 의도된 동작이라 막지 않는다.</summary>
    public CardBase GetRandomWord()
    {
        if (_words.Count == 0)
            return null;

        return _words[UnityEngine.Random.Range(0, _words.Count)];
    }

    /// <summary>exclude를 빼고 균등 랜덤으로 뽑는다. 방금 소모한 카드가 같은 슬롯에 곧바로
    /// 다시 올라오는 걸 막기 위한 것 - 슬롯 간 중복은 기존 설계대로 그대로 허용된다.
    /// 보유 단어가 하나뿐이면 제외할 수단이 없으므로 그 단어를 그대로 돌려준다(빈 슬롯 방지).</summary>
    public CardBase GetRandomWord(CardBase exclude)
    {
        if (_words.Count == 0)
            return null;

        var excludeIndex = exclude != null ? _words.IndexOf(exclude) : -1;

        // 제외 대상이 없거나(사전에서 빠진 카드) 뺄 수 없으면 평소대로 뽑는다.
        if (excludeIndex < 0 || _words.Count == 1)
            return GetRandomWord();

        // 거절 샘플링 대신 인덱스를 건너뛴다 - 반복 없이 남은 count-1개에 균등하게 걸린다.
        var pick = UnityEngine.Random.Range(0, _words.Count - 1);
        if (pick >= excludeIndex)
            pick++;

        return _words[pick];
    }

    public bool AddWord(CardBase card)
    {
        if (!AddInternal(card))
            return false;

        OnWordsChanged?.Invoke();
        return true;
    }

    /// <summary>여러 개를 한 번에 넣는다. 이벤트는 마지막에 딱 한 번만 발생한다 -
    /// 하나씩 넣으면 첫 단어가 들어간 순간 CardSlotManager가 빈 슬롯 5칸을
    /// 그 한 단어로 전부 채워버린다.</summary>
    public int AddWords(IReadOnlyList<CardBase> cards)
    {
        if (cards == null)
            return 0;

        var added = 0;
        for (var i = 0; i < cards.Count; i++)
        {
            if (AddInternal(cards[i]))
                added++;
        }

        if (added > 0)
            OnWordsChanged?.Invoke();

        return added;
    }

    /// <summary>사전에서 한 장을 뺀다. 마더 드래곤 보상의 "지우기"가 쓴다.
    /// <see cref="AddWord"/>의 대칭형이고, 실제로 빠졌을 때만 이벤트를 쏜다.</summary>
    public bool RemoveWord(CardBase card)
    {
        if (card == null || !_words.Remove(card))
            return false;

        OnWordsChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (_words.Count == 0)
            return;

        _words.Clear();
        OnWordsChanged?.Invoke();
    }

    private bool AddInternal(CardBase card)
    {
        if (card == null || _words.Contains(card))
            return false;

        // 명령 카드(넘기기/계속 등)는 조합에 쓰는 단어가 아니다. 사전에 들어가면 손패에 떠서
        // 타이핑으로 소비되고 체인에도 들어가려 든다 - CardBase를 상속하는 이상 실수로 꽂힐 수
        // 있으므로 받는 쪽에서 막는다.
        if (card.Category == CardCategory.Command)
        {
            Debug.LogWarning($"WordDictionary: 명령 카드 '{card.CardName}'는 사전에 넣을 수 없습니다. " +
                             "WordUnlockManager의 All Words 목록에 섞여 있는지 확인하세요.", this);
            return false;
        }

        _words.Add(card);
        return true;
    }
}
