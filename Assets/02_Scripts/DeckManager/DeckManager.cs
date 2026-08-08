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
        {
            if (phase == TurnPhase.PlayerInput)
                ShowYourTurnBanner();
            return;
        }

        CurrentPhase = phase;
        OnTurnPhaseChanged?.Invoke(phase);

        if (phase == TurnPhase.PlayerInput)
            ShowYourTurnBanner();
    }

    [Header("Your Turn Banner")]
    [Tooltip("Input Canvas 중앙에 생성할 YOUR TURN 프리팹.")]
    [SerializeField] private YourTurnBanner yourTurnBannerPrefab;

    private YourTurnBanner yourTurnBannerInstance;

    private void ShowYourTurnBanner()
    {
        if (yourTurnBannerInstance == null)
        {
            if (yourTurnBannerPrefab == null)
            {
                Debug.LogWarning("DeckManager: yourTurnBannerPrefab이 연결되지 않았습니다.", this);
                return;
            }

            Canvas inputCanvas = null;
            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                if (canvas != null && canvas.name == "Input Canvas")
                {
                    inputCanvas = canvas;
                    break;
                }
            }

            if (inputCanvas == null)
            {
                Debug.LogWarning("DeckManager: YOUR TURN 배너를 배치할 Input Canvas를 찾지 못했습니다.", this);
                return;
            }

            yourTurnBannerInstance = Instantiate(yourTurnBannerPrefab, inputCanvas.transform, false);
        }

        yourTurnBannerInstance.Play();
    }

    /// <summary>
    /// 새 스테이지의 첫 플레이어 턴을 연다. 이전 적이 공격 처리 도중 죽으면 코루틴이
    /// ResolvingPlayerActions에서 끝날 수 있으므로 다음 스테이지에서 반드시 초기화해야 한다.
    /// </summary>
    public void BeginNewStagePlayerInput()
    {
        SetPhase(TurnPhase.PlayerInput);
    }

    [SerializeField] private CardSlotManager cardSlotManager;

    [Tooltip("패배 시 손패가 무너지듯 떨어지는 연출에 쓴다(HandleBattleEnded). Card Canvas의 " +
             "HandFanLayout(Hand)을 연결할 것.")]
    [SerializeField] private HandFanLayout handFanLayout;

    [Tooltip("패배 연출에서 카드마다 무너지기 시작하는 시간차(초). 0이면 5장이 동시에 떨어진다.")]
    [SerializeField] private float collapseStagger = 0.06f;

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

    [Tooltip("첫 타격 이후 카메라 흔들림 강도 배율. 0.18이면 후속 타격은 첫 타격의 18% 세기입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float followUpHitShakeMultiplier = 0.18f;

    [Tooltip("적 앞으로 돌진하기 시작하기 전에 잠깐 두는 대기 시간(초).")]
    [SerializeField] private float dashStartDelay = 0.3f;

    [Header("시간 소각 (Ctrl)")]
    [Tooltip("Ctrl을 누르고 있을 때 한 번의 반복마다 깎을 시간(초). 얼마나 자주 깎이는지는 " +
             "InputManager의 ctrlRepeatInterval이 정한다. 태운 시간은 퍼펙트의 위력으로 돌아온다.")]
    [SerializeField] private float ctrlBurnSeconds = 0.25f;

    [SerializeField] private bool logDebugEvents;

    public CardSlotManager Slots => cardSlotManager;
    public CardInputHandler Input => cardInputHandler;
    public MainBufferManager Buffer => mainBufferManager;

    private void Awake()
    {
        // 0이면 TimerManager.AddTime이 Mathf.Approximately로 조용히 버려서 "Ctrl이 안 먹는다"로만
        // 보인다 - 조용한 실패를 만들지 않는다.
        if (ctrlBurnSeconds <= 0f)
            Debug.LogWarning($"{nameof(DeckManager)}: {nameof(ctrlBurnSeconds)}가 0 이하라 Ctrl로 " +
                             "시간을 깎을 수 없습니다.", this);
    }

    private void OnEnable()
    {
        // 게임플레이 배선은 디버그 로그 여부와 무관하게 항상 걸려 있어야 한다.
        wordChainManager.OnChainCompleted += HandleChainCompleted;
        timerManager.OnTimeExpired += HandleTimeExpired;
        battleManager.OnBattleEnded += HandleBattleEnded;

        if (inputManager != null)
            inputManager.OnBurnTime += HandleBurnTime;

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

        if (inputManager != null)
            inputManager.OnBurnTime -= HandleBurnTime;

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
                      $"Status=[{string.Join(", ", action.StatusEffects)}] DamageReduction={action.DamageReduction} " +
                      $"TimerChange={action.TimerChange} GrantsLootBonus={action.GrantsLootBonus}");
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

    /// <summary>플레이어가 Ctrl을 누르고 있어 남은 시간을 일부러 깎는다.
    /// 태운 시간은 퍼펙트의 위력(SkillResolver.SecondsSpentThisTurn)으로 돌아온다.
    ///
    /// ⚠️ 가드는 <see cref="InputManager.HasTypingFocus"/> 하나만 쓴다. CurrentPhase로는
    /// <b>일시정지를 막지 못한다</b> - 일시정지는 timeScale을 0으로 둘 뿐 페이즈를 바꾸지 않고,
    /// TimerManager는 여전히 running이라 AddTime의 가드도 통과한다. 이 한 줄이 일시정지·보상·
    /// 결과 화면·삭제/목록 창을 한꺼번에 덮는다("지금 손패가 입력을 받는가"가 곧 그 질문이다).</summary>
    private void HandleBurnTime()
    {
        if (inputManager == null || timerManager == null)
            return;

        if (!inputManager.HasTypingFocus(cardInputHandler))
            return;

        // ⚠️ 이 호출 안에서 남은 시간이 0이 되면 곧바로 HandleTimeExpired까지 이어진다.
        timerManager.ReduceTime(ctrlBurnSeconds);
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
        // 1. 타이머 정지는 승리/패배 상관없이 작동 (결과창 대기 중이므로)
        timerManager.StopTimer();

        // [수정] 플레이어가 사망한 경우(게임 오버)에만 BGM을 끄고 패를 비웁니다.
        if (player != null && player.currentHP <= 0)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.StopBGM();
            }

            // 손패를 그냥 비우는 대신 카드마다 시차를 두고 무너지듯 떨어뜨린다. 슬롯 데이터는
            // 건드리지 않는다 - EmptyAllSlots()가 쏘는 OnSlotChanged(null)는 CardSlotView의
            // PlaySwap을 다시 불러 방금 시작한 무너짐 코루틴을 그 자리에서 끊어버린다.
            // 슬롯 데이터는 재시작 시 StageManager.RestartStage -> RefillAll이 통째로 새로
            // 채우므로 여기서 비워둘 필요가 없다.
            if (handFanLayout != null)
            {
                var cards = handFanLayout.Cards;
                for (var i = 0; i < cards.Count; i++)
                {
                    if (cards[i] != null)
                        cards[i].PlayCollapse(i * collapseStagger);
                }
            }

            if (timerManager != null)
            {
                timerManager.ResetToFull();
            }
        }
        // 전체 클리어. 진 게 아니니 무너뜨리지 않고 그냥 아래로 가라앉힌다 - 결과 화면 명령 카드
        // ("다시하기"/"카드"/"타이틀")가 같은 아래쪽 자리로 떠오르므로, 치워두지 않으면 손패와
        // 겹친다. ResultInputHandler가 HandFanLayout.IsLeaving으로 이 연출이 끝나기를 기다린다.
        //
        // ⚠️ IsGameOver가 아니라 IsFinalResult다 - 일반 스테이지 클리어(보상 선택 중)까지 걸리면
        // 다음 스테이지로 이어지는 판에서 손패가 사라진다.
        else if (battleManager != null && battleManager.IsFinalResult && handFanLayout != null)
        {
            var cards = handFanLayout.Cards;
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                    cards[i].PlaySink();
            }
        }

        // 3. 재시작 또는 다음 스테이지 입력을 받기 위해 인풋 활성화
        inputManager.EnableInput();
        inputManager.ClearInput();

        pendingActionManager.Clear();

        if (statusEffectManager != null)
            statusEffectManager.ClearAll();
    }
    // 이벤트 스테이지(마더 드래곤 등)는 적이 죽어도 대사가 끝날 때까지 BattleManager.IsGameOver가
    // 계속 false다(ShowResult가 EventManager.EndEvent에서 늦게 불린다). CheckGameState가 죽은 적을
    // Destroy가 아니라 SetActive(false)로만 끄기 때문에 currentEnemy 참조도 그대로 남는다 -
    // IsGameOver만 보면 이 구간의 턴 전환 코루틴이 죽은 적을 상대로 계속 진행돼버린다.
    private bool IsEnemyDefeated()
    {
        var enemy = enemyManager.currentEnemy;
        return enemy == null || !enemy.gameObject.activeInHierarchy || enemy.currentHP <= 0;
    }

    private IEnumerator RunTurnTransition()
    {
        // 게임오버 여부와 무관하게 입력부터 잠근다 - 타이머가 다 됐는데 계속 타이핑되면 안 된다.
        inputManager.DisableInput();
        inputManager.ClearInput();

        // 여기가 플레이어 턴의 끝이다. "이번 턴에 몇 번 했는가" 누적을 비워야 다음 턴의
        // 퍼펙트/니킥/춉/박치기가 0에서 다시 센다. 게임오버로 빠지는 경우에도 비워야 하므로
        // 아래 IsGameOver 검사보다 위에 둔다.
        skillResolver.ResetTurn();

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

        // 재생 도중 적을 처치했거나 그 사이 전투가 끝났으면 여기서 끝낸다. 이벤트 스테이지는
        // 대사가 끝날 때까지 IsGameOver가 아직 false이므로 적 처치 여부도 함께 봐야 한다 -
        // 안 그러면 죽은 적을 상대로 가짜 적 턴까지 재생하고 타이머를 다시 시작해버려서,
        // 보상 화면이 뜬 뒤에 그 타이머가 만료되며 입력이 다시 잠기고 아무도 안 열어준다.
        if (battleManager.IsGameOver || IsEnemyDefeated())
            yield break;

        // 다음 플레이어 턴에 쓸 손패를 미리 뽑는다 - 비어 있는 대기 시간이 교체 연출을
        // 받아주고, 입력이 열릴 때쯤엔 이미 정리된 손패를 읽을 수 있다.
        cardSlotManager.RefillAll();

        // "턴이 바뀌었다"는 걸 플레이어가 인지할 시간을 준 뒤 적이 공격한다.
        SetPhase(TurnPhase.TurnChangeRest);
        yield return new WaitForSeconds(turnChangeDelay);

        SetPhase(TurnPhase.EnemyTurn);
        yield return battleManager.ExecuteEnemyTurnCoroutine();

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

        // ⚠️ 대기 중에 이벤트 대화가 열렸을 수 있다. 마더 드래곤이 그렇다 - 3턴째 적 턴이
        // Invoke로 1.5초 뒤 FinishMotherDragonBattle을 걸고, 그게 위 postAttackDelay(2초)
        // 도중에 터지며 대사창을 연다. 여기서 막지 않으면 대사를 읽는 동안 타이머가 다시 돌아
        // 만료되고, 가짜 턴 전환이 겹치면서 대사가 끝난 뒤에도 타이머가 계속 흐른다.
        //
        // IsGameOver로는 걸러지지 않는다 - 이벤트 스테이지는 대사가 끝날 때까지 false다.
        // 여기서 끊어도 갇히지 않는다: 대사가 끝나면 EndEvent -> ShowResult -> OnBattleEnded ->
        // HandleBattleEnded가 입력을 다시 열어준다.
        //
        // ⚠️ IsEventActive만으로는 부족하다. 대사가 아주 짧거나 플레이어가 스페이스를 빠르게 눌러
        // 이 대기가 끝나기 전에 대사까지 이미 다 끝나버리면(EndEvent -> ShowResult -> OnBattleEnded
        // -> HandleBattleEnded가 timerManager.StopTimer()까지 이미 실행된 상태), IsEventActive가
        // 다시 false로 돌아와 있어 이 가드를 그냥 통과한다. 그러면 아래 RestartTurn()이 방금 멈춘
        // 타이머를 승리 화면 뒤에서 도로 돌려버린다. IsGameOver도 같이 봐야 그 경로까지 막힌다.
        if (battleManager.IsGameOver || battleManager.IsEventActive)
            yield break;

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
        // 1. 큐에 쌓인 액션을 모두 꺼내서 리스트로 옮깁니다.
        List<PendingActionManager.Entry> rawActions = new List<PendingActionManager.Entry>();
        while (pendingActionManager.TryDequeue(out var entry))
        {
            rawActions.Add(entry);
        }

        // 공격(데미지가 있는 액션)을 먼저, 가드(데미지가 없는 액션)를 나중에 실행하도록 재배치합니다.
        List<PendingActionManager.Entry> actions = new List<PendingActionManager.Entry>();
        foreach (var act in rawActions)
        {
            if (act.Action.Damage > 0) actions.Add(act);
        }
        foreach (var act in rawActions)
        {
            if (act.Action.Damage <= 0) actions.Add(act);
        }

        int totalActions = actions.Count;
        if (totalActions == 0) yield break;

        // [추가] 총 타격 수(연타 포함)를 계산하여 애니메이션 압축 배속에 사용합니다.
        int totalHits = 0;
        for (int j = 0; j < totalActions; j++)
        {
            totalHits += Mathf.Max(1, actions[j].Action.HitCount);
        }

        // 2. 다이나믹 배속 계산 (최대 4초 룰)
        float availableAttackTime = maxTotalPlayTime - (moveDuration * 2);
        float baseInterval = pendingActionInterval;
        float currentInterval = baseInterval;

        // [수정] totalActions 대신 totalHits를 기준으로 압축하여, 연타가 많아도 4초 안에 끝납니다.
        if (totalHits * baseInterval > availableAttackTime)
        {
            currentInterval = availableAttackTime / totalHits;
        }

        float animSpeedMultiplier = baseInterval / currentInterval;

        bool hasPlayedPunchAnim = false;
        bool hasShaken = false;

        bool anyDamageThisTurn = false;
        for (int j = 0; j < totalActions; j++)
        {
            if (actions[j].Action.Damage > 0)
            {
                anyDamageThisTurn = true;
                break;
            }
        }

        // 3. 적 앞으로 돌진
        if (playerVisuals != null)
        {
            if (anyDamageThisTurn)
            {
                playerVisuals.PlayAttackAnimation(true, animSpeedMultiplier);
                hasPlayedPunchAnim = true;
                if (dashStartDelay > 0f)
                    yield return new WaitForSeconds(dashStartDelay);

                yield return playerVisuals.MoveToEnemyCoroutine(moveDuration);
            }
            else
            {
                // [추가] 방어만 할 때는 제자리에서 살짝 대기 시간만 가집니다.
                if (dashStartDelay > 0f)
                    yield return new WaitForSeconds(dashStartDelay);
            }

        }

        // 4. 공격 스택 하나씩 실행
        // 한 번의 공격 실행 전체에서 첫 유효 타격만 강하게 흔든다.
        bool hasPlayedStrongHitShake = false;

        for (int i = 0; i < totalActions; i++)
        {
            var actionEntry = actions[i];
            var hasDamage = actionEntry.Action.Damage > 0;
            int hitCount = Mathf.Max(1, actionEntry.Action.HitCount);

            // HitCount(연타 수)만큼 루프를 돌며 개별 타격합니다.
            for (int h = 0; h < hitCount; h++)
            {
                float hitDelay = 0f;

                if (hasDamage)
                {
                    if (playerVisuals != null)
                    {
                        // 연타 중에는 연속 펀치 느낌을 살리기 위해 랜덤 펀치가 나가게 합니다.
                        playerVisuals.PlayAttackAnimation(!hasPlayedPunchAnim, animSpeedMultiplier);
                    }
                    hasPlayedPunchAnim = true;

                    hitDelay = 0.15f / animSpeedMultiplier;
                    yield return new WaitForSeconds(hitDelay);

                    // [핵심] 매 타격마다 타격음 재생
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlayRandomPunch();
                    }
                }

                // ========== 타격감 연출 ==========
                if (hasDamage && enemyManager.currentEnemy != null)
                {
                    // [수정] hasShaken 제한을 풀어 매 타격마다 카메라가 흔들리게 합니다!
                    if (CameraShake.Instance != null)
                    {
                        float shakeMagnitude = hasPlayedStrongHitShake
                            ? hitShakeMagnitude * followUpHitShakeMultiplier
                            : hitShakeMagnitude;
                        CameraShake.Instance.Shake(hitShakeDuration, shakeMagnitude);
                        hasPlayedStrongHitShake = true;
                    }

                    // 매 타격마다 피격 이펙트 재생
                    if (HitEffectManager.Instance != null)
                    {
                        HitEffectManager.Instance.PlayHitEffect(enemyManager.currentEnemy.GetComponent<SpriteRenderer>());
                    }

                    // 매 타격마다 데미지 플로팅 텍스트 띄우기
                    if (FloatingDamageManager.Instance != null)
                    {
                        FloatingDamageManager.Instance.ShowDamage(actionEntry.Action.Damage, enemyManager.currentEnemy.transform.position);
                    }
                }

                combatManager.ExecutePlayerAction(actionEntry.Action, player, enemyManager.currentEnemy);
                battleManager.UpdateUI();

                if (battleManager.IsGameOver || IsEnemyDefeated())
                {
                    break;
                }

                if (hasDamage)
                {
                    float remainingDelay = currentInterval - hitDelay;
                    if (remainingDelay > 0)
                    {
                        yield return new WaitForSeconds(remainingDelay);
                    }
                }
            } // 연타 루프 끝

            // 럭키 보너스는 여러 대를 때려도 액션(조합) 1개당 한 번만 판정합니다.
            if (actionEntry.Action.GrantsLootBonus && wordUnlockManager != null)
                wordUnlockManager.AddLuckyBonus();

            if (battleManager.IsGameOver || IsEnemyDefeated())
            {
                break;
            }
        }

        // 5. 원래 위치로 복귀
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

