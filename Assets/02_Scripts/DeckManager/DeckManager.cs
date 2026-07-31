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

    [Tooltip("턴이 도는 동안 완성된 조합을 쌓아두는 곳. 실제 적용은 턴이 끝날 때 한다.")]
    [SerializeField] private PendingActionManager pendingActionManager;

    [Tooltip("화상 지속 피해와 상태이상 턴 감소를 적 턴 직후에 처리한다.")]
    [SerializeField] private StatusEffectManager statusEffectManager;

    [Tooltip("럭키로 처치했을 때 클리어 보상을 늘리기 위해 참조한다.")]
    [SerializeField] private WordUnlockManager wordUnlockManager;

    [Header("턴 전환 딜레이")]
    [Tooltip("타이머가 끝난 뒤 적이 공격하기까지 대기하는 시간(초)")]
    [SerializeField] private float turnChangeDelay = 2f;

    [Tooltip("적 공격이 끝난 뒤 플레이어 턴이 다시 시작되기까지 대기하는 시간(초)")]
    [SerializeField] private float postAttackDelay = 4f;

    [Tooltip("턴 종료 후 쌓인 공격을 하나씩 터뜨리는 간격(초)")]
    [SerializeField] private float pendingActionInterval = 0.3f;

    [SerializeField] private bool logDebugEvents;

    public CardSlotManager Slots => cardSlotManager;
    public CardInputHandler Input => cardInputHandler;
    public MainBufferManager Buffer => mainBufferManager;

    private void OnEnable()
    {
        // 게임플레이 배선은 디버그 로그 여부와 무관하게 항상 걸려 있어야 한다.
        wordChainManager.OnChainCompleted += HandleChainCompleted;
        timerManager.OnTimeExpired += HandleTimeExpired;
        battleManager.OnBattleEnded += HandleBattleEnded;

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
        battleManager.OnBattleEnded -= HandleBattleEnded;

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

    // 액션 단어로 체인이 완성될 때마다 호출된다. 턴을 끝내지 않는다 - 타이머가 도는 동안
    // 여러 번 일어날 수 있다. 계산(SkillResolver) -> 쌓아두기(PendingActionManager) ->
    // 체인 비우기(바로 다음 조합을 이어서 쌓을 수 있게) -> 타이머 반영.
    // 실제 피해/방어 적용은 여기서 하지 않는다 - 턴이 끝날 때 PlayPendingActions가 순서대로 재생한다.
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

        // 지금 적용하지 않고 쌓아둔다. 턴이 끝나면 PlayPendingActions가 쌓인 순서대로 터뜨린다.
        pendingActionManager.Enqueue(skillName, action);

        wordChainManager.ClearChain();

        // 타이머 증감(잽/훅/퀵/어퍼컷)만은 즉시 반영한다 - 남은 시간이 늘거나 깎이는 건
        // 이번 턴 안에서 곧바로 체감돼야 하는 리스크/보상이라 지연시키면 의미가 없다.
        //
        // 반드시 마지막에 반영한다 - 훅/어퍼컷처럼 시간을 깎는 조합이 남은 시간을 0으로 만들면
        // 이 호출 안에서 곧바로 OnTimeExpired -> 턴 전환이 시작되기 때문이다. 위의 Enqueue가
        // 이보다 앞에 있어야 그 조합이 재생 목록에 들어간 상태로 턴이 넘어간다.
        timerManager.AddTime(action.TimerChange);
    }

    // 플레이어 턴의 입력 제한 시간이 다 됐다. 여기가 실제 턴의 끝 - 미완성 체인은 버리고
    // 적 턴을 실행한 뒤 다음 플레이어 턴을 위해 타이머를 다시 채운다. 딜레이가 있어서
    // 코루틴으로 처리한다.
    private void HandleTimeExpired()
    {
        StartCoroutine(RunTurnTransition());
    }

    // 승패가 갈린 순간. 결과 화면에서 다음 스테이지로 넘어가기 전까지는 타이머도 멈추고
    // 입력도 받지 않아야 한다 - 안 그러면 적이 죽은 뒤에도 타이머가 0까지 흐르는 동안 타이핑이 먹힌다.
    private void HandleBattleEnded()
    {
        timerManager.StopTimer();
        inputManager.DisableInput();
        inputManager.ClearInput();

        // 아직 터지지 않은 공격은 버린다 - 안 그러면 다음 스테이지 첫 턴에 지난 판 공격이 튀어나온다.
        pendingActionManager.Clear();

        // 상태이상도 판이 끝나면 정리한다. 특히 데빌은 플레이어에게 걸린 것이라
        // 되돌리지 않으면 다음 판까지 피해 감소가 남는다.
        if (statusEffectManager != null)
            statusEffectManager.ClearAll();
    }

    private IEnumerator RunTurnTransition()
    {
        // 게임오버 여부와 무관하게 입력부터 잠근다 - 타이머가 다 됐는데 계속 타이핑되면 안 된다.
        inputManager.DisableInput();
        inputManager.ClearInput();

        if (battleManager.IsGameOver)
            yield break;

        if (logDebugEvents)
            Debug.Log("Timer expired - ending player turn");

        wordChainManager.ClearChain();

        // 대기 동안 게이지가 0에 붙어 있지 않고 가득 찬 채로 멈춰 있게 한다.
        // 실제 카운트다운은 아래에서 RestartTurn()이 열어준다.
        timerManager.ResetToFull();

        // 이번 턴에 쌓아둔 공격을 순서대로 터뜨린다. 여기가 이 게임의 실제 공격 연출 구간이다.
        yield return PlayPendingActions();

        // 재생 도중 적을 처치했거나 그 사이 전투가 끝났으면 여기서 끝낸다.
        if (battleManager.IsGameOver)
            yield break;

        // 다음 플레이어 턴에 쓸 손패를 미리 뽑는다 - 비어 있는 대기 시간이 교체 연출을
        // 받아주고, 입력이 열릴 때쯤엔 이미 정리된 손패를 읽을 수 있다.
        cardSlotManager.RefillAll();

        // "턴이 바뀌었다"는 걸 플레이어가 인지할 시간을 준 뒤 적이 공격한다.
        yield return new WaitForSeconds(turnChangeDelay);

        battleManager.ExecuteEnemyTurn();

        // 적 턴이 끝난 직후 화상 피해를 넣고 상태이상 지속을 1턴 줄인다.
        // 화상 피해를 적 공격과 겹치지 않게 띄워 보여주므로 코루틴으로 기다린다.
        if (statusEffectManager != null)
            yield return statusEffectManager.OnEnemyTurnEnded();

        // 적 턴에 플레이어가 죽었거나, 화상 피해로 적이 죽었을 수 있다.
        if (battleManager.IsGameOver)
            yield break;

        // 공격당한 여운을 두고 나서 플레이어 턴을 다시 연다.
        yield return new WaitForSeconds(postAttackDelay);

        inputManager.EnableInput();

        // 적이 마비 상태면 이번 턴 제한 시간이 늘어난다(GDD: 10초 + 5초).
        var timerBonus = statusEffectManager != null ? statusEffectManager.GetTimerBonus() : 0f;
        timerManager.RestartTurn(timerBonus);
    }

    // 이번 턴에 쌓인 공격을 쌓인 순서대로(먼저 완성한 것부터) 하나씩 적용하고 사이에 간격을 둔다.
    // 적을 처치하면 남은 것은 버리고 즉시 끝낸다 - CharacterStats.Die()가 Destroy를 부르므로
    // 그 뒤의 공격은 대상이 없어 어차피 헛돌고, 결과 화면이 뜬 뒤에도 타격이 이어지면 어색하다.
    private IEnumerator PlayPendingActions()
    {
        while (pendingActionManager.TryDequeue(out var entry))
        {
            combatManager.ExecutePlayerAction(entry.Action, player, enemyManager.currentEnemy);

            // OnPlayerActionResolved가 UpdateUI -> CheckGameState를 거치므로,
            // 바로 아래의 IsGameOver는 이번 타격 결과가 반영된 값이다.
            battleManager.OnPlayerActionResolved(BuildBubbleText(entry.Action));

            if (battleManager.IsGameOver || enemyManager.currentEnemy == null)
            {
                // 적을 쓰러뜨린 게 바로 이 공격이다 - 럭키가 섞여 있었다면 클리어 보상을 하나 더 준다.
                // 화상 같은 지속 피해로 죽은 경우는 여기 오지 않으므로 보너스도 붙지 않는다.
                if (entry.Action.LootBonusOnKill && wordUnlockManager != null)
                    wordUnlockManager.AddLuckyBonus();

                pendingActionManager.Clear();
                yield break;
            }

            yield return new WaitForSeconds(pendingActionInterval);
        }
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
