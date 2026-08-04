using System.Collections;
using UnityEngine;

public class EnemyBase : CharacterStats
{
    [Header("Enemy Data Reference")]
    public EnemyData enemyData;
    public bool isMotherDragon = false;
    private bool isScaled = false;

    [Header("Visuals")]
    [SerializeField] private Animator animator;
    [Tooltip("말풍선(의도/화상 등)이 뜰 위치. 비워두면 오브젝트 자신의 위치를 쓴다. 캐릭터마다 크기가 달라 프리팹별로 지정할 수 있게 뺐다.")]
    [SerializeField] private Transform bubbleAnchor;
    [Tooltip("돌진 중 정렬 순서를 조절할 스프라이트. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("공격 돌진 이동 (PlayerBattleVisuals와 짝)")]
    [Tooltip("공격 시 플레이어 앞에서 얼마나 떨어져 멈출지")]
    [SerializeField] private float dashOffset = 1.5f;
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private AnimationCurve moveToPlayerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve moveToOriginCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("트랜지션 등장 애니메이션")]
    [Tooltip("화면 밖에서 등장할 때의 이동 비율 커브")]
    public AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ==========================================================
    // [추가] 화면 밖에서 원래 위치로 미끄러지듯 등장하는 코루틴
    public IEnumerator SlideInCoroutine(Vector3 startPos, Vector3 targetPos, float duration)
    {
        // 등장 시에도 스프라이트가 올바르게 보이도록 정렬 순서 보정 (선택 사항)
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = originalSortingOrder;

        float time = 0f;
        while (time < duration)
        {
            var t = slideInCurve.Evaluate(time / duration);
            transform.position = Vector3.LerpUnclamped(startPos, targetPos, t);
            time += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
    }
    // ==========================================================

    // 스폰 위치. Start()에서 한 번만 읽는다 - 적은 스테이지마다 새로 Instantiate되므로
    // 이 시점이 곧 "복귀할 자리"다(PlayerBattleVisuals.originalPosition과 같은 패턴).
    private Vector3 originalPosition;

    // 돌진 전 정렬 순서. 플레이어 앞에 서 있는 동안만 이보다 위로 올렸다가 복귀하면 되돌린다.
    private int originalSortingOrder;

    public Vector3 BubblePosition => bubbleAnchor != null ? bubbleAnchor.position : transform.position;

    // 플레이어 앞(dashOffset만큼 떨어진 자리)까지 돌진한다.
    public IEnumerator MoveToPlayerCoroutine(Transform playerTransform)
    {
        if (playerTransform == null) yield break;

        // 상대 앞까지 가는 쪽이 항상 위로 그려져야 한다 - 상대의 정렬 순서보다 +1로 맞춘다.
        var playerSprite = playerTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && playerSprite != null)
            spriteRenderer.sortingOrder = playerSprite.sortingOrder + 1;

        float time = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(
                    playerTransform.position.x + dashOffset,
                    originalPosition.y,
                    originalPosition.z
                );

        while (time < moveDuration)
        {
            var t = moveToPlayerCurve.Evaluate(time / moveDuration);
            transform.position = Vector3.LerpUnclamped(startPos, targetPos, t);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
    }

    // 원래 스폰 위치로 복귀한다.
    public IEnumerator MoveToOriginCoroutine()
    {
        float time = 0f;
        Vector3 startPos = transform.position;

        while (time < moveDuration)
        {
            var t = moveToOriginCurve.Evaluate(time / moveDuration);
            transform.position = Vector3.LerpUnclamped(startPos, originalPosition, t);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition;

        // 제자리로 돌아왔으니 정렬 순서도 원래대로.
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = originalSortingOrder;
    }

    public void ApplyScaling(int stageIndex)
    {
        if (enemyData != null)
        {
            float multiplier = 1f + (stageIndex * 0.2f); // 스테이지당 20% 증가
            maxHP = Mathf.RoundToInt(enemyData.maxHP * multiplier);
            currentHP = maxHP;
            power = Mathf.RoundToInt(enemyData.power * multiplier);

            // 새로 등장한 적은 방어도가 없는 상태에서 시작해야 한다. 적 방어도는 턴마다 비워주는
            // 곳이 없어서(플레이어 쪽만 DeckManager가 비운다) 한 번 남으면 계속 쌓인 채로 간다.
            defense = 0;

            isScaled = true;
        }
    }

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

    public void PlaySpeakAnimation()
    {
        if (animator == null) return;
        animator.SetTrigger("Speak");
    }

    protected override void Start()
    {
        animator = GetComponent<Animator>();
        originalPosition = transform.position;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalSortingOrder = spriteRenderer.sortingOrder;

        // 스케일링이 아직 적용되지 않은 경우에만 기본 데이터 적용
        if (!isScaled && enemyData != null)
        {
            maxHP = enemyData.maxHP;
            currentHP = maxHP;
            power = enemyData.power;
        }

        if (enemyData != null) gameObject.name = enemyData.enemyName;
    }
}