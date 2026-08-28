using UnityEditor;
using UnityEngine;

/// <summary>
/// 도전과제 개발용 메뉴.
///
/// ⭐ <b>리셋이 사실상 필수다.</b> Steam은 한 번 올린 도전과제를 게임을 다시 켠다고 되돌려주지
/// 않는다. 그래서 언어별 클리어 5종처럼 조건을 반복 재현해야 하는 것을 손으로 확인하려면
/// 매번 초기화할 수단이 있어야 한다.
///
/// ⚠️ <c>Editor/</c> 폴더 안에 있으므로 <c>Assembly-CSharp-Editor</c>에 들어가고, 빌드에는
/// 포함되지 않는다(CLAUDE.md의 터미널 컴파일 검증에서 <c>/Editor/</c>를 걸러내는 이유이기도 하다).
/// </summary>
public static class AchievementDebugMenu
{
    private const string Root = "Tools/Achievements/";

    /// <summary>JSON이 제대로 읽히는지, 몇 개가 살아남았는지 콘솔에 찍는다.
    /// 잘못된 행은 <see cref="AchievementDatabase"/>가 로드하면서 에러로 알려준다.</summary>
    [MenuItem(Root + "정의 검사 (Validate)")]
    private static void Validate()
    {
        var all = AchievementDatabase.All;
        Debug.Log($"[도전과제] 정의 {all.Count}개를 읽었습니다.");

        for (var i = 0; i < all.Count; i++)
        {
            var a = all[i];
            Debug.Log($"  {a.Id,-24} {a.Condition,-26} {a.Definition.koName} / {a.Definition.enName}");
        }
    }

    [MenuItem(Root + "전부 달성 (Unlock All)")]
    private static void UnlockAll()
    {
        if (!RequirePlayMode())
            return;

        var all = AchievementDatabase.All;
        for (var i = 0; i < all.Count; i++)
            SteamAchievementService.Unlock(all[i].Id);
    }

    [MenuItem(Root + "전부 초기화 (Reset All)")]
    private static void ResetAll()
    {
        if (!RequirePlayMode())
            return;

        if (!EditorUtility.DisplayDialog("도전과제 초기화",
                "이 계정의 StreetTyper 도전과제와 통계를 전부 지웁니다. 되돌릴 수 없습니다.",
                "초기화", "취소"))
            return;

        SteamAchievementService.EditorResetAll();
    }

    /// <summary>
    /// ⚠️ Play 중이 아니면 막는다. <see cref="SteamRuntime"/>는 처음 접근할 때
    /// GameObject를 만들어 <c>SteamAPI.Init()</c>을 부르는 지연 싱글턴이라, 에디트 모드에서
    /// 건드리면 씬에 정체불명의 오브젝트가 생기고 초기화도 제대로 되지 않는다.
    /// </summary>
    private static bool RequirePlayMode()
    {
        if (Application.isPlaying)
            return true;

        Debug.LogWarning("[도전과제] Play 중에만 쓸 수 있습니다. Steam은 게임이 실행 중일 때만 붙습니다.");
        return false;
    }
}
