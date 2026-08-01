using UnityEngine;

public class EnemyBase : CharacterStats
{
    [Header("Enemy Data Reference")]
    public EnemyData enemyData;
    public bool isMotherDragon = false;

    [Header("Visuals")]
    [SerializeField] private Animator animator;
    [Tooltip("말풍선(의도/화상 등)이 뜰 위치. 비워두면 오브젝트 자신의 위치를 쓴다. 캐릭터마다 크기가 달라 프리팹별로 지정할 수 있게 뺐다.")]
    [SerializeField] private Transform bubbleAnchor;

    public Vector3 BubblePosition => bubbleAnchor != null ? bubbleAnchor.position : transform.position;

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