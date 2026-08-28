using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON 한 행을 읽어 <b>enum까지 풀어둔</b> 런타임 도전과제. 조건 이름을 매번 문자열로 비교하지
/// 않도록 로드 시점에 한 번만 파싱한다(<see cref="CardDatabase"/>가 JSON 행에서 카드 객체를
/// 만들어 두는 것과 같은 구조다).
/// </summary>
public class Achievement
{
    public readonly AchievementDefinition Definition;
    public readonly AchievementCondition Condition;

    /// <summary><see cref="AchievementCondition.ClearInLanguage"/>에서만 의미가 있다.</summary>
    public readonly GameLanguage Language;

    /// <summary><see cref="AchievementCondition.ClearWithDeckChange"/>에서만 의미가 있다.</summary>
    public readonly DeckChangeRule AddedRule;
    public readonly DeckChangeRule RemovedRule;

    public string Id => Definition.id;
    public int Threshold => Definition.threshold;

    public Achievement(AchievementDefinition definition, AchievementCondition condition,
                       GameLanguage language, DeckChangeRule addedRule, DeckChangeRule removedRule)
    {
        Definition = definition;
        Condition = condition;
        Language = language;
        AddedRule = addedRule;
        RemovedRule = removedRule;
    }
}

/// <summary>
/// 게임의 <b>도전과제</b> 단일 출처. <c>Resources/AchievementDefinitions.json</c>을 읽는다.
/// <see cref="CardDatabase"/>·<see cref="DialogueDatabase"/>와 같은 결의 <c>static</c> 클래스다 -
/// 씬 오브젝트도 <c>DontDestroyOnLoad</c>도 없으니 <b>인스펙터 배선이 늘지 않는다.</b>
///
/// ⚠️ <b>지연 로드다</b>(<see cref="EnsureLoaded"/>). 이 프로젝트엔 스크립트 실행 순서 설정이 없어서
/// <c>RuntimeInitializeOnLoadMethod</c>끼리의 순서를 기댈 수 없다(두 Database가 지연 로드인 이유와 같다).
///
/// ⭐ <b>로드 시점 검증이 이 시스템의 유일한 안전망이다.</b> 도전과제는 조건이 맞을 때까지 아무
/// 일도 일어나지 않으므로, 설정이 틀려도 <b>"아직 달성 못 한 것"과 구분되지 않는다.</b> 그래서
/// 아래 검사들은 경고가 아니라 에러로 낸다 - 화면에 증상이 나타나지 않는 종류의 실수라
/// 콘솔에서 잡지 못하면 출시 후에야 발견된다.
/// </summary>
public static class AchievementDatabase
{
    private const string ResourcePath = "AchievementDefinitions";

    private static readonly List<Achievement> _all = new List<Achievement>();
    private static bool _loaded;

    /// <summary>JSON에 정의된 도전과제 전부. <see cref="AchievementManager"/>가 이것을 훑으며
    /// 조건이 맞는 행만 평가한다.</summary>
    public static IReadOnlyList<Achievement> All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        // 실패해도 다시 시도하지 않는다 - 매 프레임 Resources.Load를 두드리며 같은 에러를
        // 반복해 찍는 것보다, 한 번 시끄럽게 알리고 빈 목록으로 두는 편이 진단하기 쉽다.
        _loaded = true;

        var json = Resources.Load<TextAsset>(ResourcePath);
        if (json == null)
        {
            Debug.LogError($"AchievementDatabase: Resources/{ResourcePath}.json을 찾을 수 없어 " +
                           "도전과제가 하나도 올라가지 않습니다.");
            return;
        }

        var parsed = JsonUtility.FromJson<AchievementDefinitionList>(json.text);
        if (parsed?.achievements == null)
        {
            Debug.LogError($"AchievementDatabase: {ResourcePath}.json을 읽지 못했습니다. " +
                           "최상위가 {\"achievements\": [...]} 형태인지 확인하세요.");
            return;
        }

        var seen = new HashSet<string>();

