using UnityEngine;

// 슈퍼/울트라/하이퍼/메가톤 -> StatBonus, 파워 -> AmplifyNextStatBonus,
// 어썸 -> ScalingStatBonus, 퀵 -> TimerBonus, 럭키 -> LootBonusOnKill
public enum ModifierEffectType
{
    StatBonus,
    AmplifyNextStatBonus,
    ScalingStatBonus,
    TimerBonus,
    LootBonusOnKill
}

[CreateAssetMenu(fileName = "New Modifier Card", menuName = "Deck Manager/Cards/Modifier Card")]
public class ModifierCardData : CardBase
{
    [SerializeField] private ModifierEffectType effectType;
    [SerializeField] private float value;

    public ModifierEffectType EffectType => effectType;
    public float Value => value;

    public override CardCategory Category => CardCategory.Modifier;
}
