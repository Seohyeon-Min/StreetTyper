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
        if (label == null)
            return;

        label.text = message;
        label.fontSize = fontSize;
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
        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        ConfigureLabel();
        label.characterSpacing = startSpacing;
        canvasGroup.alpha = 0f;

        var elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / fadeInDuration);
            var punch = spacingPunchCurve != null ? spacingPunchCurve.Evaluate(progress) : progress;
            var fade = fadeInCurve != null ? fadeInCurve.Evaluate(progress) : progress;

            label.characterSpacing = Mathf.LerpUnclamped(startSpacing, fadeInSpacing, punch);
            canvasGroup.alpha = Mathf.Clamp01(fade);
            transform.localScale = Vector3.one * (scaleCurve != null ? scaleCurve.Evaluate(progress) : 1f);
            yield return null;
        }

        var remainingDuration = holdDuration + fadeOutDuration;
        elapsed = 0f;
        while (elapsed < remainingDuration)
        {
            elapsed += Time.deltaTime;
            var totalProgress = Mathf.Clamp01(elapsed / Mathf.Max(remainingDuration, 0.001f));
            var spacingProgress = slowSpacingCurve != null
                ? slowSpacingCurve.Evaluate(totalProgress)
                : totalProgress;
            label.characterSpacing = Mathf.LerpUnclamped(fadeInSpacing, endSpacing, spacingProgress);

            if (elapsed > holdDuration)
            {
                var fadeProgress = Mathf.Clamp01((elapsed - holdDuration) / fadeOutDuration);
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
