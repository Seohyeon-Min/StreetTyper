using System.Collections.Generic;
using UnityEngine;

// 플레이어/적의 실제 상태(힘, 현재 방어도 등)는 전혀 모른 채, 체인에 담긴 단어 값만으로
// ResolvedAction을 계산한다. 실제 피해 적용은 미래의 Combat system이 담당한다.
public class SkillResolver : MonoBehaviour
{
    public ResolvedAction Resolve(IReadOnlyList<WordInstance> chain)
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

        var totalValue = actionCard.StrengthBonus;
        var timerChange = (float)actionCard.TimerDelta;
        var hitMultiplier = 1;
        var lifeStealRate = 0f;
        var damageReduction = 0f;
        var criticalChancePercent = 0f;
        var criticalMultiplier = 1f;
        var lootBonusOnKill = false;
        var statusEffect = StatusEffectType.None;

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
                        // TODO: 사전/런 시스템이 생기면 "이번 게임에서 어썸을 성공한 횟수"를 여기 더한다. 지금은 0.
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
                        if (Random.Range(0f, 100f) < attribute.ChancePercent)
                            statusEffect = attribute.StatusEffect;
                        break;
                    case AttributeEffectType.StatusChanceAll:
                        // StatusEffectType이 값 하나뿐이라 셋을 동시에 못 담는다 - GDD의
                        // "화상 > 마비 > 얼음" 우선순위를 "하나만 나타난다면 화상"으로 단순화했다.
                        if (Random.Range(0f, 100f) < attribute.ChancePercent)
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

        return result;
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
