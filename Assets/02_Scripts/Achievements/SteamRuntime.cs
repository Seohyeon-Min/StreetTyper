// 가드 이름과 조건은 SteamAchievementService와 같아야 한다 - 한쪽만 켜지면 컴파일이 깨진다.
#if !DISABLESTEAMWORKS && (UNITY_STANDALONE || UNITY_EDITOR)
#define STEAM_ENABLED
#endif

using UnityEngine;
#if STEAM_ENABLED
using Steamworks;
#endif

/// <summary>
/// Steam API의 수명을 소유한다 - 초기화 · 콜백 펌프 · 종료.
///
/// ⚠️ <b>Steamworks.NET의 UPM 패키지에는 <c>SteamManager</c>가 들어 있지 않다.</b> 패키지는
/// 바인딩(<c>SteamAPI</c>/<c>SteamUserStats</c> 등)만 담고 있고, 흔히 쓰이는 <c>SteamManager.cs</c>는
/// 그쪽 <c>.unitypackage</c>에만 있는 별도 파일이다. 그래서 그 역할을 여기서 직접 한다 -
/// 남의 파일을 <c>Assets/</c>에 복사해 넣는 것보다 우리 컨벤션에 맞춰 짧게 갖고 있는 편이 낫다.
///
/// ⭐ <b>지연 초기화다.</b> <c>RuntimeInitializeOnLoadMethod</c>로 부팅하면
/// <see cref="SteamAchievementService"/>의 초기화 메서드와 순서가 정해지지 않는다 - 이 프로젝트엔
/// 스크립트 실행 순서 설정이 없다(<c>CardDatabase</c>·<c>DialogueDatabase</c>가 지연 로드인 이유와 같다).
/// <see cref="Initialized"/>를 처음 읽는 쪽이 부팅시키므로 순서를 기댈 필요가 없다.
///
/// ⚠️ Steam이 없어도(App ID 미발급·클라이언트 미실행·WebGL) <b>게임은 정상 동작해야 한다.</b>
/// 실패하면 <see cref="Initialized"/>가 false로 남고 도전과제만 조용히 올라가지 않는다.
/// </summary>
public class SteamRuntime : MonoBehaviour
{
    private static bool _booted;
    private static bool _initialized;

    /// <summary>Steam API가 실제로 붙었는가. 처음 읽는 순간 초기화를 시도한다.</summary>
    public static bool Initialized
    {
        get
        {
            EnsureBooted();
            return _initialized;
        }
    }

    /// <summary>Steam 클라이언트에 설정된 이 게임의 언어(API 이름, 예: "koreana"/"english")를
    /// 읽는다. Steam이 안 붙어 있으면(App ID 미발급·클라이언트 미실행·WebGL) null을 돌려준다 -
    /// 부르는 쪽(<see cref="LanguageSettings"/>)이 그 경우 조용히 기존 기본값(영어)으로
    /// 폴백해야 한다. <b>최초 실행 때 기본 언어를 정하는 용도로만 쓸 것</b> - 이미 저장된
    /// PlayerPrefs 값이 있으면 그걸 덮어쓰면 안 된다(플레이어가 옵션에서 직접 고른 언어가
    /// 다음 실행에 Steam 언어로 도로 튕기게 된다).</summary>
    public static string GetGameLanguage()
    {
        EnsureBooted();

#if STEAM_ENABLED
        if (!_initialized)
            return null;

        try
        {
            return SteamApps.GetCurrentGameLanguage();
        }
        catch
        {
            // Steam이 붙어 있다고 판단했는데도 이 호출이 실패하는 경우까지 방어한다 -
            // 언어 하나 때문에 부팅을 막을 이유가 없다.
            return null;
        }
#else
        return null;
#endif
    }

    private static void EnsureBooted()
    {
        // 한 번 실패하면 다시 시도하지 않는다 - 도전과제 판정이 매 프레임 들어올 수 있어서
        // 재시도하면 같은 실패를 초당 수십 번 반복한다(두 Database와 같은 규칙).
        if (_booted)
            return;

        _booted = true;

#if STEAM_ENABLED
        try
        {
            // ⚠️ 여기에 SteamAPI.RestartAppIfNecessary(...)를 넣지 않았다. 그건 exe를 직접
            // 실행했을 때 Steam을 거쳐 다시 띄우는 것이라 App ID를 코드에 박아야 하는데,
            // 아직 발급 전이다. App ID가 나오면 그때 추가할 것(출시 빌드에만 의미가 있다).
            _initialized = SteamAPI.Init();

            if (!_initialized)
            {
                Debug.LogWarning("SteamRuntime: SteamAPI.Init()이 실패했습니다. 도전과제만 올라가지 " +
                                 "않고 게임 진행에는 영향이 없습니다. 에디터에서 붙이려면 저장소 루트에 " +
                                 "steam_appid.txt가 있고 Steam 클라이언트가 실행 중이어야 합니다.");
                return;
            }

            // 콜백을 받아줄 주인이 필요하다. Steam은 StoreStats 같은 호출의 결과를 콜백으로
            // 돌려주고, 그것을 펌프하지 않으면 큐에 계속 쌓인다.
            var host = new GameObject("SteamRuntime");
            host.AddComponent<SteamRuntime>();
            DontDestroyOnLoad(host);
        }
        catch (System.DllNotFoundException e)
        {
            // 네이티브 라이브러리(steam_api64.dll)를 못 찾은 경우. 패키지가 덜 받아졌거나
            // 플랫폼이 맞지 않는다.
            _initialized = false;
            Debug.LogWarning("SteamRuntime: Steam 네이티브 라이브러리를 찾지 못했습니다. " +
                             "도전과제가 올라가지 않습니다. " + e.Message);
        }
#endif
    }

#if STEAM_ENABLED
    private void Update()
    {
        if (_initialized)
            SteamAPI.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        if (!_initialized)
            return;

        _initialized = false;
        SteamAPI.Shutdown();
    }
#endif

#if UNITY_EDITOR
    /// <summary>Play를 멈춰도 static 필드는 남는다(도메인 리로드를 끈 경우). 다음 Play에서
    /// 다시 붙을 수 있도록 비운다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetBootState()
    {
        _booted = false;
        _initialized = false;
    }
#endif
}
