using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 카드를 <c>Resources/CardLocalization.json</c>에서 읽어 만들어 두는 곳.
/// <b>카드 객체가 태어나는 유일한 자리</b>이며, 코드는 여기서 id로 꺼내 쓴다.
///
/// <see cref="LanguageSettings"/>·<see cref="GameScenes"/>와 같은 결의 <c>static</c> 클래스다 -
/// 값과 사전이 전부라 MonoBehaviour가 필요 없고, 씬 오브젝트도 <c>DontDestroyOnLoad</c>도 없으니
/// <b>인스펙터 배선이 늘지 않는다.</b> 카드를 참조해야 하면 매니저를 만들어 끼우지 말고
/// <see cref="Get"/>에 id를 넘길 것.
///
/// ⚠️ <b>지연 로드다</b>(<see cref="EnsureLoaded"/>). 이 프로젝트엔 스크립트 실행 순서 설정이
/// 없어서 <c>RuntimeInitializeOnLoadMethod</c>끼리의 순서를 기댈 수 없고, <c>OnValidate</c>처럼
/// 에디터에서 Play 없이 불리는 경로도 있기 때문이다(<c>Resources.Load</c>는 에디터에서도 된다).
/// </summary>
public static class CardDatabase
{
    private const string ResourcePath = "CardLocalization";

    private static readonly Dictionary<string, CardBase> _byId = new Dictionary<string, CardBase>();
    private static readonly Dictionary<string, CardDefinition> _defsById = new Dictionary<string, CardDefinition>();
    private static readonly List<CardBase> _all = new List<CardBase>();

    private static bool _loaded;

