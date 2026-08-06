using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    Korean,
    English,
    French,
    Spanish,
    Japanese
}

[Serializable]
public class CardTranslation
{
    public string id;
    public string koName, koDesc, koLabel;
    public string enName, enDesc, enLabel;
    public string frName, frDesc, frLabel;
    public string esName, esDesc, esLabel;
    public string jaName, jaDesc, jaLabel;
}

[Serializable]
public class CardLocalizationData
{
    public List<CardTranslation> cards;
}

/// <summary>
/// 게임 표시 언어. 값 하나와 PlayerPrefs가 전부라 MonoBehaviour가 필요 없어 GameScenes처럼
/// static 클래스로 둔다 - 씬 오브젝트도 DontDestroyOnLoad도 없으니 인스펙터 배선이 늘지 않는다.
///
/// 전환은 타이틀에서만 해야 한다. CardName이 곧 타이핑 매칭 키라서, 런 도중에 바꾸면 사전과
/// 손패·체인·대기 중인 공격이 전부 바뀐 언어의 이름을 갖게 된다. 이 게임엔 저장이 없어
/// 타이틀로 돌아가면 런이 초기화되므로(StageManager.Start -> GrantStartingWords) 타이틀에서만
/// 바꾸는 한 마이그레이션이 필요 없다.
/// </summary>
public static class LanguageSettings
{
    // 기존 볼륨 설정과 같은 계열의 키를 쓴다(option.volume.master 등).
    private const string PrefsKey = "option.language";

    private static GameLanguage _current = GameLanguage.Korean;

    /// <summary>지금 언어. 타이핑 매칭 경로에서 글자마다 불리므로 PlayerPrefs를 매번 읽지 않고
    /// 이 필드에 캐시해 둔다.</summary>
    public static GameLanguage Current => _current;

    public static bool IsEnglish => _current == GameLanguage.English;

    /// <summary>언어가 바뀐 순간 화면에 붙은 라벨들이 스스로 갱신하도록 알린다.</summary>
    public static event Action OnChanged;

    // [추가] 번역 데이터를 담아둘 딕셔너리
    private static Dictionary<string, CardTranslation> _cardDict = new Dictionary<string, CardTranslation>();

    // 도메인 리로드 설정과 무관하게 실행마다 한 번은 확실히 읽어 오게 한다.
    // static 필드는 Play를 멈춰도 남을 수 있어서 초기화 지점을 명시해 두는 편이 안전하다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        _current = (GameLanguage)PlayerPrefs.GetInt(PrefsKey, (int)GameLanguage.Korean);
        _warnedFields.Clear();

