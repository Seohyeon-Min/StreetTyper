using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [Header("Stage Settings")]
    public List<GameObject> enemyPrefabs;
    public Transform enemySpawnPoint;

    [Header("References")]
    public BattleManager battleManager;
    public EnemyManager enemyManager;
    public CharacterStats player;
    public TimerManager timerManager;
    public InputManager inputManager;
    public WordUnlockManager wordUnlockManager;

    private int currentStageIndex = 0;
    private GameObject currentEnemyObject;

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

        // 새 스테이지는 플레이어 HP/방어도가 리셋되는 것과 마찬가지로 타이머도 깨끗하게 다시 시작한다.
        if (timerManager != null)
            timerManager.RestartTurn();

        // 이전 턴이 타이머 만료(HandleTimeExpired)로 끝났다면 DisableInput()이 걸려있고,
        // 그 직후 패배했다면 EnableInput()이 한 번도 안 불렸을 수 있다 - 스테이지가 새로
        // 시작될 땐 무조건 입력이 켜져 있어야 하므로 여기서 확실히 켠다.
        if (inputManager != null)
            inputManager.EnableInput();
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