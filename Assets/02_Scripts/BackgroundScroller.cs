using System.Collections;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [Tooltip("배경이 이동할 최대 속도")]
    public float maxSpeed = 15f;

    [Tooltip("모든 배경이 공통으로 사라질 화면 왼쪽 밖 경계선")]
    public float leftBound = -17.75f;

    [Tooltip("왼쪽 끝에 도달했을 때 오른쪽으로 점프할 총 거리 (10 × (1920 ÷ 1080); 17.777) * 2")]
    public float loopJumpDistance = 35.5f;

    [Header("Acceleration Settings")]
    [Tooltip("트랜지션 시작 시 최고 속도에 도달하기까지 걸리는 시간(초)")]
    public float accelerationDuration = 0.5f;

    [Tooltip("속도가 올라가는 느낌을 조절하는 커브")]
    public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    private float _currentSpeed = 0f;
    private Coroutine _speedCoroutine;

    void Update()
    {
        // 속도가 0보다 클 때만 이동
        if (_currentSpeed > 0f)
        {
            // 왼쪽으로 이동
            transform.position += Vector3.left * _currentSpeed * Time.deltaTime;

            // 왼쪽 경계를 넘어가면, 특정 좌표로 강제 이동하는 대신 총 길이만큼 밀어줍니다.
            if (transform.position.x <= leftBound)
            {
                transform.position += new Vector3(loopJumpDistance, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// 트랜지션 시작 시 호출되어 배경을 빠르게 이동시킵니다.
    /// </summary>
    public void StartScroll()
    {
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(AccelerateRoutine());
    }

    private IEnumerator AccelerateRoutine()
    {
        float elapsed = 0f;
        while (elapsed < accelerationDuration)
        {
            elapsed += Time.deltaTime;
            // 커브에 맞춰 0에서 maxSpeed까지 부드럽게 가속합니다.
            _currentSpeed = Mathf.Lerp(0f, maxSpeed, accelerationCurve.Evaluate(elapsed / accelerationDuration));
            yield return null;
        }
        _currentSpeed = maxSpeed;
        _speedCoroutine = null;
    }

    /// <summary>
    /// 트랜지션 종료 시 호출되어 배경 속도를 서서히 늦춰 멈춥니다.
    /// </summary>
    /// <param name="duration">멈추는 데 걸리는 시간(초)</param>
    public void StopScroll(float duration)
    {
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(DecelerateRoutine(duration));
    }

    private IEnumerator DecelerateRoutine(float duration)
    {
        float startSpeed = _currentSpeed;
        float elapsed = 0f;

        // duration 시간 동안 속도를 0으로 부드럽게(Lerp) 줄입니다.
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _currentSpeed = Mathf.Lerp(startSpeed, 0f, elapsed / duration);
            yield return null;
        }

        _currentSpeed = 0f;
        _speedCoroutine = null;
    }
}