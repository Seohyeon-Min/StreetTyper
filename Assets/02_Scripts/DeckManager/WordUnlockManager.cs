using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 난이도 하나에 대한 시작 카드 보정. JSON의 <c>grantedAtStart</c>를 기준선으로 두고 여기서
/// 빼거나 더한다.
///
/// ⚠️ <b>카드 이름이 아니라 id를 적는다</b>(<c>"lucky"</c>). 이름은 언어마다 달라지므로
/// (<c>LanguageSettings.PickCardText</c>) 이름으로 적으면 한국어에서만 걸리고 나머지 네 언어에서는
/// 조용히 아무 일도 일어나지 않는다.
/// </summary>
[Serializable]
public class StartingCardOverride
{
    public GameDifficulty difficulty;

    [Tooltip("이 난이도에서 시작 카드에서 뺄 카드 id들.")]
    public string[] removeIds;

    [Tooltip("이 난이도에서만 추가로 줄 카드 id들. JSON에서 grantedAtStart가 꺼져 있어도 들어간다.")]
    public string[] addIds;
}

// 게임에 존재하는 전체 단어 목록을 들고 있고, 언제 무엇을 사전에 넣어줄지 결정한다.
// 사전(WordDictionary)은 "지금 쓸 수 있는 단어"만 알 뿐 전체 목록은 모른다 - 그 구분이 이 둘의 역할 분담이다.
public class WordUnlockManager : MonoBehaviour
{
    [SerializeField] private WordDictionary wordDictionary;

    [Tooltip("난이도별 시작 카드 보정. 비워두면 모든 난이도가 JSON의 grantedAtStart 그대로다.\n" +
             "⚠️ 카드 이름이 아니라 id를 적을 것 - 이름은 언어마다 달라진다.")]
    [SerializeField]
    private StartingCardOverride[] startingCardOverrides =
    {
        // 어려움은 럭키를 처음부터 주지 않는다. 보상 후보로는 그대로 나오므로 "못 얻는" 게
        // 아니라 "직접 골라야 하는" 카드가 된다.
        new StartingCardOverride { difficulty = GameDifficulty.Hard, removeIds = new[] { "lucky" } }
    };

    [Tooltip("스테이지 클리어 시 보여줄 후보 단어 개수. 플레이어는 이 중 하나만 고른다.")]
    [SerializeField] private int wordsPerReward = 3;

    [Tooltip("럭키 조합이 성공했을 때 추가로 열리는 보상 라운드 수. " +
             "장수를 늘리는 게 아니라 보상 창을 한 번 더 띄운다 - 선택제에서는 후보를 늘리면 " +
             "고를 수 있는 건 여전히 하나라 오히려 보상이 줄어드는 것처럼 보인다.")]
    [SerializeField] private int luckyBonusRounds = 1;

    [Tooltip("한 스테이지에서 럭키로 받을 수 있는 보상 라운드의 최대 횟수. 이 수를 채우면 " +
             "같은 스테이지에서 럭키를 몇 번 더 성공시켜도 라운드가 늘지 않는다.")]
    [SerializeField] private int maxLuckyRoundsPerStage = 1;

    [SerializeField] private bool logDebugEvents = true;

    // 럭키로 확정된 추가 보상 라운드. 적을 처치한 순간 쌓이고, 라운드가 끝날 때마다 하나씩 소비된다.
    private int _pendingBonusRounds;

    // 8번 키가 실제 럭키 판정 전에 미리 예약한 1회. 이후 럭키가 실제로 성공하면 새 라운드를
    // 더하지 않고 이 예약을 실제 발동으로 간주해, 디버그 때문에 보상이 두 번 늘지 않게 한다.
    private bool _debugBonusPrebooked;

    // 이번 스테이지에서 럭키로 이미 열어준 보상 라운드 수. ResetStage가 되돌린다.
    private int _luckyRoundsGrantedThisStage;

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
        SetBonusRounds(0);
        _debugBonusPrebooked = false;
        _offered.Clear();

        _granted.Clear();
        var all = CardDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            var card = all[i];
            var def = CardDatabase.Definition(card.CardId);

            // 명령 카드는 grantedAtStart를 켤 일이 없지만, 켜져 있으면 손패에 떠서 타이핑으로
            // 소비되므로 여기서도 막는다(사전·해금·조합 세 곳이 각각 막는 것과 같은 이유).
            if (def == null || !def.grantedAtStart || card.Category == CardCategory.Command)
                continue;

