using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [Header("Stage Settings")]
    public List<GameObject> enemyPrefabs;
    public Transform enemySpawnPoint;

    [Tooltip("스테이지가 열리고 플레이어가 타이핑을 시작할 수 있을 때까지의 대기 시간(초)")]
    public float stageStartDelay = 2f;

    [Header("References")]
    public BattleManager battleManager;
    public EnemyManager enemyManager;
    public CharacterStats player;
    public TimerManager timerManager;
    public InputManager inputManager;
    public WordUnlockManager wordUnlockManager;
    public CardSlotManager cardSlotManager;
    public WordChainManager wordChainManager;

    private int currentStageIndex = 0;
    private GameObject currentEnemyObject;
    private Coroutine startRoutine;

    void Start()
    {
        // 사전은 비어 있는 상태로 시작하므로, 첫 스테이지를 열기 전에 시작 단어부터 채워준다.
        if (wordUnlockManager != null)
            wordUnlockManager.GrantStartingWords();

        LoadStage(currentStageIndex);
    }

    public void LoadStage(int stageIndex)
    {
        currentStageIndex = stageIndex;

        if (currentEnemyObject != null)
        {
            Destroy(currentEnemyObject);
        }

        if (currentStageIndex >= enemyPrefabs.Count)
        {
            battleManager.ShowGameClear();
            return;
        }

        currentEnemyObject = Instantiate(enemyPrefabs[currentStageIndex], enemySpawnPoint.position, Quaternion.identity);
        EnemyBase newEnemyBase = currentEnemyObject.GetComponent<EnemyBase>();

        enemyManager.currentEnemy = newEnemyBase;
        enemyManager.GenerateNextAction();

        if (player != null)
        {
            player.defense = 0;
        }

        battleManager.ResetBattle();

        // 대기 시간 동안엔 타이머가 돌지도, 입력이 들어오지도 않아야 한다.
        // 둘 다 BeginStageAfterDelay가 끝에서 다시 연다.
        if (timerManager != null)
            timerManager.StopTimer();

        if (inputManager != null)
        {
            inputManager.DisableInput();
            inputManager.ClearInput();
        }

        // 이전 스테이지에서 쌓다 만 조합은 넘겨받지 않는다 - 입력창을 비우는 것과 같은 이유다.
        if (wordChainManager != null)
            wordChainManager.ClearChain();

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        startRoutine = StartCoroutine(BeginStageAfterDelay());
    }

    // 적이 등장한 뒤 잠깐 두었다가 플레이어 턴을 연다 - 적 턴 이후의 대기와 같은 목적이다.
    private IEnumerator BeginStageAfterDelay()
    {
        yield return new WaitForSeconds(stageStartDelay);

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

    // 스테이지 클리어 지점. BattleManager가 승리 후 1번 키 입력에서만 부르므로
    // 클리어 1회당 보상이 정확히 한 번 지급된다(RestartStage는 이 경로를 타지 않는다).
    public void NextStage()
    {
        if (wordUnlockManager != null)
            wordUnlockManager.GrantStageClearReward();

        LoadStage(currentStageIndex + 1);
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