using System;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;
    public StageManager stageManager;
    public EventManager eventManager;

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
    private bool isEventTriggered = false;

    private GameObject enemyIntentBubbleObj;
    private SpeechBubble enemyIntentBubble;

    // 엄마용 전투 전용 변수
    private int mdTurnCount = 0;
    private string mdIntentString = "어디 한번 실력을 보여보거라!";
    private int savedMDDamage = 0;

    //   추가: 대기열 상태 확인용 변수
    private bool isWaitingForDragonEnd = false;

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

        // 일시정지 중에는 디버그 키가 먹히면 안 된다 - 메뉴 뒤에서 스테이지가 넘어가 버린다.
        // PauseManager를 참조하지 않고 timeScale을 보는 이유: BattleManager는 프리팹이고
        // PauseManager는 씬 오브젝트라, 참조로 엮으면 씬 인스턴스 오버라이드가 하나 더 생긴다.
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

        if (isGameOver)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                if (player == null || player.currentHP <= 0) stageManager.RestartStage();
                else stageManager.NextStage();
            }
            return;
        }

        // 이벤트 중이거나 엄마용 대기시간 중일 때는 키보드 입력 차단
        if (eventManager != null && eventManager.IsEventActive) return;
        if (isWaitingForDragonEnd) return; //   추가됨

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
            if (enemyManager.currentEnemy.isMotherDragon)
            {
                mdTurnCount++;
                if (mdTurnCount == 1) mdIntentString = "제법이구나!";
                else if (mdTurnCount == 2) mdIntentString = "조금 더 힘을 끌어내 보거라!";
                else if (mdTurnCount >= 3)
                {
                    mdIntentString = "훌륭하다. 여기까지 하마!";

                    //   수정됨: 즉시 죽이지 않고 입력 차단 후 1.5초 대기 함수 실행
                    isWaitingForDragonEnd = true;
                    Invoke("FinishMotherDragonBattle", 1.5f);
                }

                if (SpeechBubbleManager.Instance != null)
                {
                    SpeechBubbleManager.Instance.ShowBubble(mdIntentString, enemyManager.currentEnemy.transform.position, false, actionBubbleDuration);
                }
            }
            else
            {
                enemyManager.ExecuteEnemyTurn(player);

                if (SpeechBubbleManager.Instance != null)
                {
                    SpeechBubbleManager.Instance.ShowBubble(enemyManager.GetIntentString(), enemyManager.currentEnemy.transform.position, false, actionBubbleDuration);
                }
            }

            UpdateUI();
        }
    }

    //   추가된 함수: 1.5초 뒤에 실행되어 전투를 마무리합니다.
    private void FinishMotherDragonBattle()
    {
        if (enemyManager != null && enemyManager.currentEnemy != null)
        {
            savedMDDamage = enemyManager.currentEnemy.maxHP - enemyManager.currentEnemy.currentHP;
            enemyManager.currentEnemy.currentHP = 0;
            isWaitingForDragonEnd = false;

            UpdateUI(); // 이 순간 CheckGameState()가 발동하면서 이벤트 창으로 넘어감
        }
    }

    public bool IsGameOver => isGameOver;

    public void ResetBattle()
    {
        isGameOver = false;
        isEventTriggered = false;
        mdTurnCount = 0;
        mdIntentString = "어디 한번 실력을 보여보거라!";
        savedMDDamage = 0;
        isWaitingForDragonEnd = false; //   추가됨

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

    public void UpdateUI()
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

                string currentIntent = enemy.isMotherDragon ? mdIntentString : enemyManager.GetIntentString();
                enemyIntentBubble.Setup(currentIntent, false);

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
            if (!isGameOver) ShowResult("DEFEAT...\n\nPress '1' to Restart");
        }
        else if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP <= 0)
        {
            if (!isEventTriggered)
            {
                isEventTriggered = true;

                bool isMD = enemyManager.currentEnemy.isMotherDragon;
                int healAmount = 0;

                if (isMD)
                {
                    if (savedMDDamage == 0) savedMDDamage = enemyManager.currentEnemy.maxHP;
                    healAmount = savedMDDamage;
                }

                enemyManager.currentEnemy.gameObject.SetActive(false);

                if (eventManager != null)
                {
                    eventManager.StartEvent(isMD, healAmount);
                }
                else
                {
                    ShowResult("VICTORY!\n\nPress '1' for Next Stage");
                }
            }
        }
    }

    public void ShowResult(string message)
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