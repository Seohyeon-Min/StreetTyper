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
        Destroy(gameObject);
    }
}