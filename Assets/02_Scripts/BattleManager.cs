using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;

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

    void Start()
    {
        // Generate the very first action when battle starts
        if (enemyManager != null)
        {
            enemyManager.GenerateNextAction();
        }
        UpdateUI();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (player != null && enemyManager.currentEnemy != null)
            {
                enemyManager.currentEnemy.TakeDamage(player.power);
                UpdateUI();
            }
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (player != null)
            {
                player.AddDefense(player.power);
                UpdateUI();
            }
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (player != null && enemyManager.currentEnemy != null)
            {
                enemyManager.ExecuteEnemyTurn(player);
                UpdateUI();
            }
        }
    }

    void UpdateUI()
    {
        if (player != null)
        {
            playerHPText.text = "Player HP: " + player.currentHP + " / Power: " + player.power;
            playerDefText.text = "Shield: " + player.defense;
        }
        else
        {
            playerHPText.text = "Player Dead";
            playerDefText.text = "";
        }

        if (enemyManager != null && enemyManager.currentEnemy != null)
        {
            EnemyBase enemy = enemyManager.currentEnemy;
            enemyHPText.text = "Enemy HP: " + enemy.currentHP + " / Power: " + enemy.power;
            enemyDefText.text = "Shield: " + enemy.defense;

            // Show intent on UI
            if (enemyIntentText != null)
            {
                enemyIntentText.text = enemyManager.GetIntentString();
            }
        }
        else
        {
            enemyHPText.text = "Enemy Dead";
            enemyDefText.text = "";
            if (enemyIntentText != null) enemyIntentText.text = "";
        }
    }
}