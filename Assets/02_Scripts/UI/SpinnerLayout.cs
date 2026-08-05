using UnityEngine;
using UnityEngine.EventSystems;

// 세로로 늘어선 항목을 슬롯머신 스피너처럼 보여주는 순수 뷰. 지금 포커스된 항목이 가운데
// (거리 0, 알파 1)에 있고, 위아래로 한 칸씩 멀어질 때마다 그만큼 밀려나며 옅어진다.
//
// 게임 판단은 하지 않는다 - "지금 뭐가 선택됐는가"는 EventSystem.currentSelectedGameObject가
// 정하고(MenuKeyboardNavigator가 키보드로, 마우스가 클릭/호버로 채운다), 여기는 그걸 배치·
// 투명도로 비추기만 한다. 타이틀의 게임시작/옵션/게임종료 버튼 3개를 MenuKeyboardNavigator.items와
// 같은 순서로 여기 items에도 넣으면 된다 - 같은 오브젝트를 두 군데서 참조해도 된다.
public class SpinnerLayout : MonoBehaviour
{
    [Tooltip("스피너로 보여줄 항목들. 화면에 보이는 위→아래 순서로 넣을 것 - " +
             "MenuKeyboardNavigator.items와 같은 순서로 넣으면 포커스와 어긋나지 않는다.")]
    [SerializeField] private RectTransform[] items = new RectTransform[0];

    [Tooltip("항목 사이의 세로 간격(px, 기준 해상도 1920x1080).")]
    [SerializeField] private float spacing = 140f;

    [Tooltip("가운데(포커스)에서 한 칸 멀어질 때마다 곱해지는 알파 배율. 0.45면 " +
             "가운데 1 -> 한 칸 0.45 -> 두 칸 0.2로 옅어진다.")]
    [SerializeField, Range(0f, 1f)] private float fadeFalloff = 0.45f;

    [Tooltip("아무리 멀어져도 이보다 더 옅어지지는 않는다. 0이면 끝까지 투명해진다.")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0f;

    [Tooltip("위치/알파가 목표값을 따라가는 시정수(초). 0이면 즉시 튄다.")]
    [SerializeField] private float smoothTime = 0.12f;

    // items와 인덱스가 같다. 매 프레임 GetComponent를 부르지 않도록 미리 모아둔다.
    private CanvasGroup[] _canvasGroups;

    // 메뉴가 뜨는 첫 프레임엔 미끄러져 들어오지 않고 바로 제자리에 놓는다 - 포인터 스냅과
    // 같은 이유(MenuKeyboardNavigator._pointerPlaced 참조).
    private bool _placed;

    private void Awake()
    {
        _canvasGroups = new CanvasGroup[items.Length];

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                Debug.LogWarning($"SpinnerLayout: items[{i}]가 비어 있습니다.", this);
                continue;
            }

            var group = items[i].GetComponent<CanvasGroup>();
            if (group == null)
                Debug.LogWarning($"SpinnerLayout: items[{i}]({items[i].name})에 CanvasGroup이 없어 페이드가 적용되지 않습니다.", this);

            _canvasGroups[i] = group;
        }
    }

    private void OnEnable()
    {
        // 다시 열릴 때마다 제자리에서 바로 시작하게 한다(닫혀 있던 사이 위치가 틀어져
        // 있어도 이전 자리에서 미끄러져 오지 않는다).
        _placed = false;
    }

    // unscaledDeltaTime을 쓴다 - MenuKeyboardNavigator의 포인터 스무딩과 같은 이유로,
    // 이 레이아웃이 나중에 일시정지 메뉴(timeScale 0)에도 그대로 붙을 수 있어야 한다.
    // 타이틀은 timeScale이 항상 1이라 지금 당장은 값이 같다.
    private void Update()
    {
        var focusedIndex = FindFocusedIndex();
        if (focusedIndex < 0)
            return;

        var t = smoothTime > 0f
            ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothTime)
            : 1f;

        var count = items.Length;

        for (var i = 0; i < count; i++)
        {
            if (items[i] == null)
                continue;

            // 순환 목록이라 반대편으로 멀리 도는 것보다 가까운 쪽으로 감아 배치한다 -
            // 3개 중 마지막 항목이 첫 항목 위에서 이어지는 것처럼 보이게(MenuKeyboardNavigator의
            // wrapAround로 포커스가 실제로 그렇게 넘어가므로 시각적으로도 맞춰야 한다).
            var distance = i - focusedIndex;
            if (count > 0)
            {
                if (distance > count / 2f) distance -= count;
                else if (distance < -count / 2f) distance += count;
            }

            var targetPos = new Vector2(items[i].anchoredPosition.x, -distance * spacing);
            var targetAlpha = Mathf.Max(minAlpha, Mathf.Pow(fadeFalloff, Mathf.Abs(distance)));

            if (!_placed)
            {
                items[i].anchoredPosition = targetPos;
                if (_canvasGroups[i] != null)
                    _canvasGroups[i].alpha = targetAlpha;
                continue;
            }

            items[i].anchoredPosition = Vector2.Lerp(items[i].anchoredPosition, targetPos, t);
            if (_canvasGroups[i] != null)
                _canvasGroups[i].alpha = Mathf.Lerp(_canvasGroups[i].alpha, targetAlpha, t);
        }

        _placed = true;
    }

    private int FindFocusedIndex()
    {
        var eventSystem = EventSystem.current;
        var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].gameObject == selected)
                return i;
        }

        // 아직 아무것도 선택되지 않은 첫 프레임이거나(EventSystem이 곧 채워준다) 마우스가
        // 빈 곳을 눌렀을 때의 공백이다 - 첫 항목을 기본값으로 본다.
        return items.Length > 0 ? 0 : -1;
    }
}
