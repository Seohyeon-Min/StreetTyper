using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "StreetTyper/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Basic Info")]
    public string enemyName = "New Enemy";
    public int maxHP = 150;
    public int power = 5;
    public int defensePower = 5;
    public int buffPower = 2;

    [Header("Action Probabilities (Total 100)")]
    [Range(0, 100)] public int attackChance = 60;
    [Range(0, 100)] public int defendChance = 30;
    [Range(0, 100)] public int buffChance = 10;
}