// SkillResolver가 붙기 전까지는 CardBase를 감싸는 얇은 런타임 래퍼일 뿐이다.
// 강화 레벨/사용 횟수/영구 보너스는 사전(DeckManager)이 생기면 그쪽에서 채운다 - 지금은 기본값 그대로 둔다.
//
// ⚠️ 직렬화되지 않는다. CardBase가 ScriptableObject였을 때는 [Serializable] + [SerializeField]가
// 붙어 있었지만 실제로 이걸 인스펙터에 두는 곳은 없었고, 지금은 CardBase가 평범한 객체라
// 애초에 참조로 직렬화될 수 없다.
public class WordInstance
{
    private readonly CardBase card;

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
