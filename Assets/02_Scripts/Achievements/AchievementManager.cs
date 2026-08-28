using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도전과제 판정. 기존 시스템의 이벤트를 받아 <see cref="AchievementDatabase"/>의 정의를 훑고,
/// 조건이 맞는 것을 <see cref="SteamAchievementService"/>에 넘긴다.
///
/// ⭐ <b>도전과제 하나하나에 대한 코드가 여기 없다.</b> "어떤 도전과제가 있는가"는 전부 JSON이고,
/// 이 클래스는 <see cref="AchievementCondition"/> 종류별 평가만 안다. 새 도전과제가 기존 조건
/// 종류로 표현되면 JSON 한 줄로 끝나고 이 파일은 열지 않아도 된다.
///
/// ⚠️ <b>싱글턴으로 만들지 말 것.</b> 이 프로젝트의 싱글턴 7개는 "부르는 쪽이 프리팹마다 붙어서"
/// 인스펙터 연결이 불가능했던 경우다. 여기는 씬에 하나뿐인 매니저만 참조하므로 해당하지 않고,
/// 컨벤션대로 <c>[SerializeField]</c>로 받는다.
///
/// ⚠️ <b><c>DontDestroyOnLoad</c>를 붙이지 말 것.</b> 런 단위 상태(턴 액션 수)를 들고 있어서
/// 씬 스코프여야 한다 - 붙이면 두 번째 런에 지난 런 값이 이월된다(<see cref="StatisticsManager"/>와 같다).
/// </summary>
public class AchievementManager : MonoBehaviour
{
    [Header("판정에 필요한 상태")]
    [Tooltip("결과 종류(LastResultKind)와 적 처치 시점을 받는다.")]
    [SerializeField] private BattleManager battleManager;

    [Tooltip("보스 스테이지 진입을 받는다.")]
    [SerializeField] private StageManager stageManager;

    [Tooltip("완성된 조합을 받아 시너지 수를 센다.")]
    [SerializeField] private WordChainManager wordChainManager;

    [Tooltip("클리어 시점의 덱 구성과 이번 런의 카드 증감을 읽는다.")]
    [SerializeField] private WordDictionary wordDictionary;

    [Tooltip("턴이 바뀌는 시점을 받아 '한 턴에 액션 n회' 카운터를 되돌린다.")]
    [SerializeField] private DeckManager deckManager;

    [Tooltip("무피해 클리어 판정에 쓸 누적 피해량을 읽는다.")]
    [SerializeField] private StatisticsManager statisticsManager;

    // 이번 턴에 완성한 조합(= 실행한 액션) 수.
    //
    // ⚠️ SkillResolver.ActionsThisTurn을 읽고 싶어지지만 읽으면 안 된다. 그 값은 Resolve 맨
    // 끝에서 증가하는데, Resolve를 부르는 DeckManager.HandleChainCompleted도 이 이벤트의
    // 구독자다. 이 프로젝트엔 스크립트 실행 순서 설정이 없어 누가 먼저 불릴지 정해져 있지 않아,
    // 같은 조합에서 값이 1 차이로 흔들린다. 그래서 여기서 직접 센다.
    private int _actionsThisTurn;

    private void OnEnable()
    {
        if (battleManager != null)
        {
            battleManager.OnBattleEnded += HandleBattleEnded;
            battleManager.OnEnemyDefeated += HandleEnemyDefeated;
        }
        else
        {
            Debug.LogWarning("AchievementManager: battleManager가 연결되지 않아 클리어·처치 도전과제가 " +
                             "올라가지 않습니다.", this);
        }

        if (stageManager != null)
            stageManager.OnStageLoaded += HandleStageLoaded;
        else
            Debug.LogWarning("AchievementManager: stageManager가 연결되지 않아 보스 진입 도전과제가 " +
                             "올라가지 않습니다.", this);

        if (wordChainManager != null)
            wordChainManager.OnChainCompleted += HandleChainCompleted;
        else
            Debug.LogWarning("AchievementManager: wordChainManager가 연결되지 않아 컴보·시너지 도전과제가 " +
                             "올라가지 않습니다.", this);

        if (deckManager != null)
            deckManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        else
            Debug.LogWarning("AchievementManager: deckManager가 연결되지 않아 '한 턴에 액션 n회'가 " +
                             "턴마다 초기화되지 않습니다(누적되어 잘못 달성됩니다).", this);
    }

