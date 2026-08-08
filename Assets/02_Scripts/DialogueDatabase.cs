using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <c>DialogueLocalization.json</c>의 대사 한 묶음. 언어별로 <b>줄 수가 같아야 한다</b>
/// (<see cref="DialogueDatabase"/>가 로드 시점에 검사한다 - 이유는 그쪽 주석 참조).
///
/// ⚠️ JsonUtility가 읽으므로 <b>필드는 public이어야 하고 이름이 JSON 키와 정확히 같아야</b> 한다.
/// </summary>
[Serializable]
public class DialogueEntry
{
    [Tooltip("코드가 이 대사를 가리키는 키. DialogueDatabase.Lines(id)에 넘긴다.")]
    public string id;

    public string[] ko;
    public string[] en;
    public string[] fr;
    public string[] es;
    public string[] ja;
}

/// <summary>JsonUtility가 최상위 배열을 못 읽어서 두는 껍데기. JSON의 <c>{"dialogues": [...]}</c>와 짝이다.</summary>
[Serializable]
public class DialogueList
{
    public List<DialogueEntry> dialogues;
}

/// <summary>
/// 대사 id 상수. 씬 이름을 <see cref="GameScenes"/>로만 쓰는 것과 같은 이유로, 코드에 문자열을
/// 직접 타이핑하지 않는다 - 오타를 내면 그 대사가 조용히 빈 줄이 되고, JSON에서 id를 바꿀 때
/// 어디를 같이 고쳐야 하는지 찾을 수가 없다.
/// </summary>
public static class DialogueIds
{
    /// <summary>보스 스테이지 등장 - 데미가 먼저 건네는 인사.</summary>
    public const string DemiGreeting = "boss.demiGreeting";

    /// <summary>보스 스테이지 등장 - 마더 드래곤의 답변.</summary>
    public const string MotherGreeting = "boss.motherGreeting";

    /// <summary>마더 드래곤이 턴마다 하는 말. 순서가 정해져 있다(0=시작 … 3=마무리).</summary>
    public const string MotherDragonTurn = "boss.motherDragonTurn";

    // ⚠️ 옛 NormalEvent("event.normal")는 삭제했다. 일반 적 처치용이었는데 5개 언어가 전부
    // 0줄이라, 말풍선을 만들어 켰다 끄고 곧바로 EndEvent로 빠지는 통과 경로일 뿐이었다.
    // 지금은 BattleManager가 EventManager를 거치지 않고 바로 승리 처리로 간다.
    // 일반 적에게 대사를 주고 싶어지면 id를 다시 만들되, 그때는 5개 언어 줄 수를 맞출 것.

    /// <summary>마더 드래곤을 처치했을 때(보스 스테이지). 둘째 줄의 <c>{0}</c>에 회복량이 들어간다.</summary>
    public const string DragonEvent = "event.dragon";

    /// <summary>엔딩. 마지막 전투에 도달하면 전투 없이 이 대사만 재생하고 게임 클리어로 넘어간다.</summary>
    public const string EndingEvent = "event.ending";
}

/// <summary>
/// 게임에 나오는 <b>대사</b>의 단일 출처. <c>Resources/DialogueLocalization.json</c>을 읽어
/// id로 꺼내 쓴다. 카드 텍스트를 <see cref="CardDatabase"/>가 소유하는 것과 같은 구조이며,
/// <see cref="LanguageSettings"/>·<see cref="GameScenes"/>와 같은 결의 <c>static</c> 클래스다 -
/// 씬 오브젝트도 <c>DontDestroyOnLoad</c>도 없으니 <b>인스펙터 배선이 늘지 않는다.</b>
///
/// ⚠️ <b>카드 JSON과 파일을 나눈 이유</b>: <c>CardLocalization.json</c>은 텍스트뿐 아니라 수치·분류까지
/// 들고 있어 성격이 다르다. 대사는 번역가에게 통째로 넘길 물건이라 따로 둔다.
///
/// ⚠️ <b>대사를 인스펙터 필드로 되돌리지 말 것.</b> 언어가 다섯이라 배열이 5벌씩 필요한데,
/// 그러면 값이 프리팹 기본값과 씬 인스턴스 오버라이드로 흩어져 <b>실제로 화면에 나오는 값이
/// 무엇인지 파일만 봐서는 알 수 없게 된다</b> - 실제로 엔딩 대사의 진짜 값이 씬 오버라이드에
/// 숨어 프리팹의 "플레이스홀더텍스트0"을 가리고 있었다.
///
/// ⚠️ <b>지연 로드다</b>(<see cref="EnsureLoaded"/>). 이 프로젝트엔 스크립트 실행 순서 설정이
/// 없어서 <c>RuntimeInitializeOnLoadMethod</c>끼리의 순서를 기댈 수 없다(<see cref="CardDatabase"/>와 같은 이유).
/// </summary>
public static class DialogueDatabase
{
    private const string ResourcePath = "DialogueLocalization";

    private static readonly Dictionary<string, DialogueEntry> _byId = new Dictionary<string, DialogueEntry>();
    private static readonly string[] _empty = new string[0];

    private static bool _loaded;

