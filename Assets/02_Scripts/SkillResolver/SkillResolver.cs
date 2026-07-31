using System.Collections.Generic;
using UnityEngine;

// 체인에 담긴 단어 값 + 시전자의 힘만으로 ResolvedAction을 계산한다.
// 대상의 방어도나 상태이상은 여기서 모른다 - 그 상호작용은 CombatManager가 담당한다.
public class SkillResolver : MonoBehaviour
{
    [Header("디버그")]
    [Tooltip("켜면 상태이상 확률 판정을 건너뛰고 항상 부여한다. 파이어/일렉트릭/아이스가 " +
             "20%라 동작 확인이 어려울 때만 켜고, 밸런스 확인 전에 반드시 끌 것.")]
    [SerializeField] private bool alwaysApplyStatusEffect;

    // 어썸(ScalingStatBonus)의 n - "이번 게임에서 어썸을 성공한 횟수".
    // 런 단위 상태라 StageManager가 런 시작에 ResetRun()으로 되돌린다.
    private int _awesomeSuccessCount;

    /// <summary>런(게임 한 판)이 새로 시작될 때 호출한다. 어썸 누적 횟수를 0으로 되돌린다.</summary>
    public void ResetRun()
    {
        _awesomeSuccessCount = 0;
    }

    public ResolvedAction Resolve(IReadOnlyList<WordInstance> chain, int casterPower)
    {
        // WordChainManager가 마지막 단어는 항상 Action임을 보장한다.
        var actionCard = (ActionCardData)chain[chain.Count - 1].Card;

        // 파워는 타이핑 순서와 무관하게 체인 안의 모든 수치상승 단어에 똑같이 적용되어야 하므로
        // (Super Power Punch == Power Super Punch) 다른 값을 계산하기 전에 개수부터 센다.
        var powerCount = 0;
        foreach (var word in chain)
        {
            if (word.Card is ModifierCardData m && m.EffectType == ModifierEffectType.AmplifyNextStatBonus)
                powerCount++;
        }

        // 힘을 먼저 깔고 시작해야 GDD의 "펀치 = 힘+3"이 성립하고,
        // 뒤에서 곱하는 타격 횟수(트리플 등)도 (힘+보너스) 전체에 걸린다.
        var totalValue = casterPower + actionCard.StrengthBonus;
        var timerChange = (float)actionCard.TimerDelta;
        var hitMultiplier = 1;
        var lifeStealRate = 0f;
        var damageReduction = 0f;
        var criticalChancePercent = 0f;
        var criticalMultiplier = 1f;
        var lootBonusOnKill = false;
        var statusEffect = StatusEffectType.None;
        var usedAwesome = false;

        foreach (var word in chain)
        {
            if (word.Card is ModifierCardData modifier)
            {
                switch (modifier.EffectType)
                {
                    case ModifierEffectType.StatBonus:
                        totalValue += Mathf.RoundToInt(modifier.Value) + powerCount;
                        break;
                    case ModifierEffectType.ScalingStatBonus:
                        // 이번 조합은 아직 세지 않은 값이라 첫 어썸은 보너스가 0이고, 쓸수록 커진다.
                        // powerCount를 더하는 건 다른 수치 상승 단어와 같은 규칙(파워가 효과를 +1)이다.
                        totalValue += _awesomeSuccessCount + powerCount;
                        usedAwesome = true;
                        break;
                    case ModifierEffectType.TimerBonus:
                        timerChange += modifier.Value;
                        break;
                    case ModifierEffectType.LootBonusOnKill:
                        lootBonusOnKill = true;
                        break;
                    // AmplifyNextStatBonus(파워)는 위에서 powerCount로 이미 반영했다.
                }
            }
            else if (word.Card is AttributeCardData attribute)
            {
                switch (attribute.EffectType)
                {
                    case AttributeEffectType.LifeDrain:
                        lifeStealRate = attribute.Value;
                        break;
                    case AttributeEffectType.DamageReduction:
                        damageReduction = attribute.Value;
                        break;
                    case AttributeEffectType.CritMultiplier:
                        criticalChancePercent = attribute.ChancePercent;
                        criticalMultiplier = attribute.Value;
                        break;
                    case AttributeEffectType.RepeatAction:
                        hitMultiplier = Mathf.RoundToInt(attribute.Value);
                        break;
                    case AttributeEffectType.StatusChanceSingle:
                        if (RollStatusChance(attribute.ChancePercent))
                            statusEffect = attribute.StatusEffect;
                        break;
                    case AttributeEffectType.StatusChanceAll:
                        // StatusEffectType이 값 하나뿐이라 셋을 동시에 못 담는다 - GDD의
                        // "화상 > 마비 > 얼음" 우선순위를 "하나만 나타난다면 화상"으로 단순화했다.
                        if (RollStatusChance(attribute.ChancePercent))
                            statusEffect = StatusEffectType.Burn;
                        break;
                }
            }
        }

        var hitCount = hitMultiplier * RollComboHits(actionCard);
        totalValue *= hitCount;

        if (Random.Range(0f, 100f) < criticalChancePercent)
            totalValue = Mathf.RoundToInt(totalValue * criticalMultiplier);

        var result = new ResolvedAction
        {
            IgnoresDefense = actionCard.IgnoresDefense,
            BreaksEnemyDefense = actionCard.BreaksEnemyDefense,
            StatusEffect = statusEffect,
            DamageReduction = damageReduction,
            TimerChange = timerChange,
            LootBonusOnKill = lootBonusOnKill
        };

        if (actionCard.ActionKind == ActionKind.Attack)
            result.Damage = totalValue;
        else
            result.Defense = totalValue;

        var baseForHeal = actionCard.ActionKind == ActionKind.Attack ? result.Damage : result.Defense;
        result.Heal = Mathf.RoundToInt(lifeStealRate / 100f * baseForHeal);

        // 계산이 다 끝난 뒤에 센다 - 이번 조합의 보너스에는 반영되지 않아야
        // "이전에 성공한 횟수"라는 규칙이 성립한다.
        if (usedAwesome)
            _awesomeSuccessCount++;

        return result;
    }

    // 파이어/일렉트릭/아이스가 20%라 동작 확인이 어렵다. 디버그 토글이 켜져 있으면
    // 판정을 건너뛰고 항상 성공시킨다 - 밸런스를 볼 때는 반드시 꺼야 한다.
    private bool RollStatusChance(float chancePercent)
    {
        if (alwaysApplyStatusEffect)
            return true;

        return Random.Range(0f, 100f) < chancePercent;
    }

    // 뎀프시롤처럼 콤보 확률형 액션의 타격 횟수를 굴린다. 콤보가 아니면 1회.
    private static int RollComboHits(ActionCardData actionCard)
    {
        if (!actionCard.IsComboAttack)
            return 1;

        var hits = actionCard.ComboMinHits;
        while (hits < actionCard.ComboMaxHits && Random.Range(0f, 100f) < actionCard.ComboChancePercent)
            hits++;

        return hits;
    }
}
