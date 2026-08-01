using UnityEngine;

public class EnemyBase : CharacterStats
{
    [Header("Enemy Data Reference")]
    public EnemyData enemyData;
    public bool isMotherDragon = false;

    [Header("Visuals")]
    [SerializeField] private Animator animator;

    // 공격 애니메이션 트리거 (Idle -> Attack 전이는 Animator Controller의 Exit Time으로 자동 복귀)
    public void PlayAttackAnimation()
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name}: animator가 연결되지 않았습니다.", this);
            return;
        }

        animator.SetTrigger("Attack");
    }

    protected override void Start()
    {
        if (enemyData != null)
        {
            maxHP = enemyData.maxHP;
            currentHP = maxHP;
            power = enemyData.power;
            gameObject.name = enemyData.enemyName;
        }
        else
        {
            base.Start();
        }
    }
}