        for (var i = 0; i < parsed.achievements.Count; i++)
        {
            var built = Build(parsed.achievements[i], i, seen);
            if (built != null)
                _all.Add(built);
        }
    }

    /// <summary>한 행을 검증하고 런타임 객체로 만든다. 문제가 있으면 에러를 내고 null을 돌려준다 -
    /// 그 행만 빠지고 나머지는 정상 동작한다(전부 멈추는 것보다 낫다).</summary>
    private static Achievement Build(AchievementDefinition def, int index, HashSet<string> seen)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
        {
            Debug.LogError($"AchievementDatabase: {ResourcePath}.json의 {index}번째 항목에 id가 없어 건너뜁니다.");
            return null;
        }

        if (!seen.Add(def.id))
        {
            Debug.LogError($"AchievementDatabase: id '{def.id}'가 {ResourcePath}.json에 두 번 있습니다. " +
                           "뒤엣것을 무시합니다.");
            return null;
        }

        if (!TryParseEnum(def.condition, out AchievementCondition condition))
        {
            Debug.LogError($"AchievementDatabase: '{def.id}'의 condition '{def.condition}'을 읽지 못했습니다. " +
                           "AchievementCondition의 값 이름과 정확히 같아야 합니다 - 이 도전과제는 영영 올라가지 않습니다.");
            return null;
        }

        // ⚠️ 문턱값을 쓰는 조건인데 threshold가 0이면 "0 이상"/"0 이하"가 되어 항상 참이다.
        // 첫 판에 도전과제가 우수수 뜨는데 원인이 JSON의 빠진 칸이라 추적이 어렵다.
        if (UsesThreshold(condition) && def.threshold <= 0)
        {
            Debug.LogError($"AchievementDatabase: '{def.id}'({condition})에 threshold가 없습니다(현재 {def.threshold}). " +
                           "이대로면 조건이 언제나 참이 되어 시작하자마자 달성됩니다.");
            return null;
        }

        var language = GameLanguage.Korean;
        if (condition == AchievementCondition.ClearInLanguage &&
            !TryParseEnum(def.language, out language))
        {
            Debug.LogError($"AchievementDatabase: '{def.id}'의 language '{def.language}'를 읽지 못했습니다. " +
                           "GameLanguage의 값 이름(Korean/English/French/Spanish/Japanese)이어야 합니다.");
            return null;
        }

        var addedRule = DeckChangeRule.Any;
        var removedRule = DeckChangeRule.Any;

        if (condition == AchievementCondition.ClearWithDeckChange)
        {
            // ⚠️ 여기서 비어 있는 것을 Any로 넘겨주면 안 된다 - 그러면 "덱을 어떻게 바꿨든"
            // 클리어하기만 하면 뜨는 도전과제가 조용히 만들어진다.
            if (!TryParseEnum(def.addedRule, out addedRule) ||
                !TryParseEnum(def.removedRule, out removedRule))
            {
                Debug.LogError($"AchievementDatabase: '{def.id}'의 addedRule/removedRule을 읽지 못했습니다 " +
                               $"(addedRule '{def.addedRule}', removedRule '{def.removedRule}'). " +
                               "DeckChangeRule의 값 이름(Any/Zero/AtLeastOne)이어야 합니다.");
                return null;
            }
        }

        return new Achievement(def, condition, language, addedRule, removedRule);
    }

    private static bool UsesThreshold(AchievementCondition condition)
    {
        switch (condition)
        {
            case AchievementCondition.PlayerHPAtKillAtMost:
            case AchievementCondition.ActionsInTurnAtLeast:
            case AchievementCondition.SynergiesInChainAtLeast:
            case AchievementCondition.ClearWithActionsAtMost:
            case AchievementCondition.ClearWithSynergiesAtMost:
            case AchievementCondition.ClearWithDeckSizeAtLeast:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// enum 값 이름을 대소문자까지 정확히 맞춰 읽는다.
    ///
    /// ⚠️ <c>Enum.TryParse</c>는 <b>숫자 문자열도 받아들이고</b>(<c>"99"</c> → 정의되지 않은 값)
    /// 대소문자도 무시할 수 있다. 그대로 쓰면 오타가 조용히 통과하므로
    /// <see cref="Enum.IsDefined"/>로 한 번 더 거른다.
    /// </summary>
    private static bool TryParseEnum<T>(string name, out T value) where T : struct
    {
        value = default;

        if (string.IsNullOrEmpty(name))
            return false;

        if (!Enum.TryParse(name, false, out value))
            return false;

        return Enum.IsDefined(typeof(T), value);
    }

#if UNITY_EDITOR
    /// <summary>JSON을 고친 뒤 에디터를 다시 켜지 않고도 반영되게 한다.
    /// static 필드는 도메인 리로드를 끄면 Play 사이에도 남는다(두 Database와 같은 이유).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reload()
    {
        _all.Clear();
        _loaded = false;
    }
#endif
}
