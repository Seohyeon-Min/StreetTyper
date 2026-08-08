using System.Collections;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Base Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int power = 10;
    public int defense = 0;

    [Tooltip("받는 피해 배율. 데빌 같은 감소 효과가 1 미만으로 낮춘다(25% 감소면 0.75).")]
    public float damageTakenMultiplier = 1f;

    [Header("Death Effect")]
    [SerializeField, Min(0.05f)] private float deathBounceDuration = 0.3f;
    [SerializeField, Min(0f)] private float deathPauseDuration = 0.18f;
    [SerializeField, Min(0.05f)] private float deathFallDuration = 0.14f;
    [SerializeField, Min(0f)] private float deathBounceHeight = 0.55f;

    private bool _deathAnimationStarted;
    private Coroutine _deathRoutine;
    private Vector3 _initialPosition;
    private Vector3 _initialScale;
    private Quaternion _initialRotation;
    public bool IsDeathAnimationComplete { get; private set; }

    [Header("말풍선")]
    [Tooltip("말풍선(대사·의도·화상 등)이 뜰 위치. 비워두면 오브젝트 자신의 위치를 쓴다. " +
             "캐릭터마다 스프라이트 크기가 달라 프리팹/오브젝트별로 자식 Transform(보통 \"Pos\")을 " +
             "만들어 눈으로 보며 배치하는 쪽이 화면 좌표 오프셋 숫자를 맞추는 것보다 쉽다. " +
             "EnemyBase와 플레이어가 같은 개념을 쓰도록 여기(공통 베이스)에 있다.")]
    [SerializeField] private Transform bubbleAnchor;

    public Vector3 BubblePosition
    {
        get
        {
            if (bubbleAnchor == null)
                bubbleAnchor = transform.Find("Pos");

            return bubbleAnchor != null ? bubbleAnchor.position : transform.position;
        }
    }

    protected virtual void Awake()
    {
        _initialPosition = transform.position;
        _initialScale = transform.localScale;
        _initialRotation = transform.localRotation;
    }

    protected virtual void Start()
    {
        currentHP = maxHP;
    }

    // ignoreDefense: 페인트처럼 방어도를 소모하지도, 감산하지도 않고 HP를 직접 깎는 공격용.
    //
    // 방어도는 "먼저 닳는 껍데기"다 - 피해가 방어도보다 크면 넘치는 만큼은 반드시 HP로 간다.
    // (방어 4에 피해 10이면 방어가 0이 되고 HP가 6 줄어든다.)
    public void TakeDamage(int damage, bool ignoreDefense = false)
    {
        // 들어온 값 그대로를 남겨둔다 - 아래 로그에서 "얼마가 들어왔는지"와 "얼마나 막았는지"를
        // 갈라 봐야 피해가 0으로 들어온 것인지 방어가 다 먹은 것인지 구분할 수 있다.
        var incoming = damage;
        var defenseBefore = defense;
        var hpBefore = currentHP;

        // 받는 쪽에서 배율을 적용한다 - 공격하는 쪽(EnemyManager 등)을 고치지 않고
        // 데빌 같은 피해 감소를 반영하기 위한 것이다.
        if (!Mathf.Approximately(damageTakenMultiplier, 1f))
            damage = Mathf.Max(0, Mathf.RoundToInt(damage * damageTakenMultiplier));

        // 음수 피해가 들어오면 아래 뺄셈이 회복으로 둔갑한다. SkillResolver가 이미 막고 있지만
        // TakeDamage는 여러 경로(적 공격·디버그 킬스위치)에서 불리므로 받는 쪽에서도 막는다.
        damage = Mathf.Max(0, damage);

        int actualDamage;

        if (ignoreDefense)
        {
            actualDamage = damage;
        }
        else
        {
            // 방어도가 흡수하는 양은 "남은 방어도"와 "들어온 피해" 중 작은 쪽이고,
            // 나머지는 그대로 HP로 간다. 두 갈래로 나눠 쓰면 한쪽 부호를 실수했을 때
            // 넘친 피해가 통째로 사라질 수 있어 한 줄로 계산한다.
            var absorbed = Mathf.Min(defense, damage);
            defense -= absorbed;
            actualDamage = damage - absorbed;
        }

        currentHP -= actualDamage;

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }

        Debug.Log($"{gameObject.name}: 피해 {incoming}" +
                  $"{(ignoreDefense ? "(방어 무시)" : string.Empty)}" +
                  $"{(Mathf.Approximately(damageTakenMultiplier, 1f) ? string.Empty : $" x{damageTakenMultiplier} -> {damage}")}" +
                  $" / 방어 {defenseBefore} -> {defense}" +
                  $" / HP {hpBefore} -> {currentHP} (실피해 {actualDamage})", this);
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        currentHP = Mathf.Min(currentHP + amount, maxHP);
        Debug.Log(gameObject.name + " healed " + amount + ". HP: " + currentHP + " / " + maxHP);
    }

    public void AddDefense(int amount)
    {
        defense += amount;
        Debug.Log(gameObject.name + " gained " + amount + " defense. Total Defense: " + defense);
    }

    public void IncreasePower(int amount)
    {
        power += amount;
        Debug.Log(gameObject.name + " gained " + amount + " power. Total Power: " + power);
    }

    void Die()
    {
        Debug.Log(gameObject.name + " died!");
        if (_deathAnimationStarted || this is MotherDragon)
            return;

        _deathAnimationStarted = true;
        _deathRoutine = StartCoroutine(DeathEffectRoutine());
    }

    public void ResetVisualState()
    {
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        transform.position = _initialPosition;
        transform.localScale = _initialScale;
        transform.localRotation = _initialRotation;
        _deathAnimationStarted = false;
        IsDeathAnimationComplete = false;
    }

    private IEnumerator DeathEffectRoutine()
    {
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;
        float bounceDuration = Mathf.Max(0.05f, deathBounceDuration);
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float bounce = Mathf.Sin(t * Mathf.PI);
            transform.position = startPosition + Vector3.up * (bounce * deathBounceHeight);
            transform.localScale = new Vector3(
                startScale.x * Mathf.Lerp(1f, 0.82f, bounce),
                startScale.y * Mathf.Lerp(1f, 1.2f, bounce),
                startScale.z);
            yield return null;
        }

        transform.position = startPosition;
        transform.localScale = startScale;
        transform.localRotation = startRotation;

        if (deathPauseDuration > 0f)
            yield return new WaitForSeconds(deathPauseDuration);

        Vector3 fallStart = transform.position;
        Vector3 fallTarget = startPosition + Vector3.down * 0.18f;
        Vector3 crushedScale = new Vector3(startScale.x * 1.12f, 0f, startScale.z);
        float fallDuration = Mathf.Max(0.05f, deathFallDuration);
        elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.LerpUnclamped(fallStart, fallTarget, eased);
            transform.localRotation = startRotation;
            transform.localScale = Vector3.LerpUnclamped(startScale, crushedScale, eased);
            yield return null;
        }

        transform.position = fallTarget;
        transform.localRotation = startRotation;
        transform.localScale = crushedScale;
        IsDeathAnimationComplete = true;
        _deathRoutine = null;
    }
}