        // [추가] 게임 시작 시 Resources 폴더에서 JSON 로드
        LoadJsonData();
    }

    private static void LoadJsonData()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("CardLocalization");
        if (jsonAsset != null)
        {
            CardLocalizationData data = JsonUtility.FromJson<CardLocalizationData>(jsonAsset.text);
            foreach (var card in data.cards)
            {
                _cardDict[card.id] = card;
            }
        }
        else
        {
            Debug.LogError("CardLocalization.json 파일을 Resources 폴더에서 찾을 수 없습니다.");
        }
    }

    /// <summary>언어를 바꾸고 디스크에 저장한다. 값이 그대로면 아무 일도 하지 않는다.</summary>
    public static void Set(GameLanguage value)
    {
        if (_current == value)
            return;

        _current = value;
        PlayerPrefs.SetInt(PrefsKey, (int)value);
        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }

    // 방향키 입력에 맞춰 양방향으로 언어를 순환시키는 함수
    public static void ChangeLanguage(int direction)
    {
        int langCount = 5; // 언어 개수 (한국어, 영어, 프랑스어, 스페인어, 일어)
        int nextLang = ((int)_current + direction) % langCount;

        // C#의 % 연산자는 음수일 때 음수를 반환하므로 양수로 보정해줍니다.
        if (nextLang < 0)
            nextLang += langCount;

        Set((GameLanguage)nextLang);
    }

    // 같은 빈 칸을 몇 번이나 경고하지 않는다. Pick은 타이핑 매칭 경로에서 글자마다 불려서
    // 그대로 두면 콘솔이 폭주한다(SoundManager가 버스 경고를 경로별로 한 번만 남기는 것과 같은 이유).
    private static readonly HashSet<string> _warnedFields = new HashSet<string>();

    /// <summary>
    /// 지금 언어에 맞는 문자열을 고른다. 영어 자리가 비어 있으면 한국어로 넘어가되 조용히 넘어가지
    /// 않는다 - 영어 모드는 라틴 문자만 받으므로, 한글 이름이 그대로 손패에 뜨면 그 카드는
    /// 영영 칠 수 없어 턴이 잠긴다. 화면에 나오기 전에 알아야 하는 종류의 누락이다.
    ///
    /// owner/fieldName은 경고를 낼 때만 문자열로 조립한다 - 호출부에서 보간하면 한국어 모드에서도
    /// 글자마다 문자열이 새로 만들어진다.
    /// </summary>
    public static string Pick(string korean, string english, UnityEngine.Object owner = null, string fieldName = null)
    {
        if (_current == GameLanguage.Korean)
            return korean;

        if (!string.IsNullOrEmpty(english))
            return english;

        WarnOnce(owner, fieldName);
        return korean;
    }

    private static void WarnOnce(UnityEngine.Object owner, string fieldName)
    {
        var key = $"{(owner != null ? owner.name : "?")}.{fieldName}";
        if (!_warnedFields.Add(key))
            return;

        Debug.LogWarning($"LanguageSettings: '{key}'의 영문 값이 비어 있어 한국어로 대체합니다. " +
                         "영어 모드에서는 한글을 입력할 수 없으니, 타이핑 대상이라면 그 카드는 " +
                         "손패에 떠도 영영 칠 수 없습니다.", owner);
    }

    public static string PickCardText(string cardId, string textType)
    {
        if (!_cardDict.TryGetValue(cardId, out var trans))
        {
            return "MISSING_ID"; // JSON에 해당 카드가 없을 때
        }

        switch (_current)
        {
            case GameLanguage.Korean:
                if (textType == "name") return trans.koName;
                if (textType == "desc") return trans.koDesc;
                if (textType == "label") return trans.koLabel;
                break;
            case GameLanguage.English:
                if (textType == "name") return UpperName(trans.enName);
                if (textType == "desc") return trans.enDesc;
                if (textType == "label") return trans.enLabel;
                break;
            case GameLanguage.French:
                // [핵심] 프랑스어일 때 '이름(타이핑 타겟)'은 무조건 영어를 반환!
                if (textType == "name") return UpperName(trans.enName);
                if (textType == "desc") return trans.frDesc;
                if (textType == "label") return trans.frLabel;
                break;
            case GameLanguage.Spanish:
                // [핵심] 스페인어일 때 '이름(타이핑 타겟)'은 무조건 영어를 반환!
                if (textType == "name") return UpperName(trans.enName);
                if (textType == "desc") return trans.esDesc;
                if (textType == "label") return trans.esLabel;
                break;
            case GameLanguage.Japanese:
                // [핵심] 일본어일 때도 '이름(타이핑 타겟)'은 무조건 영어를 반환!
                if (textType == "name") return UpperName(trans.enName);
                if (textType == "desc") return trans.jaDesc;
                if (textType == "label") return trans.jaLabel;
                break;
        }
        return "";
    }

    /// <summary>영어 카드 이름을 대문자로 바꾼다. 이름은 <b>표시 문자열이자 타이핑 매칭 키</b>라,
    /// 이 변환은 <see cref="InputManager.HandleTextInput"/>의 대문자 정규화와 <b>반드시 짝을
    /// 이뤄야 한다</b> - 매칭 비교가 전부 Ordinal(대소문자 구분)이라 한쪽만 바꾸면 손패·보상·
    /// 명령 카드가 통째로 안 맞는다.
    ///
    /// ToUpper가 아니라 ToUpperInvariant인 건 터키어 로케일에서 i가 İ로 바뀌는 걸 피하기
    /// 위해서다(입력 쪽도 같은 이유로 Invariant를 쓴다). 한국어 이름에는 적용하지 않는다.</summary>
    private static string UpperName(string name) =>
        string.IsNullOrEmpty(name) ? name : name.ToUpperInvariant();
}
