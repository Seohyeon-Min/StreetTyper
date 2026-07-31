using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FMODUnity;

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
    [SerializeField] private PlayerBattleVisuals playerVisuals;

    [Tooltip("턴이 도는 동안 완성된 조합을 쌓아두는 곳. 실제 적용은 턴이 끝날 때 한다.")]
    [SerializeField] private PendingActionManager pendingActionManager;

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

        // 적 턴에 플레이어가 죽었을 수 있다 - 그러면 다음 턴을 시작하지 않는다.
        if (battleManager.IsGameOver)
            yield break;

        // 공격당한 여운을 두고 나서 플레이어 턴을 다시 연다.
        yield return new WaitForSeconds(postAttackDelay);

        inputManager.EnableInput();
        timerManager.RestartTurn();
    }

    // 이번 턴에 쌓인 공격을 쌓인 순서대로(먼저 완성한 것부터) 하나씩 적용하고 사이에 간격을 둔다.
    // 적을 처치하면 남은 것은 버리고 즉시 끝낸다 - CharacterStats.Die()가 Destroy를 부르므로
    // 그 뒤의 공격은 대상이 없어 어차피 헛돌고, 결과 화면이 뜬 뒤에도 타격이 이어지면 어색하다.
    private IEnumerator PlayPendingActions()
    {
        // 1. 큐에 쌓인 액션을 모두 꺼내서 리스트로 옮깁니다. (총 개수를 미리 알기 위해)
        List<PendingActionManager.Entry> actions = new List<PendingActionManager.Entry>();
        while (pendingActionManager.TryDequeue(out var entry))
        {
            actions.Add(entry);
        }

        int totalActions = actions.Count;
        if (totalActions == 0) yield break;

        // 2. 다이나믹 배속 계산 (최대 4초 룰)
        float maxTotalTime = 3.9f;
        float moveDuration = 0.2f; // 돌진 및 복귀 시간 (왕복 0.4초)
        float availableAttackTime = maxTotalTime - (moveDuration * 2); // 순수하게 때릴 수 있는 시간 (약 3.5초)

        float baseInterval = pendingActionInterval; // 인스펙터에 설정된 기본값 (0.3초)
        float currentInterval = baseInterval;

        // 공격 개수가 너무 많아서 기본 간격으로 4초를 넘어가면, 간격을 강제로 압축합니다.
        if (totalActions * baseInterval > availableAttackTime)
        {
            currentInterval = availableAttackTime / totalActions;
        }

        // 애니메이션 배속 (간격이 짧아질수록 애니메이션은 그만큼 배속으로 빨라짐)
        float animSpeedMultiplier = baseInterval / currentInterval;

        // 3. 적 앞으로 돌진
        if (playerVisuals != null)
        {
            yield return playerVisuals.MoveToEnemyCoroutine(moveDuration);
        }

        // 4. 공격 스택 하나씩 실행
        for (int i = 0; i < totalActions; i++)
        {
            var actionEntry = actions[i];

            // 1번째 공격이면 Punch1, 그 이후는 랜덤 펀치 애니메이션 재생
            if (playerVisuals != null)
            {
                playerVisuals.PlayAttackAnimation(i == 0, animSpeedMultiplier);
            }

            if (SoundManager.Instance != null && battleManager != null)
            {
                SoundManager.Instance.PlaySFX(battleManager.attackSound);
            }

            // 데미지 및 UI 텍스트 처리
            combatManager.ExecutePlayerAction(actionEntry.Action, player, enemyManager.currentEnemy);
            battleManager.OnPlayerActionResolved(BuildBubbleText(actionEntry.Action));

            // 도중에 적이 죽거나 전투가 끝났다면 콤보 즉시 중단
            if (battleManager.IsGameOver || enemyManager.currentEnemy == null)
            {
                break;
            }

            // 계산된 동적 간격만큼 대기 (배속이 걸리면 엄청 짧게 기다림)
            yield return new WaitForSeconds(currentInterval);
        }

        // 5. 원래 위치로 복귀 및 배속 원상 복구
        if (playerVisuals != null)
        {
            yield return playerVisuals.MoveToOriginCoroutine(moveDuration);
            playerVisuals.ResetAnimationSpeed();
        }

        pendingActionManager.Clear();
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
