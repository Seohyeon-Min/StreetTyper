using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FMODUnity;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    [Header("Managers")]
    public EnemyManager enemyManager;
    public StageManager stageManager;
    public EventManager eventManager;

    [Tooltip("적 인텐트 말풍선을 내 턴(TurnPhase.PlayerInput)일 때만 보여주기 위해 참조한다.")]
    public DeckManager deckManager;

    [Header("Player Reference")]
    public CharacterStats player;

    [Header("Health Bar UI")]
    public HealthBarUI playerHealthBar;
    public HealthBarUI enemyHealthBar;

    [Header("Game Result UI")]
    public TextMeshProUGUI resultText;
    [Tooltip("결과 화면 안내 문구('다음'/'다시')를 만들 때 참조한다.")]
    public ResultInputHandler resultInputHandler;

    [Tooltip("보상을 고르는 중인지 물어보려고 참조한다. 그동안에는 '다음' 안내를 띄우지 않는다 " +
             "- 보상 수신자가 우선순위상 입력을 가져가 실제로 칠 수 없기 때문이다.")]
    [SerializeField] private RewardInputHandler rewardInputHandler;

    [Header("Statistics Result UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI statsText;

    [Header("Result Presentation")]
    [Tooltip("스테이지를 클리어했을 때의 제목과 이미지")]
    [SerializeField] private ScreenPresentation victoryPresentation = new ScreenPresentation("VICTORY!", "VICTORY!");

    [Tooltip("플레이어가 쓰러졌을 때")]
    [SerializeField] private ScreenPresentation defeatPresentation = new ScreenPresentation("DEFEAT...", "DEFEAT...");

    [Tooltip("모든 스테이지를 클리어했을 때")]
    [SerializeField] private ScreenPresentation gameClearPresentation = new ScreenPresentation("ALL STAGES CLEARED!", "ALL STAGES CLEARED!");

    [Tooltip("결과 이미지를 그릴 Image. 비워두면 이미지는 건너뛴다.")]
    [SerializeField] private Image resultImage;

    [Header("통계 문구")]
    [Tooltip("{0}=제목 {1}=최고 스테이지 {2}=전체 스테이지 {3}=CPM {4}=사용 단어 {5}=가한 피해 {6}=받은 피해")]
    [SerializeField, TextArea(6, 12)]
    private string statsFormat =
        "{0}\n\n" +
        "최고 도달 스테이지 : {1} / {2}\n" +
        "평균 타자 속도 (CPM): {3}\n" +
        "사용한 단어 수 : {4}\n" +
        "누적 가한 데미지 : {5}\n" +
        "누적 받은 데미지 : {6}";

    [Tooltip("영어 통계 문구. 자리표시자는 한국어와 같다.")]
    [SerializeField, TextArea(6, 12)]
    private string statsFormatEn =
        "{0}\n\n" +
        "Highest stage : {1} / {2}\n" +
        "Average speed (CPM) : {3}\n" +
        "Words used : {4}\n" +
        "Total damage dealt : {5}\n" +
        "Total damage taken : {6}";

    [Header("마더 드래곤 대사")]
    [Tooltip("스파링 연출이라 순서가 정해져 있다. 0=시작, 1=1턴 뒤, 2=2턴 뒤, 3=마무리")]
    [SerializeField]
    private string[] motherDragonLines =
    {
        "어디 한번 실력을 보여보거라!",
        "제법이구나!",
        "조금 더 힘을 끌어내 보거라!",
        "훌륭하다. 여기까지 하마!"
    };

    [Tooltip("영어 대사. 한국어와 같은 개수로 채울 것 - 비어 있으면 한국어가 그대로 나온다.")]
    [SerializeField]
    private string[] motherDragonLinesEn =
    {
        "Come, show me what you can do!",
        "Not bad!",
        "Draw out more of your power!",
        "Splendid. That will do."
    };

    [Header("Duration")]
    public float actionBubbleDuration = 1.0f;

    private bool isGameOver = false;
    private bool isEventTriggered = false;

    // 마지막으로 띄운 결과 종류. 보상 선택이 끝난 뒤 같은 화면을 다시 그리려면 필요하다.
    private ResultKind lastResultKind = ResultKind.Victory;

    private GameObject enemyIntentBubbleObj;
    private SpeechBubble enemyIntentBubble;

    // 엄마용 전투 전용 변수
    private int mdTurnCount = 0;
    private int savedMDDamage = 0;

    // ⚠️ 필드 초기화자에서 MotherDragonLine(0)을 부르지 않는다. 직렬화 값은 필드 초기화자보다
    // 나중에 적용되므로, 인스펙터 배열을 거기서 읽으면 항상 비어 있다. Start와 ResetBattle에서 채운다.
    private string mdIntentString = string.Empty;

    // 마더 드래곤이 턴마다 하는 말. 인스펙터 배열에서 꺼내며, 영어 배열이 짧거나 비어 있으면
    // 한국어로 넘어간다 - 대사는 타이핑 대상이 아니라 읽기만 하므로 진행이 막히지는 않는다.
    private string MotherDragonLine(int index)
    {
        var lines = motherDragonLines;

        if (LanguageSettings.IsEnglish && motherDragonLinesEn != null && index < motherDragonLinesEn.Length &&
            !string.IsNullOrEmpty(motherDragonLinesEn[index]))
        {
            lines = motherDragonLinesEn;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("BattleManager: motherDragonLines가 비어 있어 마더 드래곤 대사가 나오지 않습니다.", this);
            return string.Empty;
        }

        return lines[Mathf.Clamp(index, 0, lines.Length - 1)];
    }

    //   추가: 대기열 상태 확인용 변수
    private bool isWaitingForDragonEnd = false;

    public event Action OnBattleEnded;

    void Start()
    {
        // 인스펙터 배열은 이 시점에 채워져 있다(필드 초기화자와 달리).
        mdIntentString = MotherDragonLine(0);

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

        if (deckManager != null)
            deckManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;

        UpdateUI();
    }

    void Update()
    {
        // 실제 출시(Release) 빌드에서는 컴파일되지 않도록 안전장치 추가
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current == null) return;

        // 결과 화면이 떠 있는데 킬스위치가 먹으면 그 뒤에서 스테이지가 넘어간다.
        if (isGameOver) return;

        // 일시정지 메뉴 뒤에서도 마찬가지다. Update는 timeScale 0에서도 계속 돈다.
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

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

    private void OnDestroy()
    {
        if (deckManager != null)
            deckManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    // 내 턴(PlayerInput)이 아니면(공격 애니메이션 재생 중, 적 턴 등) 적 인텐트 말풍선을 숨긴다.
    // 다시 내 턴이 되면 UpdateUI()가 적이 살아있는지부터 다시 판단해서 알아서 켠다.
    private void HandleTurnPhaseChanged(DeckManager.TurnPhase phase)
    {
        if (phase == DeckManager.TurnPhase.PlayerInput)
        {
            UpdateUI();
            return;
        }

        if (enemyIntentBubbleObj != null)
            enemyIntentBubbleObj.SetActive(false);
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
                    // 1.5초 대기
                    isWaitingForDragonEnd = true;
                    Invoke("FinishMotherDragonBattle", 1.5f);
                }

                if (SpeechBubbleManager.Instance != null)
                {
                    SpeechBubbleManager.Instance.ShowBubble(mdIntentString, enemyManager.currentEnemy.BubblePosition, false, actionBubbleDuration);

                    // [유지] 마미드래곤일 때만 말하기 애니메이션 재생
                    enemyManager.currentEnemy.PlaySpeakAnimation();
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
        if (resultImage != null) resultImage.gameObject.SetActive(false);
        UpdateUI();
    }

    public void ShowGameClear()
    {
        if (playerHealthBar != null) playerHealthBar.Hide();
        if (enemyIntentBubbleObj != null) enemyIntentBubbleObj.SetActive(false);

        ShowResult(ResultKind.GameClear);
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

            // 내 턴(PlayerInput)일 때만 인텐트 말풍선을 보여준다. UpdateUI()는 펀치 한 번마다
            // (PlayPendingActions 안에서) HP 갱신용으로 계속 호출되므로, 여기서 페이즈를 안 보면
            // 애니메이션 재생 중에도 펀치마다 말풍선이 다시 켜졌다 꺼졌다 한다.
            var isPlayerInputPhase = deckManager == null || deckManager.CurrentPhase == DeckManager.TurnPhase.PlayerInput;

            if (enemyIntentBubbleObj != null && enemyIntentBubble != null && isPlayerInputPhase)
            {
                enemyIntentBubbleObj.SetActive(true);

                // 마더 드래곤은 대사(텍스트)를 그대로 쓰고, 일반 적은 아이콘 + ActionType별 색이
                // 입혀진 텍스트를 같이 보여준다.
                if (enemy.isMotherDragon)
                    enemyIntentBubble.Setup(mdIntentString);
                else
                    enemyIntentBubble.SetupIntent(enemyManager.GetIntentIcon(), enemyManager.GetIntentString(), enemyManager.GetIntentColor());

                if (SpeechBubbleManager.Instance != null)
                {
                    enemyIntentBubbleObj.GetComponent<RectTransform>().position =
                        SpeechBubbleManager.Instance.GetBubbleScreenPosition(enemy.BubblePosition, false);
                }
            }
            else if (enemyIntentBubbleObj != null && !isPlayerInputPhase)
            {
                enemyIntentBubbleObj.SetActive(false);
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
            if (!isGameOver) ShowResult(ResultKind.Defeat);
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
                    ShowResult(ResultKind.Victory);
                }
            }
        }
    }

    /// <summary>
    /// 결과 화면을 띄운다. 제목과 이미지는 인스펙터의 ScreenPresentation에서 나오고,
    /// 무엇을 입력해야 하는지는 ResultInputHandler가 붙인다 - 명령 단어를 소유한 쪽이
    /// 안내도 만들어야 인스펙터에서 단어를 바꿨을 때 안내가 같이 따라간다.
    /// </summary>
    public void ShowResult(ResultKind kind)
    {
        bool wasOver = isGameOver;
        isGameOver = true;

        // ⚠️ OnBattleEnded가 여기서 StageManager의 보상 라운드를 열고 돌아온다.
        // 그래서 아래 ApplyResult는 "보상을 고르는 중"인 상태에서 그려지고, 선택이 끝나면
        // StageManager가 RefreshResult()를 불러 다시 그린다.
        lastResultKind = kind;
        if (!wasOver) OnBattleEnded?.Invoke();

        ApplyResult(kind);
    }

    /// <summary>결과 화면을 지금 상태에 맞춰 다시 그린다. 보상 선택이 끝났을 때
    /// StageManager가 부른다 - 그제서야 "다음"을 칠 수 있으므로 안내도 그때 뜬다.</summary>
    public void RefreshResult()
    {
        if (isGameOver)
            ApplyResult(lastResultKind);
    }

    private void ApplyResult(ResultKind kind)
    {
        var presentation = GetPresentation(kind);
        var fieldName = GetPresentationFieldName(kind);
        var title = presentation.Title(this, fieldName);

        // 플레이어가 지금 칠 수 없거나 칠 필요가 없는 단어는 안내하지 않는다 - 두 경우가 있다.
        //  ① 보상을 고르는 중: 보상 수신자가 우선순위상 입력을 먼저 가져가 "다음"이 막혀 있다.
        //  ② 자동 진행 대기 중: 보상이 끝나면 StageManager가 알아서 다음 스테이지를 연다.
        // 반대로 둘 다 아니면(패배의 "다시", 배선이 빠져 자동 진행이 안 걸린 경우) 반드시 띄워야
        // 한다 - 안 그러면 칠 것도 없고 넘어가지도 않는 화면에 갇힌다.
        var choosingReward = rewardInputHandler != null && rewardInputHandler.IsSelecting;
        var advancing = stageManager != null && stageManager.IsAdvancingAutomatically;

        string hint = string.Empty;
        if (resultInputHandler != null && !choosingReward && !advancing)
            hint = resultInputHandler.BuildHint();

        ApplyResultImage(presentation);

        // 패배와 전체 클리어에서만 통계를 펼친다. 일반 스테이지 클리어는 곧바로 다음 판으로
        // 이어지므로 중앙에 제목만 띄운다.
        bool showStats = kind != ResultKind.Victory;
        ShowStatisticsUI(title + hint, presentation.TitleColor, showStats);
    }

    private ScreenPresentation GetPresentation(ResultKind kind)
    {
        switch (kind)
        {
            case ResultKind.Defeat: return defeatPresentation;
            case ResultKind.GameClear: return gameClearPresentation;
            default: return victoryPresentation;
        }
    }

    // LanguageSettings.Pick의 누락 경고가 어느 인스펙터 칸인지 알려주도록 필드명을 넘긴다.
    private string GetPresentationFieldName(ResultKind kind)
    {
        switch (kind)
        {
            case ResultKind.Defeat: return nameof(defeatPresentation);
            case ResultKind.GameClear: return nameof(gameClearPresentation);
            default: return nameof(victoryPresentation);
        }
    }

    private void ApplyResultImage(ScreenPresentation presentation)
    {
        if (resultImage == null)
            return;

        // ⚠️ 스프라이트가 없을 때 sprite = null로 두면 사라지는 게 아니라 흰 사각형이 그려진다.
        // 오브젝트째 꺼야 한다(CardView가 배지를 다루는 방식과 같다).
        if (presentation.Image == null)
        {
            resultImage.gameObject.SetActive(false);
            return;
        }

        resultImage.sprite = presentation.Image;
        resultImage.gameObject.SetActive(true);
    }

    private void ShowStatisticsUI(string titleMessage, Color titleColor, bool showStats)
    {
        // 결과 화면이 떴으므로 타자 속도 계산을 위한 타이머 중지
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.StopTracking();

        if (showStats && resultPanel != null && statsText != null && StatisticsManager.Instance != null)
        {
            statsText.text = BuildStatsText(titleMessage);
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
                resultText.color = titleColor;
                resultText.gameObject.SetActive(true); // 기존 중앙 텍스트 켜기
            }

            // 통계 패널 끄기 (겹침 방지)
            if (resultPanel != null) resultPanel.SetActive(false);
        }
    }

    private string BuildStatsText(string titleMessage)
    {
        var stats = StatisticsManager.Instance;
        var format = LanguageSettings.Pick(statsFormat, statsFormatEn, this, nameof(statsFormatEn));

        if (string.IsNullOrEmpty(format))
            return titleMessage;

        // 분모를 리터럴로 박으면 스테이지 수를 바꿨을 때 조용히 어긋난다.
        var totalStages = stageManager != null ? stageManager.totalStages : 0;

        return string.Format(
            format,
            titleMessage,
            stats.highestStageReached,
            totalStages,
            Mathf.RoundToInt(stats.GetCPM()),
            stats.validWordsUsed,
            stats.totalDamageDealt,
            stats.totalDamageTaken);
    }
}