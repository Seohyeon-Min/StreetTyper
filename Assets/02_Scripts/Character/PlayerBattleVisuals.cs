using System.Collections;
using UnityEngine;

public class PlayerBattleVisuals : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public EnemyManager enemyManager; // 현재 적의 위치를 찾기 위해 참조

    [Header("Settings")]
    public float dashOffset = 1.5f; // 적 앞에서 얼마나 떨어져서 멈출지

    private Vector3 originalPosition;

    void Start()
    {
        originalPosition = transform.position;
    }

    // 적 앞으로 이동하는 코루틴
    public IEnumerator MoveToEnemyCoroutine(float duration)
    {
        if (enemyManager == null || enemyManager.currentEnemy == null) yield break;

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
            transform.position = Vector3.Lerp(startPos, targetPos, time / duration);
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
            transform.position = Vector3.Lerp(startPos, originalPosition, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition;
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
}