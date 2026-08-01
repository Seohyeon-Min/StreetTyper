using System;
using UnityEngine;
using TMPro;
using FMODUnity;
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
    [Tooltip("결과 화면 안내 문구('다음'/'다시')를 만들 때 참조한다.")]
    public ResultInputHandler resultInputHandler;

    [Header("Statistics Result UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI statsText;

    [Header("Duration")]
    public float actionBubbleDuration = 1.0f;

    private bool isGameOver = false;
    private bool isEventTriggered = false;

    private GameObject enemyIntentBubbleObj;
    private SpeechBubble enemyIntentBubble;

    // 엄마용 전투 전용 변수
    private int mdTurnCount = 0;
    private string mdIntentString = MotherDragonLine(0);
    private int savedMDDamage = 0;

    // 마더 드래곤이 턴마다 하는 말. 스파링 연출이라 순서가 정해져 있다.
    // 0=시작, 1=1턴 뒤, 2=2턴 뒤, 3=마무리
    private static string MotherDragonLine(int index)
    {
        if (LanguageSettings.IsEnglish)
        {
            return index switch
            {
                1 => "Not bad!",
                2 => "Draw out more of your power!",
                3 => "Splendid. That will do.",
                _ => "Come, show me what you can do!"
            };
        }

        return index switch
        {
            1 => "제법이구나!",
            2 => "조금 더 힘을 끌어내 보거라!",
            3 => "훌륭하다. 여기까지 하마!",
            _ => "어디 한번 실력을 보여보거라!"
        };
    }

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
        // 여기서 예외가 나면 아래 UpdateUI()까지 막혀 첫 프레임에 HP가 표시되지 않는다.
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayBattleBGM();

        UpdateUI();
    }

    void Update()
    {
        // 실제 출시(Release) 빌드에서는 컴파일되지 않도록 안전장치 추가
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current == null) return;

        // 숫자 0 누르면 플레이어 즉사
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            if (player != null && player.currentHP > 0)
            {
                Debug.Log("[DEBUG] 킬스위치 발동: 플레이어 사망");
                // 방어력을 무시(true)하고 현재 체력만큼 데미지를 줍니다.
                player.TakeDamage(player.currentHP, true); 
                UpdateUI();
            }
        }

        // 숫자 9 누르면 현재 적 즉사
        if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            if (enemyManager != null && enemyManager.currentEnemy != null && enemyManager.currentEnemy.currentHP > 0)
            {
                Debug.Log("[DEBUG] 킬스위치 발동: 적 사망");
                // 방어력을 무시(true)하고 현재 체력만큼 데미지를 줍니다.
                enemyManager.currentEnemy.TakeDamage(enemyManager.currentEnemy.currentHP, true); 
                UpdateUI();
            }
        }
