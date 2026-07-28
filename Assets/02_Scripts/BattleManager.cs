using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;
    public StageManager stageManager;

    [Header("Player Reference")]
    public CharacterStats player;

    [Header("HP UI References")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI enemyHPText;
    public Slider playerHPBar;
    public Slider enemyHPBar;

    [Header("Defense UI References")]
    public TextMeshProUGUI playerDefText;
    public TextMeshProUGUI enemyDefText;

    [Header("Speech Bubble Prefabs")]
    public GameObject playerSpeechBubblePrefab;
    public GameObject enemySpeechBubblePrefab;
    public Transform canvasTransform;

    // Variables to store the instantiated objects and texts
    private GameObject playerSpeechBubble;
    private TextMeshProUGUI playerActionText;

    private GameObject enemySpeechBubble;
    private TextMeshProUGUI enemyIntentText;

    [Header("Game Result UI")]
    public TextMeshProUGUI resultText;

    private bool isGameOver = false;

    void Start()
    {
        if (resultText != null) resultText.gameObject.SetActive(false);

        // Instantiate Player Speech Bubble
        if (playerSpeechBubblePrefab != null && canvasTransform != null)
        {
            playerSpeechBubble = Instantiate(playerSpeechBubblePrefab, canvasTransform);
            playerActionText = playerSpeechBubble.GetComponentInChildren<TextMeshProUGUI>();
            playerSpeechBubble.SetActive(false);
        }

        // Instantiate Enemy Speech Bubble
        if (enemySpeechBubblePrefab != null && canvasTransform != null)
        {
            enemySpeechBubble = Instantiate(enemySpeechBubblePrefab, canvasTransform);
            enemyIntentText = enemySpeechBubble.GetComponentInChildren<TextMeshProUGUI>();
            enemySpeechBubble.SetActive(false);
        }

        if (enemyManager != null)
        {
            enemyManager.GenerateNextAction();
        }
        UpdateUI();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

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

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (player != null && enemyManager.currentEnemy != null)
            {
                StartCoroutine(ShowPlayerActionBubble("Take This!"));
                enemyManager.currentEnemy.TakeDamage(player.power);
                UpdateUI();
            }
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (player != null)
            {
                StartCoroutine(ShowPlayerActionBubble("Defense!"));
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

    // Coroutine to show the player's speech bubble temporarily
    IEnumerator ShowPlayerActionBubble(string message)
    {
        if (playerSpeechBubble != null && playerActionText != null)
        {
            playerActionText.text = message;
            playerSpeechBubble.SetActive(true);

            yield return new WaitForSeconds(1.0f);

            playerSpeechBubble.SetActive(false);
        }
    }

    public void ResetBattle()
    {
        isGameOver = false;
        if (resultText != null) resultText.gameObject.SetActive(false);
        if (playerSpeechBubble != null) playerSpeechBubble.SetActive(false);
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

        if (playerHPText != null) playerHPText.gameObject.SetActive(false);
        if (playerDefText != null) playerDefText.gameObject.SetActive(false);
        if (playerHPBar != null) playerHPBar.gameObject.SetActive(false);
        if (playerSpeechBubble != null) playerSpeechBubble.SetActive(false);
    }

    void UpdateUI()
    {
        // Update Player UI
        if (player != null && player.currentHP > 0)
        {
            if (playerHPText != null) playerHPText.gameObject.SetActive(true);
            if (playerDefText != null) playerDefText.gameObject.SetActive(true);
            if (playerHPBar != null) playerHPBar.gameObject.SetActive(true);

            if (playerHPText != null) playerHPText.text = "Player HP: " + player.currentHP + " / Power: " + player.power;
            if (playerDefText != null) playerDefText.text = "Shield: " + player.defense;

            if (playerHPBar != null)
            {
                playerHPBar.maxValue = player.maxHP;
                playerHPBar.value = player.currentHP;
            }
        }
        else
        {
            if (playerHPText != null) playerHPText.gameObject.SetActive(false);
            if (playerDefText != null) playerDefText.gameObject.SetActive(false);
            if (playerHPBar != null) playerHPBar.gameObject.SetActive(false);
            if (playerSpeechBubble != null) playerSpeechBubble.SetActive(false);
        }

        // Update Enemy UI
        if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP > 0)
        {
            EnemyBase enemy = enemyManager.currentEnemy;

            if (enemyHPText != null) enemyHPText.gameObject.SetActive(true);
            if (enemyDefText != null) enemyDefText.gameObject.SetActive(true);
            if (enemyHPBar != null) enemyHPBar.gameObject.SetActive(true);

            if (enemySpeechBubble != null) enemySpeechBubble.SetActive(true);
            if (enemyIntentText != null) enemyIntentText.gameObject.SetActive(true);

            if (enemyHPText != null) enemyHPText.text = "Enemy HP: " + enemy.currentHP + " / Power: " + enemy.power;
            if (enemyDefText != null) enemyDefText.text = "Shield: " + enemy.defense;

            if (enemyHPBar != null)
            {
                enemyHPBar.maxValue = enemy.maxHP;
                enemyHPBar.value = enemy.currentHP;
            }

            if (enemyIntentText != null)
            {
                enemyIntentText.text = enemyManager.GetIntentString();
            }
        }
        else
        {
            if (enemyHPText != null) enemyHPText.gameObject.SetActive(false);
            if (enemyDefText != null) enemyDefText.gameObject.SetActive(false);
            if (enemyHPBar != null) enemyHPBar.gameObject.SetActive(false);

            if (enemySpeechBubble != null) enemySpeechBubble.SetActive(false);
        }

        CheckGameState();
    }

    void CheckGameState()
    {
        if (player == null || player.currentHP <= 0)
        {
            ShowResult("DEFEAT...\n\nPress '1' to Restart");
        }
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