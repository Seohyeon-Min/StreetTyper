using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FMODUnity;

public class DeckManager : MonoBehaviour
{
    // 내 턴(입력) -> 내 공격 재생 -> 턴전환휴식1 -> 적 턴 -> 턴전환휴식2 -> 다시 내 턴, 순환.
    // 다른 시스템(예: 적 인텐트 말풍선을 내 공격 애니메이션 중에는 숨기는 것)이 지금이 정확히
    // 어느 구간인지 알아야 할 때 이걸 본다.
    public enum TurnPhase
    {
        PlayerInput,
        ResolvingPlayerActions,
        TurnChangeRest,
        EnemyTurn,
        PostAttackRest
    }

    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.PlayerInput;

    /// <summary>턴 구간이 바뀔 때마다 발생한다.</summary>
    public event Action<TurnPhase> OnTurnPhaseChanged;

    private void SetPhase(TurnPhase phase)
    {
        if (CurrentPhase == phase)
            return;

        CurrentPhase = phase;
        OnTurnPhaseChanged?.Invoke(phase);
    }

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

    [Tooltip("공격 스택 재생(돌진+펀치+복귀) 전체가 아무리 많이 쌓여도 이 시간 안에 끝나도록 압축한다(초).")]
    [SerializeField] private float maxTotalPlayTime = 3.9f;

    [Tooltip("적 앞으로 돌진/원래 자리로 복귀하는 데 걸리는 시간(초, 각각).")]
    [SerializeField] private float moveDuration = 0.2f;

    [Header("타격감 연출")]
    [Tooltip("펀치 한 번당 카메라가 흔들리는 시간(초). CameraShake.Shake(duration, magnitude)의 첫 번째 인자.")]
    [SerializeField] private float hitShakeDuration = 0.1f;

    [Tooltip("펀치 한 번당 카메라 흔들림 크기. CameraShake의 shakeMultiplier와 곱해져서 최종 크기가 된다.")]
    [SerializeField] private float hitShakeMagnitude = 0.2f;

    [Tooltip("적 앞으로 돌진하기 시작하기 전에 잠깐 두는 대기 시간(초).")]
    [SerializeField] private float dashStartDelay = 0.3f;

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

        // 결과 화면에서는 "다음"/"다시"를 타이핑해 넘어가므로 입력을 끄지 않고 오히려 켠다
        // (일시정지에서 "계속"을 치는 것과 같은 구조). 대신 치다 만 글자는 비운다.
        inputManager.EnableInput();
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
        SetPhase(TurnPhase.ResolvingPlayerActions);
        yield return PlayPendingActions();

        // 재생 도중 적을 처치했거나 그 사이 전투가 끝났으면 여기서 끝낸다.
        if (battleManager.IsGameOver)
            yield break;

        // 다음 플레이어 턴에 쓸 손패를 미리 뽑는다 - 비어 있는 대기 시간이 교체 연출을
        // 받아주고, 입력이 열릴 때쯤엔 이미 정리된 손패를 읽을 수 있다.
        cardSlotManager.RefillAll();

        // "턴이 바뀌었다"는 걸 플레이어가 인지할 시간을 준 뒤 적이 공격한다.
        SetPhase(TurnPhase.TurnChangeRest);
        yield return new WaitForSeconds(turnChangeDelay);

        SetPhase(TurnPhase.EnemyTurn);
        battleManager.ExecuteEnemyTurn();

        // 적 턴이 끝난 직후 화상 피해를 넣고 상태이상 지속을 1턴 줄인다.
        // 화상 피해를 적 공격과 겹치지 않게 띄워 보여주므로 코루틴으로 기다린다.
        if (statusEffectManager != null)
            yield return statusEffectManager.OnEnemyTurnEnded();

        // 적 턴에 플레이어가 죽었거나, 화상 피해로 적이 죽었을 수 있다.
        if (battleManager.IsGameOver)
            yield break;

        // 공격당한 여운을 두고 나서 플레이어 턴을 다시 연다.
        SetPhase(TurnPhase.PostAttackRest);
        yield return new WaitForSeconds(postAttackDelay);

        // 이번 턴에 쌓은 방어도는 적 공격을 막는 데까지만 쓰인다. 여기서 비우지 않으면
        // 가드를 반복하는 것만으로 영구히 무적이 된다.
        // 적 방어도는 건드리지 않는다 - 적은 자기 턴에 스스로 쌓는다.
        if (player != null)
        {
            player.defense = 0;
            battleManager.UpdateUI();
        }

