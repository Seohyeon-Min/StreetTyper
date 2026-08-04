using System.Collections;
using UnityEngine;

public class PlayerBattleVisuals : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public EnemyManager enemyManager; // 현재 적의 위치를 찾기 위해 참조
    [Tooltip("돌진 중 정렬 순서를 조절할 스프라이트. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Settings")]
    public float dashOffset = 1.5f; // 적 앞에서 얼마나 떨어져서 멈출지

    [Header("이동 애니메이션 커브")]
    [Tooltip("적 앞으로 돌진할 때(공격 시작) 시간에 따른 이동 비율. x=0~1(경과 비율), y=0~1(이동 비율).")]
    public AnimationCurve moveToEnemyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("원래 자리로 복귀할 때(공격 종료) 시간에 따른 이동 비율.")]
    public AnimationCurve moveToOriginCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 originalPosition;

    // 돌진 전 정렬 순서. 적 앞에 서 있는 동안만 이보다 위로 올렸다가 복귀하면 되돌린다.
    private int originalSortingOrder;

    void Start()
    {
        originalPosition = transform.position;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalSortingOrder = spriteRenderer.sortingOrder;
    }

    // 적 앞으로 이동하는 코루틴
    public IEnumerator MoveToEnemyCoroutine(float duration)
    {
        if (enemyManager == null || enemyManager.currentEnemy == null) yield break;

        // 상대 앞까지 가는 쪽이 항상 위로 그려져야 한다 - 상대의 정렬 순서보다 +1로 맞춘다.
        var enemySprite = enemyManager.currentEnemy.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && enemySprite != null)
            spriteRenderer.sortingOrder = enemySprite.sortingOrder + 1;

        float time = 0;
        Vector3 startPos = transform.position;
        // 적의 위치에서 x축으로 dashOffset만큼 왼쪽(-)에 서기
        Vector3 targetPos = new Vector3(
                    enemyManager.currentEnemy.transform.position.x - dashOffset,
                    originalPosition.y,
                    originalPosition.z
                );
        while (time < duration)
        {
            var t = moveToEnemyCurve.Evaluate(time / duration);
            transform.position = Vector3.LerpUnclamped(startPos, targetPos, t);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
    }

    // 원래 자리로 돌아오는 코루틴
    public IEnumerator MoveToOriginCoroutine(float duration)
    {
        float time = 0;
        Vector3 startPos = transform.position;

        while (time < duration)
        {
            var t = moveToOriginCurve.Evaluate(time / duration);
            transform.position = Vector3.LerpUnclamped(startPos, originalPosition, t);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition;

        // 제자리로 돌아왔으니 정렬 순서도 원래대로.
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = originalSortingOrder;
    }

    // 공격 애니메이션 실행 (배속 적용 포함)
    public void PlayAttackAnimation(bool isFirstAttack, float speedMultiplier)
    {
        if (animator == null) return;

        animator.speed = speedMultiplier;

        if (isFirstAttack)
        {
            animator.SetTrigger("Punch1"); // 1번 이미지 (돌진)
        }
        else
        {
            // 2, 3, 4번 중 랜덤 재생
            int randomAnim = Random.Range(2, 5);
            animator.SetTrigger("Punch" + randomAnim);
        }
    }

    // 턴 끝나면 배속 초기화
    public void ResetAnimationSpeed()
    {
        if (animator != null)
        {
            animator.speed = 1.0f;
        }
    }

    public void PlayGuardAnimation()
    {
        if (animator == null) return;

        animator.SetTrigger("Guard");
    }

}