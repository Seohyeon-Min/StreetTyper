using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private CardInputHandler cardInputHandler;
    [SerializeField] private MainBufferManager mainBufferManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private WordChainManager wordChainManager;
    [SerializeField] private SkillResolver skillResolver;
    [SerializeField] private TimerManager timerManager;

    [Header("Combat")]
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private CharacterStats player;

    [Header("턴 전환 딜레이")]
    [Tooltip("타이머가 끝난 뒤 적이 공격하기까지 대기하는 시간(초)")]
    [SerializeField] private float turnChangeDelay = 2f;

    [Tooltip("적 공격이 끝난 뒤 플레이어 턴이 다시 시작되기까지 대기하는 시간(초)")]
    [SerializeField] private float postAttackDelay = 4f;

    [SerializeField] private bool logDebugEvents;

    public CardSlotManager Slots => cardSlotManager;
    public CardInputHandler Input => cardInputHandler;
    public MainBufferManager Buffer => mainBufferManager;

    private void OnEnable()
    {
        // 게임플레이 배선은 디버그 로그 여부와 무관하게 항상 걸려 있어야 한다.
        wordChainManager.OnChainCompleted += HandleChainCompleted;
        timerManager.OnTimeExpired += HandleTimeExpired;

        if (!logDebugEvents)
            return;

        cardInputHandler.OnCardMatched += HandleCardMatched;
        cardInputHandler.OnTypo += HandleTypo;
        mainBufferManager.OnCardAdded += HandleCardAdded;
        mainBufferManager.OnBufferCleared += HandleBufferCleared;
    }

    private void OnDisable()
    {
        wordChainManager.OnChainCompleted -= HandleChainCompleted;
        timerManager.OnTimeExpired -= HandleTimeExpired;

        if (!logDebugEvents)
            return;

        cardInputHandler.OnCardMatched -= HandleCardMatched;
        cardInputHandler.OnTypo -= HandleTypo;
        mainBufferManager.OnCardAdded -= HandleCardAdded;
        mainBufferManager.OnBufferCleared -= HandleBufferCleared;
    }

    private void HandleCardMatched(CardBase card)
    {
        Debug.Log($"Matched: {card.CardName}");
    }

    private void HandleTypo()
    {
        Debug.Log("Typo");
    }

    private void HandleCardAdded(CardBase card)
    {
        Debug.Log($"Buffer += {card.CardName} (count: {mainBufferManager.Buffer.Count})");
    }

    private void HandleBufferCleared()
    {
        Debug.Log("Buffer cleared");
    }

    // 액션 단어로 체인이 완성될 때마다 호출된다. 더 이상 턴을 끝내지 않는다 - 타이머가 도는
    // 동안 여러 번 일어날 수 있다. 계산(SkillResolver) -> 적용(CombatManager) -> 타이머 반영
    // -> UI/말풍선(BattleManager) -> 체인 비우기(바로 다음 조합을 이어서 쌓을 수 있게).
    private void HandleChainCompleted(IReadOnlyList<WordInstance> chain)
    {
        var skillName = BuildSkillName(chain);
        var action = skillResolver.Resolve(chain, player.power);

        if (logDebugEvents)
        {
            Debug.Log($"Resolved [{skillName}]: Damage={action.Damage} Defense={action.Defense} Heal={action.Heal} " +
                      $"IgnoresDefense={action.IgnoresDefense} BreaksEnemyDefense={action.BreaksEnemyDefense} " +
                      $"Status={action.StatusEffect} DamageReduction={action.DamageReduction} " +
                      $"TimerChange={action.TimerChange} LootBonusOnKill={action.LootBonusOnKill}");
        }

        combatManager.ExecutePlayerAction(action, player, enemyManager.currentEnemy);
        timerManager.AddTime(action.TimerChange);
        battleManager.OnPlayerActionResolved(BuildBubbleText(action));
        wordChainManager.ClearChain();
    }

    // 플레이어 턴의 입력 제한 시간이 다 됐다. 여기가 실제 턴의 끝 - 미완성 체인은 버리고
    // 적 턴을 실행한 뒤 다음 플레이어 턴을 위해 타이머를 다시 채운다. 딜레이가 있어서
    // 코루틴으로 처리한다.
    private void HandleTimeExpired()
    {
        StartCoroutine(RunTurnTransition());
    }

    private IEnumerator RunTurnTransition()
    {
        if (battleManager.IsGameOver)
            yield break;

        if (logDebugEvents)
            Debug.Log("Timer expired - ending player turn");

        inputManager.DisableInput();
        inputManager.ClearInput();
        wordChainManager.ClearChain();

        // "턴이 바뀌었다"는 걸 플레이어가 인지할 시간을 준 뒤 적이 공격한다.
        yield return new WaitForSeconds(turnChangeDelay);

        battleManager.ExecuteEnemyTurn();

        // 적 턴에 플레이어가 죽었을 수 있다 - 그러면 다음 턴을 시작하지 않는다.
        if (battleManager.IsGameOver)
            yield break;

        // 공격당한 여운을 두고 나서 플레이어 턴을 다시 연다.
        yield return new WaitForSeconds(postAttackDelay);

        inputManager.EnableInput();
        timerManager.RestartTurn();
    }

    // 말풍선엔 스킬 이름이 아니라 실제 적용된 공격력/방어력 수치를 보여준다.
    // Damage/Defense는 액션의 ActionKind에 따라 둘 중 하나만 채워진다.
    private static string BuildBubbleText(ResolvedAction action)
    {
        var value = action.Damage != 0 ? action.Damage : action.Defense;
        return value.ToString();
    }

    private static string BuildSkillName(IReadOnlyList<WordInstance> chain)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < chain.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(chain[i].WordName);
        }

        return sb.ToString();
    }
}
