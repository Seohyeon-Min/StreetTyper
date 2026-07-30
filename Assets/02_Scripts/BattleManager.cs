using System;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;
    public StageManager stageManager;

    [Header("Player Reference")]
    public CharacterStats player;

    [Header("Health Bar UI")]
    public HealthBarUI playerHealthBar;
    public HealthBarUI enemyHealthBar;

    [Header("Game Result UI")]
    public TextMeshProUGUI resultText;

    [Header("Duration")]
    public float actionBubbleDuration = 1.0f;

    private bool isGameOver = false;

    private GameObject enemyIntentBubbleObj;
    private SpeechBubble enemyIntentBubble;

    public event Action OnBattleEnded;

    void Start()
    {
        if (resultText != null) resultText.gameObject.SetActive(false);

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

        // 기존 숫자키 배틀 디버그
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (enemyManager != null && enemyManager.currentEnemy != null)
            {
                enemyManager.currentEnemy.currentHP -= 10;
                if (enemyManager.currentEnemy.currentHP < 0)
                    enemyManager.currentEnemy.currentHP = 0;

                OnPlayerActionResolved("Attack 10!");
            }
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (player != null)
            {
                player.defense += 10;
                OnPlayerActionResolved("Defense 10!");
            }
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            ExecuteEnemyTurn();
        }

        // ★ 실제 캐릭터 위치 기반 말풍선 오프셋 디버그 (F1, F2) ★
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            if (player != null && SpeechBubbleManager.Instance != null)
            {
                SpeechBubbleManager.Instance.ShowBubble("Player Pos Test!", player.transform.position, true, 2.0f);
            }
        }

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            if (enemyManager != null && enemyManager.currentEnemy != null && SpeechBubbleManager.Instance != null)
            {
                SpeechBubbleManager.Instance.ShowBubble("Enemy Pos Test!", enemyManager.currentEnemy.transform.position, false, 2.0f);
            }
        }
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

    public void ExecuteEnemyTurn()
    {
        if (isGameOver) return;

        if (player != null && enemyManager != null && enemyManager.currentEnemy != null)
        {
            enemyManager.ExecuteEnemyTurn(player);
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

        if (playerHealthBar != null) playerHealthBar.Hide();
        if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);
    }

    void UpdateUI()
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

            if (enemyIntentBubbleObj != null && enemyIntentBubble != null)
            {
                enemyIntentBubbleObj.SetActive(true);
                enemyIntentBubble.Setup(enemyManager.GetIntentString(), false);

                if (SpeechBubbleManager.Instance != null)
                {
                    enemyIntentBubbleObj.GetComponent<RectTransform>().position =
                        SpeechBubbleManager.Instance.GetBubbleScreenPosition(enemy.transform.position, false);
                }
            }
        }
        else
        {
            if (enemyHealthBar != null) enemyHealthBar.Hide();
            if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);
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