    /// <summary>JSON에 적힌 순서 그대로의 전체 카드(명령 카드 포함).
    /// 해금 후보를 고르는 <see cref="WordUnlockManager"/>가 이걸 훑는다.</summary>
    public static IReadOnlyList<CardBase> All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }

    /// <summary>id로 카드를 꺼낸다. 없으면 <c>null</c>이며 경고를 남긴다 -
    /// 인스펙터에 적은 id에 오타가 나면 그 명령이 통째로 사라지므로 조용히 넘어가면 안 된다.</summary>
    public static CardBase Get(string id)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(id))
            return null;

        if (_byId.TryGetValue(id, out var card))
            return card;

        Debug.LogWarning($"CardDatabase: '{id}'라는 카드가 {ResourcePath}.json에 없습니다. " +
                         "id 오타이거나 JSON에 항목을 추가하지 않은 것입니다.");
        return null;
    }

    /// <summary>
    /// id로 카드를 꺼내되 종류까지 확인한다. 인스펙터에 id를 문자열로 적는 자리
    /// (<see cref="PauseManager"/>의 명령 카드 등)가 <c>Awake</c>에서 한 번 부르는 용도다.
    ///
    /// 못 찾거나 종류가 다르면 <c>null</c>과 함께 <b>어느 오브젝트의 어느 칸인지</b>를 찍는다 -
    /// 이 프로젝트에서 배선 누락은 예외가 아니라 "아무 일도 안 일어남"으로 나타나서
    /// 필드명 없는 경고로는 추적이 매우 어렵다.
    /// </summary>
    public static T Get<T>(string id, UnityEngine.Object owner = null, string fieldName = null)
        where T : CardBase
    {
        if (string.IsNullOrEmpty(id))
            return null;

        var card = Get(id);
        if (card == null)
            return null;

        if (card is T typed)
            return typed;

        Debug.LogWarning($"CardDatabase: {(owner != null ? owner.name : "?")}.{fieldName}에 적힌 " +
                         $"'{id}'는 {card.GetType().Name}인데 {typeof(T).Name}이(가) 필요합니다.", owner);
        return null;
    }

    /// <summary>그 id가 JSON에 있는지만 본다. 경고를 내지 않으므로
    /// <c>OnValidate</c>에서 직접 문구를 만들어 알릴 때 쓴다.</summary>
    public static bool Contains(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);
    }

    /// <summary>표시 문자열을 고르려고 <see cref="LanguageSettings.PickCardText"/>가 읽는 원본 행.
    /// 없으면 <c>null</c>이다(경고는 그쪽에서 낸다 - 글자마다 불리는 경로라 여기서 내면 폭주한다).</summary>
    public static CardDefinition Definition(string id)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(id))
            return null;

        return _defsById.TryGetValue(id, out var def) ? def : null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        // 실패해도 다시 시도하지 않는다 - 매 프레임 Resources.Load를 두드리며 같은 에러를
        // 반복해 찍는 것보다, 한 번 시끄럽게 알리고 빈 사전으로 두는 편이 진단하기 쉽다.
        _loaded = true;

        var json = Resources.Load<TextAsset>(ResourcePath);
        if (json == null)
        {
            Debug.LogError($"CardDatabase: Resources/{ResourcePath}.json을 찾을 수 없어 카드가 하나도 " +
                           "없습니다. 손패·보상·명령 카드가 전부 비어 보입니다.");
            return;
        }

        var parsed = JsonUtility.FromJson<CardDefinitionList>(json.text);
        if (parsed?.cards == null)
        {
            Debug.LogError($"CardDatabase: {ResourcePath}.json을 읽지 못했습니다. " +
                           "최상위가 {\"cards\": [...]} 형태인지 확인하세요.");
            return;
        }

        for (var i = 0; i < parsed.cards.Count; i++)
        {
            var def = parsed.cards[i];

            if (def == null || string.IsNullOrEmpty(def.id))
            {
                Debug.LogError($"CardDatabase: {ResourcePath}.json의 {i}번째 항목에 id가 없어 건너뜁니다.");
                continue;
            }

            if (_byId.ContainsKey(def.id))
            {
                // id는 인스펙터·코드가 카드를 가리키는 키다. 겹치면 한쪽은 영영 꺼낼 수 없다.
                Debug.LogError($"CardDatabase: id '{def.id}'가 {ResourcePath}.json에 두 번 있습니다. " +
                               "뒤엣것을 무시합니다.");
                continue;
            }

            var card = Create(def);
            if (card == null)
                continue;

            _byId[def.id] = card;
            _defsById[def.id] = def;
            _all.Add(card);
        }
    }

    private static CardBase Create(CardDefinition def)
    {
        switch (def.type)
        {
            case "Action":    return new ActionCardData(def);
            case "Modifier":  return new ModifierCardData(def);
            case "Attribute": return new AttributeCardData(def);
            case "Command":   return new CommandCardData(def);
        }

        Debug.LogError($"CardDatabase: 카드 '{def.id}'의 type이 '{def.type}'인데 " +
                       "Action/Modifier/Attribute/Command 중 하나여야 합니다. 이 카드는 빠집니다.");
        return null;
    }

    /// <summary>JSON에 이름으로 적힌 enum을 읽는다. 못 읽으면 기본값으로 두고 경고를 남긴다 -
    /// 조용히 0번 값이 되면 카드가 엉뚱한 효과로 동작한다(에셋 시절 인덱스 밀림과 같은 사고다).</summary>
    public static T ParseEnum<T>(string text, string cardId, string fieldName, T fallback = default)
        where T : struct, Enum
    {
        if (string.IsNullOrEmpty(text))
            return fallback;

        if (Enum.TryParse<T>(text, true, out var value))
            return value;

        Debug.LogError($"CardDatabase: 카드 '{cardId}'의 {fieldName}이(가) '{text}'인데 " +
                       $"{typeof(T).Name}에 없는 값입니다. {fallback}으로 둡니다.");
        return fallback;
    }

#if UNITY_EDITOR
    /// <summary>JSON을 고친 뒤 Play를 다시 누르지 않고도 반영되게 한다.
    /// static 필드는 도메인 리로드를 끄면 Play 사이에도 남기 때문에 초기화 지점을 명시해 둔다
    /// (<see cref="LanguageSettings"/>·<see cref="SkillResolver"/>와 같은 이유).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reload()
    {
        _byId.Clear();
        _defsById.Clear();
        _all.Clear();
        _loaded = false;
    }
#endif
}
