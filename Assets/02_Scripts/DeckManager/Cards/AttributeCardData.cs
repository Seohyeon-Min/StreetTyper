// 파이어/일렉트릭/아이스 -> StatusChanceSingle, 컬러풀 -> StatusChanceAll,
// 페인풀 -> Bleed, 드레인 -> LifeDrain, 데빌 -> DamageReduction,
// 스마트 -> CritMultiplier, 더블/트리플 -> RepeatAction
//
// JSON에 이름으로 적히므로 순서를 바꿔도 안전하다 - 에셋에 인덱스로 직렬화되던 시절에는
// 중간 값 하나만 지워도 뒤에 있던 카드가 조용히 다른 효과가 됐다(CardDefinition 참조).
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

/// <summary>속성·특수효과 단어. 값은 <c>CardLocalization.json</c>의 한 행에서 온다.</summary>
public class AttributeCardData : CardBase
{
    private readonly AttributeEffectType effectType;
    private readonly StatusEffectType statusEffect;
    private readonly float chancePercent;
    private readonly float value;

    public AttributeCardData(CardDefinition definition) : base(definition)
    {
        effectType = CardDatabase.ParseEnum<AttributeEffectType>(definition.effectType, definition.id, nameof(definition.effectType));
        statusEffect = CardDatabase.ParseEnum<StatusEffectType>(definition.statusEffect, definition.id, nameof(definition.statusEffect));
        chancePercent = definition.chancePercent;
        value = definition.value;
    }

    public AttributeEffectType EffectType => effectType;
    public StatusEffectType StatusEffect => statusEffect;
    public float ChancePercent => chancePercent;
    public float Value => value;

    /// <summary>확률과 수치를 설명에 그대로 띄운다 - {0}에 chancePercent, {1}에 value가 들어간다.
    /// 글자만 고쳐두면 JSON에서 확률을 바꿨을 때 설명이 옛 숫자로 남는데, 카드에 적힌 숫자와
    /// 실제 동작이 어긋나는 건 곧 버그로 보인다(SkillResolver.ScalingBonus가 계산과 표시에 같은
    /// 함수를 쓰는 것과 같은 이유다). 포맷 자리가 없으면 적힌 그대로 나온다.</summary>
    public override string Description => Fill(RawDescription, chancePercent, value);

    // 더블/트리플(RepeatAction) -> Time, 파이어/일렉트릭/아이스/컬러풀(Status*) -> Type,
    // 드레인/데빌/스마트 -> Modifier.
    public override CardCategory Category => effectType switch
    {
        AttributeEffectType.RepeatAction => CardCategory.Time,
        AttributeEffectType.StatusChanceSingle => CardCategory.Type,
        AttributeEffectType.StatusChanceAll => CardCategory.Type,
        AttributeEffectType.Bleed => CardCategory.Type,
        _ => CardCategory.Modifier
    };
}
