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

    [Header("HP Bar Fill Images (For Color Change)")]
    public Image playerHPFill;
    public Image enemyHPFill;

    [Header("Defense UI References")]
    public GameObject playerDefIcon; 
    public GameObject enemyDefIcon; 
    public TextMeshProUGUI playerDefText;
    public TextMeshProUGUI enemyDefText;

    [Header("Speech Bubble Prefabs")]
    public GameObject playerSpeechBubblePrefab;
    public GameObject enemySpeechBubblePrefab;
    public Transform canvasTransform;

    private GameObject playerSpeechBubble;
    private TextMeshProUGUI playerActionText;

    private GameObject enemySpeechBubble;
    private TextMeshProUGUI enemyIntentText;

    [Header("Game Result UI")]
    public TextMeshProUGUI resultText;

    [Header("연출 시간")]
    [Tooltip("공격 말풍선이 떠 있는 시간(초)")]
    public float actionBubbleDuration = 1.0f;

    private bool isGameOver = false;

    void Start()
    {
        if (resultText != null) resultText.gameObject.SetActive(false);

        if (playerSpeechBubblePrefab != null && canvasTransform != null)
        {
            playerSpeechBubble = Instantiate(playerSpeechBubblePrefab, canvasTransform);
            playerActionText = playerSpeechBubble.GetComponentInChildren<TextMeshProUGUI>();
            playerSpeechBubble.SetActive(false);
        }

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

        // 공격/방어는 이제 타이핑(체인 완성)이 담당한다 - OnPlayerActionResolved를 통해 들어온다.
        // 적 턴 수동 실행만 디버그용으로 남긴다.
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            ExecuteEnemyTurn();
        }
    }

    IEnumerator ShowPlayerActionBubble(string message)
    {
        if (playerSpeechBubble != null && playerActionText != null)
        {
            playerActionText.text = message;
            playerSpeechBubble.SetActive(true);

            yield return new WaitForSeconds(actionBubbleDuration);

            playerSpeechBubble.SetActive(false);
        }
    }

    // 체인이 완성되어 CombatManager가 효과를 적용한 직후 호출된다. 타이머가 도는 동안 여러 번
    // 호출될 수 있으므로 여기서는 턴을 끝내지 않는다 - 말풍선/UI 갱신만 한다.
    // bubbleText: 스킬 이름이 아니라 방금 적용된 공격력/방어력 수치.
    public void OnPlayerActionResolved(string bubbleText)
    {
        if (isGameOver)
            return;

        StartCoroutine(ShowPlayerActionBubble(bubbleText));
        UpdateUI();
    }

    // 타이머가 0이 되어 플레이어 턴이 끝났을 때 호출된다(DeckManager.HandleTimeExpired 경유).
    public void ExecuteEnemyTurn()
    {
        if (isGameOver)
            return;

        if (player != null && enemyManager != null && enemyManager.currentEnemy != null)
        {
            enemyManager.ExecuteEnemyTurn(player);
            UpdateUI();
        }
    }

    // DeckManager가 타이머 만료 처리 도중(적 턴 전후) 전투가 이미 끝났는지 확인할 때 쓴다.
    public bool IsGameOver => isGameOver;

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
        if (playerHPBar != null) playerHPBar.gameObject.SetActive(false);
        if (playerDefIcon != null) playerDefIcon.SetActive(false);
        if (playerSpeechBubble != null) playerSpeechBubble.SetActive(false);
    }

    void UpdateUI()
    {
        // Update Player UI
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

            // Player Defense Logic
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
            if (playerSpeechBubble != null) playerSpeechBubble.SetActive(false);
        }

        // Update Enemy UI
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

            if (enemySpeechBubble != null) enemySpeechBubble.SetActive(true);
            if (enemyIntentText != null)
            {
                enemyIntentText.gameObject.SetActive(true);
                enemyIntentText.text = enemyManager.GetIntentString();
            }

            if (enemyHPText != null) enemyHPText.text = enemy.currentHP + " / " + enemy.maxHP;

            // Enemy Defense Logic
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