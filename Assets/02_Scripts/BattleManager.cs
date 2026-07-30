using System;
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

    [Header("HP Bar Fill Images")]
    public Image playerHPFill;
    public Image enemyHPFill;

    [Header("Defense UI References")]
    public GameObject playerDefIcon;
    public GameObject enemyDefIcon;
    public TextMeshProUGUI playerDefText;
    public TextMeshProUGUI enemyDefText;

    [Header("Game Result UI")]
    public TextMeshProUGUI resultText;

    [Header("Duration")]
    public float actionBubbleDuration = 1.0f;

    private bool isGameOver = false;

    public event Action OnBattleEnded;

    void Start()
    {
        if (resultText != null) resultText.gameObject.SetActive(false);

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

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            ExecuteEnemyTurn();
        }
    }

    public void OnPlayerActionResolved(string bubbleText)
    {
        if (isGameOver)
            return;

        if (SpeechBubbleManager.Instance != null && player != null)
        {
            SpeechBubbleManager.Instance.ShowBubble(bubbleText, player.transform.position, true, actionBubbleDuration);
        }

        UpdateUI();
    }

    public void ExecuteEnemyTurn()
    {
        if (isGameOver)
            return;

        if (player != null && enemyManager != null && enemyManager.currentEnemy != null)
        {
            enemyManager.ExecuteEnemyTurn(player);

            if (SpeechBubbleManager.Instance != null)
            {
                SpeechBubbleManager.Instance.ShowBubble(enemyManager.GetIntentString(), enemyManager.currentEnemy.transform.position, false, actionBubbleDuration);
            }

            UpdateUI();
        }
    }

    public bool IsGameOver => isGameOver;

    public void ResetBattle()
    {
        isGameOver = false;
        if (resultText != null) resultText.gameObject.SetActive(false);
        UpdateUI();
    }

    public void ShowGameClear()
    {
        bool wasOver = isGameOver;
        isGameOver = true;
        if (!wasOver) OnBattleEnded?.Invoke();

        if (resultText != null)
        {
            resultText.text = "ALL STAGES CLEARED!";
            resultText.gameObject.SetActive(true);
        }

        if (playerHPText != null) playerHPText.gameObject.SetActive(false);
        if (playerHPBar != null) playerHPBar.gameObject.SetActive(false);
        if (playerDefIcon != null) playerDefIcon.SetActive(false);
    }

    void UpdateUI()
    {
        if (player != null && player.currentHP > 0)
        {
            if (playerHPText != null) playerHPText.gameObject.SetActive(true);
            if (playerHPBar != null)
            {
                playerHPBar.gameObject.SetActive(true);
                playerHPBar.maxValue = player.maxHP;
                playerHPBar.value = player.currentHP;
            }

            if (playerHPText != null) playerHPText.text = player.currentHP + " / " + player.maxHP;

            if (player.defense > 0)
            {
                if (playerDefIcon != null) playerDefIcon.SetActive(true);
                if (playerDefText != null)
                {
                    playerDefText.gameObject.SetActive(true);
                    playerDefText.text = player.defense.ToString();
                }
                if (playerHPFill != null) playerHPFill.color = Color.gray;
            }
            else
            {
                if (playerDefIcon != null) playerDefIcon.SetActive(false);
                if (playerDefText != null) playerDefText.gameObject.SetActive(false);
                if (playerHPFill != null) playerHPFill.color = Color.green;
            }
        }
        else
        {
            if (playerHPText != null) playerHPText.gameObject.SetActive(false);
            if (playerHPBar != null) playerHPBar.gameObject.SetActive(false);
            if (playerDefIcon != null) playerDefIcon.SetActive(false);
        }

        if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP > 0)
        {
            EnemyBase enemy = enemyManager.currentEnemy;

            if (enemyHPText != null) enemyHPText.gameObject.SetActive(true);
            if (enemyHPBar != null)
            {
                enemyHPBar.gameObject.SetActive(true);
                enemyHPBar.maxValue = enemy.maxHP;
                enemyHPBar.value = enemy.currentHP;
            }

            if (enemyHPText != null) enemyHPText.text = enemy.currentHP + " / " + enemy.maxHP;

            if (enemy.defense > 0)
            {
                if (enemyDefIcon != null) enemyDefIcon.SetActive(true);
                if (enemyDefText != null)
                {
                    enemyDefText.gameObject.SetActive(true);
                    enemyDefText.text = enemy.defense.ToString();
                }
                if (enemyHPFill != null) enemyHPFill.color = Color.gray;
            }
            else
            {
                if (enemyDefIcon != null) enemyDefIcon.SetActive(false);
                if (enemyDefText != null) enemyDefText.gameObject.SetActive(false);
                if (enemyHPFill != null) enemyHPFill.color = Color.red;
            }
        }
        else
        {
            if (enemyHPText != null) enemyHPText.gameObject.SetActive(false);
            if (enemyHPBar != null) enemyHPBar.gameObject.SetActive(false);
            if (enemyDefIcon != null) enemyDefIcon.SetActive(false);
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
        bool wasOver = isGameOver;
        isGameOver = true;
        if (!wasOver) OnBattleEnded?.Invoke();

        if (resultText != null)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(true);
        }
    }
}