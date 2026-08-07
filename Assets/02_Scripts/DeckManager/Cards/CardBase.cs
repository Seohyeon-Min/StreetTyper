using System;

public enum CardCategory
{
    Modifier,
    Time,
    Type,
    Action,
    Command
}

public enum TurnScalingSource
{
    None,

    /// <summary>이번 턴에 <b>실제로 흘러간</b> 초 = 자연 감소 + Ctrl 소각 + 훅 감소 − 잽/퀵 증가.
    /// 타이머의 남은 시간에서 바로 계산한다(<see cref="SkillResolver.SecondsSpentThisTurn"/>) -
    /// 예전에는 카드 효과로 변한 초만 세서 가만히 있으면 오르지 않았다.</summary>
    SecondsSpentThisTurn,

    ActionsThisTurn,
    ModifiersThisTurn,
}

/// <summary>
/// 카드 한 장. <b>ScriptableObject가 아니라 평범한 C# 객체</b>이고,
/// <see cref="CardDatabase"/>가 <c>CardLocalization.json</c>의 한 행으로 만들어 준다.
///
/// ⚠️ <b>에셋으로 되돌리지 말 것.</b> 예전에는 카드 한 장이 <c>.asset</c>(수치) + JSON(문구)
/// 두 곳에 나뉘어 있었고, 그래서 어퍼컷의 설명이 에셋엔 "15의 피해" JSON엔 "4의 피해"로
/// 남아 있는 식의 어긋남이 생겼다. 시작 단어도 프리팹 기본값과 씬 인스턴스 오버라이드에
/// 따로 저장되어 실제 값이 무엇인지 파일만 봐서는 알 수 없었다. 지금은 카드 한 장이
/// JSON 한 행에만 존재한다.
///
/// 표시 문자열은 하나도 들고 있지 않다 - <see cref="CardId"/>로 그때그때 JSON에서 꺼내므로
/// 언어를 바꾸면 별도 갱신 없이 따라온다.
/// </summary>
public abstract class CardBase
{
    /// <summary>JSON 행의 id. 코드와 인스펙터가 카드를 가리키는 유일한 키다.</summary>
    public string CardId { get; }

    protected CardBase(CardDefinition definition)
    {
        CardId = definition.id;
    }

    /// <summary>표시 이름이자 <b>타이핑 매칭 키</b>. 한국어가 아닌 언어에서는 영어 대문자로
    /// 고정된다(<see cref="LanguageSettings.PickCardText"/>).</summary>
    public string CardName => LanguageSettings.PickCardText(CardId, "name");

    public virtual string Description => RawDescription;
    protected string RawDescription => LanguageSettings.PickCardText(CardId, "desc");
    public virtual string StatsLabel => LanguageSettings.PickCardText(CardId, "label");

    public abstract CardCategory Category { get; }

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
            return text;
        }
    }
}
