using System;
using System.Collections;
using UnityEngine;
using FMODUnity;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;
    public StageManager stageManager;
    public EventManager eventManager;

    [Tooltip("적 인텐트 말풍선을 내 턴(TurnPhase.PlayerInput)일 때만 보여주기 위해 참조한다.")]
    public DeckManager deckManager;

    [Header("Player Reference")]
    public CharacterStats player;

    [Header("Health Bar UI")]
    public HealthBarUI playerHealthBar;
    public HealthBarUI enemyHealthBar;

    // 결과 화면은 종류마다 프리팹이 따로다 - 코드가 제목을 갈아끼우지 않고 어느 패널을 켤지만
    // 정한다. 겉모습은 DefeatPanel/GameClearPanel 변형이 통째로 갖는다(ResultPanelView 참조).
    [Header("Result Panels")]
    [Tooltip("플레이어가 쓰러졌을 때 띄울 결과 창 한 벌.")]
    [SerializeField] private ResultPanelView defeatResult;

    [Tooltip("모든 스테이지를 클리어했을 때 띄울 결과 창 한 벌.")]
    [SerializeField] private ResultPanelView gameClearResult;

    [Header("마더 드래곤 대사")]
    [Tooltip("스파링 연출이라 순서가 정해져 있다. 0=시작, 1=1턴 뒤, 2=2턴 뒤, 3=마무리")]
    [SerializeField]
    private string[] motherDragonLines =
    {
        "어디 한번 실력을 보여보거라!",
        "제법이구나!",
        "조금 더 힘을 끌어내 보거라!",
        "훌륭하다. 여기까지 하마!"
    };

    [Tooltip("영어 대사. 한국어와 같은 개수로 채울 것 - 비어 있으면 한국어가 그대로 나온다.")]
    [SerializeField]
    private string[] motherDragonLinesEn =
    {
        "Come, show me what you can do!",
        "Not bad!",
        "Draw out more of your power!",
        "Splendid. That will do."
    };

    [Header("Duration")]
    public float actionBubbleDuration = 1.0f;

    private bool isGameOver = false;
    private bool isEventTriggered = false;

    // 마지막으로 띄운 결과 종류. 보상 선택이 끝난 뒤 같은 화면을 다시 그리려면 필요하다.
    private ResultKind lastResultKind = ResultKind.Victory;

    private GameObject enemyIntentBubbleObj;
    private SpeechBubble enemyIntentBubble;

    // 엄마용 전투 전용 변수
    private int mdTurnCount = 0;
    private int savedMDDamage = 0;

    // mdIntentString이 ExecuteEnemyTurnCoroutine에서 이미 일시적 말풍선(ShowBubble)으로
    // "말해진" 상태인가. true면 UpdateUI()의 영구 인텐트 말풍선에 같은 대사를 또 띄우지 않는다
    // (안 그러면 같은 대사가 두 메커니즘으로 연속 표시된다). 전투 시작/재시작에서만 false로 되돌린다.
    private bool mdLineAnnounced = false;

    // ⚠️ 필드 초기화자에서 MotherDragonLine(0)을 부르지 않는다. 직렬화 값은 필드 초기화자보다
    // 나중에 적용되므로, 인스펙터 배열을 거기서 읽으면 항상 비어 있다. Start와 ResetBattle에서 채운다.
    private string mdIntentString = string.Empty;

    // 마더 드래곤이 턴마다 하는 말. 인스펙터 배열에서 꺼내며, 영어 배열이 짧거나 비어 있으면
    // 한국어로 넘어간다 - 대사는 타이핑 대상이 아니라 읽기만 하므로 진행이 막히지는 않는다.
    private string MotherDragonLine(int index)
    {
        var lines = motherDragonLines;

        if (LanguageSettings.IsEnglish && motherDragonLinesEn != null && index < motherDragonLinesEn.Length &&
            !string.IsNullOrEmpty(motherDragonLinesEn[index]))
        {
            lines = motherDragonLinesEn;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("BattleManager: motherDragonLines가 비어 있어 마더 드래곤 대사가 나오지 않습니다.", this);
            return string.Empty;
        }

        return lines[Mathf.Clamp(index, 0, lines.Length - 1)];
    }

    //   추가: 대기열 상태 확인용 변수
    private bool isWaitingForDragonEnd = false;

    public event Action OnBattleEnded;

    void Start()
    {
        // 인스펙터 배열은 이 시점에 채워져 있다(필드 초기화자와 달리).
        mdIntentString = MotherDragonLine(0);
        mdLineAnnounced = false;

        HideResultUI();

        if (SpeechBubbleManager.Instance != null)
        {
            enemyIntentBubbleObj = Instantiate(SpeechBubbleManager.Instance.speechBubblePrefab, SpeechBubbleManager.Instance.canvasTransform);
            enemyIntentBubble = enemyIntentBubbleObj.GetComponent<SpeechBubble>();
            enemyIntentBubbleObj.SetActive(false);
        }

        if (enemyManager != null)
        {
            enemyManager.GenerateNextAction();
        }
        // 여기서 예외가 나면 아래 UpdateUI()까지 막혀 첫 프레임에 HP가 표시되지 않는다.
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayBattleBGM();

        if (deckManager != null)
            deckManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;

        UpdateUI();
    }

    void Update()
    {
        // 실제 출시(Release) 빌드에서는 컴파일되지 않도록 안전장치 추가
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current == null) return;

        // 결과 화면이 떠 있는데 킬스위치가 먹으면 그 뒤에서 스테이지가 넘어간다.
        if (isGameOver) return;

        // 일시정지 메뉴 뒤에서도 마찬가지다. Update는 timeScale 0에서도 계속 돈다.
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

        // 이벤트 대화 중에는 디버그 입력으로 전투/스테이지 상태가 뒤에서 바뀌면 안 된다.
        if (IsEventActive) return;

        // 숫자 0 누르면 플레이어 즉사
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            if (player != null && player.currentHP > 0)
            {
                Debug.Log("[DEBUG] 킬스위치 발동: 플레이어 사망");
                // 방어력을 무시(true)하고 현재 체력만큼 데미지를 줍니다.
                player.TakeDamage(player.currentHP, true); 
                UpdateUI();
            }
        }

        // 숫자 9 누르면 현재 적 즉사
        if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP > 0)
            {
                Debug.Log("[DEBUG] 킬스위치 발동: 적 사망");
                // 방어력을 무시(true)하고 현재 체력만큼 데미지를 줍니다.
                enemyManager.currentEnemy.TakeDamage(enemyManager.currentEnemy.currentHP, true);
                UpdateUI();
            }
        }

        // 숫자 8 누르면 현재 스테이지의 럭키 실제 판정과 표시를 모두 100%로 강제
        if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            SkillResolver.DebugForceLuckyChance100();

            // 확률만 올리면 럭키 조합을 실제로 완성해야 판정이 발생한다. 8→9 킬스위치로도
            // 럭키 보상 UI를 시험할 수 있게 보너스 라운드를 최소 1회 함께 예약한다.
            if (stageManager != null && stageManager.wordUnlockManager != null)
                stageManager.wordUnlockManager.DebugEnsureLuckyBonus();

            Debug.Log("[DEBUG] 럭키 확률 100% 강제 + 보상 라운드 예약 (현재 스테이지)");
        }

        // 숫자 5 누르면 마더 드래곤 스테이지(인덱스 4)로 즉시 이동.
        // 결과 화면·일시정지·이벤트 가드는 Update 앞머리에서 공통으로 처리한다.
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            if (stageManager != null)
            {
                Debug.Log("[DEBUG] 킬스위치 발동: 마더 드래곤 스테이지로 이동");
                stageManager.LoadStage(4);
            }
        }
