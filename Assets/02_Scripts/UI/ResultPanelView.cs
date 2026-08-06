using System;
using UnityEngine;

/// <summary>
/// 결과 화면 한 벌 — 패널 오브젝트 + 통계 뷰 + 게이트 연출을 인스펙터에서 한 묶음으로 받는다.
/// <see cref="BattleManager"/>가 패배용과 전체 클리어용으로 하나씩 들고, 결과에 맞는 쪽만 켠다.
///
/// <b>제목·이미지는 여기서 다루지 않는다.</b> 결과 창의 겉모습은 프리팹(`DefeatPanel`/
/// `GameClearPanel` 변형)이 통째로 갖고, 코드가 하는 일은 "어느 패널을 켜고 어떤 수치를
/// 채울지"뿐이다. 예전엔 <c>ScreenPresentation</c> 세 벌이 제목을 갈아끼우려 했지만 그 제목이
/// 들어갈 칸이 프리팹에서 연결조차 되어 있지 않아 화면에 나간 적이 없었고, 결국 두 결과가
/// 똑같은 화면으로 떴다 - 그래서 문구가 아니라 <b>프리팹 단위로</b> 가르는 쪽으로 바꿨다.
///
/// <c>MonoBehaviour</c>가 아니라 평범한 <c>[Serializable]</c> 클래스인 건 <b>인스펙터 배선을
/// 늘리지 않기 위해서다</b> - 컴포넌트로 뽑으면 결과 종류마다 "어느 View를 쓸지" 참조가 하나 더
/// 생기고 씬 인스턴스 오버라이드만 늘어난다(<see cref="ScreenPresentation"/>·
/// <see cref="DisplacedUI"/>·<see cref="TypedCommand"/>와 같은 판단).
/// </summary>
[Serializable]
public class ResultPanelView
{
    [Tooltip("결과 창 루트. 평소엔 비활성이어야 한다. End Canvas 밑에 두되 명령 카드 줄" +
             "(PauseHand)보다 앞 형제여야 한다 - 풀스크린이라 뒤에 오면 카드를 덮는다.")]
    [SerializeField] private GameObject panel;

    [Tooltip("이 패널 안의 통계 뷰. 비워두면 수치만 건너뛰고 패널은 정상적으로 켜진다 " +
             "- 통계를 안 보여주는 결과 화면도 만들 수 있게 선택으로 둔다.")]
    [SerializeField] private ResultStatsView stats;

    [Tooltip("패널이 뜰 때 같이 재생할 TextGateRevealAnimation들(제목 텍스트, 배경 윈도우 등 " +
             "- 마스크마다 컴포넌트가 하나씩 따로 필요하다).\n" +
             "비워둬도 된다 - 그 경우 게이트 쪽에서 Play On Enable을 켜면 패널이 켜질 때 스스로 " +
             "재생된다(권장). 여기 넣는 건 패널 밖에 있는 게이트까지 같이 재생하고 싶을 때다.\n" +
             "⚠️ 둘 다 하면 같은 프레임에 두 번 재생된다 - Play()가 이전 코루틴을 끊고 다시 " +
             "시작하므로 눈에 띄는 고장은 아니지만, 한쪽으로 정할 것.")]
    [SerializeField] private TextGateRevealAnimation[] reveals;

    /// <summary>이 패널을 감춘다. 연결이 비어 있으면 조용히 넘어간다 - 감추는 건 실패해도
    /// 해로울 게 없고, 배선 누락 경고는 <see cref="Show"/> 쪽에서 한 번만 내는 게 낫다.</summary>
    public void Hide()
    {
        if (panel == null || !panel.activeSelf)
            return;

        var background = panel.GetComponentInChildren<FadeInBackground>(true);
        if (background != null && panel.activeInHierarchy)
            background.FadeOut(() => panel.SetActive(false));
        else
            panel.SetActive(false);
    }

    /// <summary>수치를 채우고 패널을 켠다.</summary>
    /// <param name="owner">경고에 찍을 주인. 어느 컴포넌트인지 알려준다.</param>
    /// <param name="fieldName">경고에 찍을 인스펙터 필드 이름(예: defeatResult).</param>
    public bool Show(int highestStage, int totalStages, int cpm, int wordsUsed,
        int damageDealt, int damageTaken, UnityEngine.Object owner, string fieldName)
    {
        // 조용히 폴백하지 않는다 - 폴백할 곳이 있으면 배선이 빠진 걸 못 알아채고
        // 아무것도 안 뜬 채로 넘어간다(프로젝트 컨벤션).
        if (panel == null)
        {
            Debug.LogWarning($"{owner.GetType().Name}: {fieldName}.panel이 연결되지 않아 " +
                             "결과 화면을 띄울 수 없습니다.", owner);
            return false;
        }

        // ⚠️ 게이트 연출은 꺼짐 -> 켜짐으로 <b>바뀔 때만</b> 재생한다. ApplyResult는 같은 결과로
        // 여러 번 불릴 수 있어서(RefreshResult, CheckGameState -> ShowResult), 무조건 재생하면
        // 이미 다 열린 마스크가 처음부터 되감긴다.
        var wasHidden = !panel.activeSelf;

        if (stats != null)
            stats.SetStats(highestStage, totalStages, cpm, wordsUsed, damageDealt, damageTaken);

        panel.SetActive(true);

        if (!wasHidden)
            return false;

        if (reveals != null)
        {
            foreach (var reveal in reveals)
            {
                if (reveal != null)
                    reveal.Play();
            }
        }

        return true;
    }

    public void PlayConfetti(Color[] colors, Sprite sparkleSprite)
    {
        if (panel != null)
            UIConfettiBurst.Play(panel.GetComponent<RectTransform>(), colors, sparkleSprite);
    }
}
