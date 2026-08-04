using System.Collections;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [Tooltip("배경이 이동할 최대 속도")]
    public float maxSpeed = 15f;

    [Tooltip("배경이 이동할 왼쪽 끝 X 좌표 (화면 밖)")]
    public float leftBound = -25f;

    [Tooltip("배경이 다시 나타날 오른쪽 끝 X 좌표 (화면 밖)")]
    public float rightBound = 25f;

    private float _currentSpeed = 0f;
    private Coroutine _speedCoroutine;

    void Update()
    {
        // 속도가 0보다 클 때만 이동
        if (_currentSpeed > 0f)
        {
            // 왼쪽으로 이동
            transform.position += Vector3.left * _currentSpeed * Time.deltaTime;

            // 왼쪽 경계를 넘어가면 오른쪽 경계로 순간이동 (루프)
            if (transform.position.x <= leftBound)
            {
                transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
            }
        }
    }


    /// <summary>
    /// 트랜지션 시작 시 호출되어 배경을 빠르게 이동시킵니다.
    /// </summary>
    public void StartScroll()
    {
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _currentSpeed = maxSpeed; // 즉시 최대 속도로 이동
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