using System.Collections;
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

    [Tooltip("돌진 도착 후 애니메이션의 '주먹이 뻗어 나가는' 타격 시점까지 대기하는 시간. " +
             "플레이어 쪽(DeckManager의 0.15f)과 같은 역할.")]
    [SerializeField] private float attackHitDelay = 0.15f;

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

    // Execute the action that was already decided.
    // 공격일 때만 플레이어 쪽(PlayerBattleVisuals)과 짝을 이루는 돌진 연출이 들어간다 -
    // 방어/버프는 제자리에서 하는 행동이라 이동할 이유가 없다.
    public IEnumerator ExecuteEnemyTurnCoroutine(CharacterStats player)
    {
        if (currentEnemy == null || currentEnemy.enemyData == null || player == null) yield break;

        EnemyData data = currentEnemy.enemyData;

        switch (nextAction)
        {
            case ActionType.Attack:
                Debug.Log(data.enemyName + " Action: ATTACK!");

                // 플레이어 쪽(PlayerBattleVisuals)과 같은 순서 - 공격 자세를 먼저 잡고
                // 그 자세인 채로 돌진한다(다 이동한 뒤에 자세를 잡는 게 아니다).
                currentEnemy.PlayAttackAnimation();
                yield return currentEnemy.MoveToPlayerCoroutine(player.transform);

                // 주먹이 뻗어 나가는 타격 시점까지 대기(플레이어 쪽 hitDelay와 같은 역할).
                yield return new WaitForSeconds(attackHitDelay);

                player.TakeDamage(currentEnemy.power);

                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.AddDamageTaken(currentEnemy.power);

                if (HitEffectManager.Instance != null)
                {
                    HitEffectManager.Instance.PlayHitEffect(player.GetComponent<SpriteRenderer>());
                }

                if (SoundManager.Instance != null)
                    SoundManager.Instance.PlayRandomPunch();

                // 원래 자리로 복귀.
                yield return currentEnemy.MoveToOriginCoroutine();
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
