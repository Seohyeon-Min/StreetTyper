using System.Collections;
using UnityEngine;

// 화면 밖 특정 좌표에서 지금 배치된 자리로 날아 들어오는 연출. 타이틀 화면 로고/버튼처럼
// 켜지자마자(OnEnable) 자동 재생되는 용도.
[RequireComponent(typeof(RectTransform))]
public class FlyInAnimation : MonoBehaviour
{
    [Header("시작 위치")]
    [Tooltip("애니메이션을 시작할 anchoredPosition. 여기서 출발해 지금 배치된 자리(도착점)까지 날아온다.")]
    [SerializeField] private Vector2 startPosition;

    [Header("애니메이션")]
    [Tooltip("시작 위치로 순간이동한 뒤, 실제로 날아오기 시작하기까지 대기하는 시간(초). " +
             "여러 개를 시차를 두고 띄울 때 쓴다(0 = 바로 시작).")]
    [SerializeField] private float delay = 0f;

    [Tooltip("재생 시간(초).")]
    [SerializeField] private float duration = 0.6f;

    [Tooltip("진행도(0~1)에 따른 이동 비율. 기본은 EaseOut(초반 빠르게, 도착 근처에서 감속).")]
    [SerializeField] private AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Tooltip("켜질 때(OnEnable)마다 자동 재생할지 - 꺼두면 Play()를 직접 불러야 한다.")]
    [SerializeField] private bool playOnEnable = true;

    private RectTransform _rect;
    private Vector2 _targetPosition;
    private Coroutine _routine;

    private void Awake()
    {
        _rect = (RectTransform)transform;

        // 씬에 배치된 자리를 "도착점"으로 캐시한다 - Play()가 몇 번 불려도 기준이 흔들리지
        // 않게 여기서 한 번만 읽는다(StageStartEffect가 원래 크기를 캐시하는 것과 같은 이유).
        _targetPosition = _rect.anchoredPosition;
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    public void Play()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(FlyInRoutine());
    }

    private IEnumerator FlyInRoutine()
    {
        // 대기 중에도 도착 자리가 아니라 시작 자리에 가만히 있어야 한다 - 안 그러면 원래
        // 자리에 잠깐 나타났다가 시작 좌표로 순간이동하는 게 보인다.
        _rect.anchoredPosition = startPosition;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            _rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, _targetPosition, t);
            yield return null;
        }

        _rect.anchoredPosition = _targetPosition;
        _routine = null;
    }
}
