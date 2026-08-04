using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 텍스트를 가운데서부터 좌우로 열리며 드러낸다: || -> |U| -> |AUS| -> |PAUSE| -> PAUSE.
// 셰이더가 아니라 RectMask2D로 구현한다 - 텍스트를 감싼 마스크의 폭을 가운데 기준으로 키우면
// 글자가 중앙부터 바깥으로 드러나고, 마스크 양쪽 끝에 막대(|)를 붙여서 같이 벌어지게 한다.
// 1단계(0~revealRatio): 마스크가 0에서 풀 텍스트 폭까지 벌어지고 막대가 그 끝을 따라간다
// (|| -> |U| -> |AUS| -> |PAUSE|).
// 2단계(revealRatio~1): 텍스트는 다 드러난 채로, 막대만 바깥으로 더 밀려나며 페이드아웃한다
// (|PAUSE| -> PAUSE).
public class TextGateRevealAnimation : MonoBehaviour
{
    [Tooltip("드러날 텍스트를 감싼 마스크. RectMask2D가 붙어 있어야 한다. sizeDelta.x를 키워서 연다. " +
             "시작 상태로 폭을 0(또는 막대 두께만큼)에 닫아두는 게 정상이다.")]
    [SerializeField] private RectTransform maskRect;

    [Tooltip("다 열렸을 때 마스크가 도달해야 하는 폭(px) - 안에 든 텍스트/이미지가 전부 보이는 폭. " +
             "마스크 자신의 현재 크기에서 읽어오지 않는다 - 시작 상태를 닫아두면(sizeDelta.x가 0에 " +
             "가까움) 그 값을 그대로 \"목표 폭\"으로 캐시해버려서 거의 안 열리는 것처럼 보인다.")]
    [SerializeField] private float fullWidth = 400f;

    [Tooltip("마스크 왼쪽 끝을 따라가는 막대. Image든 TMP_Text든 Graphic이면 된다(페이드아웃에 색 알파를 씀).")]
    [SerializeField] private Graphic leftBar;

    [Tooltip("마스크 오른쪽 끝을 따라가는 막대.")]
    [SerializeField] private Graphic rightBar;

    [Tooltip("텍스트가 다 드러난 뒤, 막대가 텍스트 바깥으로 더 밀려나는 거리(px).")]
    [SerializeField] private float barOvershoot = 24f;

    [Tooltip("전체 재생 시간(초).")]
    [SerializeField] private float duration = 0.6f;

    [Tooltip("전체 시간 중 1단계(텍스트가 다 드러나기까지)가 차지하는 비율. 나머지는 막대가 밀려나 사라지는 2단계.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float revealRatio = 0.7f;

    [Tooltip("진행도(0~1)에 따른 벌어짐 비율. 기본은 EaseInOut.")]
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("PlayReverse(닫기)가 다 닫힌(막대가 가운데서 맞닿은) 뒤, 막대까지 마저 " +
             "페이드아웃하는 데 걸리는 시간(초). 열릴 때의 경로를 그대로 거꾸로 훑기만 하면 " +
             "다 닫힌 자리에 막대(||)가 불투명하게 남는데, 이 시간 동안 그것마저 지운다.")]
    [SerializeField] private float closeFadeOutDuration = 0.15f;

    private RectTransform _leftBarRect;
    private RectTransform _rightBarRect;
    private Coroutine _routine;

    private void Awake()
    {
        if (leftBar != null)
            _leftBarRect = leftBar.rectTransform;

        if (rightBar != null)
            _rightBarRect = rightBar.rectTransform;
    }

    public void Play()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(PlayRoutine());
    }

    /// <summary>열릴 때와 정확히 반대 경로로 닫는다(같은 Apply(t)를 t=1→0으로 훑는다) -
    /// |PAUSE| -> |AUS| -> |U| -> || 처럼 막대가 다시 모이며 텍스트를 가운데서부터 덮는다.
    /// onComplete는 다 닫히고 페이드아웃까지 끝난 뒤 불린다(비워도 된다) - 닫힘 연출이 끝나야
    /// 패널을 꺼도 되는 호출부(OptionsPanel.Close 등)가 이걸로 타이밍을 맞춘다.</summary>
    public void PlayReverse(Action onComplete = null)
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(PlayReverseRoutine(onComplete));
    }

    private IEnumerator PlayRoutine()
    {
        SetBarAlpha(1f);

        var elapsed = 0f;
        while (elapsed < duration)
        {
            // unscaledDeltaTime을 쓴다 - 이 연출은 일시정지 패널이 뜨는 순간(Time.timeScale이
            // 곧 0이 될 수 있는 타이밍)에도 끝까지 재생돼야 한다.
            elapsed += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        Apply(1f);
        _routine = null;
    }

    private IEnumerator PlayReverseRoutine(Action onComplete)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Apply(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        Apply(0f);

        // Apply(0f)은 "다 닫힌" 상태라 막대가 가운데서 맞닿은 채 불투명하다(Apply(t)가
        // 알파를 overshootT로만 계산하고, t=0 부근은 overshootT=0이라 항상 1이기 때문) -
        // 열 때는 그 상태에서 시작해 밖으로 벌어지므로 자연스럽지만, 닫을 때는 그 자리에
        // 막대가 남아 있으면 안 지워진 자국처럼 보인다. 여기서 마저 페이드아웃한다.
        if (closeFadeOutDuration > 0f)
        {
            var fadeElapsed = 0f;
            while (fadeElapsed < closeFadeOutDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                SetBarAlpha(1f - Mathf.Clamp01(fadeElapsed / closeFadeOutDuration));
                yield return null;
            }
        }

        SetBarAlpha(0f);
        _routine = null;
        onComplete?.Invoke();
    }

    private void Apply(float t)
    {
        var revealT = curve.Evaluate(Mathf.Clamp01(t / revealRatio));
        var revealWidth = fullWidth * revealT;

        if (maskRect != null)
            maskRect.sizeDelta = new Vector2(revealWidth, maskRect.sizeDelta.y);

        var halfWidth = revealWidth * 0.5f;

        // 1단계 동안은 막대가 마스크 끝을 그대로 따라간다. 다 드러난 뒤(t > revealRatio)엔
        // 텍스트 폭은 고정해두고 막대만 바깥으로 더 밀어내며 페이드아웃한다.
        var overshootT = Mathf.Clamp01((t - revealRatio) / Mathf.Max(0.0001f, 1f - revealRatio));
        var overshoot = barOvershoot * overshootT;

        if (_leftBarRect != null)
            _leftBarRect.anchoredPosition = new Vector2(-halfWidth - overshoot, _leftBarRect.anchoredPosition.y);

        if (_rightBarRect != null)
            _rightBarRect.anchoredPosition = new Vector2(halfWidth + overshoot, _rightBarRect.anchoredPosition.y);

        SetBarAlpha(1f - overshootT);
    }

    private void SetBarAlpha(float alpha)
    {
        if (leftBar != null)
        {
            var c = leftBar.color;
            c.a = alpha;
            leftBar.color = c;
        }

        if (rightBar != null)
        {
            var c = rightBar.color;
            c.a = alpha;
            rightBar.color = c;
        }
    }
}
