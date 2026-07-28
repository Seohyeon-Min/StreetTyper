using System.Collections.Generic;
using UnityEngine;

public class ActionCalculator : MonoBehaviour
{
    public ResolvedAction CalculatePlayerAction(IReadOnlyList<CardBase> buffer, CharacterStats playerStats)
    {
        ResolvedAction result = new ResolvedAction();

        int currentPower = playerStats.power;

        foreach (var card in buffer)
        {
            if (card is ModifierCardData modCard)
            {
                if (modCard.EffectType == ModifierEffectType.StatBonus)
                {
                    currentPower += (int)modCard.Value;
                }
            }
            else if (card is ActionCardData actionCard)
            {
                if (actionCard.ActionKind == ActionKind.Attack)
                {
                    result.Damage += (currentPower + actionCard.StrengthBonus);
                    result.IgnoresDefense = actionCard.IgnoresDefense;
                    result.BreaksEnemyDefense = actionCard.BreaksEnemyDefense;
                }
                else if (actionCard.ActionKind == ActionKind.Defense)
                {
                    result.Defense += (currentPower + actionCard.StrengthBonus);
                }
            }
            else if (card is AttributeCardData attrCard)
            {
                
            }
        }

        return result;
    }
}