#endif
    }

    private void OnDestroy()
    {
        if (deckManager != null)
            deckManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    // 내 턴(PlayerInput)이 아니면(공격 애니메이션 재생 중, 적 턴 등) 적 인텐트 말풍선을 숨긴다.
    // 다시 내 턴이 되면 UpdateUI()가 적이 살아있는지부터 다시 판단해서 알아서 켠다.
    private void HandleTurnPhaseChanged(DeckManager.TurnPhase phase)
    {
        if (phase == DeckManager.TurnPhase.PlayerInput)
        {
            UpdateUI();
            return;
        }

        if (enemyIntentBubbleObj != null)
            enemyIntentBubbleObj.SetActive(false);
    }

    public void OnPlayerActionResolved(string bubbleText)
    {
        if (isGameOver) return;

        if (SpeechBubbleManager.Instance != null && player != null)
        {
            SpeechBubbleManager.Instance.ShowBubble(bubbleText, player.transform.position, true, actionBubbleDuration);
        }

        UpdateUI();
    }

    // ⚠️ 코루틴이다 - 공격일 때 EnemyManager가 플레이어 앞까지 돌진했다 복귀하는 동안
    // 호출자(DeckManager.RunTurnTransition)가 그 연출이 끝날 때까지 기다려야 하기 때문이다.
    public IEnumerator ExecuteEnemyTurnCoroutine()
    {
        if (isGameOver) yield break;

        if (player != null && enemyManager != null && enemyManager.currentEnemy != null)
        {
            if (enemyManager.currentEnemy.isEndingBoss)
            {
                mdTurnCount++;
                if (mdTurnCount == 1) mdIntentString = MotherDragonLine(1);
                else if (mdTurnCount == 2) mdIntentString = MotherDragonLine(2);
                else if (mdTurnCount >= 3)
                {
                    mdIntentString = MotherDragonLine(3);
                    // 1.5초 대기
                    isWaitingForDragonEnd = true;
                    Invoke("FinishMotherDragonBattle", 1.5f);
                }

                if (SpeechBubbleManager.Instance != null)
                {
                    SpeechBubbleManager.Instance.ShowBubble(mdIntentString, enemyManager.currentEnemy.BubblePosition, false, actionBubbleDuration);

                    // [유지] 마미드래곤일 때만 말하기 애니메이션 재생
                    enemyManager.currentEnemy.PlaySpeakAnimation();

                    // 이 대사는 방금 일시적 말풍선으로 이미 보여줬다 - UpdateUI()의 영구 인텐트
                    // 말풍선이 같은 문자열을 또 띄우지 않도록 표시해 둔다.
                    mdLineAnnounced = true;
                }
            }
            else
            {
                // 공격 자체는 FloatingDamageManager가 피해 숫자로 보여준다. 여기서 말풍선까지
                // 띄우면 공격 전 인텐트 말풍선(UpdateUI)과 같은 숫자를 한 번 더 보여주는 셈이라
                // (게다가 EnemyManager.ExecuteEnemyTurnCoroutine이 끝에서 GenerateNextAction을
                // 불러 이미 다음 턴 인텐트로 바뀐 값이라 오히려 틀린 값이었다) 뺀다.
                yield return enemyManager.ExecuteEnemyTurnCoroutine(player);
            }

            UpdateUI();
        }
    }

    //   추가된 함수: 1.5초 뒤에 실행되어 전투를 마무리합니다.
    private void FinishMotherDragonBattle()
    {
        if (enemyManager != null && enemyManager.currentEnemy != null)
        {
            savedMDDamage = enemyManager.currentEnemy.maxHP - enemyManager.currentEnemy.currentHP;
            enemyManager.currentEnemy.currentHP = 0;
            isWaitingForDragonEnd = false;

            UpdateUI(); // 이 순간 CheckGameState()가 발동하면서 이벤트 창으로 넘어감
        }
    }

    public bool IsGameOver => isGameOver;

    /// <summary>지금 이벤트 대화(마더 드래곤 등)가 열려 있는가. `DeckManager`가 대사 중에
    /// 타이머를 다시 돌리지 않으려고 본다 - 이벤트 스테이지는 대사가 끝날 때까지
    /// <see cref="IsGameOver"/>가 false로 남아서 그것만으로는 구분할 수 없다.
    ///
    /// `DeckManager`에 `EventManager` 참조를 새로 꽂지 않으려고 여기로 한 번 중계한다
    /// (매니저 인스펙터 배선을 늘리지 않는 쪽이 이 프로젝트의 기본이다).</summary>
    public bool IsEventActive => eventManager != null && eventManager.IsEventActive;

    /// <summary>
    /// 런이 끝나 <b>결과 창이 떠 있는</b> 상태인가. 패배와 전체 클리어 둘뿐이고, 일반 스테이지
    /// 클리어(<see cref="ResultKind.Victory"/>)는 보상 선택으로 이어지므로 포함하지 않는다
    /// (ShowStatisticsUI가 불리는 조건과 같다 - ApplyResult 참조).
    ///
    /// "여기서 더 진행할 수 있는가"의 단일 판정이다. <see cref="IsGameOver"/>만 보면 보상을
    /// 고르는 중인 일반 클리어까지 걸리는데, 그때는 아직 게임이 이어지므로 일시정지도 걸려야
    /// 하고 결과 화면 명령 카드가 떠서도 안 된다. <see cref="PauseManager"/>가 일시정지를
    /// 막을지, <see cref="ResultInputHandler"/>가 명령 카드를 띄울지를 둘 다 이걸로 정한다.
    /// </summary>
    public bool IsFinalResult => isGameOver && lastResultKind != ResultKind.Victory;

    public void ResetBattle()
    {
        isGameOver = false;
        isEventTriggered = false;
        mdTurnCount = 0;
        mdIntentString = MotherDragonLine(0);
        mdLineAnnounced = false;
        savedMDDamage = 0;
        isWaitingForDragonEnd = false; //   추가됨

        HideResultUI();
        UpdateUI();
    }

    public void ShowGameClear()
    {
        if (playerHealthBar != null) playerHealthBar.Hide();
        if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);

        ShowResult(ResultKind.GameClear);
    }

    public void UpdateUI()
    {
        if (player != null && player.currentHP > 0)
        {
            if (playerHealthBar != null)
                playerHealthBar.UpdateUI(player.currentHP, player.maxHP, player.defense);
        }
        else
        {
            if (playerHealthBar != null) playerHealthBar.Hide();
        }

        if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP > 0)
        {
            EnemyBase enemy = enemyManager.currentEnemy;

            if (enemyHealthBar != null)
                enemyHealthBar.UpdateUI(enemy.currentHP, enemy.maxHP, enemy.defense);

            // 내 턴(PlayerInput)일 때만 인텐트 말풍선을 보여준다. UpdateUI()는 펀치 한 번마다
            // (PlayPendingActions 안에서) HP 갱신용으로 계속 호출되므로, 여기서 페이즈를 안 보면
            // 애니메이션 재생 중에도 펀치마다 말풍선이 다시 켜졌다 꺼졌다 한다.
            var isPlayerInputPhase = deckManager == null || deckManager.CurrentPhase == DeckManager.TurnPhase.PlayerInput;

            if (enemyIntentBubbleObj != null && enemyIntentBubble != null && isPlayerInputPhase)
            {
                // [추가] 엔딩 보스일 때는 전투 인텐트 말풍선을 아예 띄우지 않습니다.
                if (enemy.isEndingBoss)
                {
                    enemyIntentBubbleObj.SetActive(false);
                }
                else if (enemy is MotherDragon && mdLineAnnounced)
                {
                    enemyIntentBubbleObj.SetActive(false);
                }
                else
                {
                    enemyIntentBubbleObj.SetActive(true);

                    if (enemy is MotherDragon)
                        enemyIntentBubble.Setup(mdIntentString);
                    else
                        enemyIntentBubble.SetupIntent(enemyManager.GetIntentIcon(), enemyManager.GetIntentString(), enemyManager.GetIntentColor());

                    // 위치는 여기서 한 번 잡지 않고 LateUpdate가 매 프레임 갱신한다 - 적이
                    // 돌진했다 복귀하는 동안에도 말풍선이 따라가야 하기 때문이다.
                }
            }
            else if (enemyIntentBubbleObj != null && !isPlayerInputPhase)
            {
                enemyIntentBubbleObj.SetActive(false);
            }
        }
        else
        {
            if (enemyHealthBar != null) enemyHealthBar.Hide();
            if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);
        }

        CheckGameState();
    }

    private void LateUpdate()
    {
        // 인텐트 말풍선이 활성화되어 있고, 적이 화면에 존재할 때 매 프레임 위치를 갱신
        if (enemyIntentBubbleObj != null && enemyIntentBubbleObj.activeSelf)
        {
            if (enemyManager != null && enemyManager.currentEnemy != null)
            {
                if (SpeechBubbleManager.Instance != null)
                {
                    enemyIntentBubbleObj.GetComponent<RectTransform>().position =
                        SpeechBubbleManager.Instance.GetBubbleScreenPosition(enemyManager.currentEnemy.BubblePosition, false);
                }
            }
        }
    }

    void CheckGameState()
    {
        if (player == null || player.currentHP <= 0)
        {
            if (!isGameOver) ShowResult(ResultKind.Defeat);
        }
        else if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP <= 0)
        {
            if (!isEventTriggered)
            {
                isEventTriggered = true;

                // 엔딩 보스일 경우에만 대사 이벤트를 실행
                if (enemyManager.currentEnemy.isEndingBoss && eventManager != null)
                {
                    eventManager.StartEvent(true, 0);
                }
                else
                {
                    // 일반 적 및 중간보스 마더 드래곤은 이벤트 대사 없이 즉시 사망 처리
                    enemyManager.currentEnemy.gameObject.SetActive(false);

                    // 곧바로 승리 처리 (이후 StageManager가 보상 라운드를 엽니다)
                    ShowResult(ResultKind.Victory);
                }
            }
        }
    }

    /// <summary>
    /// 결과 화면을 띄운다. 제목과 이미지는 `ResultPanel.prefab`이 통째로 갖고 여기서는
    /// 수치만 넘긴다. 무엇을 입력해야 하는지는 `ResultInputHandler`가 손패 자리에 띄우는
    /// 명령 카드("다시하기"/"카드"/"타이틀")가 그대로 보여준다.
    /// </summary>
    public void ShowResult(ResultKind kind)
    {
        bool wasOver = isGameOver;
        var previousKind = lastResultKind;
        isGameOver = true;

        // ⚠️ OnBattleEnded가 여기서 StageManager의 보상 라운드를 열고 돌아온다.
        // 그래서 아래 ApplyResult는 "보상을 고르는 중"인 상태에서 그려지고, 선택이 끝나면
        // StageManager가 RefreshResult()를 불러 다시 그린다.
        lastResultKind = kind;

        // ⚠️ "처음 끝났을 때만"으로 막으면 안 된다. CheckGameState가 UpdateUI마다 ShowResult를
        // 다시 부르므로 걸러야 하는 건 맞지만, 그건 언제나 <b>같은 kind</b>다. kind가 바뀌는 건
        // 새로운 사건이라 반드시 알려야 한다 - 특히 마지막 스테이지는 승리(Victory)로 결과가
        // 이미 떠 있는 상태에서 보상을 고른 뒤 전체 클리어(GameClear)로 넘어가는데, wasOver만
        // 보면 그 전환에서 이벤트가 통째로 묻힌다. 그러면 DeckManager가 타이머를 멈추지도
        // 손패를 치우지도 않고, ResultInputHandler의 명령 카드 3장도 뜨지 않아 화면이 잠긴다.
        if (!wasOver || previousKind != kind)
            OnBattleEnded?.Invoke();

        ApplyResult(kind);
    }

    /// <summary>결과 화면을 지금 상태에 맞춰 다시 그린다. 보상 선택이 끝났을 때
    /// StageManager가 부른다 - 그제서야 "다음"을 칠 수 있으므로 안내도 그때 뜬다.</summary>
    public void RefreshResult()
    {
        if (isGameOver)
            ApplyResult(lastResultKind);
    }

    private void ApplyResult(ResultKind kind)
    {
        // 통계 추적은 결과 종류와 무관하게 여기서 멈춘다. 보상을 고르는 시간이 플레이 타임에
        // 들어가면 CPM이 실제보다 낮게 나온다. (예전엔 ShowStatisticsUI 안에 있었는데, 일반
        // 클리어에서 그 호출 자체가 사라지면서 이리로 올렸다 - 같이 없애면 시간이 계속 흐른다.)
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.StopTracking();

        // ⚠️ 일반 스테이지 클리어는 결과를 띄우지 않는다. 곧바로 보상 선택으로 이어지는데
        // 결과 창이 풀스크린이고 End Canvas(13)가 RewardCanvas(3)보다 위라, 띄우면 고를
        // 카드를 통째로 덮어버린다. 승리 표시는 보상 화면과 자동 진행이 대신한다.
        if (kind == ResultKind.Victory)
        {
            HideResultUI();
            return;
        }

        // ⚠️ 반대쪽 패널을 먼저 끈다. 마지막 스테이지는 Victory(숨김) -> GameClear(표시)로
        // 넘어가고 재시작도 같은 화면에서 시작하므로, 안 끄면 반대쪽이 켜진 채 겹칠 수 있다.
        HideResultUI();

        var view = PanelFor(kind);
        if (view == null)
        {
            Debug.LogWarning($"BattleManager: {FieldNameFor(kind)}가 없어 결과 화면을 띄울 수 없습니다.", this);
            return;
        }

        var stats = StatisticsManager.Instance;

        // 분모를 리터럴로 박으면 스테이지 수를 바꿨을 때 조용히 어긋난다.
        var totalStages = stageManager != null ? stageManager.totalStages : 0;

        view.Show(
            stats != null ? stats.highestStageReached : 0,
            totalStages,
            stats != null ? Mathf.RoundToInt(stats.GetCPM()) : 0,
            stats != null ? stats.validWordsUsed : 0,
            stats != null ? stats.totalDamageDealt : 0,
            stats != null ? stats.totalDamageTaken : 0,
            this,
            FieldNameFor(kind));
    }

    /// <summary>결과 종류에 맞는 화면. Victory는 결과를 띄우지 않으므로 여기 오지 않는다
    /// (<see cref="ApplyResult"/>가 앞에서 걸러낸다).</summary>
    private ResultPanelView PanelFor(ResultKind kind)
    {
        return kind == ResultKind.GameClear ? gameClearResult : defeatResult;
    }

    // 배선 누락 경고가 어느 인스펙터 칸인지 알려주도록 필드명을 넘긴다.
    private string FieldNameFor(ResultKind kind)
    {
        return kind == ResultKind.GameClear ? nameof(gameClearResult) : nameof(defeatResult);
    }

    /// <summary>결과 UI를 통째로 감춘다. 전투가 시작될 때와 일반 스테이지 클리어에서 쓴다.
    ///
    /// ⚠️ <b>둘 다 끈다.</b> 하나만 끄면 결과 종류가 바뀌는 경로(마지막 스테이지의
    /// Victory -> GameClear, 패배 후 "다시하기")에서 반대쪽이 켜진 채 남는다.</summary>
    private void HideResultUI()
    {
        // Unity는 [SerializeField]인 [Serializable] 클래스의 인스턴스를 항상 만들어 주지만,
        // 스크립트를 막 바꿔 아직 재직렬화되지 않은 순간에는 비어 있을 수 있다.
        defeatResult?.Hide();
        gameClearResult?.Hide();
    }

}
