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

    public void TakeDamage(int damage)
    {
        int actualDamage = 0;

        if (defense >= damage)
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