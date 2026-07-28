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

    private int currentStageIndex = 0;
    private GameObject currentEnemyObject;

    void Start()
    {
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
    }

    public void NextStage()
    {
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