            _granted.Add(card);
        }

        ApplyDifficultyOverride();

        if (_granted.Count == 0)
        {
            Debug.LogWarning("WordUnlockManager: grantedAtStart가 켜진 카드가 없어 사전이 빈 채로 시작합니다. " +
                             "CardLocalization.json에서 시작 단어에 \"grantedAtStart\": true를 넣으세요.", this);
            return;
        }

        wordDictionary.AddWords(_granted);

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 시작 단어 {_granted.Count}개 지급 - {JoinNames(_granted)}", this);
    }

    /// <summary>
    /// JSON으로 만든 시작 카드 목록(<c>_granted</c>)에 지금 난이도의 보정을 얹는다.
    ///
    /// ⚠️ <b>난이도를 CardLocalization.json에 넣지 않은 이유가 이 구조다.</b> 그 파일은 "이 카드가
    /// 무엇인가"의 단일 출처이고 런 구성은 다른 축이라, JSON을 기준선으로 두고 여기서 델타만
    /// 얹어야 카드를 추가할 때 난이도를 신경 쓰지 않아도 된다.
    /// </summary>
    private void ApplyDifficultyOverride()
    {
        var rule = FindOverride(DifficultySettings.Current);
        if (rule == null)
            return;

        if (rule.removeIds != null)
        {
            for (var i = 0; i < rule.removeIds.Length; i++)
            {
                var id = rule.removeIds[i];
                if (string.IsNullOrEmpty(id))
                    continue;

                // id로 지운다 - CardName은 언어마다 달라 비교 대상으로 쓸 수 없다.
                _granted.RemoveAll(card => card != null && card.CardId == id);
            }
        }

        if (rule.addIds != null)
        {
            for (var i = 0; i < rule.addIds.Length; i++)
            {
                var id = rule.addIds[i];
                if (string.IsNullOrEmpty(id))
                    continue;

                var card = CardDatabase.Get<CardBase>(id, this, nameof(startingCardOverrides));
                if (card == null || card.Category == CardCategory.Command)
                    continue;

                if (!_granted.Contains(card))
                    _granted.Add(card);
            }
        }

        // ⚠️ 액션 단어로만 체인이 완성된다 - 하나도 없으면 어떤 조합도 못 만들어 런이 잠긴다.
        // 지금 설정(럭키 제외)은 수식어를 빼는 것이라 안전하지만, 이 목록은 범용이라 막아둔다.
        var hasAction = false;
        for (var i = 0; i < _granted.Count; i++)
        {
            if (_granted[i] != null && _granted[i].Category == CardCategory.Action)
            {
                hasAction = true;
                break;
            }
        }

        if (!hasAction)
            Debug.LogError($"WordUnlockManager: {DifficultySettings.Current} 보정을 적용하니 시작 카드에 " +
                           "액션 단어가 하나도 없습니다. 액션 단어로만 조합이 완성되므로 이대로면 " +
                           $"공격이 영영 불가능합니다 - {nameof(startingCardOverrides)}를 확인하세요.", this);

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 난이도 {DifficultySettings.Current} 보정 적용", this);
    }

    private StartingCardOverride FindOverride(GameDifficulty difficulty)
    {
        if (startingCardOverrides == null)
            return null;

        for (var i = 0; i < startingCardOverrides.Length; i++)
        {
            if (startingCardOverrides[i] != null && startingCardOverrides[i].difficulty == difficulty)
                return startingCardOverrides[i];
        }

        return null;
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

    /// <summary>럭키 조합이 성공했을 때 호출한다(적을 처치했는지는 보지 않는다 - 클리어해야
    /// 보상 창이 열리므로 그 조건만으로 이미 성립한다). 다음 클리어 보상에 luckyBonusRounds만큼
    /// 라운드를 더 연다.
    ///
    /// ⚠️ <b>한 스테이지에 <see cref="maxLuckyRoundsPerStage"/>번까지만 열린다.</b> 예전에는
    /// 성공할 때마다 무제한으로 쌓여서, 럭키를 반복해 치면 보상 창이 그만큼 연달아 떴다.</summary>
    public void AddLuckyBonus()
    {
        if (_debugBonusPrebooked)
        {
            // 예약을 "소비"만 한다 - 라운드를 실제로 더한 건 DebugEnsureLuckyBonus이고
            // 스테이지 카운터도 거기서 이미 올렸다. 여기서 또 올리면 상한이 두 번 깎인다.
            _debugBonusPrebooked = false;

            if (logDebugEvents)
                Debug.Log("WordUnlock: 럭키 성공 - 8번 디버그로 예약된 보상 라운드 사용", this);
            return;
        }

        if (_luckyRoundsGrantedThisStage >= Mathf.Max(0, maxLuckyRoundsPerStage))
        {
            if (logDebugEvents)
                Debug.Log($"WordUnlock: 럭키 성공했지만 이번 스테이지 상한({maxLuckyRoundsPerStage}회)을 " +
                          "이미 채워 보상 라운드를 더하지 않습니다.", this);
            return;
        }

        SetBonusRounds(_pendingBonusRounds + luckyBonusRounds);
        _luckyRoundsGrantedThisStage++;

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 럭키 성공 - 보상 라운드 +{luckyBonusRounds} (누적 +{_pendingBonusRounds}, " +
                      $"이번 스테이지 {_luckyRoundsGrantedThisStage}/{maxLuckyRoundsPerStage})", this);
    }

    /// <summary>스테이지가 새로 열릴 때 호출한다(<see cref="StageManager.LoadStage"/>).
    /// 럭키 상한 카운터를 되돌리고, 소비되지 않고 남은 보너스 라운드도 비운다.
    ///
    /// 남은 라운드까지 비우는 이유: 보통은 보상 루프가 다 소비하지만, 미보유 단어가 떨어져
    /// 후보가 비면 BeginRewardRound가 소비 없이 FinishReward로 빠져 라운드가 남는다.</summary>
    public void ResetStage()
    {
        _luckyRoundsGrantedThisStage = 0;
        _debugBonusPrebooked = false;
        SetBonusRounds(0);
    }

    /// <summary>8번 디버그용. 럭키 조합을 직접 쓰지 않고 9번으로 적을 처치해도 럭키 보상창을
    /// 확인할 수 있도록, 아직 예약된 보너스가 없을 때만 최소 한 묶음을 예약한다.</summary>
    public void DebugEnsureLuckyBonus()
    {
        if (_pendingBonusRounds > 0)
            return;

        SetBonusRounds(Mathf.Max(1, luckyBonusRounds));
        _debugBonusPrebooked = true;

        // 실제로 라운드를 더한 건 여기다 - 스테이지 상한도 여기서 같이 센다.
        _luckyRoundsGrantedThisStage++;

        Debug.Log($"[DEBUG] 럭키 보상 라운드 예약 +{_pendingBonusRounds}", this);
    }

    /// <summary>보너스 라운드 수를 바꾸는 <b>유일한 통로</b>. 값을 넣고 럭키 카드가 읽는 창구까지
    /// 같이 갱신한다 - 한쪽만 바꾸면 카드에 "보상됨"이 남거나 반대로 안 뜬다.
    /// (카드는 ScriptableObject라 이 컴포넌트를 참조할 수 없어 static 창구를 거친다.)</summary>
    private void SetBonusRounds(int rounds)
    {
        _pendingBonusRounds = Mathf.Max(0, rounds);
        SkillResolver.SetLootBonusRounds(_pendingBonusRounds);
    }

    /// <summary>남은 보너스 라운드가 있으면 하나 소비하고 true를 돌려준다.
    /// 보상 한 라운드가 끝날 때마다 물어보는 용도다(StageManager).</summary>
    public bool TryConsumeBonusRound()
    {
        if (_pendingBonusRounds <= 0)
            return false;

        SetBonusRounds(_pendingBonusRounds - 1);

        if (_pendingBonusRounds == 0)
            _debugBonusPrebooked = false;

        if (logDebugEvents)
            Debug.Log($"WordUnlock: 럭키 보상 라운드 소비 (남은 {_pendingBonusRounds})", this);

        return true;
    }

    // 아직 사전에 없는 단어들을 후보로 모은다.
    //
    // ⚠️ 명령 카드(넘기기/계속 등)는 제외한다. CardDatabase.All에는 같이 들어 있는데, 보상
    // 후보로 나오면 고른 순간 사전에 들어가 손패에 뜬다. 예전에는 이 목록이 인스펙터에 따로
    // 있어서 "실수로 끌어다 넣지 말 것"이 규칙이었지만, 지금은 전체 목록을 그대로 훑으므로
    // 여기서 거르는 게 유일한 방어선이다.
    private void CollectLockedWords()
    {
        _candidates.Clear();

        var all = CardDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            var card = all[i];
            if (card.Category != CardCategory.Command && !wordDictionary.Contains(card))
                _candidates.Add(card);
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

    /// <summary>
    /// 카드 목록이 인스펙터를 떠나 JSON으로 갔으므로, 여기서는 <b>이름 중복</b>만 본다.
    /// <see cref="CardName"/>은 타이핑 매칭 키라서 두 카드가 같은 이름을 가지면 하나는 영영
    /// 입력할 수 없다 - 특히 JSON에 id를 추가하면서 이름을 복사해 붙였을 때 나기 쉽다.
    ///
    /// ⚠️ 지금 언어의 이름만 본다. 다른 언어 칸의 중복은 그 언어로 바꿔봐야 드러난다.
    /// </summary>
    private void OnValidate()
    {
        var all = CardDatabase.All;

        for (var i = 0; i < all.Count; i++)
        {
            var name = all[i].CardName;
            if (string.IsNullOrEmpty(name))
                continue;

            for (var j = i + 1; j < all.Count; j++)
            {
                if (all[j].CardName != name)
                    continue;

                Debug.LogWarning($"WordUnlockManager: 카드 이름 '{name}'이(가) id '{all[i].CardId}'와 " +
                                 $"'{all[j].CardId}'에 중복되어 있습니다. 이름은 타이핑 매칭 키라서 " +
                                 "중복되면 하나는 영영 입력할 수 없습니다.", this);
            }
        }
    }
}
