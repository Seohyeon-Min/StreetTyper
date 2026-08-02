using UnityEngine;

// 붙은 오브젝트를 제자리에서 위아래로 천천히 흔든다(사인파). 마더 드래곤처럼 Animator 없는
// 스프라이트에 최소한의 생동감을 주는 용도.
public class FloatBob : MonoBehaviour
{
    [Tooltip("위아래로 움직이는 폭 (월드 단위)")]
    [SerializeField] private float amplitude = 0.1f;

    [Tooltip("왕복 속도. 값이 작을수록 천천히 흔들린다")]
    [SerializeField] private float speed = 1f;

    [Tooltip("켜면 Time.timeScale을 무시하고 계속 흔들린다 (일시정지 화면 위에 놓인 오브젝트용)")]
    [SerializeField] private bool useUnscaledTime = false;

    private Vector3 _originalPos;

    private void OnEnable()
    {
        _originalPos = transform.localPosition;
    }

    private void Update()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float offsetY = Mathf.Sin(time * speed) * amplitude;
        transform.localPosition = _originalPos + new Vector3(0f, offsetY, 0f);
    }
}
