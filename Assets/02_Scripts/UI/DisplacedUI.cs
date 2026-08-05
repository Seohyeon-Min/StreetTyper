using System;
using UnityEngine;

/// <summary>
/// 어떤 창이 열려 있는 동안 잠시 비켜나 있어야 하는 UI 한 개. 얼마나 어디로 비킬지는 전부
/// 인스펙터에서 정한다 - 화면 밖으로 완전히 내보낼지, 살짝만 내릴지는 배치를 보면서 맞출 값이라
/// 코드에 둘 수 없다(이 프로젝트의 "화면에 나가는 건 인스펙터에서 바꾼다" 규칙과 같은 결).
///
/// ⚠️ 필드 이름(<c>target</c>/<c>offset</c>)을 바꾸지 말 것. 씬에 이미 저장된 값이 이 이름으로
/// 직렬화되어 있어서, 바꾸면 <b>경고 없이 빈 값</b>이 되어 아무것도 비켜나지 않는다.
/// </summary>
[Serializable]
public class DisplacedUI
{
    [Tooltip("비켜날 UI. 명령 카드 줄(PauseHand)이나 입력창(InputFieldDisplay)처럼 창을 가리는 " +
             "것들을 넣는다. 다른 캔버스의 오브젝트도 된다 - 캔버스들이 같은 기준 해상도" +
             "(1920x1080)를 쓰므로 px 값이 어디서나 같은 의미를 갖는다.")]
    public RectTransform target;

    [Tooltip("원래 자리에서 밀려날 거리(px). 아래로 내리려면 y에 음수를, 화면 밖으로 완전히 " +
             "내보내려면 화면 절반(1080 기준 540)보다 큰 값을 준다.")]
    public Vector2 offset;

    // 원래 자리. 씬에 배치된 값을 창이 처음 깨어날 때 한 번만 읽어둔다 - 직렬화하면 밀려난
    // 좌표가 씬에 굳어버릴 수 있어서 런타임 전용으로 둔다.
    [NonSerialized] public Vector2 Origin;
    [NonSerialized] public bool Captured;
}

/// <summary>
/// <see cref="DisplacedUI"/> 여러 개를 한 묶음으로 밀어냈다 되돌리는 런타임 상태 + 로직.
///
/// 직렬화되지 않는 평범한 클래스다 - 창(<see cref="CardCollectionPanel"/>/
/// <see cref="CardDeletePanel"/>)이 <c>readonly</c> 필드로 하나씩 들고 쓴다. MonoBehaviour로
/// 뽑지 않은 이유는 <b>인스펙터 배선을 늘리지 않기 위해서</b>다 - 컴포넌트로 만들면 창마다
/// "어느 Displacer를 쓸지" 참조가 하나 더 생기고, 씬 인스턴스 오버라이드만 늘어난다
/// (<c>LanguageSettings</c>를 static으로 둔 것과 같은 판단).
///
/// 쓰는 쪽이 하는 일은 넷뿐이다:
/// <list type="number">
/// <item>Awake에서 <see cref="CaptureOrigins"/></item>
/// <item>여닫을 때 <see cref="MarkDirty"/></item>
/// <item>LateUpdate에서 <see cref="Tick"/></item>
/// <item>OnValidate에서 <see cref="MarkDirty"/>(Play 중 인스펙터 조정이 바로 보이게)</item>
/// </list>
/// </summary>
public class UIDisplacement
{
    // 0 = 제자리, 1 = offset만큼 밀려난 상태.
    private float _progress;

    // 목표에 도달했는가. 도달했으면 좌표 쓰기를 멈춘다 - 계속 쓰면 다른 스크립트가 이 UI를
    // 옮길 수 없다(HandFanLayout이 같은 RectTransform을 건드리는 경우가 실제로 있다).
    private bool _settled = true;

    /// <summary>씬에 배치된 자리를 기억해둔다. <b>Awake에서 한 번만</b> 부를 것 - 여닫는 도중에
    /// 다시 읽으면 밀려나 있던 좌표가 "원래 자리"로 굳어 UI가 화면 밖에 남는다.</summary>
    /// <param name="owner">경고에 찍을 주인. 어느 컴포넌트의 어느 칸이 비었는지 알려준다.</param>
    /// <param name="fieldName">경고에 찍을 인스펙터 필드 이름.</param>
    public void CaptureOrigins(DisplacedUI[] entries, UnityEngine.Object owner, string fieldName)
    {
        if (entries == null)
            return;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null)
                continue;

            if (entry.target == null)
            {
                Debug.LogWarning($"{owner.GetType().Name}: {fieldName}[{i}]에 오브젝트가 비어 있어 " +
                                 "이 칸은 아무것도 밀어내지 않습니다.", owner);
                continue;
            }

            entry.Origin = entry.target.anchoredPosition;
            entry.Captured = true;
        }
    }

    /// <summary>다시 움직여야 한다고 표시한다. 창을 여닫을 때와, Play 중 인스펙터에서 offset을
    /// 만졌을 때(OnValidate) 부른다 - 후자가 없으면 도착해서 좌표 쓰기를 멈춘 상태에서는
    /// 다음 여닫이까지 조정이 눈에 안 보인다. offset은 배치를 눈으로 보며 맞추는 값이다.</summary>
    public void MarkDirty()
    {
        _settled = false;
    }

    /// <summary>
    /// 목표(<paramref name="displaced"/>)를 향해 한 프레임만큼 움직인다.
    ///
    /// ⚠️ <b>LateUpdate에서 부를 것.</b> HandFanLayout이 카드(자식)를 배치한 <b>뒤에</b> 줄
    /// 전체(부모)를 옮겨야 한다 - Update에서 부르면 같은 프레임에 카드 배치가 덮어쓴다.
    /// </summary>
    /// <param name="useUnscaledTime">일시정지(timeScale = 0) <b>위에서</b> 열리는 창이면 true.
    /// 그 경우 보통의 deltaTime으로는 아예 움직이지 않는다. timeScale이 1인 구간에서만 뜨는
    /// 창은 false로 둘 것 - 일시정지가 timeScale 하나로 성립하는 프로젝트 전제를 따르는 쪽이 맞다.</param>
    /// <param name="curve">진행도(0~1, 경과 시간 기준 선형)를 실제 밀림 비율로 바꾸는 곡선.
    /// 비워두면(null) 선형 그대로 쓴다. 닫힐 때는 같은 진행도가 1→0으로 줄어들며 이 곡선을
    /// 거꾸로 훑으므로, 열 때 EaseOut이면 닫을 때는 자연히 EaseIn처럼 보인다 - 별도로
    /// 반대 곡선을 만들 필요가 없다(TextGateRevealAnimation의 PlayReverse와 같은 방식).</param>
    public void Tick(DisplacedUI[] entries, bool displaced, float duration, bool useUnscaledTime, AnimationCurve curve = null)
    {
        if (_settled)
            return;

        var goal = displaced ? 1f : 0f;

        if (duration <= 0f)
        {
            _progress = goal;
        }
        else
        {
            var delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _progress = Mathf.MoveTowards(_progress, goal, delta / duration);
        }

        Apply(entries, curve);

        if (Mathf.Approximately(_progress, goal))
            _settled = true;
    }

    private void Apply(DisplacedUI[] entries, AnimationCurve curve)
    {
        if (entries == null)
            return;

        var t = curve != null ? curve.Evaluate(_progress) : _progress;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null || !entry.Captured || entry.target == null)
                continue;

            entry.target.anchoredPosition = entry.Origin + entry.offset * t;
        }
    }
}
