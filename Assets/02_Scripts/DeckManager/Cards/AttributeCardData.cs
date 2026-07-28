using UnityEngine;

// 파이어/일렉트릭/아이스 -> StatusChanceSingle, 컬러풀 -> StatusChanceAll,
// 페인풀 -> Bleed, 드레인 -> LifeDrain, 데빌 -> DamageReduction,
// 인텔리 -> CritMultiplier, 더블/트리플 -> RepeatAction
public enum AttributeEffectType
{
    StatusChanceSingle,
    StatusChanceAll,
    Bleed,
    LifeDrain,
    DamageReduction,
    CritMultiplier,
    RepeatAction
}

public enum StatusEffectType
{
    None,
    Burn,
    Paralysis,
    Freeze
}

[CreateAssetMenu(fileName = "New Attribute Card", menuName = "Deck Manager/Cards/Attribute Card")]
public class AttributeCardData : CardBase
{
    [SerializeField] private AttributeEffectType effectType;
    [SerializeField] private StatusEffectType statusEffect;
    [SerializeField] private float chancePercent;
    [SerializeField] private float value;

    public AttributeEffectType EffectType => effectType;
    public StatusEffectType StatusEffect => statusEffect;
    public float ChancePercent => chancePercent;
    public float Value => value;

    public override CardCategory Category => CardCategory.Attribute;
}
