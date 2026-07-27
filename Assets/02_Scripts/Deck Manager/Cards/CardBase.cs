using UnityEngine;

public enum CardCategory
{
    Modifier,
    Attribute,
    Action
}

public abstract class CardBase : ScriptableObject
{
    [SerializeField] private string cardName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea] private string description;

    public string CardName => cardName;
    public Sprite Icon => icon;
    public string Description => description;

    public abstract CardCategory Category { get; }
}
