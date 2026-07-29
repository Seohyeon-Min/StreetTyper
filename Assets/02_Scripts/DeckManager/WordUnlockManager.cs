using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 게임에 존재하는 전체 단어 목록을 들고 있고, 언제 무엇을 사전에 넣어줄지 결정한다.
// 사전(WordDictionary)은 "지금 쓸 수 있는 단어"만 알 뿐 전체 목록은 모른다 - 그 구분이 이 둘의 역할 분담이다.
public class WordUnlockManager : MonoBehaviour
{
    [Serializable]
    public class WordEntry
    {
        public CardBase card;

        [Tooltip("체크하면 게임 시작 시 사전에 바로 들어갑니다.")]
        public bool grantedAtStart;
    }

    [SerializeField] private WordDictionary wordDictionary;

    [Tooltip("게임에 존재하는 모든 단어. 여기 없는 단어는 어떤 경로로도 등장하지 않습니다.")]
    [SerializeField] private List<WordEntry> allWords = new List<WordEntry>();

    [Tooltip("스테이지 클리어 시 지급할 단어 개수")]
    [SerializeField] private int wordsPerReward = 3;

    [SerializeField] private bool logDebugEvents = true;

    // 지급할 때마다 새 리스트를 만들지 않도록 재사용한다.
    private readonly List<CardBase> _granted = new List<CardBase>();
    private readonly List<CardBase> _candidates = new List<CardBase>();

    /// <summary>게임(런) 시작 시 호출. 사전을 비우고 grantedAtStart로 표시된 단어를 전부 넣는다.</summary>
    public void GrantStartingWords()
    {
        if (!HasDictionary())
            return;

        wordDictionary.Clear();

        _granted.Clear();
        for (var i = 0; i < allWords.Count; i++)
        {
            var entry = allWords[i];
            if (entry != null && entry.grantedAtStart && entry.card != null)
                _granted.Add(entry.card);
        }

        if (_granted.Count == 0)
        {
            Debug.LogWarning("WordUnlockManager: Granted At Start로 표시된 단어가 없어 사전이 빈 채로 시작합니다. " +
                             "All Words 목록에서 시작 단어를 체크하세요.", this);
            return;
        }

        wordDictionary.AddWords(_granted);

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 시작 단어 {_granted.Count}개 지급 - {JoinNames(_granted)}", this);
    }

    /// <summary>스테이지 클리어 보상. 아직 사전에 없는 단어 중 랜덤으로 골라 지급하고 그 목록을 돌려준다
    /// (나중에 선택 UI나 보상 연출이 "무엇을 얻었는지" 알아야 하므로).</summary>
    public IReadOnlyList<CardBase> GrantStageClearReward()
    {
        _granted.Clear();

        if (!HasDictionary())
            return _granted;

        CollectLockedWords();

        var count = Mathf.Min(wordsPerReward, _candidates.Count);
        for (var i = 0; i < count; i++)
        {
            // 뽑은 건 후보에서 빼서 같은 단어가 두 번 나오지 않게 한다.
            var pick = UnityEngine.Random.Range(0, _candidates.Count);
            _granted.Add(_candidates[pick]);
            _candidates.RemoveAt(pick);
        }

        wordDictionary.AddWords(_granted);

        if (logDebugEvents)
        {
            if (_granted.Count > 0)
                Debug.Log($"WordUnlock: 클리어 보상 {_granted.Count}개 지급 - {JoinNames(_granted)}", this);
            else
                Debug.Log("WordUnlock: 더 이상 해금할 단어가 없습니다.", this);
        }

        return _granted;
    }

    // 아직 사전에 없는 단어들을 후보로 모은다.
    private void CollectLockedWords()
    {
        _candidates.Clear();

        for (var i = 0; i < allWords.Count; i++)
        {
            var entry = allWords[i];
            if (entry?.card != null && !wordDictionary.Contains(entry.card))
                _candidates.Add(entry.card);
        }
    }

    private bool HasDictionary()
    {
        if (wordDictionary != null)
            return true;

        Debug.LogWarning("WordUnlockManager: Word Dictionary가 연결되지 않아 단어를 지급할 수 없습니다.", this);
        return false;
    }

    private static string JoinNames(IReadOnlyList<CardBase> cards)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < cards.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(cards[i].CardName);
        }

        return sb.ToString();
    }

    private void OnValidate()
    {
        if (allWords == null)
            return;

        for (var i = 0; i < allWords.Count; i++)
        {
            var entry = allWords[i];

            if (entry == null || entry.card == null)
            {
                Debug.LogWarning($"WordUnlockManager: All Words[{i}]에 카드가 비어 있습니다.", this);
                continue;
            }

            for (var j = i + 1; j < allWords.Count; j++)
            {
                var other = allWords[j];
                if (other?.card == null || other.card.CardName != entry.card.CardName)
                    continue;

                Debug.LogWarning($"WordUnlockManager: '{entry.card.CardName}'이(가) All Words[{i}]와 [{j}]에 중복되어 " +
                                 "있습니다. CardName은 타이핑 매칭 키라서 중복되면 하나는 영영 입력할 수 없습니다.", this);
            }
        }
    }
}
