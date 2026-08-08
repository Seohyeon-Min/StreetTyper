using System.Collections;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [Tooltip("배경이 1회 전환 시 이동할 정확한 목표 거리 (한 바퀴 = 17.75)")]
    public float targetDistance = 17.75f;

    [Tooltip("모든 배경이 공통으로 사라질 화면 왼쪽 밖 경계선")]
    public float leftBound = -17.75f;

    [Tooltip("왼쪽 끝에 도달했을 때 오른쪽으로 점프할 총 거리 (17.75 * 2)")]
    public float loopJumpDistance = 35.5f;

    [Header("Acceleration Settings")]
    [Tooltip("트랜지션 시작 시 최고 속도에 도달하기까지 걸리는 시간(초)")]
    public float accelerationDuration = 0.5f;

    [Tooltip("속도가 올라가는 느낌을 조절하는 커브")]
    public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [HideInInspector] public float maxSpeed;

    private float _currentSpeed = 0f;
    private float _totalMoved = 0f; // ★ 누적 이동 거리 추적
    private Coroutine _speedCoroutine;

    // ⚠️ Update 함수는 오차의 주범이므로 완전히 삭제합니다!

    public void StartScroll(float runDuration, float stopDuration)
    {
        float actualAccel = Mathf.Min(accelerationDuration, runDuration);
        float constantTime = Mathf.Max(0f, runDuration - actualAccel);
        float totalAreaTime = (actualAccel * 0.5f) + constantTime + (stopDuration * 0.5f);

        maxSpeed = targetDistance / totalAreaTime;
        _totalMoved = 0f; // 전환 시작 시 이동 거리 초기화

        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(RunRoutine(actualAccel));
    }

    public void StartConstantScroll(float speed)
    {
        if (_speedCoroutine != null)
            StopCoroutine(_speedCoroutine);

        _currentSpeed = Mathf.Max(0f, speed);
        _totalMoved = 0f;
        _speedCoroutine = StartCoroutine(ConstantScrollRoutine());
    }

    private IEnumerator ConstantScrollRoutine()
    {
        while (true)
        {
            ApplyMovement(_currentSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator RunRoutine(float actualAccel)
    {
        float elapsed = 0f;

        // 1. 가속 구간
        while (elapsed < actualAccel)
        {
            elapsed += Time.deltaTime;
            _currentSpeed = Mathf.Lerp(0f, maxSpeed, accelerationCurve.Evaluate(elapsed / actualAccel));
            ApplyMovement(_currentSpeed * Time.deltaTime);
            yield return null;
        }

        _currentSpeed = maxSpeed;

        // 2. 등속 구간 (StageManager가 StopScroll을 부를 때까지 무한 반복)
        while (true)
        {
            ApplyMovement(_currentSpeed * Time.deltaTime);
            yield return null;
        }
    }

    public void StopScroll(float duration)
    {
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(DecelerateRoutine(duration));
    }

    private IEnumerator DecelerateRoutine(float duration)
    {
        // ★ 오차 교정 핵심: 지금까지 진짜로 이동한 거리를 빼서, 정확히 남은 거리만 이동시킵니다!
        float remainingDistance = targetDistance - _totalMoved;

        // 아주 심한 렉으로 이미 목표치를 넘겼다면 역주행하지 않도록 방어
        if (remainingDistance < 0f) remainingDistance = 0f;

        float movedInDecel = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 감속 곡선 공식: t * (2 - t) -> 자연스럽게 속도가 줄어드는 EaseOut 형태
            float targetDistAtT = remainingDistance * (t * (2f - t));
            float step = targetDistAtT - movedInDecel;
            movedInDecel = targetDistAtT;

            ApplyMovement(step);
            yield return null;
        }

        // 도착 후 소수점 이하의 미세한 오차까지 완벽하게 강제 스냅
        float error = remainingDistance - movedInDecel;
        if (Mathf.Abs(error) > 0.0001f)
        {
            ApplyMovement(error);
        }

        _currentSpeed = 0f;
        _speedCoroutine = null;
    }

    // 좌표 이동 및 텔레포트를 담당하는 통합 함수
    private void ApplyMovement(float amount)
    {
        _totalMoved += amount;
        transform.position += Vector3.left * amount;

        // 경계선을 넘으면 오른쪽으로 텔레포트 (기존 오프셋을 완벽하게 유지하며 점프)
        if (transform.position.x <= leftBound)
        {
            transform.position += new Vector3(loopJumpDistance, 0f, 0f);
        }
    }
}
