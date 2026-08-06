using System;
using UnityEngine;

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

public abstract class CardBase : ScriptableObject
{
    [Header("카드 식별자 (JSON ID)")]
    [Tooltip("CardLocalization.json 파일에 적힌 id 값과 일치해야 합니다. (기존 영어 이름 유지)")]
    [SerializeField] private string cardNameEn;

    [Header("공통")]
    [SerializeField] private Sprite icon;

    // JSON과 매칭할 고유 ID
    public string CardId => cardNameEn;

    // JSON 파서를 통해 다국어 텍스트 반환
    public string CardName => LanguageSettings.PickCardText(CardId, "name");

    public virtual string Description => RawDescription;
    protected string RawDescription => LanguageSettings.PickCardText(CardId, "desc");
    public virtual string StatsLabel => LanguageSettings.PickCardText(CardId, "label");

    public Sprite Icon => icon;

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