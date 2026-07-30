using UnityEngine;

public class EnemyBase : CharacterStats
{
    [Header("Enemy Data Reference")]
    public EnemyData enemyData;
    public bool isMotherDragon = false;

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