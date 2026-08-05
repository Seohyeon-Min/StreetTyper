using System;
using System.Collections;
using UnityEngine;

// 스테이지 시작 배너("STAGE n START!" 등)의 등장/퇴장 연출.
//
// 등장(OnEnable마다 자동 재생): 원래 크기보다 커진 상태 + 투명에서 시작해, 원래 크기로
// 줄어들며 페이드인한다.
//
// 퇴장(PlayExit로 직접 호출): 등장의 거울상이다 - 원래 크기에서 더 작아지며 페이드아웃한 뒤
// 오브젝트를 끈다.
[RequireComponent(typeof(CanvasGroup))]
public class StageStartEffect : MonoBehaviour
{
    [Header("등장 - 확대된 상태에서 줄어들며 페이드인")]
    [Tooltip("시작 크기 배율(원래 크기 대비). 1보다 크면 커진 상태에서 시작해 줄어들며 나타난다.")]
    [SerializeField] private float introStartScale = 1.6f;

    [Tooltip("등장에 걸리는 시간(초).")]
    [SerializeField] private float introDuration = 0.35f;

    [Tooltip("등장 진행 곡선. 기본은 처음에 빠르게 줄어들다 제 크기 근처에서 멈추듯 감속한다(EaseOut).")]
    [SerializeField] private AnimationCurve introCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("퇴장 - 축소하며 페이드아웃")]
    [Tooltip("도착 크기 배율(원래 크기 대비). 1보다 작으면 점점 작아지며 사라진다.")]
    [SerializeField] private float exitEndScale = 0.7f;

    [Tooltip("퇴장에 걸리는 시간(초).")]
    [SerializeField] private float exitDuration = 0.3f;

    [Tooltip("퇴장 진행 곡선. 기본은 EaseIn(초반 느리게, 후반 빠르게 사라진다).")]
    [SerializeField] private AnimationCurve exitCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));

    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;
    private Coroutine _routine;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        // 이 오브젝트가 실제로 화면에 배치된 크기를 "원래 크기"로 캐시한다 - 등장/퇴장이 항상
        // 이 값을 기준으로 시작/도착하므로, 배율이 몇 배든 기준점이 흔들리면 안 된다.
        _originalScale = transform.localScale;
    }

    // SetActive(true)될 때마다(스테이지가 바뀔 때마다) 자동으로 다시 재생된다.
    private void OnEnable()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(IntroRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator IntroRoutine()
    {
        var startScale = _originalScale * introStartScale;

        var elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;
            var t = introCurve.Evaluate(Mathf.Clamp01(elapsed / introDuration));
            transform.localScale = Vector3.LerpUnclamped(startScale, _originalScale, t);
            _canvasGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        transform.localScale = _originalScale;
        _canvasGroup.alpha = 1f;
        _routine = null;
    }

    /// <summary>등장의 거울상 - 더 작아지며 페이드아웃한 뒤 오브젝트를 끈다. onComplete는 항상
    /// 마지막에 불린다(비워도 된다 - StageManager는 지금 이 연출을 기다리지 않고 그냥 지나간다).
    /// deactivateOnComplete를 false로 주면 다 사라진 채로(알파 0, 축소된 크기) 오브젝트는 켜둔
    /// 채로 남겨둔다 - 같은 오브젝트에서 다른 닫힘 연출(예: OptionsPanel의 titleReveals)이
    /// 아직 돌고 있을 때, 여기서 먼저 끝났다고 SetActive(false)를 불러버리면 그 코루틴들이
    /// 그 자리에서 끊긴다. 그런 경우엔 호출부가 모든 연출이 끝난 뒤 직접 꺼야 한다.</summary>
    public void PlayExit(Action onComplete, bool deactivateOnComplete = true)
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ExitRoutine(onComplete, deactivateOnComplete));
    }

    private IEnumerator ExitRoutine(Action onComplete, bool deactivateOnComplete)
    {
        // 지금 크기/알파에서 시작한다(원래 크기로 고정하지 않는다) - 등장이 아직 안 끝난
        // 상태에서 퇴장이 걸려도(스테이지 대기 시간이 아주 짧으면 가능하다) 튀지 않고
        // 이어서 자연스럽게 작아진다.
        var startScale = transform.localScale;
        var endScale = _originalScale * exitEndScale;
        var startAlpha = _canvasGroup.alpha;

        var elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            var t = exitCurve.Evaluate(Mathf.Clamp01(elapsed / exitDuration));
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        if (deactivateOnComplete)
            gameObject.SetActive(false);

        _routine = null;
        onComplete?.Invoke();
    }
}
