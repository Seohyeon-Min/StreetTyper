using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;
    public StageManager stageManager; // Added reference to StageManager

    [Header("Player Reference")]
    public CharacterStats player;

    [Header("HP UI References")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI enemyHPText;

    [Header("Defense UI References")]
    public TextMeshProUGUI playerDefText;
    public TextMeshProUGUI enemyDefText;

    [Header("Intent UI Reference")]
    public TextMeshProUGUI enemyIntentText;

    [Header("Game Result UI")]
    public TextMeshProUGUI resultText;

    private bool isGameOver = false;

    void Update()
    {
        if (Keyboard.current == null) return;

        // Handle inputs when the game is over
        if (isGameOver)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                if (player == null || player.currentHP <= 0)
                {
                    stageManager.RestartStage();
                }
                else
                {
                    stageManager.NextStage();
                }
            }
            return;
        }

        // Action: Attack
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (player != null && enemyManager.currentEnemy != null)
            {
                enemyManager.currentEnemy.TakeDamage(player.power);
                UpdateUI();
            }
        }

        // Action: Defend
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (player != null)
            {
                player.AddDefense(player.power);
                UpdateUI();
            }
        }

        // Action: End Turn
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (player != null && enemyManager.currentEnemy != null)
            {
                enemyManager.ExecuteEnemyTurn(player);
                UpdateUI();
            }
        }
    }

    public void ResetBattle()
    {
        // Reset game over state and hide result text
        isGameOver = false;
        if (resultText != null) resultText.gameObject.SetActive(false);
        UpdateUI();
    }

    public void ShowGameClear()
    {
        isGameOver = true;
        if (resultText != null)
        {
            resultText.text = "ALL STAGES CLEARED!";
            resultText.gameObject.SetActive(true);
        }

        // Hide player UI
        if (playerHPText != null) playerHPText.gameObject.SetActive(false);
        if (playerDefText != null) playerDefText.gameObject.SetActive(false);
    }

    void UpdateUI()
    {
        // Update Player UI
        if (player != null && player.currentHP > 0)
        {
            playerHPText.gameObject.SetActive(true);
            playerDefText.gameObject.SetActive(true);

            playerHPText.text = "Player HP: " + player.currentHP + " / Power: " + player.power;
            playerDefText.text = "Shield: " + player.defense;
        }
        else
        {
            if (playerHPText != null) playerHPText.gameObject.SetActive(false);
            if (playerDefText != null) playerDefText.gameObject.SetActive(false);
        }

        // Update Enemy UI
        if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP > 0)
        {
            EnemyBase enemy = enemyManager.currentEnemy;

            enemyHPText.gameObject.SetActive(true);
            enemyDefText.gameObject.SetActive(true);
            if (enemyIntentText != null) enemyIntentText.gameObject.SetActive(true);

            enemyHPText.text = "Enemy HP: " + enemy.currentHP + " / Power: " + enemy.power;
            enemyDefText.text = "Shield: " + enemy.defense;

            if (enemyIntentText != null)
            {
                enemyIntentText.text = enemyManager.GetIntentString();
            }
        }
        else
        {
            if (enemyHPText != null) enemyHPText.gameObject.SetActive(false);
            if (enemyDefText != null) enemyDefText.gameObject.SetActive(false);
            if (enemyIntentText != null) enemyIntentText.gameObject.SetActive(false);
        }

        CheckGameState();
    }

    void CheckGameState()
    {
        // Check Player Death
        if (player == null || player.currentHP <= 0)
        {
            ShowResult("DEFEAT...\n\nPress '1' to Restart");
        }
        // Check Enemy Death
        else if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP <= 0)
        {
            ShowResult("VICTORY!\n\nPress '1' for Next Stage");
        }
    }

    void ShowResult(string message)
    {
        isGameOver = true;
        if (resultText != null)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(true);
        }
    }
}