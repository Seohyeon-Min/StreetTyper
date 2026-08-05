using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 위치를 따라 오브젝트를 살짝 밀어내는 패럴랙스. 화면 중앙을 0으로 두고 마우스가 그
// 중앙에서 얼마나 벗어나 있는지(-1~1)를 보고 그 방향으로 strength만큼 오프셋을 준다 - 배경/
// 장식/메뉴 등 아무 오브젝트에나 붙이면 커서를 따라 미세하게 움직이는 깊이감을 준다.
// 레이어마다 strength를 다르게 주면 서로 다른 속도로 움직여 원근감이 생긴다(강할수록 가까운
// 레이어처럼 보인다).
//
// Keyboard.current를 직접 읽는 이 프로젝트의 다른 입력 코드(InputManager/MenuKeyboardNavigator)와
// 같은 이유로 Input Action 에셋을 우회하고 Mouse.current를 직접 읽는다.
//
// RectTransform(UI)과 일반 Transform(월드 스프라이트 배경) 양쪽에 다 붙을 수 있도록
// anchoredPosition이 아니라 localPosition을 움직인다 - 앵커가 늘어나 있지 않은 보통의 UI
// 항목이라면 localPosition을 움직이는 것과 anchoredPosition을 움직이는 것이 사실상 같다.
public class MouseParallax : MonoBehaviour
{
    [Tooltip("마우스가 화면 가장자리에 있을 때 얼마나 밀려날지(px, 기준 해상도 1920x1080).")]
    [SerializeField] private Vector2 strength = new Vector2(30f, 20f);

    [Tooltip("따라가는 부드러움(초). 0이면 마우스 위치에 즉시 붙는다.")]
    [SerializeField] private float smoothTime = 0.15f;

    [Tooltip("켜면 마우스와 반대 방향으로 밀린다(마우스가 오른쪽에 있으면 왼쪽으로) - 먼 배경이 " +
             "카메라가 그쪽으로 살짝 도는 것처럼 반대로 움직이는 느낌을 낼 때 쓴다. 끄면 마우스 " +
             "쪽으로 같이 따라간다.")]
    [SerializeField] private bool invert;

    private Vector3 _originalPos;
    private Vector2 _velocity;

    private void Awake()
    {
        _originalPos = transform.localPosition;
    }

    private void Update()
    {
        if (Mouse.current == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        var mouse = Mouse.current.position.ReadValue();

        // 화면 중앙을 0으로 두고 좌우/상하 끝을 -1~1로 정규화한다.
        var normalizedX = Mathf.Clamp(mouse.x / Screen.width * 2f - 1f, -1f, 1f);
        var normalizedY = Mathf.Clamp(mouse.y / Screen.height * 2f - 1f, -1f, 1f);

        var direction = invert ? -1f : 1f;
        var targetOffset = new Vector2(normalizedX * strength.x, normalizedY * strength.y) * direction;
        var targetPos = (Vector2)_originalPos + targetOffset;

        var current = (Vector2)transform.localPosition;
        var next = smoothTime > 0f
            ? Vector2.SmoothDamp(current, targetPos, ref _velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime)
            : targetPos;

        transform.localPosition = new Vector3(next.x, next.y, _originalPos.z);
    }
}
