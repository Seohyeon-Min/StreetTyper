using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("Current Active Enemy")]
    public EnemyBase currentEnemy;

    // Enum to define action types
    public enum ActionType { Attack, Defend, Buff }
    public ActionType nextAction;

    // Determine the next action beforehand
    public void GenerateNextAction()
    {
        if (currentEnemy == null || currentEnemy.enemyData == null) return;

        EnemyData data = currentEnemy.enemyData;
        int randomValue = Random.Range(0, 100);

        if (randomValue < data.attackChance)
        {
            nextAction = ActionType.Attack;
        }
        else if (randomValue < data.attackChance + data.defendChance)
        {
            nextAction = ActionType.Defend;
        }
        else
        {
            nextAction = ActionType.Buff;
        }
    }

    // Get the intent text for UI
    public string GetIntentString()
    {
        if (currentEnemy == null || currentEnemy.enemyData == null) return "";

        switch (nextAction)
        {
            case ActionType.Attack:
                return "Intent: Attack (" + currentEnemy.power + ")";
            case ActionType.Defend:
                return "Intent: Defend (" + currentEnemy.enemyData.defensePower + ")";
            case ActionType.Buff:
                return "Intent: Buff (" + currentEnemy.enemyData.buffPower + ")";
            default:
                return "";
        }
    }

    // Execute the action that was already decided
    public void ExecuteEnemyTurn(CharacterStats player)
    {
        if (currentEnemy == null || currentEnemy.enemyData == null || player == null) return;

        EnemyData data = currentEnemy.enemyData;

        switch (nextAction)
        {
            case ActionType.Attack:
                Debug.Log(data.enemyName + " Action: ATTACK!");
                currentEnemy.PlayAttackAnimation();
                player.TakeDamage(currentEnemy.power);

                if (HitEffectManager.Instance != null)
                {
                    HitEffectManager.Instance.PlayHitEffect(player.GetComponent<SpriteRenderer>());
                }

                if (SoundManager.Instance != null)
                    SoundManager.Instance.PlayRandomPunch();
                break;
            case ActionType.Defend:
                Debug.Log(data.enemyName + " Action: DEFEND!");
                currentEnemy.AddDefense(data.defensePower);
                break;
            case ActionType.Buff:
                Debug.Log(data.enemyName + " Action: BUFF POWER!");
                currentEnemy.IncreasePower(data.buffPower);
                break;
        }

        // Generate the intent for the next turn after executing
        GenerateNextAction();
    }
}
