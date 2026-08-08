using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>플레이어 입력이 열릴 때 화면 중앙에 잠시 나타나는 YOUR TURN 배너.</summary>
public class YourTurnBanner : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string message = "YOUR TURN!";
    [SerializeField] private float fontSize = 72f;
    [SerializeField] private Color color = Color.white;

    [Header("Timing")]
    [Min(0.01f)] [SerializeField] private float fadeInDuration = 0.18f;
    [Min(0f)] [SerializeField] private float holdDuration = 0.22f;
    [Min(0.01f)] [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Countdown")]
    [Tooltip("YOUR TURN 앞에 띄울 카운트다운의 시작 숫자. 3이면 3-2-1. 0이면 카운트다운을 하지 않는다.")]
    [Min(0)] [SerializeField] private int countdownFrom = 3;

    [Tooltip("숫자 하나가 화면에 머무는 시간(초). ⚠️ 전체 길이(숫자 수 x 이 값)가 DeckManager의 " +
             "postAttackDelay 안에 들어가야 턴 간격이 늘지 않는다 - 넘으면 넘은 만큼 턴 시작이 늦어진다.")]
    [Min(0.05f)] [SerializeField] private float countdownStepDuration = 0.35f;

    [Tooltip("카운트다운 숫자의 글자 크기. 0 이하면 위 fontSize를 그대로 쓴다.")]
    [SerializeField] private float countdownFontSize = 120f;

    [Header("Letter Spacing")]
    [SerializeField] private float startSpacing = -8f;
    [SerializeField] private float fadeInSpacing = 28f;
    [SerializeField] private float endSpacing = 58f;
    [SerializeField] private AnimationCurve spacingPunchCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 5f),
        new Keyframe(0.72f, 1.08f, 0f, 0f),
        new Keyframe(1f, 1f, -0.4f, 0f));
    [SerializeField] private AnimationCurve slowSpacingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fade / Scale")]
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.82f),
        new Keyframe(0.68f, 1.08f),
        new Keyframe(1f, 1f));

    private TMP_Text label;
    private CanvasGroup canvasGroup;
    private Coroutine playRoutine;

    private void Awake()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        ConfigureLabel();
    }

    private void ConfigureLabel()
    {
        ConfigureLabel(message, fontSize);
    }

    private void ConfigureLabel(string text, float size)
    {
        if (label == null)
            return;

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
    }

    public void Play()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (playRoutine != null)
            StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine(message, fontSize, fadeInDuration, holdDuration, fadeOutDuration, true));
    }

    /// <summary>카운트다운 전체 길이(초). 부르는 쪽이 기존 대기에서 이만큼을 떼어내
    /// 턴 간격이 늘지 않게 하는 데 쓴다.</summary>
    public float CountdownDuration => Mathf.Max(0, countdownFrom) * countdownStepDuration;

    /// <summary>
    /// YOUR TURN 앞에 붙는 3-2-1. 부르는 쪽(DeckManager)이 <c>yield return</c>으로 기다린다.
    ///
    /// 배너 오브젝트가 아니라 <b>부르는 쪽이 이 코루틴을 소유해야 한다</b> - 중간에 끊고 싶을 때
    /// (대사창이 열리거나 판이 끝났을 때) 자기 StopCoroutine으로 정리할 수 있어야 하기 때문이다.
    /// 그래서 여기서 StartCoroutine을 하지 않고 IEnumerator를 그대로 돌려준다.
    /// </summary>
    public IEnumerator PlayCountdownRoutine()
    {
        if (countdownFrom <= 0)
            yield break;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        // 한 숫자 안의 배분. 준비 신호이지 연출이 아니라서 짧게 지나간다.
        var fadeIn = countdownStepDuration * 0.25f;
        var hold = countdownStepDuration * 0.35f;
        var fadeOut = countdownStepDuration * 0.4f;
        var size = countdownFontSize > 0f ? countdownFontSize : fontSize;

        for (var number = countdownFrom; number >= 1; number--)
        {
            // ⚠️ 숫자 하나가 끝날 때마다 PlayRoutine이 오브젝트를 끄므로 매번 다시 켜야 한다.
            // 이 코루틴은 DeckManager가 돌리고 있어 오브젝트가 꺼져도 계속 진행되는데,
            // 켜지 않으면 두 번째 숫자부터 화면에 보이지 않는다.
            gameObject.SetActive(true);

            // 자간 애니메이션은 여러 글자를 벌리는 연출이라 한 글자에는 의미가 없다 - 숫자는
            // 스케일 팝과 페이드만 쓴다.
            yield return PlayRoutine(number.ToString(), size, fadeIn, hold, fadeOut, false);
        }
    }

    /// <summary>카운트다운을 중간에 끊었을 때 화면에 숫자가 남지 않게 정리한다.</summary>
    public void HideImmediate()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    private IEnumerator PlayRoutine(string text, float size, float fadeIn, float hold, float fadeOut, bool animateSpacing)
    {
        ConfigureLabel(text, size);
        label.characterSpacing = animateSpacing ? startSpacing : 0f;
        canvasGroup.alpha = 0f;

        var elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / fadeIn);
            var fade = fadeInCurve != null ? fadeInCurve.Evaluate(progress) : progress;

            if (animateSpacing)
            {
                var punch = spacingPunchCurve != null ? spacingPunchCurve.Evaluate(progress) : progress;
                label.characterSpacing = Mathf.LerpUnclamped(startSpacing, fadeInSpacing, punch);
            }

            canvasGroup.alpha = Mathf.Clamp01(fade);
            transform.localScale = Vector3.one * (scaleCurve != null ? scaleCurve.Evaluate(progress) : 1f);
            yield return null;
        }

        var remainingDuration = hold + fadeOut;
        elapsed = 0f;
        while (elapsed < remainingDuration)
        {
            elapsed += Time.deltaTime;

            if (animateSpacing)
            {
                var totalProgress = Mathf.Clamp01(elapsed / Mathf.Max(remainingDuration, 0.001f));
                var spacingProgress = slowSpacingCurve != null
                    ? slowSpacingCurve.Evaluate(totalProgress)
                    : totalProgress;
                label.characterSpacing = Mathf.LerpUnclamped(fadeInSpacing, endSpacing, spacingProgress);
            }

            if (elapsed > hold)
            {
                var fadeProgress = Mathf.Clamp01((elapsed - hold) / fadeOut);
                canvasGroup.alpha = Mathf.Clamp01(fadeOutCurve != null
                    ? fadeOutCurve.Evaluate(fadeProgress)
                    : 1f - fadeProgress);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }

            transform.localScale = Vector3.one;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        playRoutine = null;
        gameObject.SetActive(false);
    }
}
