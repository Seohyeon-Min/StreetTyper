using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 일시정지/결과/보상처럼 화면을 어둡게 덮는 검정 패널이 SetActive(true)로 켜질 때(OnEnable)
// 알파를 0에서 원래 값까지 페이드인한다. 패널을 여는 쪽(PauseManager/BattleManager/
// RewardCardView) 코드를 손대지 않고, 어두운 배경 Image가 있는 오브젝트에 이 컴포넌트만
// 붙이면 된다 - OnEnable은 SetActive(true)든 부모가 켜지든 항상 불린다.
[RequireComponent(typeof(Image))]
public class FadeInBackground : MonoBehaviour
{
    [Tooltip("페이드인에 걸리는 시간(초).")]
    [SerializeField] private float duration = 0.25f;

    private Image _image;

    // 인스펙터에 저장된 원래 알파(예: 0.65). 페이드인의 도착 지점이다.
    private float _targetAlpha;
    private Coroutine _routine;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _targetAlpha = _image.color.a;
    }

    private void OnEnable()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(FadeRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator FadeRoutine()
    {
        SetAlpha(0f);

        var elapsed = 0f;
        while (elapsed < duration)
        {
            // unscaledDeltaTime을 쓴다 - 이 패널이 뜨는 순간(특히 일시정지) timeScale이
            // 곧 0이 될 수 있는 타이밍이라, 보통의 deltaTime이면 페이드가 멈춰버린다.
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(0f, _targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(_targetAlpha);
        _routine = null;
    }

    private void SetAlpha(float alpha)
    {
        var c = _image.color;
        c.a = alpha;
        _image.color = c;
    }
}
