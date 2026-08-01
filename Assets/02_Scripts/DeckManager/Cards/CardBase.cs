using UnityEngine;

public enum CardCategory
{
    Modifier,
    Time,
    Type,
    Action
}

public abstract class CardBase : ScriptableObject
{
    [SerializeField] private string cardName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea] private string description;

    [Tooltip("카드 위쪽에 크게 뜨는 한 줄 요약(위력·상태이상·배수 등). " +
             "표시 칸이 좁고 글자가 크므로 4자 이내로 적을 것 - 넘으면 칸 밖으로 삐져나옵니다.")]
    [SerializeField] private string statsLabel;

    public string CardName => cardName;
    public Sprite Icon => icon;
    public string Description => description;
    public string StatsLabel => statsLabel;

    public abstract CardCategory Category { get; }
}