    /// <summary>
    /// 지금 언어의 대사 줄들. 그 언어 칸이 비어 있으면 <b>영어 → 한국어</b> 순으로 넘어간다.
    ///
    /// 없는 id면 빈 배열을 주고 경고를 남긴다 - 대사가 0줄이면 이벤트가 그 자리에서 끝나므로
    /// 진행이 막히지는 않지만, 조용히 넘어가면 "왜 대사가 안 나오지"를 추적할 수 없다.
    /// </summary>
    public static string[] Lines(string id)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out var entry))
        {
            Debug.LogWarning($"DialogueDatabase: '{id}' 대사가 {ResourcePath}.json에 없습니다. " +
                             "id 오타이거나 항목을 추가하지 않은 것입니다.");
            return _empty;
        }

        var lines = ForCurrentLanguage(entry);

        // ⚠️ 빈 배열도 정상 설정일 수 있다("대사 없음") - 그때는 폴백하지 않고 그대로 0줄을
        // 돌려줘야 한다. 폴백은 "이 언어 칸을 아직 안 채웠다"일 때만 의미가 있는데, 그건
        // 아래 로드 시점 검사가 이미 에러로 잡는다.
        return lines ?? _empty;
    }

    /// <summary>한 줄만 필요한 곳(보스 인사 등)에서 쓴다. 범위를 벗어나면 빈 문자열.</summary>
    public static string Line(string id, int index = 0)
    {
        var lines = Lines(id);
        return index >= 0 && index < lines.Length ? lines[index] : string.Empty;
    }

    /// <summary>그 id의 줄 수. 지금 언어 기준이며, 위 검사 덕분에 모든 언어가 같은 값이다.</summary>
    public static int Count(string id) => Lines(id).Length;

    private static string[] ForCurrentLanguage(DialogueEntry entry)
    {
        switch (LanguageSettings.Current)
        {
            case GameLanguage.Korean:   return entry.ko;
            case GameLanguage.English:  return Fallback(entry.en, entry);
            case GameLanguage.French:   return Fallback(entry.fr, entry);
            case GameLanguage.Spanish:  return Fallback(entry.es, entry);
            case GameLanguage.Japanese: return Fallback(entry.ja, entry);
        }

        return entry.ko;
    }

    // 그 언어 칸이 아예 없을 때(null) 영어 -> 한국어로 넘어간다. null과 빈 배열은 뜻이 다르다 -
    // 빈 배열은 "대사 없음"이라는 정상 설정이고, null은 "칸을 안 만들었다"이다.
    private static string[] Fallback(string[] preferred, DialogueEntry entry)
    {
        if (preferred != null)
            return preferred;

        return entry.en ?? entry.ko;
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
            Debug.LogError($"DialogueDatabase: Resources/{ResourcePath}.json을 찾을 수 없어 대사가 " +
                           "하나도 나오지 않습니다.");
            return;
        }

        var parsed = JsonUtility.FromJson<DialogueList>(json.text);
        if (parsed?.dialogues == null)
        {
            Debug.LogError($"DialogueDatabase: {ResourcePath}.json을 읽지 못했습니다. " +
                           "최상위가 {\"dialogues\": [...]} 형태인지 확인하세요.");
            return;
        }

        for (var i = 0; i < parsed.dialogues.Count; i++)
        {
            var entry = parsed.dialogues[i];

            if (entry == null || string.IsNullOrEmpty(entry.id))
            {
                Debug.LogError($"DialogueDatabase: {ResourcePath}.json의 {i}번째 항목에 id가 없어 건너뜁니다.");
                continue;
            }

            if (_byId.ContainsKey(entry.id))
            {
                Debug.LogError($"DialogueDatabase: id '{entry.id}'가 {ResourcePath}.json에 두 번 있습니다. " +
                               "뒤엣것을 무시합니다.");
                continue;
            }

            WarnIfLineCountsDiffer(entry);
            _byId[entry.id] = entry;
        }
    }

    /// <summary>
    /// ⭐ 언어별 줄 수가 다르면 에러를 낸다. <b>이 프로젝트에서 실제로 겪은 회귀를 막는 장치다.</b>
    ///
    /// 대사는 스페이스로 한 줄씩 넘기는데(<see cref="EventManager.HandleAdvance"/>), 언어마다
    /// 줄 수가 다르면 <b>그 언어에서만 넘기는 횟수가 어긋나 마지막 줄에서 이벤트가 끝나지 않는다.</b>
    /// 그러면 <c>ShowResult</c>가 불리지 않아 <b>결과 화면도 클리어 보상도 통째로 나오지 않는다</b> -
    /// 화면이 잠긴 것처럼 보이는데 원인이 번역 파일이라 추적이 매우 어렵다.
    ///
    /// null(칸 자체가 없음)은 폴백이 처리하므로 검사에서 제외하고, 채워진 칸끼리만 비교한다.
    /// </summary>
    private static void WarnIfLineCountsDiffer(DialogueEntry entry)
    {
        var expected = -1;
        string expectedLang = null;

        var langs = new[] { "ko", "en", "fr", "es", "ja" };
        var arrays = new[] { entry.ko, entry.en, entry.fr, entry.es, entry.ja };

        for (var i = 0; i < arrays.Length; i++)
        {
            if (arrays[i] == null)
                continue;

            if (expected < 0)
            {
                expected = arrays[i].Length;
                expectedLang = langs[i];
                continue;
            }

            if (arrays[i].Length != expected)
            {
                Debug.LogError($"DialogueDatabase: '{entry.id}'의 줄 수가 언어마다 다릅니다 " +
                               $"({expectedLang} {expected}줄, {langs[i]} {arrays[i].Length}줄). " +
                               "그 언어에서만 대사를 넘기는 횟수가 어긋나 이벤트가 끝나지 않고, " +
                               "결과 화면과 클리어 보상이 나오지 않습니다.");
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>JSON을 고친 뒤 에디터를 다시 켜지 않고도 반영되게 한다.
    /// static 필드는 도메인 리로드를 끄면 Play 사이에도 남는다(<see cref="CardDatabase"/>와 같은 이유).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reload()
    {
        _byId.Clear();
        _loaded = false;
    }
#endif
}