    private void OnDisable()
    {
        if (battleManager != null)
        {
            battleManager.OnBattleEnded -= HandleBattleEnded;
            battleManager.OnEnemyDefeated -= HandleEnemyDefeated;
        }

        if (stageManager != null)
            stageManager.OnStageLoaded -= HandleStageLoaded;

        if (wordChainManager != null)
            wordChainManager.OnChainCompleted -= HandleChainCompleted;

        if (deckManager != null)
            deckManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────

    private void HandleStageLoaded(int battleIndex, bool isBossBattle)
    {
        // ⚠️ 턴 카운터를 여기서도 반드시 되돌려야 한다. 아래 HandleTurnPhaseChanged만으로는
        // 새지 않는 구멍이 있다 - SetPhase는 페이즈가 <b>이미 같으면 이벤트를 쏘지 않는데</b>,
        // 적이 플레이어 턴 도중에 죽으면 페이즈가 PlayerInput인 채로 스테이지가 넘어가고
        // 다음 스테이지의 BeginNewStagePlayerInput()이 SetPhase(PlayerInput)를 불러도
        // 조기 리턴한다. 그러면 이전 스테이지에서 친 액션 수가 그대로 이월되어
        // "한 턴에 액션 10회"가 두 스테이지에 걸쳐 잘못 달성된다.
        _actionsThisTurn = 0;

        if (!isBossBattle)
            return;

        Unlock(AchievementCondition.BossEncountered);
    }

    private void HandleEnemyDefeated(int playerHP, bool isMotherDragon)
    {
        Unlock(AchievementCondition.EnemyDefeated);

        if (isMotherDragon)
            Unlock(AchievementCondition.BossDefeated);

        UnlockIfAtMost(AchievementCondition.PlayerHPAtKillAtMost, playerHP);
    }

    private void HandleChainCompleted(IReadOnlyList<WordInstance> chain)
    {
        _actionsThisTurn++;
        UnlockIfAtLeast(AchievementCondition.ActionsInTurnAtLeast, _actionsThisTurn);

        UnlockIfAtLeast(AchievementCondition.SynergiesInChainAtLeast, CountSynergies(chain));
    }

    private void HandleTurnPhaseChanged(DeckManager.TurnPhase phase)
    {
        // 플레이어 입력이 다시 열리는 순간이 곧 새 턴의 시작이다. SetPhase가 값이 실제로 바뀔
        // 때만 이벤트를 쏘므로 턴당 정확히 한 번 리셋된다.
        if (phase == DeckManager.TurnPhase.PlayerInput)
            _actionsThisTurn = 0;
    }

    /// <summary>
    /// 결과 화면이 떴다. <b>전체 클리어일 때만</b> 클리어 계열 조건을 평가한다.
    ///
    /// ⚠️ 일반 스테이지 승리(<see cref="ResultKind.Victory"/>)로도 이 이벤트가 오므로 반드시
    /// 종류를 봐야 한다 - 안 보면 첫 스테이지를 깬 순간 클리어 도전과제가 전부 뜬다.
    /// </summary>
    private void HandleBattleEnded()
    {
        if (battleManager == null || battleManager.LastResultKind != ResultKind.GameClear)
            return;

        var deckSize = 0;
        var actionCards = 0;
        var synergyCards = 0;

        if (wordDictionary != null)
        {
            var words = wordDictionary.Words;
            deckSize = words.Count;

            for (var i = 0; i < words.Count; i++)
            {
                if (words[i] == null)
                    continue;

                if (words[i].Category == CardCategory.Action)
                    actionCards++;
                else if (IsSynergy(words[i].Category))
                    synergyCards++;
            }
        }
        else
        {
            Debug.LogWarning("AchievementManager: wordDictionary가 연결되지 않아 덱 관련 도전과제를 " +
                             "판정할 수 없습니다.", this);
            return;
        }

        var added = wordDictionary.WordsAddedThisRun;
        var removed = wordDictionary.WordsRemovedThisRun;

        // ⚠️ statisticsManager가 없으면 "피해 0"으로 오판해 금강불괴를 거저 준다. 그럴 바에는
        // 그 조건만 건너뛴다.
        var knowsDamage = statisticsManager != null;
        var tookNoDamage = knowsDamage && statisticsManager.totalDamageTaken == 0;

        if (!knowsDamage)
            Debug.LogWarning("AchievementManager: statisticsManager가 연결되지 않아 무피해 클리어를 " +
                             "판정할 수 없습니다.", this);

        var all = AchievementDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            var achievement = all[i];

            switch (achievement.Condition)
            {
                case AchievementCondition.ClearInLanguage:
                    if (LanguageSettings.Current == achievement.Language)
                        SteamAchievementService.Unlock(achievement.Id);
                    break;

                case AchievementCondition.ClearWithNoDamage:
                    if (tookNoDamage)
                        SteamAchievementService.Unlock(achievement.Id);
                    break;

                case AchievementCondition.ClearWithActionsAtMost:
                    if (actionCards <= achievement.Threshold)
                        SteamAchievementService.Unlock(achievement.Id);
                    break;

                case AchievementCondition.ClearWithSynergiesAtMost:
                    if (synergyCards <= achievement.Threshold)
                        SteamAchievementService.Unlock(achievement.Id);
                    break;

                case AchievementCondition.ClearWithDeckSizeAtLeast:
                    if (deckSize >= achievement.Threshold)
                        SteamAchievementService.Unlock(achievement.Id);
                    break;

                case AchievementCondition.ClearWithDeckChange:
                    if (Matches(achievement.AddedRule, added) && Matches(achievement.RemovedRule, removed))
                        SteamAchievementService.Unlock(achievement.Id);
                    break;
            }
        }
    }

