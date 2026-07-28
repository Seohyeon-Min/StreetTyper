using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Base Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int power = 10;
    public int defense = 0;

    protected virtual void Start()
    {
        currentHP = maxHP;
    }

    // ignoreDefense: 페인트처럼 방어도를 소모하지도, 감산하지도 않고 HP를 직접 깎는 공격용.
    public void TakeDamage(int damage, bool ignoreDefense = false)
    {
        int actualDamage;

        if (ignoreDefense)
        {
            actualDamage = damage;
        }
        else if (defense >= damage)
        {
            defense -= damage;
            actualDamage = 0;
        }
        else
        {
            actualDamage = damage - defense;
            defense = 0;
        }

        currentHP -= actualDamage;

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }

        Debug.Log(gameObject.name + " took " + actualDamage + " damage. HP left: " + currentHP + " / Defense: " + defense);
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