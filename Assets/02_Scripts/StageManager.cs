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

    [Tooltip("클리어 보상으로 얻은 단어 카드를 화면에 펼쳐 보여준다.")]
    public RewardCardView rewardCardView;

    private int currentStageIndex = 0;
    private GameObject currentEnemyObject;
    private Coroutine startRoutine;

    private void OnEnable()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded += HandleBattleEnded;
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded -= HandleBattleEnded;
    }

    void Start()
    {
        // 사전은 비어 있는 상태로 시작하므로, 첫 스테이지를 열기 전에 시작 단어부터 채워준다.
        if (wordUnlockManager != null)
            wordUnlockManager.GrantStartingWords();

        // 여기가 런의 시작점이다 - 어썸 누적 횟수도 같이 되돌린다.
        if (skillResolver != null)
            skillResolver.ResetRun();

        LoadStage(currentStageIndex);
    }

    public void LoadStage(int stageIndex)
    {
        currentStageIndex = stageIndex;

        // [추가] 최고 도달 스테이지 기록 갱신
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.UpdateHighestStage(currentStageIndex + 1);

        if (currentEnemyObject != null)
        {
            Destroy(currentEnemyObject);
        }

        // [수정] 8스테이지(인덱스 7) 클리어 시(즉, 9번째 스테이지 진입 시) 게임 종료
        // 주의: enemyPrefabs.Count가 아닌 totalStages(8)로 검사해야 합니다.
        if (currentStageIndex >= totalStages)
        {
            battleManager.ShowGameClear();
            return;
        }

        // [수정] 프리팹 결정 로직
        GameObject prefabToSpawn;

        // 4스테이지(인덱스 3) 또는 8스테이지(인덱스 7)이면 보스 스폰
        if ((currentStageIndex == 3 || currentStageIndex == 7) && motherDragonPrefab != null)
        {
            prefabToSpawn = motherDragonPrefab;
        }
        else
        {
            // 일반 스테이지는 무조건 첫 번째 프리팹(enemyPrefabs[0])을 재사용
            if (enemyPrefabs != null && enemyPrefabs.Count > 0)
            {
                prefabToSpawn = enemyPrefabs[0];
            }
            else
            {
                Debug.LogError("StageManager: Enemy Prefabs 리스트가 비어있습니다! 인스펙터를 확인하세요.");
                return;
            }
        }

        // [핵심] 기존 enemyPrefabs[currentStageIndex] 대신 prefabToSpawn 변수로 생성!
        currentEnemyObject = Instantiate(prefabToSpawn, enemySpawnPoint.position, Quaternion.identity);

        EnemyBase newEnemyBase = currentEnemyObject.GetComponent<EnemyBase>();

        // [추가] 생성 직후 스탯 스케일링 적용
        newEnemyBase.ApplyScaling(currentStageIndex);

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

        if (statusEffectManager != null)
            statusEffectManager.ClearAll();

        if (rewardCardView != null)
            rewardCardView.Clear();

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        startRoutine = StartCoroutine(BeginStageAfterDelay());
    }

    // 적이 등장한 뒤 잠깐 두었다가 플레이어 턴을 연다 - 적 턴 이후의 대기와 같은 목적이다.
    private IEnumerator BeginStageAfterDelay()
    {
        yield return new WaitForSeconds(stageStartDelay);

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
        LoadStage(currentStageIndex + 1);
    }

    // 적 HP가 0이 되어 승패가 갈리는 순간 호출된다(BattleManager.OnBattleEnded).
    // 결과 화면과 함께 이번 판에서 얻은 단어를 바로 펼쳐 보여준다 - 플레이어가 "다음"을 치기 전에
    // 무엇을 얻었는지 확인할 수 있어야 하기 때문이다.
    private void HandleBattleEnded()
    {
        // 패배에는 보상이 없다. 죽은 쪽이 플레이어면 여기서 끝.
        if (player == null || player.currentHP <= 0)
            return;

        if (wordUnlockManager == null)
            return;

        var reward = wordUnlockManager.GrantStageClearReward();

        if (rewardCardView != null && reward != null && reward.Count > 0)
            rewardCardView.Show(reward);
    }

    public void RestartStage()
    {
        if (player != null)
        {
            player.currentHP = player.maxHP;
            player.defense = 0;

            // Reactivate the player GameObject if it was disabled
            player.gameObject.SetActive(true);

            LoadStage(currentStageIndex);
        }
        else
        {
            // Fallback: Reload the entire scene if the player was completely destroyed
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}