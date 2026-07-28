using UnityEngine;

[System.Serializable]
public class ResolvedAction
{
    public int Damage;
    public int Defense;
    public int Heal;

    public bool IgnoresDefense;
    public bool BreaksEnemyDefense;

    // public StatusEffectType StatusEffect; 
}