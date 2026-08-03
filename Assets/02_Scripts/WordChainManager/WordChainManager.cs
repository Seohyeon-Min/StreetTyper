using System;
using System.Collections.Generic;
using UnityEngine;

// 입력한 단어가 플레이어의 사전에 존재하는지 검사하고 현재 기술 조합(체인)을 관리한다.
// 분류는 CardBase.Category(Modifier/Time/Type/Action) 하나만 사용한다 - 별도 WordData 계층을 두지 않는다.
public class WordChainManager : MonoBehaviour
{
    [Tooltip("플레이어가 현재 보유한 단어 사전. 여기 없는 단어는 타이핑해도 체인에 들어가지 않습니다.")]
    [SerializeField] private WordDictionary wordDictionary;

    [SerializeField] private bool logDebugEvents;

    private readonly List<WordInstance> _chain = new List<WordInstance>();
    private bool _timeUsed;
    private bool _typeUsed;

    public IReadOnlyList<WordInstance> CurrentChain => _chain;
    public bool IsComplete { get; private set; }

    public event Action<WordInstance> OnWordAdded;
    public event Action OnWordRemoved;
    public event Action OnChainCleared;
    public event Action<IReadOnlyList<WordInstance>> OnChainCompleted;

    public WordSubmitResult SubmitWord(string input)
    {
        if (string.IsNullOrEmpty(input))
            return WordSubmitResult.EmptyInput;

        var card = ResolveWord(input);
        if (card == null)
            return WordSubmitResult.NotOwned;

        // 명령 카드(넘기기/계속 등)는 조합 단어가 아니다. IsCategoryFull이 모르는 분류를
        // "안 참" 취급하므로 여기서 막지 않으면 체인에 무제한으로 쌓인다.
        // 사전에 애초에 못 들어가지만(WordDictionary.AddInternal), 경로가 늘어도 안전하도록 여기도 막는다.
        if (card.Category == CardCategory.Command)
        {
            if (logDebugEvents)
                Debug.Log($"WordChain: {card.CardName} skipped (command card)", this);
            return WordSubmitResult.NotOwned;
        }

        // 완성된 체인 뒤에 유효한 새 단어가 들어오면 그 순간을 다음 체인의 시작으로 보고
        // 여기서 자동으로 비운다. SkillResolver 같은 소비 시스템이 생기면 그쪽이
        // OnChainCompleted 직후 스스로 ClearChain을 부를 테니, 그때는 이미 비어 있어 무해하다.
        if (IsComplete)
            ClearChain();

        // 슈퍼+슈퍼처럼 같은 단어의 중복 사용은 막되, CardInputHandler 쪽 매칭/버퍼 흐름은
        // 이 반환값을 보지 않으므로 그대로 진행된다 - 체인에만 조용히 안 들어간다.
        if (IsDuplicate(card))
        {
            if (logDebugEvents)
                Debug.Log($"WordChain: {card.CardName} skipped (duplicate)", this);
            return WordSubmitResult.DuplicateWord;
        }

        if (IsCategoryFull(card.Category))
            return WordSubmitResult.CategoryFull;

        var word = new WordInstance(card);
        _chain.Add(word);

        if (card.Category == CardCategory.Time)
            _timeUsed = true;
        else if (card.Category == CardCategory.Type)
            _typeUsed = true;

        if (logDebugEvents)
            Debug.Log($"WordChain += {word.WordName} ({word.Category})", this);
        OnWordAdded?.Invoke(word);

        if (card.Category != CardCategory.Action)
            return WordSubmitResult.Success;

        IsComplete = true;
        if (logDebugEvents)
            Debug.Log("WordChain: skill completed", this);
        OnChainCompleted?.Invoke(_chain);
        return WordSubmitResult.SkillCompleted;
    }

    public void RemoveLastWord()
    {
        if (_chain.Count == 0)
            return;

        var last = _chain[_chain.Count - 1];
        _chain.RemoveAt(_chain.Count - 1);

        if (last.Category == CardCategory.Action)
            IsComplete = false;

        RecountCategoryFlags();
        OnWordRemoved?.Invoke();
    }

    public void ClearChain()
    {
        _chain.Clear();
        _timeUsed = false;
        _typeUsed = false;
        IsComplete = false;

        if (logDebugEvents)
            Debug.Log("WordChain cleared", this);
        OnChainCleared?.Invoke();
    }

    public bool CanCompleteSkill()
    {
        return !IsComplete;
    }

    private bool IsDuplicate(CardBase card)
    {
        foreach (var word in _chain)
        {
            if (word.Card == card)
                return true;
        }

        return false;
    }

    private bool IsCategoryFull(CardCategory category)
    {
        // Action의 "정확히 1개"는 여기서 막지 않는다 - SubmitWord 초반의 IsComplete 검사가 이미 보장한다.
        return category switch
        {
            CardCategory.Time => _timeUsed,
            CardCategory.Type => _typeUsed,
            _ => false
        };
    }

    private void RecountCategoryFlags()
    {
        _timeUsed = false;
        _typeUsed = false;

        foreach (var word in _chain)
        {
            if (word.Category == CardCategory.Time)
                _timeUsed = true;
            else if (word.Category == CardCategory.Type)
                _typeUsed = true;
        }
    }

    // 사전 조회를 한 곳에 가둬둬서 SubmitWord는 사전이 어떻게 생겼는지 몰라도 된다.
    // 단어별 사용 횟수 제한이 생기면 그 검사도 여기에 붙는다 - 지금은 보유 여부만 본다.
    private CardBase ResolveWord(string input)
    {
        if (wordDictionary == null)
            return null;

        return wordDictionary.TryGetWord(input, out var card) ? card : null;
    }
}
