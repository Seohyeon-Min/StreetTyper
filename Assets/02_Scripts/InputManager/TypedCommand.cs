using System;
using UnityEngine;

/// <summary>
/// 플레이어가 타이핑해서 실행하는 명령 단어 한 개와 그 안내 문구. 한국어/영어 두 벌을 한 묶음으로
/// 들고 있어서 인스펙터에서 단어와 안내가 항상 붙어 다닌다.
///
/// 안내 문구를 단어 옆에 두는 게 핵심이다. 예전에는 ResultInputHandler가 "...을 입력하세요"와
/// "...를 입력하세요"를 별도 필드로 들고 있었는데, 그건 순전히 받침에 따라 조사가 달라져서였다.
/// 안내가 단어에 딸려 있으면 그 문제가 자연스럽게 풀리고, 단어를 바꿨을 때 안내만 옛 상태로
/// 남는 일도 없어진다.
/// </summary>
[Serializable]
public class TypedCommand
{
    [Tooltip("한국어 모드에서 칠 명령 단어")]
    [SerializeField] private string korean;

    [Tooltip("영어 모드에서 칠 명령 단어. 대소문자는 아무렇게나 적어도 된다 - Word()가 대문자로 " +
             "맞춰준다(입력도 대문자로 정규화되어 들어온다).")]
    [SerializeField] private string english;

    [Tooltip("이 명령을 안내하는 한국어 문구. {0} 자리에 지금 언어의 명령 단어가 들어간다. " +
             "비워두면 이 명령은 안내에 나오지 않는다.")]
    [SerializeField, TextArea] private string hintKorean;

    [Tooltip("영어 안내 문구. 비워두면 한국어 안내로 대체된다.")]
    [SerializeField, TextArea] private string hintEnglish;

    /// <summary>Unity 역직렬화용. [Serializable] 클래스에 매개변수 없는 생성자가 없으면
    /// 인스펙터에서 인스턴스를 만들지 못한다 - 아래 생성자를 정의하는 순간 컴파일러가
    /// 기본 생성자를 자동으로 만들어주지 않으므로 여기 명시해야 한다.</summary>
    public TypedCommand() { }

    /// <summary>인스펙터에서 채우기 전의 기본값을 코드에 남겨두기 위한 생성자.
    /// 필드를 새로 추가한 직후에는 씬/프리팹 YAML에 값이 없어서, 이 기본값이 그대로 쓰인다.</summary>
    public TypedCommand(string korean, string english, string hintKorean, string hintEnglish)
    {
        this.korean = korean;
        this.english = english;
        this.hintKorean = hintKorean;
        this.hintEnglish = hintEnglish;
    }

    /// <summary>지금 언어로 쳐야 하는 단어. 타이핑 매칭 키다.
    ///
    /// ⚠️ 비한국어는 <b>대문자로 정규화</b>한다. 입력(<see cref="InputManager.HandleTextInput"/>)이
    /// 대문자로 들어오고 매칭 비교가 Ordinal이라, 인스펙터에 소문자로 적혀 있으면 영영 안 맞는다.
    /// 여기서 맞춰주면 씬/프리팹에 이미 저장된 옛 소문자 값도 그대로 동작한다
    /// (실제로 CardCollectionPanel의 "close"가 이 경로다 - 안 맞추면 영어 모드에서
    /// 보유 카드 목록을 타이핑으로 닫을 수 없다).</summary>
    public string Word(UnityEngine.Object owner, string fieldName)
    {
        var word = LanguageSettings.Pick(korean, english, owner, fieldName);

        if (LanguageSettings.Current == GameLanguage.Korean || string.IsNullOrEmpty(word))
            return word;

        return word.ToUpperInvariant();
    }

    /// <summary>지금 언어의 안내 문구. 포맷이 비어 있으면 빈 문자열을 돌려주므로
    /// 부르는 쪽이 그걸 걸러내면 이 명령만 안내에서 빠진다.</summary>
    public string Hint(UnityEngine.Object owner, string fieldName)
    {
        // 양쪽 다 비었으면 안내를 일부러 안 다는 경우다. Pick에 넘기면 번역 누락으로 보고
        // 경고를 내므로 여기서 먼저 걸러낸다.
        if (string.IsNullOrEmpty(hintKorean) && string.IsNullOrEmpty(hintEnglish))
            return string.Empty;

        var format = LanguageSettings.Pick(hintKorean, hintEnglish, owner, fieldName);
        if (string.IsNullOrEmpty(format))
            return string.Empty;

        return string.Format(format, Word(owner, fieldName));
    }
}
