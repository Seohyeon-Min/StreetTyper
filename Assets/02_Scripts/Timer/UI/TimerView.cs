using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// TimerManager의 남은 시간을 슬라이더로 보여준다. 늘면 초록, 줄면 빨강으로 잠깐 반짝인다
// (GDD "시간이 늘어날 땐 초록색, 깎일 땐 빨간색 피드백"). 순수 뷰 - 판단은 안 한다.
public class TimerView : MonoBehaviour
{
    [SerializeField] private TimerManager timerManager;
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;

    [Header("색 피드백")]
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color increaseColor = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private Color decreaseColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float flashDuration = 0.3f;

    private float _previousTime;
    private bool _hasPreviousTime;
    private Coroutine _flashCoroutine;

    private void OnEnable()
    {
        timerManager.OnTimeChanged += HandleTimeChanged;
    }

    private void OnDisable()
    {
        timerManager.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(float remaining)
    {
        if (slider != null)
        {
            slider.maxValue = timerManager.Duration;
            slider.value = remaining;
        }

        // 턴이 막 시작해 기준값 자체로 리셋된 경우(RestartTurn/StartTimer)는 "시간을 얻었다"는
        // 의미가 아니므로 반짝이지 않는다 - 단어 효과로 도중에 늘거나 줄 때만 반짝인다.
        var isFreshTurn = Mathf.Approximately(remaining, timerManager.Duration);

        if (_hasPreviousTime && !isFreshTurn)
        {
            if (remaining > _previousTime)
                Flash(increaseColor);
            else if (remaining < _previousTime)
                Flash(decreaseColor);
        }

        _previousTime = remaining;
        _hasPreviousTime = true;
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
}
