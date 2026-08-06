using UnityEngine;

// SkillResolver가 계산해둔 ResolvedAction을 받아 시전자와 대상 사이에 실제로 적용한다.
// 수치 계산은 하지 않는다 - 대상의 방어도/생사처럼 "상대가 있어야 알 수 있는 것"만 처리한다.
public class CombatManager : MonoBehaviour
{
    [Tooltip("상태이상(화상/마비/얼음)과 데빌을 실제로 거는 곳.")]
    [SerializeField] private StatusEffectManager statusEffectManager;

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
            {
                target.TakeDamage(action.Damage, action.IgnoresDefense);
                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.AddDamageDealt(action.Damage);
            }
        }

        if (action.Defense > 0)
        {
            player.AddDefense(action.Defense);

            PlayerBattleVisuals visuals = player.GetComponent<PlayerBattleVisuals>();
            if (visuals != null)
            {
                visuals.PlayGuardAnimation();
            }

            if (HealEffectManager.Instance != null)
                HealEffectManager.Instance.PlayGuardEffect(player.GetComponent<SpriteRenderer>());
        }

        if (action.Heal > 0)
        {
            player.Heal(action.Heal);

            if (HealEffectManager.Instance != null)
                HealEffectManager.Instance.PlayHealEffect(player.GetComponent<SpriteRenderer>());
        }

        ApplyStatusEffects(action, player, target);
    }

    // 상태이상은 대상의 스탯을 직접 건드리므로 피해/방어를 적용한 뒤에 건다.
    // 지속 감소와 화상 피해는 DeckManager가 적 턴 직후에 처리한다.
    private void ApplyStatusEffects(ResolvedAction action, CharacterStats player, CharacterStats target)
    {
        var hasStatus = action.StatusEffects != null && action.StatusEffects.Count > 0;

        if (statusEffectManager == null)
        {
            if (hasStatus || action.DamageReduction > 0f)
                Debug.LogWarning("CombatManager: statusEffectManager가 연결되지 않아 상태이상이 적용되지 않습니다.", this);
            return;
        }

        // 컬러풀은 화상·마비·얼음을 각각 굴려 걸린 것을 전부 담아 온다. 하나씩 거는 건
        // StatusEffectManager가 이미 할 줄 알고(_enemyEffects가 Dictionary라 여러 개를 동시에 든다),
        // 여기서는 담겨 온 만큼 반복해서 넘기기만 하면 된다.
        if (hasStatus && target != null)
        {
            for (var i = 0; i < action.StatusEffects.Count; i++)
                statusEffectManager.ApplyToEnemy(action.StatusEffects[i], target);
        }

        // 데빌은 적이 아니라 시전자(플레이어)가 받는 피해를 줄인다.
        if (action.DamageReduction > 0f)
            statusEffectManager.ApplyDevil(player);
    }

    // 여기서 소비하지 않는 값이 둘 있다.
    // - TimerChange: DeckManager가 체인 완성 시점에 TimerManager.AddTime으로 직접 쓴다.
    // - GrantsLootBonus: 보상 라운드를 쌓는 건 WordUnlockManager의 일이라 DeckManager.PlayPendingActions가
    //   하므로 거기서 WordUnlockManager.AddLuckyBonus를 부른다.
}
