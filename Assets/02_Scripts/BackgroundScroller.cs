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

    // 이제 인스펙터에서 숨기고, 공식에 의해 자동으로 계산됩니다.
    [HideInInspector] public float maxSpeed;

    private float _currentSpeed = 0f;
    private Coroutine _speedCoroutine;

    void Update()
    {
        if (_currentSpeed > 0f)
        {
            transform.position += Vector3.left * _currentSpeed * Time.deltaTime;

            if (transform.position.x <= leftBound)
            {
                transform.position += new Vector3(loopJumpDistance, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// StageManager로부터 시간 정보를 받아, 목표 거리(17.75)에 딱 맞는 최대 속도를 계산하고 가속합니다.
    /// </summary>
    public void StartScroll(float runDuration, float stopDuration)
    {
        float actualAccel = Mathf.Min(accelerationDuration, runDuration);

        // 잘라낸 가속 시간을 바탕으로 완벽한 거리를 위한 최대 속도를 자동 계산합니다.
        float constantTime = Mathf.Max(0f, runDuration - actualAccel);
        float totalAreaTime = (actualAccel * 0.5f) + constantTime + (stopDuration * 0.5f);
        maxSpeed = targetDistance / totalAreaTime;

        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);

        // 코루틴에도 실제 적용된 가속 시간을 넘겨줍니다.
        _speedCoroutine = StartCoroutine(AccelerateRoutine(actualAccel));
    }

    private IEnumerator AccelerateRoutine(float actualAccel) // 파라미터 추가됨
    {
        float elapsed = 0f;
        while (elapsed < actualAccel)
        {
            elapsed += Time.deltaTime;
            _currentSpeed = Mathf.Lerp(0f, maxSpeed, accelerationCurve.Evaluate(elapsed / actualAccel));
            yield return null;
        }
        _currentSpeed = maxSpeed;
        _speedCoroutine = null;
    }

    public void StopScroll(float duration)
    {
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(DecelerateRoutine(duration));
    }

    private IEnumerator DecelerateRoutine(float duration)
    {
        float startSpeed = _currentSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 감속은 기본 Lerp(선형)를 사용해 부드럽게 줄입니다.
            _currentSpeed = Mathf.Lerp(startSpeed, 0f, elapsed / duration);
            yield return null;
        }

        _currentSpeed = 0f;
        _speedCoroutine = null;
    }
}