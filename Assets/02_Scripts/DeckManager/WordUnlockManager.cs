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

    [Tooltip("스테이지 클리어 시 보여줄 후보 단어 개수. 플레이어는 이 중 하나만 고른다.")]
    [SerializeField] private int wordsPerReward = 3;

    [Tooltip("럭키가 포함된 공격으로 적을 처치했을 때 추가로 열리는 보상 라운드 수. " +
             "장수를 늘리는 게 아니라 보상 창을 한 번 더 띄운다 - 선택제에서는 후보를 늘리면 " +
             "고를 수 있는 건 여전히 하나라 오히려 보상이 줄어드는 것처럼 보인다.")]
    [SerializeField] private int luckyBonusRounds = 1;

    [SerializeField] private bool logDebugEvents = true;

    // 럭키로 확정된 추가 보상 라운드. 적을 처치한 순간 쌓이고, 라운드가 끝날 때마다 하나씩 소비된다.
    private int _pendingBonusRounds;

    // 시작 단어 지급용. 매번 새 리스트를 만들지 않도록 재사용한다.
    private readonly List<CardBase> _granted = new List<CardBase>();
    private readonly List<CardBase> _candidates = new List<CardBase>();

    // ⚠️ 보상 후보는 _granted와 따로 둔다. 선택제에서는 플레이어가 고를 때까지 이 목록을 여러
    // 프레임에 걸쳐 들고 있어야 하는데, _granted는 GrantStartingWords가 같이 쓰는 재사용
    // 리스트라 그 사이에 내용이 갈릴 수 있다.
    private readonly List<CardBase> _offered = new List<CardBase>();

    /// <summary>게임(런) 시작 시 호출. 사전을 비우고 grantedAtStart로 표시된 단어를 전부 넣는다.</summary>
    public void GrantStartingWords()
    {
        if (!HasDictionary())
            return;

        wordDictionary.Clear();

        // 런이 다시 시작되는 지점이다. 지난 런에서 쌓다 만 럭키 라운드가 이월되지 않게 비운다.
        _pendingBonusRounds = 0;
        _offered.Clear();

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

    /// <summary>
    /// 스테이지 클리어 보상 후보를 뽑는다. 아직 사전에 없는 단어 중 랜덤으로 wordsPerReward개를
    /// 고르며, <b>사전에는 넣지 않는다</b> - 플레이어가 그중 하나를 골라야 비로소 확정된다.
    ///
    /// ⚠️ 예전 <c>GrantStageClearReward</c>는 뽑기와 사전 등록을 한 메서드 안에서 같이 했다.
    /// "뽑았지만 아직 확정 안 함"이라는 상태가 없어서 선택제가 성립하지 않았고, 그 분리가
    /// 이 메서드와 <see cref="ConfirmReward"/>다. 다시 합치지 말 것.
    ///
    /// 후보가 모자라면 있는 만큼만 돌려주고, 하나도 없으면 빈 목록을 준다.
    /// </summary>
    public IReadOnlyList<CardBase> RollRewardCandidates()
    {
        _offered.Clear();

        if (!HasDictionary())
            return _offered;

        CollectLockedWords();

        var count = Mathf.Min(wordsPerReward, _candidates.Count);
        for (var i = 0; i < count; i++)
        {
            // 뽑은 건 후보에서 빼서 같은 단어가 두 번 나오지 않게 한다.
            var pick = UnityEngine.Random.Range(0, _candidates.Count);
            _offered.Add(_candidates[pick]);
            _candidates.RemoveAt(pick);
        }

        if (logDebugEvents)
        {
            if (_offered.Count > 0)
                Debug.Log($"WordUnlock: 보상 후보 {_offered.Count}장 - {JoinNames(_offered)}", this);
            else
                Debug.Log("WordUnlock: 더 이상 해금할 단어가 없습니다.", this);
        }

        return _offered;
    }

    /// <summary>플레이어가 고른 한 장을 사전에 넣는다. 넘겼다면 아무것도 부르지 않으면 된다 -
    /// 그게 곧 "이번 보상은 없음"이다.</summary>
    public bool ConfirmReward(CardBase card)
    {
        if (card == null || !HasDictionary())
            return false;

        var added = wordDictionary.AddWord(card);

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 보상 확정 - {card.CardName}{(added ? string.Empty : " (이미 보유 중이라 무시)")}", this);

        return added;
    }

    /// <summary>럭키가 포함된 공격으로 적을 처치했을 때 호출한다. 다음 클리어 보상에
    /// luckyBonusRounds만큼 라운드를 더 연다. 한 턴에 여러 번 성공하면 그만큼 쌓인다.</summary>
    public void AddLuckyBonus()
    {
        _pendingBonusRounds += luckyBonusRounds;

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 럭키 처치 - 보상 라운드 +{luckyBonusRounds} (누적 +{_pendingBonusRounds})", this);
    }

    /// <summary>남은 보너스 라운드가 있으면 하나 소비하고 true를 돌려준다.
    /// 보상 한 라운드가 끝날 때마다 물어보는 용도다(StageManager).</summary>
    public bool TryConsumeBonusRound()
    {
        if (_pendingBonusRounds <= 0)
            return false;

        _pendingBonusRounds--;

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 럭키 보상 라운드 소비 (남은 {_pendingBonusRounds})", this);

        return true;
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
