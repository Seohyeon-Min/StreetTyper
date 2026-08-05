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