        inputManager.EnableInput();
        SetPhase(TurnPhase.PlayerInput);

        // 적이 마비 상태면 이번 턴 제한 시간이 늘어난다(GDD: 10초 + 5초).
        var timerBonus = statusEffectManager != null ? statusEffectManager.GetTimerBonus() : 0f;
        timerManager.RestartTurn(timerBonus);
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
        float availableAttackTime = maxTotalPlayTime - (moveDuration * 2); // 순수하게 때릴 수 있는 시간

        float baseInterval = pendingActionInterval; // 인스펙터에 설정된 기본값 (0.3초)
        float currentInterval = baseInterval;

        // 공격 개수가 너무 많아서 기본 간격으로 4초를 넘어가면, 간격을 강제로 압축합니다.
        if (totalActions * baseInterval > availableAttackTime)
        {
            currentInterval = availableAttackTime / totalActions;
        }

        // 애니메이션 배속 (간격이 짧아질수록 애니메이션은 그만큼 배속으로 빨라짐)
        float animSpeedMultiplier = baseInterval / currentInterval;

        // 펀치 애니메이션/카메라 쉐이크/히트 이펙트는 전부 "데미지가 있는 액션"에서만 재생한다
        // (가드처럼 데미지 0인 조합은 펀치가 안 나감). Punch1 여부와 쉐이크 1회 제한을 루프
        // 인덱스 i가 아니라 "실제로 몇 번째로 재생된 펀치인가"로 따로 세야 한다 - 안 그러면
        // 가드가 맨 앞에 쌓였을 때 진짜 첫 펀치가 Punch1로 안 나가거나 쉐이크가 아예 안 걸린다.
        bool hasPlayedPunchAnim = false;
        bool hasShaken = false;

        // 이번 턴에 데미지가 있는 액션이 하나도 없으면(전부 가드 등) 돌진할 때도 펀치 자세를
        // 취하면 안 된다 - 아래에서 돌진과 Punch1을 같이 트리거할지 판단하는 데 쓴다.
        bool anyDamageThisTurn = false;
        for (int j = 0; j < totalActions; j++)
        {
            if (actions[j].Action.Damage > 0)
            {
                anyDamageThisTurn = true;
                break;
            }
        }

        // 3. 적 앞으로 돌진 - Punch1은 즉시 재생하고, 이동은 dashStartDelay만큼 늦게 시작한다
        // (때리는 자세를 먼저 잡고 나서 날아가는 느낌). 가만히 날아갔다가 도착한 뒤에야
        // 펀치하는 게 아니라, 날아가기 직전부터 이미 펀치 자세를 취하고 있게 한다.
        if (playerVisuals != null)
        {
            if (anyDamageThisTurn)
            {
                playerVisuals.PlayAttackAnimation(true, animSpeedMultiplier);
                hasPlayedPunchAnim = true;
            }

            if (dashStartDelay > 0f)
                yield return new WaitForSeconds(dashStartDelay);

            yield return playerVisuals.MoveToEnemyCoroutine(moveDuration);
        }

        // 4. 공격 스택 하나씩 실행

        for (int i = 0; i < totalActions; i++)
        {
            var actionEntry = actions[i];
            var hasDamage = actionEntry.Action.Damage > 0;

            // 아래 루프 끝에서 "이번 인터벌 중 이미 기다린 시간"을 빼는 데 쓴다 - 데미지 없는
            // 액션은 펀치 딜레이를 안 기다리므로 0으로 둔다(= 인터벌을 그대로 다 기다림).
            float hitDelay = 0f;

            if (hasDamage)
            {
                // 실제로 재생되는 첫 펀치면 Punch1, 그 이후는 랜덤 펀치 애니메이션 재생
                if (playerVisuals != null)
                {
                    playerVisuals.PlayAttackAnimation(!hasPlayedPunchAnim, animSpeedMultiplier);
                }
                hasPlayedPunchAnim = true;

                // 주먹이 뻗어 나가는 타격 시점까지 대기. 애니메이션 재생 속도(animSpeedMultiplier)에
                // 맞춰 대기 시간도 조절된다.
                hitDelay = 0.15f / animSpeedMultiplier; // 0.15f는 예시입니다. 애니메이션에 맞게 조절하세요.
                yield return new WaitForSeconds(hitDelay);

                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayRandomPunch();
                }
            }

