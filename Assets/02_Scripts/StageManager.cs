using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [Header("Stage Settings")]
    public List<GameObject> enemyPrefabs;
    public GameObject motherDragonPrefab;
    public Transform enemySpawnPoint;

    [Tooltip("스테이지가 열리고 플레이어가 타이핑을 시작할 수 있을 때까지의 대기 시간(초)")]
    public float stageStartDelay = 2f;

    [Tooltip("총 스테이지 수")]
    public int totalStages = 8;

    [Header("References")]
    public BattleManager battleManager;
    public EnemyManager enemyManager;
    public CharacterStats player;
    public TimerManager timerManager;
    public InputManager inputManager;
    public WordUnlockManager wordUnlockManager;
    public CardSlotManager cardSlotManager;
    public WordChainManager wordChainManager;
    public PendingActionManager pendingActionManager;

    [Tooltip("적 HP 바의 위치 추종 컴포넌트. 적은 스테이지마다 새로 스폰되므로 여기서 대상을 넘겨준다.")]
    public WorldAnchoredUI enemyHealthBarAnchor;

    public StatusEffectManager statusEffectManager;

    [Tooltip("런이 시작될 때 어썸 누적 횟수를 되돌리기 위해 참조한다.")]
    public SkillResolver skillResolver;

    [Tooltip("클리어 보상 후보를 화면에 펼쳐 보여준다.")]
    public RewardCardView rewardCardView;

    [Tooltip("보상 후보 중 하나를 타이핑으로 고르게 하는 수신자. 비워두면 후보를 전부 지급하는 " +
             "옛 동작으로 떨어진다(경고를 남긴다).")]
    public RewardInputHandler rewardInputHandler;

    [Tooltip("보상 선택이 끝나고 다음 스테이지가 열리기까지의 대기 시간(초). 승리 화면에서 " +
             "'다음'을 칠 필요가 없어진 대신, 방금 얻은 카드와 VICTORY를 볼 짧은 여유를 준다. " +
             "0으로 두어도 최소 한 프레임은 기다린다.")]
    public float rewardAdvanceDelay = 0.6f;

    [Header("UI")]
    public TMPro.TextMeshProUGUI stageStartText;
    public TMPro.TextMeshProUGUI currentStageText;

    private int currentBattleIndex = 0;
    private int totalBattles = 10;
    private GameObject currentEnemyObject;
    private Coroutine startRoutine;
    private Coroutine advanceRoutine;

    /// <summary>보상이 끝나 다음 스테이지로 자동으로 넘어가는 중인가. `BattleManager`가
    /// 결과 화면에 "다음" 안내를 띄울지 정하는 데 쓴다 - 칠 필요가 없는 단어를 안내하면
    /// 플레이어만 헷갈린다.</summary>
    public bool IsAdvancingAutomatically => advanceRoutine != null;

    private void OnEnable()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded += HandleBattleEnded;

        if (rewardInputHandler != null)
            rewardInputHandler.OnSelectionFinished += HandleRewardSelectionFinished;
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded -= HandleBattleEnded;

        if (rewardInputHandler != null)
            rewardInputHandler.OnSelectionFinished -= HandleRewardSelectionFinished;
    }

    void Start()
    {
        // 사전은 비어 있는 상태로 시작하므로, 첫 스테이지를 열기 전에 시작 단어부터 채워준다.
        if (wordUnlockManager != null)
            wordUnlockManager.GrantStartingWords();

        // 여기가 런의 시작점이다 - 어썸 누적 횟수도 같이 되돌린다.
        if (skillResolver != null)
            skillResolver.ResetRun();

        LoadStage(currentBattleIndex);
    }

    public void LoadStage(int stageIndex)
    {
        currentBattleIndex = stageIndex;
        // 총 10번의 전투(인덱스 0~9)를 모두 마치고 인덱스 10에 도달하면 게임 클리어
        if (currentBattleIndex >= totalBattles)
        {
            battleManager.ShowGameClear();
            return;
        }


        // 인덱스 4(5번째 전투 = 4스테이지 클리어 후)와 인덱스 9(10번째 전투 = 8스테이지 클리어 후)를 보스전으로 설정
        bool isBossBattle = (currentBattleIndex == 4 || currentBattleIndex == 9);

        if (SoundManager.Instance != null)
        {
            if (isBossBattle)
            {
                SoundManager.Instance.PlayBossBGM(); // 보스전이면 보스 브금 재생
            }
            else
            {
                SoundManager.Instance.PlayBattleBGM(); // 일반 전투면 일반 브금 재생 (이미 재생 중이면 알아서 무시됨)
            }
        }

        // 실제 UI 및 통계에 표시될 스테이지 번호 계산 (보스는 카운트 제외)
        int displayStage = currentBattleIndex + 1;
        if (currentBattleIndex >= 4) displayStage -= 1; // 첫 번째 보스전 및 그 이후 인덱스 보정
        if (currentBattleIndex >= 9) displayStage -= 1; // 두 번째 보스전 및 그 이후 인덱스 보정

        // [추가] 최고 도달 스테이지 기록 갱신
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.UpdateHighestStage(currentBattleIndex + 1);

        if (currentStageText != null)
        {
            if (isBossBattle)
                currentStageText.text = "MOMMY";
            else
                currentStageText.text = $"STAGE {displayStage}";
        }

        if (currentEnemyObject != null)
        {
            Destroy(currentEnemyObject);
        }

        // [수정] 프리팹 결정 로직
        GameObject prefabToSpawn;
        if (isBossBattle && motherDragonPrefab != null)
        {
            prefabToSpawn = motherDragonPrefab;
        }
        else
        {
            if (enemyPrefabs != null && enemyPrefabs.Count > 0)
            {
                prefabToSpawn = enemyPrefabs[0]; // 일반 스테이지는 무조건 첫 번째 프리팹 사용
            }
            else
            {
                Debug.LogError("StageManager: Enemy Prefabs 리스트가 비어있습니다! 인스펙터를 확인하세요.");
                return;
            }
        }

        // [핵심] 기존 enemyPrefabs[currentBattleIndex] 대신 prefabToSpawn 변수로 생성!
        currentEnemyObject = Instantiate(prefabToSpawn, enemySpawnPoint.position, Quaternion.identity);

        EnemyBase newEnemyBase = currentEnemyObject.GetComponent<EnemyBase>();

        // [추가] 생성 직후 스탯 스케일링 적용
        newEnemyBase.ApplyScaling(displayStage - 1);

        enemyManager.currentEnemy = newEnemyBase;
        enemyManager.GenerateNextAction();

        // HP 바 연결
        if (enemyHealthBarAnchor != null)
            enemyHealthBarAnchor.Bind(currentEnemyObject.transform);
        else
            Debug.LogWarning("StageManager: enemyHealthBarAnchor가 할당되지 않았습니다.", this);

        if (player != null)
        {
            player.defense = 0;
        }

        battleManager.ResetBattle();

        if (timerManager != null)
            timerManager.ResetToFull();

        if (inputManager != null)
        {
            inputManager.DisableInput();
            inputManager.ClearInput();
        }

        if (wordChainManager != null)
            wordChainManager.ClearChain();

        if (pendingActionManager != null)
            pendingActionManager.Clear();

        // 이전 스테이지에서 세다 만 턴 누적이 새 스테이지 첫 턴으로 넘어가지 않게 한다
        // (퍼펙트/니킥/춉/박치기가 읽는 값이다). 어썸의 런 누적은 여기서 건드리지 않는다.
        if (skillResolver != null)
            skillResolver.ResetTurn();

        if (statusEffectManager != null)
            statusEffectManager.ClearAll();

        if (rewardCardView != null)
            rewardCardView.Clear();

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        // 자동 진행 대기 중에 플레이어가 "다음"을 쳐서 먼저 넘어왔을 수 있다. 그대로 두면
        // 남은 코루틴이 뒤늦게 NextStage()를 한 번 더 불러 스테이지를 하나 건너뛴다.
        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        if (stageStartText != null)
        {
            if (isBossBattle)
                stageStartText.text = "MOMMY DRAGON";
            else
                stageStartText.text = $"STAGE {displayStage}\nSTART!";

            stageStartText.gameObject.SetActive(true);
        }

        startRoutine = StartCoroutine(BeginStageAfterDelay());
    }

    public void RestartStage()
    {
        if (player != null)
        {
            player.currentHP = player.maxHP;
            player.defense = 0;
            player.gameObject.SetActive(true);
            LoadStage(currentBattleIndex);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // 적이 등장한 뒤 잠깐 두었다가 플레이어 턴을 연다 - 적 턴 이후의 대기와 같은 목적이다.
    private IEnumerator BeginStageAfterDelay()
    {
        yield return new WaitForSeconds(stageStartDelay);

        if (stageStartText != null)
        {
            stageStartText.gameObject.SetActive(false);
        }

        // [추가된 부분] 대기하는 동안 게임 오버가 되었다면 (예: 킬스위치 즉사) 더 이상 진행하지 않음
        if (battleManager != null && battleManager.IsGameOver)
        {
            startRoutine = null;
            yield break; // 여기서 코루틴을 강제 종료하여 타이머가 다시 켜지는 것을 막습니다.
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.StartTracking();

        // 손패는 스테이지마다 새로 뽑는다. 안 그러면 이전 스테이지에서 들고 있던 카드가 그대로 남는다.
        if (cardSlotManager != null)
            cardSlotManager.RefillAll();

        // 이전 턴이 타이머 만료(HandleTimeExpired)로 끝났다면 DisableInput()이 걸려 있고,
        // 그 직후 패배했다면 EnableInput()이 한 번도 안 불렸을 수 있다 - 여기서 확실히 켠다.
        if (inputManager != null)
            inputManager.EnableInput();

        if (timerManager != null)
            timerManager.RestartTurn();

        startRoutine = null;
    }

    // 스테이지 클리어 지점. 결과 화면에서 "다음"을 타이핑했을 때 ResultInputHandler가 부른다.
    // 보상은 승리가 확정된 순간(HandleBattleEnded)에 이미 지급되고 화면에도 떠 있으므로,
    // 여기서는 그 카드를 치우고 다음 스테이지를 여는 일만 한다.
    public void NextStage()
    {
        LoadStage(currentBattleIndex + 1);
    }

    // 적 HP가 0이 되어 승패가 갈리는 순간 호출된다(BattleManager.OnBattleEnded).
    // 보상 후보를 펼쳐 플레이어가 하나를 고르게 한다 - 다 고르기 전에는 "다음"이 먹지 않는다
    // (RewardInputHandler가 결과 화면보다 높은 우선순위로 입력을 가져가기 때문이다).
    private void HandleBattleEnded()
    {
        // 패배에는 보상도 자동 진행도 없다 - 플레이어가 "다시"를 쳐서 재도전한다.
        if (player == null || player.currentHP <= 0)
            return;

        // ⚠️ wordUnlockManager가 없다고 여기서 리턴하면 안 된다. 그 경우에도 BeginRewardRound가
        // FinishReward로 빠져 자동 진행을 걸어주는데, 여기서 끊으면 보상도 자동 진행도 없이
        // 승리 화면에 멈춘다(승리에는 "다음" 안내를 띄우지 않으므로 칠 것도 없다).
        BeginRewardRound();
    }

    // 보상 한 라운드. 럭키가 쌓여 있으면 선택이 끝난 뒤 여기로 다시 들어온다.
    private void BeginRewardRound()
    {
        // 지금은 호출부 두 곳 모두 null을 걸러내고 들어오지만, 라운드가 여러 경로로 열리게 된
        // 뒤라 여기서도 막아둔다.
        if (wordUnlockManager == null)
        {
            FinishReward();
            return;
        }

        var candidates = wordUnlockManager.RollRewardCandidates();

        // 미보유 단어가 다 떨어졌다. 보상 창을 띄우지 않고 곧바로 결과 화면으로 넘긴다 -
        // 여기서 멈추면 플레이어가 "다음"을 칠 수도 없어 진행이 막힌다.
        if (candidates == null || candidates.Count == 0)
        {
            FinishReward();
            return;
        }

        if (rewardCardView != null)
            rewardCardView.Show(candidates);

        if (rewardInputHandler != null)
        {
            rewardInputHandler.BeginSelection(candidates);
            return;
        }

        // 폴백: 고를 수단이 없으면 후보를 전부 지급하는 옛 동작으로 떨어진다.
        // 조용히 보상이 증발하는 것보다는 낫다.
        Debug.LogWarning("StageManager: rewardInputHandler가 연결되지 않아 보상을 고를 수 없습니다. " +
                         "후보를 전부 지급하는 예전 동작으로 대체합니다. 씬 인스턴스에서 연결하세요.", this);

        for (var i = 0; i < candidates.Count; i++)
            wordUnlockManager.ConfirmReward(candidates[i]);

        FinishReward();
    }

    // 한 라운드가 끝났다(골랐든 넘겼든). 럭키로 열린 라운드가 남았으면 한 번 더 띄운다.
    private void HandleRewardSelectionFinished()
    {
        if (wordUnlockManager != null && wordUnlockManager.TryConsumeBonusRound())
        {
            BeginRewardRound();
            return;
        }

        FinishReward();
    }

    // 보상이 완전히 끝났다. 카드를 치우고 곧바로 다음 스테이지로 넘어간다 - 승리 화면에서
    // "다음"을 칠 필요가 없다.
    //
    // ⚠️ 여기서 NextStage()를 바로 부르면 안 된다. 이 경로는 BattleManager.ShowResult 안의
    //    OnBattleEnded에서 시작될 수 있는데, 그 호출은 우리가 돌아간 뒤에 이어서 결과 화면을
    //    그린다 - 스테이지를 먼저 갈아끼우면 그 위에 VICTORY 화면이 덮인다. 코루틴으로 최소
    //    한 프레임 미뤄 그 호출을 완전히 빠져나온 뒤에 넘긴다.
    private void FinishReward()
    {
        if (rewardCardView != null)
            rewardCardView.Clear();

        if (advanceRoutine != null)
            StopCoroutine(advanceRoutine);

        advanceRoutine = StartCoroutine(AdvanceAfterReward());

        // 자동 진행이 걸린 상태로 결과 화면을 다시 그린다 - 그래야 "다음" 안내가 뜨지 않는다.
        // (보상 후보가 아예 없어 선택 창을 건너뛴 경우, 이게 없으면 안내가 잠깐 깜빡인다.)
        if (battleManager != null)
            battleManager.RefreshResult();
    }

    private IEnumerator AdvanceAfterReward()
    {
        // Time.deltaTime 기반이라 일시정지(timeScale = 0) 중에는 멈춰 있는다 - 프로젝트의
        // 다른 대기와 같은 규칙이다.
        yield return new WaitForSeconds(rewardAdvanceDelay);

        // NextStage -> LoadStage가 이 코루틴을 멈추려 들기 전에 먼저 비운다.
        advanceRoutine = null;
        NextStage();
    }
}