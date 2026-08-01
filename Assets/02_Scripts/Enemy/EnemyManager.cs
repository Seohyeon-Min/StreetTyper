using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("Current Active Enemy")]
    public EnemyBase currentEnemy;

    // Enum to define action types
    public enum ActionType { Attack, Defend, Buff }
    public ActionType nextAction;

    [Header("Intent 아이콘 (ActionType별로 하나씩)")]
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite defendIcon;
    [SerializeField] private Sprite buffIcon;

    [Header("Intent 텍스트 색 (ActionType별로 하나씩)")]
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private Color defendColor = Color.blue;
    [SerializeField] private Color buffColor = Color.yellow;

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

    // 인텐트 수치만 반환한다("Intent: Attack (10)" 같은 라벨 문구는 아이콘이 대신하므로 뺐다).
    public string GetIntentString()
    {
        if (currentEnemy == null || currentEnemy.enemyData == null) return "";

        switch (nextAction)
        {
            case ActionType.Attack:
                return currentEnemy.power.ToString();
            case ActionType.Defend:
                return currentEnemy.enemyData.defensePower.ToString();
            case ActionType.Buff:
                return currentEnemy.enemyData.buffPower.ToString();
            default:
                return "";
        }
    }

    // 텍스트 대신 아이콘으로 인텐트를 보여줄 때 쓴다(SpeechBubble.SetupIcon과 짝).
    public Sprite GetIntentIcon()
    {
        switch (nextAction)
        {
            case ActionType.Attack:
                return attackIcon;
            case ActionType.Defend:
                return defendIcon;
            case ActionType.Buff:
                return buffIcon;
            default:
                return null;
        }
    }

    // 인텐트 텍스트에 입힐 색(SpeechBubble.SetupIntent와 짝).
    public Color GetIntentColor()
    {
        switch (nextAction)
        {
            case ActionType.Attack:
                return attackColor;
            case ActionType.Defend:
                return defendColor;
            case ActionType.Buff:
                return buffColor;
            default:
                return Color.white;
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

                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.AddDamageTaken(currentEnemy.power);

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
