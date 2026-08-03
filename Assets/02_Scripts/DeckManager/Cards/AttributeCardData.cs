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

    /// <summary>확률과 수치를 설명에 그대로 띄운다 - {0}에 chancePercent, {1}에 value가 들어간다.
    /// 글자만 고쳐두면 인스펙터에서 확률을 바꿨을 때 설명이 옛 숫자로 남는데, 카드에 적힌 숫자와
    /// 실제 동작이 어긋나는 건 곧 버그로 보인다(SkillResolver.ScalingBonus가 계산과 표시에 같은
    /// 함수를 쓰는 것과 같은 이유다). 포맷 자리가 없으면 적힌 그대로 나온다.</summary>
    public override string Description => Fill(RawDescription, chancePercent, value);

    // 더블/트리플(RepeatAction) -> Time, 파이어/일렉트릭/아이스/컬러풀(Status*) -> Type,
    // 드레인/데빌/인텔리 -> Modifier. Bleed(페인풀)는 단어 목록에서 빠졌지만
    // enum 인덱스가 밀리면 기존 카드 에셋(예: Intelli의 effectType 5)이 조용히 어긋나므로 값은 유지.
    public override CardCategory Category => effectType switch
    {
        AttributeEffectType.RepeatAction => CardCategory.Time,
        AttributeEffectType.StatusChanceSingle => CardCategory.Type,
        AttributeEffectType.StatusChanceAll => CardCategory.Type,
        AttributeEffectType.Bleed => CardCategory.Type,
        _ => CardCategory.Modifier
    };
}
