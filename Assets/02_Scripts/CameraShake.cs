using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    [Tooltip("화면 흔들림 전체 배율 (이 값을 키우면 흔들립니다)")]
    public float shakeMultiplier = 1.5 f;

    private Vector3 _originalPos;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        _originalPos = transform.localPosition;
    }

    public void Shake(float duration, float magnitude)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(duration, magnitude * shakeMultiplier));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = new Vector3(_originalPos.x + x, _originalPos.y + y, _originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = _originalPos;
    }
}