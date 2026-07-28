using UnityEngine;

// SkillResolver가 계산해둔 ResolvedAction을 받아 시전자와 대상 사이에 실제로 적용한다.
// 수치 계산은 하지 않는다 - 대상의 방어도/생사처럼 "상대가 있어야 알 수 있는 것"만 처리한다.
public class CombatManager : MonoBehaviour
{
    public void ExecutePlayerAction(ResolvedAction action, CharacterStats player, CharacterStats target)
    {
        if (action == null || player == null)
            return;

        if (target != null)
        {
            if (action.BreaksEnemyDefense)
            {
                target.defense = 0;
                Debug.Log($"{target.gameObject.name} defense broken");
            }

            if (action.Damage > 0)
                target.TakeDamage(action.Damage, action.IgnoresDefense);
        }

        if (action.Defense > 0)
            player.AddDefense(action.Defense);

        if (action.Heal > 0)
            player.Heal(action.Heal);

        LogPendingEffects(action);
    }

    // 아직 소비할 시스템이 없는 값들. SkillResolver는 이미 계산해서 넘겨주고 있으므로,
    // 각 시스템(StatusEffectManager/BattleRewardManager)이 생기면 여기에 연결하면 된다.
    // TimerChange는 여기 없다 - DeckManager가 TimerManager.AddTime으로 직접 소비한다.
    private static void LogPendingEffects(ResolvedAction action)
    {
        if (action.StatusEffect != StatusEffectType.None)
            Debug.Log($"[미구현] 상태이상 {action.StatusEffect} 부여");

        if (action.DamageReduction > 0f)
            Debug.Log($"[미구현] 받는 피해 {action.DamageReduction}% 감소");

        if (action.LootBonusOnKill)
            Debug.Log("[미구현] 처치 시 추가 보상");
    }
}
