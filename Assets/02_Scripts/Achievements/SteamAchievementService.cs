// ⚠️ #define은 파일 맨 앞(다른 토큰보다 먼저)에 와야 한다.
//
// DISABLESTEAMWORKS는 Steamworks.NET 자신이 쓰는 이름이다. 패키지를 아직 설치하지 않았거나
// Steam 없이 빌드하고 싶을 때 Player Settings의 Scripting Define Symbols에 이 이름을 넣으면
// 아래 코드가 전부 빠진다.
//
// ⚠️ 초기화는 SteamRuntime이 소유한다. UPM 패키지에는 SteamManager가 없어서 직접 만들었다 -
// 자세한 경위는 그쪽 주석 참조. 이 파일과 SteamRuntime의 가드 조건은 반드시 같아야 한다.
//
// WebGL은 Steam API 자체가 없다. CLAUDE.md의 WebGL 검증 절차를 계속 쓸 수 있도록 여기서 걸러내고,
// 판정 로직(AchievementManager)은 플랫폼과 무관하게 그대로 돌게 둔다 - HangulImeMode가
// Windows 밖에서 no-op이 되는 것과 같은 구조다.
#if !DISABLESTEAMWORKS && (UNITY_STANDALONE || UNITY_EDITOR)
#define STEAM_ENABLED
#endif

using System.Collections.Generic;
using UnityEngine;
#if STEAM_ENABLED
using Steamworks;
#endif

/// <summary>
/// 도전과제를 Steam에 올리는 유일한 창구. 값도 씬 오브젝트도 없어
/// <see cref="LanguageSettings"/>·<see cref="DifficultySettings"/>와 같은 이유로 <c>static</c>이다 -
/// <b>인스펙터 배선이 늘지 않는다.</b>
///
/// ⚠️ <b>Steam이 없어도 게임은 정상 동작해야 한다.</b> 패키지 미설치·WebGL·Steam 클라이언트 미실행
/// 셋 다 여기서 조용히 no-op이 되고, 그 사실은 <b>첫 번째 호출에서 한 번만</b> 경고로 남긴다
/// (<see cref="SoundManager"/>가 버스 경고를 경로별 한 번만 남기는 것과 같은 이유 - 도전과제
/// 판정은 매 프레임 들어올 수 있어서 그대로 두면 콘솔이 폭주한다).
/// </summary>
public static class SteamAchievementService
{
    // ⚠️ 이 가드가 없으면 StoreStats()를 초당 수십 번 부른다. 조건 중에는 매 프레임 평가되는
    // 것이 있고(클리어 조건은 결과 화면이 다시 그려질 때마다 온다), Steam은 같은 도전과제를
    // 다시 올려도 막아주지 않는다.
    private static readonly HashSet<string> _unlocked = new HashSet<string>();

    private static bool _warned;

    /// <summary>지금 Steam에 실제로 올릴 수 있는 상태인가. 에디터 도구가 상태를 표시하는 데 쓴다.</summary>
    public static bool IsAvailable
    {
        get
        {
#if STEAM_ENABLED
            return SteamRuntime.Initialized;
#else
            return false;
#endif
        }
    }

    /// <summary>도전과제 하나를 올린다. 이미 올렸으면 아무 일도 하지 않는다.</summary>
    public static void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || !_unlocked.Add(id))
            return;

#if STEAM_ENABLED
        if (!SteamRuntime.Initialized)
        {
            WarnOnce("Steam이 초기화되지 않아");
            return;
        }

        // ⚠️ SetAchievement는 등록되지 않은 API Name에 대해 false를 돌려주는데, 그것 말고는
        // 아무 증상이 없다 - 파트너 사이트에 id를 안 올렸을 때 "달성했는데 안 뜬다"가 되고
        // 원인을 찾기가 매우 어렵다. 그래서 반환값을 반드시 본다.
        if (!SteamUserStats.SetAchievement(id))
        {
            Debug.LogError($"SteamAchievementService: '{id}' 달성 처리에 실패했습니다. " +
                           "Steamworks 파트너 사이트에 같은 이름의 API Name이 등록되어 있는지 확인하세요.");
            return;
        }

        // StoreStats()를 불러야 실제로 서버에 올라가고 화면에 알림이 뜬다.
        SteamUserStats.StoreStats();
        Debug.Log($"[도전과제] {id} 달성");
#else
        WarnOnce("이 빌드에 Steam이 없어");
        Debug.Log($"[도전과제] {id} 달성 (Steam 없음 - 기록만 남김)");
#endif
    }

    private static void WarnOnce(string reason)
    {
        if (_warned)
            return;

        _warned = true;
        Debug.LogWarning($"SteamAchievementService: {reason} 도전과제가 올라가지 않습니다. " +
                         "게임 진행에는 영향이 없습니다. (에디터에서 확인하려면 저장소 루트에 " +
                         "steam_appid.txt가 있고 Steam 클라이언트가 실행 중이어야 합니다.)");
    }

    /// <summary>
    /// 게임 실행 자체가 조건인 도전과제를 올린다.
    ///
    /// 씬과 무관한 조건이라 <see cref="AchievementManager"/>(전투 씬에만 있다)가 아니라 여기서
    /// 처리한다. id를 코드에 박지 않고 <see cref="AchievementDatabase"/>에서 조건으로 찾으므로,
    /// 같은 조건의 도전과제를 나중에 더 만들어도 JSON만 고치면 된다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void UnlockLaunchAchievements()
    {
        var all = AchievementDatabase.All;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Condition == AchievementCondition.GameLaunched)
                Unlock(all[i].Id);
        }
    }

#if UNITY_EDITOR
    /// <summary>에디터에서 Play를 반복할 때 "이미 올렸다" 기록이 남지 않게 비운다.
    /// static 필드는 도메인 리로드를 끄면 Play 사이에도 살아남는다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetSessionState()
    {
        _unlocked.Clear();
        _warned = false;
    }

    /// <summary>에디터 도구 전용. Steam에 올라간 도전과제를 전부 지운다.</summary>
    public static void EditorResetAll()
    {
        _unlocked.Clear();

#if STEAM_ENABLED
        if (!SteamRuntime.Initialized)
        {
            Debug.LogWarning("SteamAchievementService: Steam이 초기화되지 않아 초기화할 것이 없습니다.");
            return;
        }

        // true = 도전과제까지 함께 초기화(false면 통계만).
        SteamUserStats.ResetAllStats(true);
        SteamUserStats.StoreStats();
        Debug.Log("[도전과제] Steam 기록을 전부 초기화했습니다.");
#else
        Debug.LogWarning("SteamAchievementService: 이 빌드에 Steam이 없어 로컬 기록만 비웠습니다.");
#endif
    }
#endif
}