#endif
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
                if (mdTurnCount == 1) mdIntentString = MotherDragonLine(1);
                else if (mdTurnCount == 2) mdIntentString = MotherDragonLine(2);
                else if (mdTurnCount >= 3)
                {
                    mdIntentString = MotherDragonLine(3);

                    //   수정됨: 즉시 죽이지 않고 입력 차단 후 1.5초 대기 함수 실행
                    isWaitingForDragonEnd = true;
                    Invoke("FinishMotherDragonBattle", 1.5f);
                }

                if (SpeechBubbleManager.Instance != null)
                {
                    SpeechBubbleManager.Instance.ShowBubble(mdIntentString, enemyManager.currentEnemy.BubblePosition, false, actionBubbleDuration);
                }
            }
            else
            {
                enemyManager.ExecuteEnemyTurn(player);

                if (SpeechBubbleManager.Instance != null)
                {
                    SpeechBubbleManager.Instance.ShowBubble(enemyManager.GetIntentString(), enemyManager.currentEnemy.BubblePosition, false, actionBubbleDuration);
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
        mdIntentString = MotherDragonLine(0);
        savedMDDamage = 0;
        isWaitingForDragonEnd = false; //   추가됨

        if (resultText != null) resultText.gameObject.SetActive(false);
        UpdateUI();
    }

    //public void ShowGameClear()
    //{
    //    bool wasOver = isGameOver;
    //    isGameOver = true;
    //    if (!wasOver) OnBattleEnded?.Invoke();

    //    if (resultText != null)
    //    {
    //        resultText.text = "ALL STAGES CLEARED!";
    //        resultText.gameObject.SetActive(true);
    //    }

    //    if (playerHealthBar != null) playerHealthBar.Hide();
    //    if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);

    //    ShowStatisticsUI("ALL STAGES CLEARED!");
    //}

    public void ShowGameClear()
    {
        bool wasOver = isGameOver;
        isGameOver = true;
        if (!wasOver) OnBattleEnded?.Invoke();

        if (playerHealthBar != null) playerHealthBar.Hide();
        if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);

        // [수정] 통계창 출력 호출
        ShowStatisticsUI("ALL STAGES CLEARED!", true);
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

                enemyIntentBubble.Setup(currentIntent);

                if (SpeechBubbleManager.Instance != null)
                {
                    enemyIntentBubbleObj.GetComponent<RectTransform>().position =
                        SpeechBubbleManager.Instance.GetBubbleScreenPosition(enemy.BubblePosition, false);
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
            if (!isGameOver) ShowResult("DEFEAT...");
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
                    ShowResult("VICTORY!");
                }
            }
        }
    }

    // message는 결과만 담고("VICTORY!"), 무엇을 입력해야 하는지는 여기서 붙인다.
    // ResultInputHandler가 명령 단어를 소유하므로 안내도 그쪽에서 만들어야 인스펙터에서
    // 단어를 바꿨을 때 안내가 같이 따라간다.
    //public void ShowResult(string message)
    //{
    //    bool wasOver = isGameOver;
    //    isGameOver = true;
    //    if (!wasOver) OnBattleEnded?.Invoke();

    //    if (resultText != null)
    //    {
    //        string hint = string.Empty;
    //        if (resultInputHandler != null)
    //        {
    //            bool isVictory = player != null && player.currentHP > 0;
    //            hint = resultInputHandler.GetHintText(isVictory);
    //        }

    //        resultText.text = message + hint;
    //        resultText.gameObject.SetActive(true);
    //    }
    //}

    public void ShowResult(string message)
    {
        bool wasOver = isGameOver;
        isGameOver = true;
        if (!wasOver) OnBattleEnded?.Invoke();

        string hint = string.Empty;
        if (resultInputHandler != null)
        {
            bool isVictory = player != null && player.currentHP > 0;
            hint = resultInputHandler.GetHintText(isVictory);
        }


        bool showStats = (player == null || player.currentHP <= 0);
        ShowStatisticsUI(message + hint, showStats);
    }

    private void ShowStatisticsUI(string titleMessage, bool showStats)
    {
        // 결과 화면이 떴으므로 타자 속도 계산을 위한 타이머 중지
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.StopTracking();

        if (showStats && resultPanel != null && statsText != null && StatisticsManager.Instance != null)
        {
            var stats = StatisticsManager.Instance;
            string statsInfo = $"{titleMessage}\n\n" +
                               $"최고 도달 스테이지 : {stats.highestStageReached} / 8\n" +
                               $"평균 타자 속도 (CPM): {Mathf.RoundToInt(stats.GetCPM())}\n" +
                               $"사용한 단어 수 : {stats.validWordsUsed}\n" +
                               $"누적 가한 데미지 : {stats.totalDamageDealt}\n" +
                               $"누적 받은 데미지 : {stats.totalDamageTaken}";

            statsText.text = statsInfo;
            resultPanel.SetActive(true); // 통계 패널 켜기

            // 기존 중앙 텍스트 끄기 (겹침 방지)
            if (resultText != null) resultText.gameObject.SetActive(false);
        }
        else
        {
            // 통계를 보여주지 않는 경우 (일반 스테이지 클리어)
            if (resultText != null)
            {
                resultText.text = titleMessage;
                resultText.gameObject.SetActive(true); // 기존 중앙 텍스트 켜기
            }

            // 통계 패널 끄기 (겹침 방지)
            if (resultPanel != null) resultPanel.SetActive(false);
        }
    }
}