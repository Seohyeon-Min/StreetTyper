using System;
using UnityEngine;

public enum CardCategory
{
    Modifier,
    Time,
    Type,
    Action,

    /// <summary>조합에 들어가지 않는 명령 단어(넘기기/지우기/계속/카드/타이틀).
    /// 카드 모양으로 보여주기만 하려고 만든 분류라 전용 프레임을 쓰고 배지가 없다.
    ///
    /// ⚠️ 이 값을 가진 카드는 <b>사전·해금 목록·조합에 들어가면 안 된다.</b> 들어가면 손패에 떠서
    /// 타이핑으로 소비되거나 체인에 무제한으로 쌓인다 - <see cref="WordDictionary"/>,
    /// <see cref="WordUnlockManager"/>, <see cref="WordChainManager"/> 세 곳이 각각 막고 있다.</summary>
    Command
}

/// <summary>
/// 카드의 값이 "이번 턴에 무엇을 얼마나 했는가"에 따라 변할 때 무엇을 셀지 고른다.
/// <b>액션 카드(니킥/춉/박치기)와 수식어 카드(퍼펙트)가 같이 쓴다</b> - 그래서 특정 카드 타입이
/// 아니라 여기(CardBase.cs)에 둔다.
///
/// ⚠️ 세는 값은 전부 <b>지금 완성하는 이 조합 이전까지</b>의 누적이다 - 자기 자신은 세지 않는다.
/// 어썸(<see cref="ModifierEffectType.ScalingStatBonus"/>)이 "이전에 성공한 횟수"만 세는 것과 같은
/// 규칙이고, 그래서 이런 카드들은 턴 후반에 쓸수록(또는 니킥처럼 앞에 쓸수록) 값이 달라진다.
///
/// ⚠️ <see cref="AttributeEffectType"/>과 마찬가지로 <b>순서를 바꾸지 말 것</b> - 직렬화되는 건
/// 인덱스라서, 중간에 값을 끼워 넣으면 기존 에셋이 조용히 다른 항목을 가리키게 된다.
/// </summary>
public enum TurnScalingSource
{
    /// <summary>스케일링 없음. 기존 카드는 전부 여기다.</summary>
    None,

    /// <summary>이번 턴에 단어 효과로 <b>줄어든</b> 초(퍼펙트). 훅/어퍼컷처럼 시간을 깎았으면
    /// 양수가 되어 값이 오르고, 잽/퀵으로 늘렸으면 음수가 되어 깎인다.
    /// ⚠️ 세는 건 <b>효과로 변한 초</b>뿐이다 - 자연 감소는 포함하지 않는다(그것까지 세면
    /// 늘어나는 경우가 없어져 "늘어난 초마다 감소"라는 규칙 자체가 성립하지 않는다).</summary>
    SecondsSpentThisTurn,

    /// <summary>이번 턴에 완성한 조합(액션)의 수. 춉은 양수로 올리고 니킥은 음수로 깎는다.</summary>
    ActionsThisTurn,

    /// <summary>이번 턴에 쓴 수식어(<see cref="CardCategory.Modifier"/>) 카드의 수(박치기).</summary>
    ModifiersThisTurn,
}

// 표시 문자열은 한국어/영어 한 쌍씩 들고 있고 프로퍼티에서 고른다. CardName 소비처가
// 열다섯 곳인데 전부 프로퍼티를 거치므로, 여기서 고르면 매칭·표시·체인·로그가 한 번에 따라온다.
public abstract class CardBase : ScriptableObject
{
    [Header("한국어")]
    [SerializeField] private string cardName;

    [SerializeField, TextArea] private string description;

    [Tooltip("카드 위쪽에 크게 뜨는 한 줄 요약(위력·상태이상·배수 등). " +
             "표시 칸이 좁고 글자가 크므로 4자 이내로 적을 것 - 넘으면 칸 밖으로 삐져나옵니다. " +
             "어썸처럼 값이 런 도중 변하는 카드는 여기에 {0}을 넣어 포맷으로 씁니다(ModifierCardData 참조).")]
    [SerializeField] private string statsLabel;

    [Header("영어")]
    [Tooltip("영어 모드에서 타이핑할 단어. 소문자로 적을 것 - 입력이 소문자로 정규화되어 들어옵니다. " +
             "공백을 넣지 말 것(띄어쓰기 없이 이어 치는 게 규칙입니다). " +
             "다른 카드 이름의 접두사가 되면 그 카드는 영영 입력할 수 없습니다.")]
    [SerializeField] private string cardNameEn;

    [SerializeField, TextArea] private string descriptionEn;

    [SerializeField] private string statsLabelEn;

    [Header("공통")]
    [SerializeField] private Sprite icon;

    public string CardName => LanguageSettings.Pick(cardName, cardNameEn, this, "cardNameEn");

    /// <summary>카드 하단 설명. 확률처럼 인스펙터 값에서 나와야 하는 수치가 들어가는 카드는
    /// 하위 클래스가 재정의해 포맷 자리에 실제 값을 끼워 넣는다 - <see cref="AttributeCardData"/>와
    /// <see cref="ActionCardData"/>가 그렇게 한다. <see cref="StatsLabel"/>과 같은 이유이고
    /// 같은 방식이다(설명에 적힌 숫자와 실제 동작이 어긋나면 그게 곧 버그로 보인다).</summary>
    public virtual string Description => RawDescription;

    /// <summary>포맷을 적용하기 전의 인스펙터 원문. 하위 클래스가 재정의할 때 쓴다.</summary>
    protected string RawDescription => LanguageSettings.Pick(description, descriptionEn, this, "descriptionEn");
    /// <summary>카드 위쪽 큰 글씨 요약. 값이 런 도중 변하는 카드(어썸)는 하위 클래스가
    /// 재정의해 지금 수치를 끼워 넣는다 - <see cref="ModifierCardData.StatsLabel"/> 참조.</summary>
    public virtual string StatsLabel => LanguageSettings.Pick(statsLabel, statsLabelEn, this, "statsLabelEn");

    public Sprite Icon => icon;

    public abstract CardCategory Category { get; }

    /// <summary>포맷 자리가 있을 때만 <paramref name="args"/>를 끼워 넣고, 없으면 적힌 그대로 돌려준다.
    /// 포맷을 안 넣은 카드에서 <c>string.Format</c>이 예외를 내거나 글자가 통째로 사라지는 일을 막는
    /// 폴백이며, 인자가 모자란 경우까지 같이 막는다(문구를 고치다 {1}만 남기는 실수가 잦다).</summary>
    protected static string Fill(string text, params object[] args)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
            return text;

        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            // 자리 번호가 인자 수를 넘었다. 카드가 통째로 비어 보이는 것보다 원문이 낫다.
            return text;
        }
    }
}