    // ── 판정 도구 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 시너지 카드인가. <b>액션도 명령도 아닌 것</b>이 전부 시너지다(수식어·시간·속성).
    ///
    /// 이 기준은 화면과 일치한다 - <c>CardView</c>가 카드 프레임을 액션(분홍)과 그 외(남색)
    /// 둘로만 가르므로, 플레이어가 "시너지"라고 부를 카드가 정확히 이 집합이다.
    /// 명령 카드(넘기기/지우기/계속…)는 애초에 덱에도 조합에도 들어가지 않지만, 규칙을 눈에
    /// 보이게 적어두려고 명시적으로 제외한다.
    /// </summary>
    private static bool IsSynergy(CardCategory category)
    {
        return category != CardCategory.Action && category != CardCategory.Command;
    }

    private static int CountSynergies(IReadOnlyList<WordInstance> chain)
    {
        if (chain == null)
            return 0;

        var count = 0;
        for (var i = 0; i < chain.Count; i++)
        {
            if (chain[i] != null && IsSynergy(chain[i].Category))
                count++;
        }

        return count;
    }

    private static bool Matches(DeckChangeRule rule, int count)
    {
        switch (rule)
        {
            case DeckChangeRule.Zero: return count == 0;
            case DeckChangeRule.AtLeastOne: return count >= 1;
            default: return true;
        }
    }

    /// <summary>문턱값이 없는 조건 - 그 종류의 도전과제를 전부 올린다.</summary>
    private static void Unlock(AchievementCondition condition)
    {
        var all = AchievementDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Condition == condition)
                SteamAchievementService.Unlock(all[i].Id);
        }
    }

    private static void UnlockIfAtLeast(AchievementCondition condition, int value)
    {
        var all = AchievementDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Condition == condition && value >= all[i].Threshold)
                SteamAchievementService.Unlock(all[i].Id);
        }
    }

    private static void UnlockIfAtMost(AchievementCondition condition, int value)
    {
        var all = AchievementDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Condition == condition && value <= all[i].Threshold)
                SteamAchievementService.Unlock(all[i].Id);
        }
    }
}
