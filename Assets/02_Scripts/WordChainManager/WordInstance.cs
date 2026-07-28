using System;
using UnityEngine;

// SkillResolver가 붙기 전까지는 CardBase를 감싸는 얇은 런타임 래퍼일 뿐이다.
// 강화 레벨/사용 횟수/영구 보너스는 사전(DeckManager)이 생기면 그쪽에서 채운다 - 지금은 기본값 그대로 둔다.
[Serializable]
public class WordInstance
{
    [SerializeField] private CardBase card;

    public int UpgradeLevel;
    public int UseCount;
    public int PermanentValueBonus;

    public CardBase Card => card;
    public CardCategory Category => card.Category;
    public string WordName => card.CardName;

    public WordInstance(CardBase card)
    {
        this.card = card;
    }
}
