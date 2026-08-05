using System.Collections;
using UnityEngine;

public class EnemyBase : CharacterStats
{
    [Header("Enemy Data Reference")]
    public EnemyData enemyData;
    private bool isScaled = false;

    /// <summary>이 적이 방어할 때 한 번에 쌓는 방어도. <see cref="CharacterStats.power"/>와 같은
    /// <b>런타임 스탯</b>이다 - 스테이지마다 값이 달라지므로 EnemyData 에셋을 직접 읽으면 안 된다
    /// (예전에는 EnemyManager가 <c>enemyData.defensePower</c>를 그대로 읽어서 방어도만 끝까지
    /// 고정이었다). 실제 값은 <see cref="ApplyScaling"/>이 채운다.</summary>
    [HideInInspector] public int defensePower;

    [HideInInspector]
    public bool isEndingBoss = false;

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
        // ⚠️ 복귀 지점을 <b>여기서</b> 확정한다. StageManager가 적을 화면 밖(spawnOffScreenX)에서
        // Instantiate하므로, Start()가 읽는 transform.position은 그 시점에 아직 화면 밖이다 -
        // 그대로 두면 공격을 마치고 "원래 자리"로 돌아갈 때 화면 밖으로 날아간다.
        // 슬라이드가 끝나서 실제로 서 있을 자리는 인자로 받은 targetPos다.
        originalPosition = targetPos;
        originPinned = true;

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

    // 공격을 마치고 돌아갈 자리. 슬라이드 인을 쓰면 SlideInCoroutine이 확정하고,
    // 안 쓰면 Start()가 스폰 위치를 읽는다(PlayerBattleVisuals.originalPosition과 같은 패턴).
    private Vector3 originalPosition;

    // SlideInCoroutine이 복귀 지점을 확정했는가. ⚠️ Start()가 덮어쓰지 못하게 막는 표시다 -
    // Instantiate 직후 StartCoroutine으로 슬라이드를 걸면 그 첫 구간이 Start()보다 <b>먼저</b>
    // 실행되므로, 이 가드가 없으면 Start()가 슬라이드 도중의 좌표를 "원래 자리"로 굳혀버린다.
    private bool originPinned;

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

        // ⚠️ 여기서 originalPosition을 targetPos로 덮어쓰지 말 것. 그러면 "원래 자리"가
        // 플레이어 앞으로 갱신되어, 뒤이은 MoveToOriginCoroutine이 이미 그 자리에 서 있는 채로
        // 끝나 <b>적이 복귀하지 않는다</b>. 실제로 그렇게 커밋된 적이 있다(f88aabd).
        // 화면 밖으로 날아가는 증상 때문에 넣은 것이었다면 원인은 이쪽이 아니라
        // SlideInCoroutine이 복귀 지점을 확정하지 않았던 것이다.
    }

    // 원래 자리로 복귀한다.
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

    /// <summary>스폰 직후 스테이지에 맞는 스탯을 입힌다(<see cref="StageManager.LoadStage"/>).
    ///
    /// <para><b>수치는 전부 부르는 쪽(StageManager)이 정한다.</b> 스테이지 진행을 아는 쪽이
    /// 계산해야 하고, 인스펙터에서 조절할 값도 거기 한곳에 모여 있어야 하기 때문이다.
    /// 셋 다 <b>이미 계산이 끝난 절대값</b>이며 여기서는 대입만 한다.</para>
    ///
    /// <para>⚠️ 각 인자는 0 이하면 그 스탯을 건드리지 않는다 - 마더 드래곤처럼 스케일링에서
    /// 빠져야 하는 적을 위한 통로다(3턴을 버텨야 스파링이 성립하는데 일반 공식을 태우면 그 전에
    /// 죽어 아웃로 이벤트가 통째로 깨진다).</para></summary>
    public void ApplyScaling(int scaledMaxHP, int scaledPower, int scaledDefensePower)
    {
        if (enemyData == null)
            return;

        if (scaledMaxHP > 0)
        {
            maxHP = scaledMaxHP;
            currentHP = maxHP;
        }

        if (scaledPower > 0)
            power = scaledPower;

        if (scaledDefensePower > 0)
            defensePower = scaledDefensePower;

        // 새로 등장한 적은 방어도가 없는 상태에서 시작해야 한다. 적 방어도는 턴마다 비워주는
        // 곳이 없어서(플레이어 쪽만 DeckManager가 비운다) 한 번 남으면 계속 쌓인 채로 간다.
        defense = 0;

        // ⚠️ 체력을 안 건드린 경우에도 세운다. 이게 false면 Start()가 enemyData 값으로
        // maxHP를 덮어써서, 체력 스케일링에서 빼둔 의미가 사라진다.
        isScaled = true;
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

        // 슬라이드 인이 이미 복귀 지점을 확정했으면 건드리지 않는다 - 이 시점의
        // transform.position은 아직 화면 밖(또는 슬라이드 도중)이라 믿을 수 없다.
        if (!originPinned)
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
            defensePower = enemyData.defensePower;
        }

        // 스케일링에서 방어도를 뺀 적(마더 드래곤)은 여기가 0으로 남는다 - 방어를 고르는
        // 순간 0을 쌓게 되므로 에셋 값으로 채워둔다.
        if (defensePower <= 0 && enemyData != null)
            defensePower = enemyData.defensePower;

        if (enemyData != null) gameObject.name = enemyData.enemyName;
    }
}