            // ========== 타격감 연출 ==========
            if (hasDamage && enemyManager.currentEnemy != null)
            {
                // 1. 카메라 쉐이크 - 펀치마다 흔들면 Shake()가 매번 StopAllCoroutines로 이전
                // 흔들림을 끊고 다시 시작해서 펀치가 여러 번일 때 쉴 새 없이 흔들리는 것처럼 보인다.
                // 시퀀스당 실제 첫 펀치 한 번만 흔든다.
                // (세기는 인스펙터의 hitShakeDuration/hitShakeMagnitude로 조절한다 - 흔들림이
                //  약하거나 세다고 느껴지면 코드가 아니라 그쪽 값을 만질 것.)
                if (!hasShaken && CameraShake.Instance != null)
                {
                    CameraShake.Instance.Shake(hitShakeDuration, hitShakeMagnitude);
                    hasShaken = true;
                }

                // 2. 피격 이펙트
                if (HitEffectManager.Instance != null)
                {
                    HitEffectManager.Instance.PlayHitEffect(enemyManager.currentEnemy.GetComponent<SpriteRenderer>());
                }

                // 3. 플로팅 데미지 띄우기
                if (FloatingDamageManager.Instance != null)
                {
                    FloatingDamageManager.Instance.ShowDamage(actionEntry.Action.Damage, enemyManager.currentEnemy.transform.position);
                }
            }
            // 데미지 및 UI 텍스트 처리
            combatManager.ExecutePlayerAction(actionEntry.Action, player, enemyManager.currentEnemy);

            // 처치 판정을 UI 갱신보다 "먼저" 한다. OnPlayerActionResolved는 UpdateUI -> CheckGameState
            // -> ShowResult -> OnBattleEnded까지 한 호출 안에서 이어지고, 그 안에서 StageManager가
            // 클리어 보상을 지급해 버린다. 럭키 보너스가 그 뒤에 얹히면 이번 판이 아니라
            // 다음 스테이지 보상에 반영되어, 로그만 찍히고 카드는 3장 그대로인 상태가 된다.
            if (actionEntry.Action.LootBonusOnKill && wordUnlockManager != null)
            {
                var killed = enemyManager.currentEnemy;

                // Die()가 Destroy를 부르면 Unity의 == null이 즉시 true가 되므로 둘 다 본다.
                if (killed == null || killed.currentHP <= 0)
                    wordUnlockManager.AddLuckyBonus();
            }

            // 타격 하나가 적용될 때마다 HP/방어도 표시를 갱신한다. 이게 없으면 수치는
            // 한 대씩 제대로 깎이는데 화면만 그대로 있다가 턴이 끝날 때 한 번에 뚝 떨어져서,
            // 공격이 한꺼번에 들어간 것처럼 보인다.
            // 반드시 위의 럭키 처리보다 "뒤"에 있어야 한다 - UpdateUI는 CheckGameState ->
            // ShowResult -> OnBattleEnded까지 한 호출 안에서 이어지고, 그 안에서 StageManager가
            // 클리어 보상을 지급해 버리기 때문이다.
            // (예전엔 이 자리에서 OnPlayerActionResolved가 말풍선과 함께 UpdateUI를 불렀다.
            //  말풍선은 FloatingDamageManager로 대체되어 빠졌지만, 갱신은 여전히 필요하다.)
            battleManager.UpdateUI();

            // 도중에 적이 죽거나 전투가 끝났다면 콤보 즉시 중단 (럭키는 위에서 이미 처리했다)
            if (battleManager.IsGameOver || enemyManager.currentEnemy == null)
            {
                break;
            }

            // 전체 인터벌에서 이미 기다린 타격 딜레이(hitDelay)를 빼고 남은 시간만 대기한다.
            // 데미지 없는 액션(가드 등)은 애니메이션이 아예 없으므로 이 대기 자체를 건너뛴다 -
            // 안 그러면 펀치 사이에 가드가 끼어 있을 때마다 아무것도 안 보이면서
            // currentInterval만큼 조용히 멈춰서, 펀치 3번이 바로 이어지지 않고 뜨문뜨문 보인다.
            if (hasDamage)
            {
                float remainingDelay = currentInterval - hitDelay;
                if (remainingDelay > 0)
                {
                    yield return new WaitForSeconds(remainingDelay);
                }
            }
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

    private void HandleCardAdded(CardBase card)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayRandomCardUse();
        }

        Debug.Log($"Buffer += {card.CardName} (count: {mainBufferManager.Buffer.Count})");
    }

}

