using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TimerManager의 남은 시간을 슬라이더로 보여준다. 늘면 초록, 줄면 빨강으로 잠깐 반짝인다
// (GDD "시간이 늘어날 땐 초록색, 깎일 땐 빨간색 피드백"). 순수 뷰 - 판단은 안 한다.
public class TimerView : MonoBehaviour
{
    [SerializeField] private TimerManager timerManager;
    [SerializeField] private Image fillImage;

    [Tooltip("게이지 채우기는 UIStyle이 담당한다(fillAmount).")]
    [SerializeField] private UIStyle.UIStyle uiStyleFill;

    [Header("색 피드백")]
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color increaseColor = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private Color decreaseColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float flashDuration = 0.3f;

    [Header("남은 시간 표시")]
    [Tooltip("남은 초를 숫자로 보여줄 라벨(선택). 바만으로는 몇 초인지 알기 어렵다.")]
    [SerializeField] private TMP_Text remainingText;

    [Header("표시/숨김")]
    [Tooltip("SetVisible로 페이드인/아웃할 대상. 게이지·텍스트를 한 번에 묶어서 숨기려고 " +
             "CanvasGroup을 쓴다(WorldAnchoredUI/StageStartEffect와 같은 이유). 비워두면 " +
             "SetVisible을 불러도 아무 일도 일어나지 않는다.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("페이드인/아웃에 걸리는 시간(초).")]
    [SerializeField] private float visibilityFadeDuration = 0.2f;

    private Coroutine _flashCoroutine;
    private Coroutine _visibilityCoroutine;

    private void OnEnable()
    {
        timerManager.OnTimeChanged += HandleTimeChanged;
        timerManager.OnTimeAdjusted += HandleTimeAdjusted;
    }

    private void OnDisable()
    {
        timerManager.OnTimeChanged -= HandleTimeChanged;
        timerManager.OnTimeAdjusted -= HandleTimeAdjusted;
    }

    // 매 프레임(자연 감소 포함) 발생 - 게이지 채우기만 갱신한다. 색 반짝임은 여기서 판단하지
    // 않는다 - 정상적인 카운트다운도 매 프레임 "감소"라서 반짝임이 끝날 틈도 없이 계속
    // 재시작되어 사실상 항상 빨간색으로 고정돼 버린다.
    private void HandleTimeChanged(float remaining)
    {
        if (uiStyleFill != null)
        {
            // maxValue 대신 Duration으로 나눈다 - 마비로 총 시간이 늘어나도 게이지 길이는
            // 그대로 두고 "느리게 흐르는" 것으로 보이게 한다.
            float ratio = timerManager.Duration > 0f ? remaining / timerManager.Duration : 0f;
            uiStyleFill.SetFillAmount(ratio);
        }

        if (remainingText != null)
            remainingText.text = remaining.ToString("0.0");
    }

    // AddTime/ReduceTime으로 실제 효과가 적용됐을 때만 발생 - 이때만 반짝인다.
    private void HandleTimeAdjusted(float delta)
    {
        if (delta > 0f)
            Flash(increaseColor);
        else if (delta < 0f)
            Flash(decreaseColor);
    }

    private void Flash(Color color)
    {
        if (fillImage == null)
            return;

        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(FlashRoutine(color));
    }

    private IEnumerator FlashRoutine(Color color)
    {
        fillImage.color = color;
        yield return new WaitForSeconds(flashDuration);
        fillImage.color = neutralColor;
        _flashCoroutine = null;
    }

    /// <summary>보상 선택 중처럼 타이머가 의미 없어지는 구간에 페이드아웃/인한다. 판단은
    /// 부르는 쪽(RewardInputHandler 등)이 하고, 여기는 그리기만 한다.</summary>
    public void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("TimerView: canvasGroup이 연결되지 않아 SetVisible이 아무 일도 하지 않습니다.", this);
            return;
        }

        if (_visibilityCoroutine != null)
            StopCoroutine(_visibilityCoroutine);

        _visibilityCoroutine = StartCoroutine(SetVisibleRoutine(visible ? 1f : 0f));
    }

    private IEnumerator SetVisibleRoutine(float target)
    {
        var start = canvasGroup.alpha;
        var elapsed = 0f;

        while (elapsed < visibilityFadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / visibilityFadeDuration));
            yield return null;
        }

        canvasGroup.alpha = target;
        _visibilityCoroutine = null;
    }
}
