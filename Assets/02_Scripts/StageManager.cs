using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [Header("Stage Settings")]
    public List<GameObject> enemyPrefabs;
    private GameObject lastNormalEnemyPrefab;
    public GameObject motherDragonPrefab;
    public Transform enemySpawnPoint;

    [Tooltip("스테이지가 열리고 플레이어가 타이핑을 시작할 수 있을 때까지의 대기 시간(초)")]
    public float stageStartDelay = 2f;

    [Header("적 체력 스케일링")]
    [Tooltip("첫 스테이지 일반 적의 최대 체력.")]
    public int enemyBaseMaxHP = 20;

    [Tooltip("일반 스테이지를 하나 지날 때마다 적 최대 체력에 더할 양.")]
    public int enemyHPGainPerStage = 10;

    [Tooltip("마더 드래곤(보스)을 지날 때마다 위 '스테이지당 증가량'이 이만큼 커진다. " +
             "0이면 증가량이 끝까지 일정하다.")]
    public int enemyHPGainIncreaseAfterBoss = 10;

    [Header("적 공격력 스케일링")]
    [Tooltip("첫 스테이지 일반 적의 공격력. EnemyData의 power 대신 이 값을 기준으로 삼는다.")]
    public int enemyBasePower = 5;

    [Tooltip("공격력은 체력과 달리 배율로 오른다. 일반 스테이지를 하나 지날 때마다 " +
             "늘어나는 비율(%)이다. 0이면 위 기본값이 끝까지 그대로 쓰인다.")]
    public float enemyPowerGainPercentPerStage = 20f;

    [Tooltip("마더 드래곤(보스)을 지날 때마다 위 증가 비율(%)이 이만큼 커진다. " +
             "0이면 비율이 끝까지 일정하다.")]
    public float enemyPowerGainPercentIncreaseAfterBoss = 0f;

    [Header("적 방어력 스케일링")]
    [Tooltip("첫 스테이지 일반 적이 '방어'를 고를 때 한 번에 쌓는 방어도. " +
             "EnemyData의 defensePower 대신 이 값을 기준으로 삼는다.")]
    public int enemyBaseDefensePower = 5;

    [Tooltip("방어력도 공격력과 같은 배율 방식이다. 일반 스테이지를 하나 지날 때마다 " +
             "늘어나는 비율(%)이다. 0이면 위 기본값이 끝까지 그대로 쓰인다.")]
    public float enemyDefenseGainPercentPerStage = 20f;

    [Tooltip("마더 드래곤(보스)을 지날 때마다 위 증가 비율(%)이 이만큼 커진다. " +
             "0이면 비율이 끝까지 일정하다.")]
    public float enemyDefenseGainPercentIncreaseAfterBoss = 0f;

    [Header("References")]
    public BattleManager battleManager;
    public EnemyManager enemyManager;
    public CharacterStats player;
    public TimerManager timerManager;
    public InputManager inputManager;
    public WordUnlockManager wordUnlockManager;
    public CardSlotManager cardSlotManager;
    public WordChainManager wordChainManager;
    public PendingActionManager pendingActionManager;
    public EventManager eventManager;

    [Tooltip("적 HP 바의 위치 추종 컴포넌트. 적은 스테이지마다 새로 스폰되므로 여기서 대상을 넘겨준다.")]
    public WorldAnchoredUI enemyHealthBarAnchor;

    public StatusEffectManager statusEffectManager;

    [Tooltip("런이 시작될 때 어썸 누적 횟수를 되돌리기 위해 참조한다.")]
    public SkillResolver skillResolver;

    [Tooltip("클리어 보상 후보를 화면에 펼쳐 보여준다.")]
    public RewardCardView rewardCardView;

    [Tooltip("보상 후보 중 하나를 타이핑으로 고르게 하는 수신자. 비워두면 후보를 전부 지급하는 " +
             "옛 동작으로 떨어진다(경고를 남긴다).")]
    public RewardInputHandler rewardInputHandler;

    [Tooltip("보상 선택이 끝나고 다음 스테이지가 열리기까지의 대기 시간(초). 승리 화면에서 " +
             "'다음'을 칠 필요가 없어진 대신, 방금 얻은 카드와 VICTORY를 볼 짧은 여유를 준다. " +
             "0으로 두어도 최소 한 프레임은 기다린다.")]
    public float rewardAdvanceDelay = 0.6f;

    [Header("UI")]
    [Tooltip("스테이지 등장 연출로 켜고 끌 오브젝트. 문구는 코드가 써 넣지 않으므로, 필요하면 " +
             "오브젝트 자체(프리팹/애니메이션)에 미리 담아둘 것.")]
    public GameObject stageStartObject;

    [Tooltip("stageStartObject에 붙은 등장/퇴장 연출(확대→축소 페이드인으로 등장, 축소 " +
             "페이드아웃으로 퇴장). 비워두면 퇴장 시 그냥 SetActive(false)로 즉시 끈다.")]
    public StageStartEffect stageStartEffect;

    [Header("보스 인트로 (마더 드래곤 전용)")]
    [Tooltip("보스전 시작 시 stageStartObject 대신 먼저 띄우는 타이틀 카드. 비워두면 " +
             "일반 스테이지와 같은 stageStartObject 배너로 폴백한다(안전한 기본값).")]
    public GameObject bossTitleCardObject;

    [Tooltip("bossTitleCardObject에 붙은 등장/퇴장 연출. stageStartEffect와 같은 컴포넌트를 " +
             "재사용하되 별도 인스턴스로 둔다. 비워두면 퇴장 없이 바로 SetActive(false).")]
    public StageStartEffect bossTitleCardEffect;

    // 타이틀 카드의 제목·부제·이미지는 그 오브젝트에 붙은 BossTitleCardView가 OnEnable에서
    // 스스로 채운다 - StageManager는 켜고 끄기만 하면 되고 문구를 알 필요가 없다(참조를 하나
    // 더 두면 그 배선이 빌 때 영어 모드에서 한국어가 그대로 나오는 조용한 실패가 생긴다).

    [Tooltip("타이틀 카드를 띄운 채로 유지하는 시간(초) - 퇴장 연출 전에 읽을 여유를 준다.")]
    public float bossTitleCardHoldDuration = 1.5f;

    [Tooltip("타이틀 카드가 퇴장한 뒤 인사 대사가 시작되기까지의 대기 시간(초).")]
    public float bossIntroDialogueDelay = 0.6f;

    [Tooltip("인사 대사 한 줄이 화면에 떠 있는 시간(초). 다음 줄로 넘어가기 전 대기 시간도 같다.")]
    public float bossGreetingLineDuration = 1.8f;

    [Tooltip("데미가 먼저 건네는 인사 대사.")]
    public string demiGreetingLine = "엄마!";
    public string demiGreetingLineEn = "Mom!";

    [Tooltip("마더 드래곤의 답변 대사.")]
    public string motherGreetingLine = "어디 실력좀 볼까?";
    public string motherGreetingLineEn = "Let's see what you've got.";

    public TMPro.TextMeshProUGUI currentStageText;

    [Tooltip("currentStageText에 쓸 포맷 문자열. {0} 자리에 표시 스테이지 번호(displayStage)가 들어간다.")]
    [SerializeField] private string stageLabelFormat = "STAGE {0}";

    [Tooltip("보스전(마더 드래곤)일 때 currentStageText에 쓸 문구. 번호가 없어 포맷이 필요 없다.")]
    [SerializeField] private string bossStageLabel = "MOMMY";

    [Header("Transition Settings")]
    [Tooltip("씬에 배치된 배경 스크롤러들을 모두 연결해 줍니다.")]
    public BackgroundScroller[] backgroundScrollers;

    [Tooltip("보상 획득 후 배경이 스크롤되며 달려가는 연출 시간")]
    public float transitionDuration = 1.5f;

    [Tooltip("새로운 적이 화면 밖에서 미끄러져 들어오는 시간")]
    public float enemySlideInDuration = 0.5f;

    [Tooltip("적이 처음 생성될 화면 오른쪽 밖의 X 오프셋 거리")]
    public float spawnOffScreenX = 15f;

    /// <summary>결과 화면의 "최고 도달 스테이지" 분모. 마더 드래곤 전투를 뺀 일반 스테이지 수다.
    ///
    /// <para>⚠️ 예전에는 인스펙터 필드였고, 분자(displayStage)가 보스를 빼고 세는데 분모는
    /// 손으로 적은 값이라 <b>둘이 조용히 어긋났다</b>(실제로 "11 / 12"가 떴다). 이제
    /// <see cref="totalBattles"/>와 <see cref="bossBattleIndices"/>에서 계산하므로 전투 구성을
    /// 바꾸면 분모도 저절로 따라온다.</para></summary>
    public int TotalStages
    {
        get
        {
            var count = 0;
            for (var i = 0; i < totalBattles; i++)
            {
                if (!IsBossBattle(i))
                    count++;
            }

            return count;
        }
    }

    [Header("스테이지 구성")]
    [Tooltip("이번 런의 총 전투 수(보스 포함). 마지막 전투가 엔딩 대결이 된다.")]
    public int totalBattles = 13;

    [Tooltip("마더 드래곤이 나오는 전투 인덱스(0부터). 여기 적힌 전투는 싸우지 않고 대사로 " +
             "진행되며 체력·공격력·방어력 스케일링에서도 빠진다.\n" +
             "⚠️ 마지막 전투(totalBattles - 1)는 엔딩 대결이라 여기 없어도 자동으로 포함된다.")]
    public int[] bossBattleIndices = { 4, 9 };

    private int currentBattleIndex = 0;

    // 지금 스테이지에 스폰된 적이 마더 드래곤인가. 보상에 "지우기" 카드를 놓을지 판단하는 데 쓴다.
    private bool stageWasMotherDragon;
    private GameObject currentEnemyObject;
    private Coroutine startRoutine;
    private Coroutine advanceRoutine;

    // FinishReward()가 불린 순간부터 다음 스테이지가 실제로 열리기(LoadStage)까지 true.
    // advanceRoutine만 보면 퇴장 연출(RewardCardView.PlayExit)이 도는 동안(코루틴이 아직 안
    // 걸린 구간)엔 false가 되어, 그 짧은 사이에 "다음" 안내가 잘못 깜빡인다.
    private bool _advancingAfterReward;

    /// <summary>보상이 끝나 다음 스테이지로 자동으로 넘어가는 중인가(퇴장 연출까지 포함).
    /// `BattleManager`가 결과 화면에 "다음" 안내를 띄울지 정하는 데 쓴다 - 칠 필요가 없는
    /// 단어를 안내하면 플레이어만 헷갈린다.</summary>
    public bool IsAdvancingAutomatically => _advancingAfterReward;

    private void OnEnable()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded += HandleBattleEnded;

        if (rewardInputHandler != null)
            rewardInputHandler.OnSelectionFinished += HandleRewardSelectionFinished;
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded -= HandleBattleEnded;

        if (rewardInputHandler != null)
            rewardInputHandler.OnSelectionFinished -= HandleRewardSelectionFinished;
    }

    void Start()
    {
        // 사전은 비어 있는 상태로 시작하므로, 첫 스테이지를 열기 전에 시작 단어부터 채워준다.
        if (wordUnlockManager != null)
            wordUnlockManager.GrantStartingWords();

        // 여기가 런의 시작점이다 - 어썸 누적 횟수도 같이 되돌린다.
        if (skillResolver != null)
            skillResolver.ResetRun();

        LoadStage(currentBattleIndex);
    }

    public void LoadStage(int stageIndex)
    {
        currentBattleIndex = stageIndex;
        // 총 10번의 전투(인덱스 0~9)를 모두 마치고 인덱스 10에 도달하면 게임 클리어
        if (currentBattleIndex >= totalBattles)
        {
            // ⚠️ 여기서 곧바로 return하면 아래의 정리(코루틴 정지·체인/보상 카드 지우기)가
            // 통째로 건너뛰어진다. 새 스테이지를 여는 게 아니라 런이 끝나는 것이지만, 직전
            // 스테이지가 남긴 것들은 똑같이 치워야 한다 - 실제로 전체 클리어 화면 위에 보상
            // 카드가 남고 자동 진행 코루틴이 살아 있던 적이 있다.
            //
            // 타이머 정지·손패 치우기·입력 열기는 여기서 하지 않는다. ShowGameClear가 부르는
            // ShowResult가 OnBattleEnded를 쏘고, DeckManager.HandleBattleEnded가 그 셋을 전부
            // 맡는다(패배와 같은 경로다). ⚠️ 그 이벤트는 결과 종류가 바뀔 때도 나가야 하며,
            // 예전엔 "처음 끝났을 때만" 나가서 여기서 묻혔다 - BattleManager.ShowResult 참조.
            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
                startRoutine = null;
            }

            if (advanceRoutine != null)
            {
                StopCoroutine(advanceRoutine);
                advanceRoutine = null;
            }

            _advancingAfterReward = false;

            if (wordChainManager != null)
                wordChainManager.ClearChain();

            if (pendingActionManager != null)
                pendingActionManager.Clear();

            if (rewardCardView != null)
                rewardCardView.Clear();

            // 스테이지 등장 배너가 떠 있는 채로 클리어 화면이 겹치지 않게 끈다. 여기선 퇴장
            // 연출(PlayExit)을 쓰지 않고 즉시 끈다 - 런이 끝나는 순간이라 배너가 축소되며
            // 사라지는 걸 볼 이유가 없고, 결과 창이 곧바로 덮는다.
            if (stageStartObject != null)
                stageStartObject.SetActive(false);

            battleManager.ShowGameClear();
            return;
        }


        // 보스전 인덱스는 IsBossBattle 한 곳에서만 정한다 - 스폰 분기와 스탯 공식(체력·공격력·
        // 방어력)이 같은 판단을 써야 엔딩 보스가 일반 적 공식에 휘말리지 않는다.
        bool isBossBattle = IsBossBattle(currentBattleIndex);

        if (SoundManager.Instance != null)
        {
            if (isBossBattle)
            {
                SoundManager.Instance.PlayBossBGM(); // 보스전이면 보스 브금 재생
            }
            else
            {
                SoundManager.Instance.PlayBattleBGM(); // 일반 전투면 일반 브금 재생 (이미 재생 중이면 알아서 무시됨)
            }
        }

        // 실제 UI 및 통계에 표시될 스테이지 번호 계산 (보스는 카운트 제외)
        // 보스전은 표시 번호에서 빼야 "STAGE 1,2,3,4 → MOMMY → STAGE 5..."로 이어진다.
        // ⚠️ 인덱스를 하드코딩하지 않고 IsBossBattle을 그대로 센다 - 보스 구성이 바뀌면
        // 여기도 같이 따라와야 하는데, 예전에는 4·9·12가 박혀 있어 따로 놀았다.
        int displayStage = currentBattleIndex + 1;
        for (var i = 0; i < currentBattleIndex; i++)
        {
            if (IsBossBattle(i))
                displayStage--;
        }

        // [추가] 최고 도달 스테이지 기록 갱신 (보스를 제외한 순수 스테이지 번호 전달)
        //
        // ⚠️ 보스전에서는 갱신하지 않는다. 위 displayStage는 "이 전투 앞의 일반 스테이지 수 + 1"이라
        // 보스전에서도 값이 하나 올라가는데, 보스는 스테이지로 세지 않으므로 그대로 넘기면
        // 분자가 분모(TotalStages)를 넘어선다 - 엔딩 보스에서 "11 / 10"이 되는 식이다.
        if (StatisticsManager.Instance != null && !isBossBattle)
            StatisticsManager.Instance.UpdateHighestStage(displayStage);

        if (currentStageText != null)
        {
            // 문구를 코드에 박지 않고 인스펙터에서 받는다 - 이 프로젝트의 기본 사양이다.
            currentStageText.text = isBossBattle ? bossStageLabel : string.Format(stageLabelFormat, displayStage);
        }

        if (currentEnemyObject != null)
        {
            Destroy(currentEnemyObject);
        }

        // [수정] 프리팹 결정 로직
        GameObject prefabToSpawn;
        if (isBossBattle && motherDragonPrefab != null)
        {
            prefabToSpawn = motherDragonPrefab;
        }
        else
        {
            if (enemyPrefabs != null && enemyPrefabs.Count > 0)
            {
                if (enemyPrefabs.Count == 1)
                {
                    prefabToSpawn = enemyPrefabs[0];
                }
                else
                {
                    // 직전에 나온 적과 겹치지 않도록 랜덤으로 뽑기
                    int safetyCount = 0;
                    do
                    {
                        // UnityEngine.Random을 명시하여 System.Random과의 충돌을 방지합니다.
                        prefabToSpawn = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
                        safetyCount++;
                    } while (prefabToSpawn == lastNormalEnemyPrefab && safetyCount < 20);

                    lastNormalEnemyPrefab = prefabToSpawn;
                }
            }
            else
            {
                Debug.LogError("StageManager: Enemy Prefabs 리스트가 비어있습니다! 인스펙터를 확인하세요.");
                return;
            }
        }

        // =========================================================
        // [수정] 화면 오른쪽 밖에서 생성
        Vector3 offScreenPos = enemySpawnPoint.position + new Vector3(spawnOffScreenX, 0f, 0f);
        currentEnemyObject = Instantiate(prefabToSpawn, offScreenPos, Quaternion.identity);

        EnemyBase newEnemyBase = currentEnemyObject.GetComponent<EnemyBase>();

        // [추가] 마지막 전투 인덱스일 경우 엔딩 보스로 설정
        if (currentBattleIndex == totalBattles - 1)
        {
            newEnemyBase.isEndingBoss = true;
        }

        // [유지] 이 줄은 절대 지우지 마세요! 보상(지우기 카드) 처리에 꼭 필요합니다.
        stageWasMotherDragon = newEnemyBase is MotherDragon;

        // [추가] 생성 직후 스탯 스케일링 적용.
        // ⚠️ 마더 드래곤에는 체력 공식을 태우지 않는다(0을 넘기면 체력을 건드리지 않는다).
        // 3턴을 버텨야 스파링이 성립하는데 일반 적 공식(기본 20)을 태우면 그 전에 죽어
        // 아웃로 이벤트가 통째로 깨진다 - 프리팹/EnemyData 값을 그대로 쓴다.
        int scaledHP = 0, scaledPower = 0, scaledDefense = 0;

        if (!isBossBattle)
        {
            scaledHP = ComputeEnemyMaxHP(currentBattleIndex);
            scaledPower = Mathf.Max(1, Mathf.RoundToInt(enemyBasePower *
                ComputeGrowthMultiplier(currentBattleIndex, enemyPowerGainPercentPerStage, enemyPowerGainPercentIncreaseAfterBoss)));
            scaledDefense = Mathf.Max(1, Mathf.RoundToInt(enemyBaseDefensePower *
                ComputeGrowthMultiplier(currentBattleIndex, enemyDefenseGainPercentPerStage, enemyDefenseGainPercentIncreaseAfterBoss)));
        }

        newEnemyBase.ApplyScaling(scaledHP, scaledPower, scaledDefense);

        // [트랜지션 마무리 연출]
        // 1. 배경 스크롤 서서히 정지
        if (backgroundScrollers != null)
        {
            foreach (var scroller in backgroundScrollers)
            {
                if (scroller != null) scroller.StopScroll(enemySlideInDuration);
            }
        }

        // 2. 데미 애니메이션 배속 원상 복구
        if (player != null)
        {
            PlayerBattleVisuals visuals = player.GetComponent<PlayerBattleVisuals>();
            if (visuals != null) visuals.ResetAnimationSpeed();
        }

        // 3. 새로운 적이 화면 밖에서 제자리로 슬라이드 인
        StartCoroutine(newEnemyBase.SlideInCoroutine(offScreenPos, enemySpawnPoint.position, enemySlideInDuration));
        // =========================================================

        enemyManager.currentEnemy = newEnemyBase;
        enemyManager.GenerateNextAction();

        // HP 바 연결
        if (enemyHealthBarAnchor != null)
            enemyHealthBarAnchor.Bind(currentEnemyObject.transform);
        else
            Debug.LogWarning("StageManager: enemyHealthBarAnchor가 할당되지 않았습니다.", this);

        // 스테이지를 넘어갈 때도 플레이어 방어도를 비운다. 턴마다 비우는 곳이 따로 있지만
        // (DeckManager.RunTurnTransition) 마지막 턴에 쌓은 방어도가 남은 채 다음 스테이지로
        // 넘어가는 경로가 생기지 않도록 여기서도 확실히 0으로 둔다.
        // ⚠️ 적 방어도는 여기서도 건드리지 않는다 - 적은 방어를 쌓아 단단해지는 게 의도된 밸런스이고,
        // 줄어드는 건 플레이어가 때릴 때뿐이다(새 적은 EnemyBase.ApplyScaling이 0에서 시작시킨다).
        if (player != null)
        {
            player.defense = 0;
        }

        battleManager.ResetBattle();

        // ⚠️ ResetBattle 뒤에 불러야 한다 - 그 안의 UpdateUI()가 낡은 페이즈(PlayerInput)를 보고
        // 적 인텐트 말풍선을 이미 켰을 수 있고, 그대로 두면 등장 배너·보스 인트로 내내 떠 있는다.
        battleManager.BeginStagePreparation();

        if (timerManager != null)
            timerManager.ResetToFull();

        if (inputManager != null)
        {
            inputManager.DisableInput();
            inputManager.ClearInput();
        }

        if (wordChainManager != null)
            wordChainManager.ClearChain();

        // 손패도 여기서 비운다. 새로 뽑는 건 stageStartDelay가 끝난 뒤 BeginStageAfterDelay ->
        // RefillAll이 하므로, 비우지 않으면 새 적이 등장하는 그 몇 초 동안 이전 스테이지의
        // 카드가 그대로 남아 있다. 비운 자리는 빈 카드로 보이고 타이핑에도 반응하지 않는다.
        if (cardSlotManager != null)
            cardSlotManager.EmptyAllSlots();

        if (pendingActionManager != null)
            pendingActionManager.Clear();

        // 스테이지 단위 누적을 되돌린다 - 럭키 확률(LuckyUses)이 여기서 기본값으로 돌아가고,
        // 세다 만 턴 누적(퍼펙트/니킥/촙/박치기)도 새 스테이지 첫 턴으로 넘어가지 않게 같이 비운다.
        // 어썸의 런 누적은 여기서 건드리지 않는다.
        if (skillResolver != null)
            skillResolver.ResetStage();

        // 럭키 보상은 스테이지당 정해진 횟수까지만 열린다 - 그 카운터와 소비되지 않고 남은
        // 보너스 라운드를 여기서 되돌린다(누적의 주인이 WordUnlockManager라 따로 부른다).
        if (wordUnlockManager != null)
            wordUnlockManager.ResetStage();

        if (statusEffectManager != null)
            statusEffectManager.ClearAll();

        if (rewardCardView != null)
            rewardCardView.Clear();

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        // 자동 진행 대기 중에 플레이어가 "다음"을 쳐서 먼저 넘어왔을 수 있다. 그대로 두면
        // 남은 코루틴이 뒤늦게 NextStage()를 한 번 더 불러 스테이지를 하나 건너뛴다.
        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        // 새 스테이지를 로드하는 시점이므로 "보상 뒤 자동 진행 중" 상태는 끝난다.
        _advancingAfterReward = false;

        // =========================================================
        // [수정] 아래 부분을 교체하여 엔딩 보스일 경우 전투 스킵 및 즉시 이벤트 실행
        if (newEnemyBase.isEndingBoss)
        {
            if (eventManager != null)
            {
                // true를 넘겨 wasMotherDragon(엔딩 이벤트)으로 처리되게 합니다.
                eventManager.StartEvent(true, 0, true);
            }
            else
            {
                battleManager.ShowGameClear();
            }
        }
        else if (isBossBattle && bossTitleCardObject != null)
        {
            startRoutine = StartCoroutine(PlayBossIntroThenBegin());
        }
        else
        {
            if (stageStartObject != null)
                stageStartObject.SetActive(true);

            if (rewardInputHandler != null)
                rewardInputHandler.RestoreHand();

            startRoutine = StartCoroutine(BeginStageAfterDelay());
        }
    }

    // 보스전 전용 인트로: 타이틀 카드 등장 → 유지 → 퇴장 → 짧은 대기 → 데미/마더 드래곤 인사
    // 대사(시간차, 스페이스 입력 불필요 - 이 시점엔 입력이 이미 잠겨 있고 화면에 상호작용할
    // 대상도 없다) → 기존 "게임시작" 배너 → BeginStageAfterDelay로 그대로 이어진다.
    private IEnumerator PlayBossIntroThenBegin()
    {
        // 켜기만 하면 된다 - StageStartEffect가 OnEnable에서 등장 연출을, BossTitleCardView가
        // OnEnable에서 지금 언어에 맞는 제목·부제·이미지를 각자 알아서 채운다.
        if (bossTitleCardObject != null)
            bossTitleCardObject.SetActive(true);

        yield return new WaitForSeconds(bossTitleCardHoldDuration);

        if (bossTitleCardObject != null)
        {
            var titleView = bossTitleCardObject.GetComponent<BossTitleCardView>();
            if (titleView != null)
            {
                bool gateClosed = false;
                titleView.PlayGateClose(() => gateClosed = true);
                yield return new WaitUntil(() => gateClosed);
            }
        }

        if (bossTitleCardEffect != null)
        {
            bool exited = false;
            bossTitleCardEffect.PlayExit(() => exited = true);
            yield return new WaitUntil(() => exited);
        }
        else if (bossTitleCardObject != null)
        {
            bossTitleCardObject.SetActive(false);
        }

        yield return new WaitForSeconds(bossIntroDialogueDelay);

        if (SpeechBubbleManager.Instance != null && player != null)
        {
            string demiLine = LanguageSettings.Pick(demiGreetingLine, demiGreetingLineEn, this, nameof(demiGreetingLine));
            SpeechBubbleManager.Instance.ShowMotherDragonBubble(
                demiLine, player.BubblePosition, true, bossGreetingLineDuration);
        }

        yield return new WaitForSeconds(bossGreetingLineDuration);

        if (SpeechBubbleManager.Instance != null && currentEnemyObject != null)
        {
            var enemyBase = currentEnemyObject.GetComponent<EnemyBase>();
            Vector3 motherPos = enemyBase != null ? enemyBase.BubblePosition : currentEnemyObject.transform.position;
            string motherLine = LanguageSettings.Pick(motherGreetingLine, motherGreetingLineEn, this, nameof(motherGreetingLine));
            SpeechBubbleManager.Instance.ShowMotherDragonBubble(
                motherLine, motherPos, false, bossGreetingLineDuration);
        }

        yield return new WaitForSeconds(bossGreetingLineDuration);

        if (stageStartObject != null)
            stageStartObject.SetActive(true);

        // 보상 중 가라앉혀뒀던 손패를 여기서 되돌린다 - 배너가 화면을 덮고 있는 동안 다시
        // 떠올라야, 보상 카드가 미처 정리되기도 전에 손패가 불쑥 올라오는 것처럼 안 보인다.
        // 숨긴 게 없으면(보상을 거치지 않고 재시작한 경우 등) 아무 일도 하지 않는다.
        if (rewardInputHandler != null)
            rewardInputHandler.RestoreHand();

        // ⚠️ 여기서는 StartCoroutine으로 새로 걸지 않는다 - 이 코루틴 자신이 이미 startRoutine이라
        // 다시 대입하면 이쪽이 추적에서 빠져 LoadStage의 StopCoroutine 가드가 무력해진다.
        yield return BeginStageAfterDelay();
    }

    /// <summary>이 전투 인덱스가 보스전(마더 드래곤)인가. 스폰 분기·스탯 공식·엔딩 판정이
    /// 모두 이 하나를 써야 서로 어긋나지 않는다.
    ///
    /// <para>⚠️ <b>마지막 전투는 인스펙터 목록에 없어도 항상 보스다.</b> 예전에는 보스 목록이
    /// 4·9·12로 하드코딩되어 있고 엔딩은 <c>totalBattles - 1</c>로 따로 계산돼서, 전투 수를
    /// 바꾸면 엔딩이 보스가 아닌 자리에 떨어져 <b>마더 드래곤 대신 일반 드래곤이 스폰</b>됐다.
    /// 그러면 엔딩 대사도 스케일링 제외도 어긋난다.</para></summary>
    private bool IsBossBattle(int battleIndex)
    {
        if (battleIndex == totalBattles - 1)
            return true;

        if (bossBattleIndices == null)
            return false;

        for (var i = 0; i < bossBattleIndices.Length; i++)
        {
            if (bossBattleIndices[i] == battleIndex)
                return true;
        }

        return false;
    }

    /// <summary>이 전투의 일반 적 최대 체력.
    ///
    /// <para>첫 스테이지는 <see cref="enemyBaseMaxHP"/>에서 시작하고, 일반 스테이지를 하나
    /// 지날 때마다 <see cref="enemyHPGainPerStage"/>씩 더한다. 보스를 지나면 그 뒤부터
    /// 증가량이 <see cref="enemyHPGainIncreaseAfterBoss"/>만큼 커진다.</para>
    ///
    /// <para>기본값(20 / 10 / 10) 기준 진행: 20 → 30 → 40 → 50 → [보스] → 70 → 90 → 110 → 130.
    /// 보스 다음 스테이지부터 증가량이 10에서 20으로 올라간 것이다.</para>
    ///
    /// <para>⚠️ 보스전 자체는 이 값을 쓰지 않는다(LoadStage가 0을 넘긴다). 보스는 체력이 아니라
    /// 턴 수로 끝나는 스파링이다.</para></summary>
    private int ComputeEnemyMaxHP(int battleIndex)
    {
        var hp = enemyBaseMaxHP;
        var isFirstNormal = true;
        var bossesPassed = 0;

        for (var i = 0; i <= battleIndex; i++)
        {
            // 보스는 체력이 누적되는 스테이지가 아니다 - 증가량만 키우고 지나간다.
            if (IsBossBattle(i))
            {
                bossesPassed++;
                continue;
            }

            // 첫 일반 스테이지는 기본값 그대로다(아직 지나온 스테이지가 없다).
            if (isFirstNormal)
            {
                isFirstNormal = false;
                continue;
            }

            hp += enemyHPGainPerStage + enemyHPGainIncreaseAfterBoss * bossesPassed;
        }

        return Mathf.Max(1, hp);
    }

    /// <summary>공격력·방어력이 공유하는 배율 계산(체력만 덧셈이라 따로 있다).
    ///
    /// <para>첫 스테이지는 1배, 일반 스테이지를 하나 지날 때마다 <paramref name="percentPerStage"/>%씩
    /// 더해지고, 보스를 지나면 그 뒤부터 <paramref name="percentIncreaseAfterBoss"/>%만큼 걸음이
    /// 커진다. 체력 공식(<see cref="ComputeEnemyMaxHP"/>)과 같은 걸음이다.</para>
    ///
    /// <para>기본값(20% / 0%) 기준: 1.0 → 1.2 → 1.4 → 1.6 → [보스] → 1.8 → 2.0 → 2.2 → 2.4.
    /// 예전의 "스테이지당 +20%"와 같은 진행이다.</para></summary>
    private float ComputeGrowthMultiplier(int battleIndex, float percentPerStage, float percentIncreaseAfterBoss)
    {
        var percent = 0f;
        var step = percentPerStage;
        var isFirstNormal = true;

        for (var i = 0; i <= battleIndex; i++)
        {
            // 보스는 수치가 누적되는 스테이지가 아니다 - 걸음만 키우고 지나간다.
            if (IsBossBattle(i))
            {
                step += percentIncreaseAfterBoss;
                continue;
            }

            // 첫 일반 스테이지는 배율 1배 그대로다(아직 지나온 스테이지가 없다).
            if (isFirstNormal)
            {
                isFirstNormal = false;
                continue;
            }

            percent += step;
        }

        return Mathf.Max(0f, 1f + percent / 100f);
    }

    public void RestartStage()
    {
        if (player != null)
        {
            player.currentHP = player.maxHP;
            player.defense = 0;
            player.gameObject.SetActive(true);
            LoadStage(currentBattleIndex);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // 적이 등장한 뒤 잠깐 두었다가 플레이어 턴을 연다 - 적 턴 이후의 대기와 같은 목적이다.
    private IEnumerator BeginStageAfterDelay()
    {
        yield return new WaitForSeconds(stageStartDelay);

        // stageStartEffect가 있으면 축소하며 페이드아웃하는 연출을 맡기고(백그라운드로 흘러가며,
        // 턴 시작을 더 늦추지는 않는다), 없으면 예전처럼 바로 끈다.
        if (stageStartEffect != null)
            stageStartEffect.PlayExit(null);
        else if (stageStartObject != null)
            stageStartObject.SetActive(false);

        // [추가된 부분] 대기하는 동안 게임 오버가 되었다면 (예: 킬스위치 즉사) 더 이상 진행하지 않음
        if (battleManager != null && battleManager.IsGameOver)
        {
            startRoutine = null;
            yield break; // 여기서 코루틴을 강제 종료하여 타이머가 다시 켜지는 것을 막습니다.
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.StartTracking();

        // 손패는 스테이지마다 새로 뽑는다. 안 그러면 이전 스테이지에서 들고 있던 카드가 그대로 남는다.
        if (cardSlotManager != null)
            cardSlotManager.RefillAll();

        // 이전 턴이 타이머 만료(HandleTimeExpired)로 끝났다면 DisableInput()이 걸려 있고,
        // 그 직후 패배했다면 EnableInput()이 한 번도 안 불렸을 수 있다 - 여기서 확실히 켠다.
        if (inputManager != null)
            inputManager.EnableInput();

        if (timerManager != null)
            timerManager.RestartTurn();

        // 이제부터 진짜 내 턴이다 - 감춰뒀던 적 인텐트 말풍선을 다시 판단해 켠다.
        if (battleManager != null)
            battleManager.EndStagePreparation();

        startRoutine = null;
    }

    // 스테이지 클리어 지점. 결과 화면에서 "다음"을 타이핑했을 때 ResultInputHandler가 부른다.
    // 보상은 승리가 확정된 순간(HandleBattleEnded)에 이미 지급되고 화면에도 떠 있으므로,
    // 여기서는 그 카드를 치우고 다음 스테이지를 여는 일만 한다.
    public void NextStage()
    {
        LoadStage(currentBattleIndex + 1);
    }

    // 적 HP가 0이 되어 승패가 갈리는 순간 호출된다(BattleManager.OnBattleEnded).
    // 보상 후보를 펼쳐 플레이어가 하나를 고르게 한다 - 다 고르기 전에는 "다음"이 먹지 않는다
    // (RewardInputHandler가 결과 화면보다 높은 우선순위로 입력을 가져가기 때문이다).
    private void HandleBattleEnded()
    {
        // 패배에는 보상도 자동 진행도 없다 - 플레이어가 "다시하기"를 쳐서 재도전한다.
        if (player == null || player.currentHP <= 0)
            return;

        // ⚠️ 런이 끝났으면(패배·전체 클리어) 보상 라운드를 열지 않는다. 전체 클리어는 플레이어가
        // 살아 있어서 위 HP 검사에 걸리지 않는데, 그대로 두면 결과 화면이 뜬 <b>뒤에</b> 보상 창이
        // 한 번 더 올라온다 - 더 갈 스테이지가 없으니 거기서 카드를 골라봐야 쓸 곳도 없다.
        //
        // 마지막 스테이지의 보상은 이 시점이 아니라 그 직전 Victory에서 이미 받았다.
        // (OnBattleEnded가 kind가 바뀔 때도 나가게 되면서 이 경로가 생겼다 - ShowResult 참조.)
        if (battleManager != null && battleManager.IsFinalResult)
            return;

        // ⚠️ wordUnlockManager가 없다고 여기서 리턴하면 안 된다. 그 경우에도 BeginRewardRound가
        // FinishReward로 빠져 자동 진행을 걸어주는데, 여기서 끊으면 보상도 자동 진행도 없이
        // 승리 화면에 멈춘다(승리에는 "다음" 안내를 띄우지 않으므로 칠 것도 없다).
        BeginRewardRound();
    }

    // 보상 한 라운드. 럭키가 쌓여 있으면 선택이 끝난 뒤 여기로 다시 들어온다.
    // isBonusRound는 이 라운드가 럭키로 얻은 보너스 라운드인지다 - "럭키 보상!" 배너를 켤지
    // 화면(RewardCardView)에 전달하는 용도로만 쓴다.
    private void BeginRewardRound(bool isBonusRound = false)
    {
        // 지금은 호출부 두 곳 모두 null을 걸러내고 들어오지만, 라운드가 여러 경로로 열리게 된
        // 뒤라 여기서도 막아둔다.
        if (wordUnlockManager == null)
        {
            FinishReward();
            return;
        }

        var candidates = wordUnlockManager.RollRewardCandidates();

        // 미보유 단어가 다 떨어졌다. 보상 창을 띄우지 않고 곧바로 결과 화면으로 넘긴다 -
        // 여기서 멈추면 플레이어가 "다음"을 칠 수도 없어 진행이 막힌다.
        if (candidates == null || candidates.Count == 0)
        {
            FinishReward();
            return;
        }

        // 카드를 펼치는 것도 BeginSelection이 한다 - 줄에 명령 카드까지 섞이므로, 단어를 가진
        // 쪽이 순서를 정해야 매칭 인덱스와 화면 배치가 어긋나지 않는다.
        if (rewardInputHandler != null)
        {
            rewardInputHandler.BeginSelection(candidates, stageWasMotherDragon, isBonusRound);
            return;
        }

        // 폴백 경로에서는 고를 수단이 없으니 표시만이라도 해준다.
        if (rewardCardView != null)
            rewardCardView.Show(candidates, isBonusRound);

        // 폴백: 고를 수단이 없으면 후보를 전부 지급하는 옛 동작으로 떨어진다.
        // 조용히 보상이 증발하는 것보다는 낫다.
        Debug.LogWarning("StageManager: rewardInputHandler가 연결되지 않아 보상을 고를 수 없습니다. " +
                         "후보를 전부 지급하는 예전 동작으로 대체합니다. 씬 인스턴스에서 연결하세요.", this);

        for (var i = 0; i < candidates.Count; i++)
            wordUnlockManager.ConfirmReward(candidates[i]);

        FinishReward();
    }

    // 한 라운드가 끝났다(골랐든 넘겼든). 럭키로 열린 라운드가 남았으면 한 번 더 띄운다.
    private void HandleRewardSelectionFinished()
    {
        if (wordUnlockManager != null && wordUnlockManager.TryConsumeBonusRound())
        {
            BeginRewardRound(isBonusRound: true);
            return;
        }

        FinishReward();
    }

    // 보상이 완전히 끝났다. 카드를 치우고 곧바로 다음 스테이지로 넘어간다 - 승리 화면에서
    // "다음"을 칠 필요가 없다.
    //
    // ⚠️ 여기서 NextStage()를 바로 부르면 안 된다. 이 경로는 BattleManager.ShowResult 안의
    //    OnBattleEnded에서 시작될 수 있는데, 그 호출은 우리가 돌아간 뒤에 이어서 결과 화면을
    //    그린다 - 스테이지를 먼저 갈아끼우면 그 위에 VICTORY 화면이 덮인다. 코루틴으로 최소
    //    한 프레임 미뤄 그 호출을 완전히 빠져나온 뒤에 넘긴다.
    private void FinishReward()
    {
        // 퇴장 연출 도중(advanceRoutine이 아직 안 걸린 구간)에도 "자동 진행 중"으로 보이게 한다.
        _advancingAfterReward = true;

        // 자동 진행 대기가 이미 걸려 있었다면 새로 거는 쪽(퇴장 연출이 끝난 뒤)이 대신하므로 끊는다.
        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        if (rewardCardView != null)
        {
            // 고른 카드가 있으면(RewardInputHandler.LastPickedRowIndex) 그 카드만 잠시 남기고
            // 나머지는 떨어뜨리며 닫는다. 넘겼거나(스킵) rewardInputHandler가 없으면 -1이라
            // 전부 떨어진다. 다음 스테이지로의 진행은 이 연출이 끝난 뒤(BeginAdvanceAfterReward)로 미룬다.
            var keepIndex = rewardInputHandler != null ? rewardInputHandler.LastPickedRowIndex : -1;
            rewardCardView.PlayExit(keepIndex, BeginAdvanceAfterReward);
        }
        else
        {
            BeginAdvanceAfterReward();
        }

        // 자동 진행이 걸린 상태로 결과 화면을 다시 그린다 - 그래야 "다음" 안내가 뜨지 않는다.
        // (보상 후보가 아예 없어 선택 창을 건너뛴 경우, 이게 없으면 안내가 잠깐 깜빡인다.)
        if (battleManager != null)
            battleManager.RefreshResult();
    }

    // 퇴장 연출(또는 rewardCardView가 없어 연출 없이 곧바로)이 끝난 뒤에 부른다.
    // ⚠️ NextStage()를 여기서 바로 부르지 않고 한 번 더 코루틴으로 감싼다 - AdvanceAfterReward의
    //    대기(rewardAdvanceDelay)가 "방금 얻은 카드와 VICTORY를 볼 짧은 여유"라는 원래 의도를
    //    유지하기 위해서다(퇴장 연출 직후 바로 다음 스테이지로 넘어가면 그 여유가 없어진다).
    private void BeginAdvanceAfterReward()
    {
        if (advanceRoutine != null)
            StopCoroutine(advanceRoutine);

        advanceRoutine = StartCoroutine(AdvanceAfterReward());
    }

    private IEnumerator AdvanceAfterReward()
    {
        // Time.deltaTime 기반이라 일시정지(timeScale = 0) 중에는 멈춰 있는다 - 프로젝트의
        // 다른 대기와 같은 규칙이다.
        yield return new WaitForSeconds(rewardAdvanceDelay);

        // =========================================================
        // [트랜지션 연출 시작]
        // 2. 배경 스크롤 시작
        if (backgroundScrollers != null)
        {
            foreach (var scroller in backgroundScrollers)
            {
                if (scroller != null) scroller.StartScroll(transitionDuration, enemySlideInDuration);
            }
        }

        // 3. 데미 달리기(돌진) 애니메이션 재생
        if (player != null)
        {
            PlayerBattleVisuals visuals = player.GetComponent<PlayerBattleVisuals>();
            if (visuals != null) visuals.PlayDashAnimation(1.5f); // 살짝 배속을 주어 다급하게 달리는 느낌
        }

        // 4. 달려가는 연출 시간 동안 대기
        yield return new WaitForSeconds(transitionDuration);
        // =========================================================

        // NextStage -> LoadStage가 이 코루틴을 멈추려 들기 전에 먼저 비운다.
        advanceRoutine = null;
        NextStage();
    }
}
