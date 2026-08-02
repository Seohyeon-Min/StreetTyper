using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>포인터를 항목의 어느 쪽에 붙일지.</summary>
public enum PointerSide
{
    /// <summary>항목 왼쪽 가장자리 (▶ 모양 포인터의 보통 자리)</summary>
    Left,

    /// <summary>항목 오른쪽 가장자리</summary>
    Right,

    /// <summary>항목 한가운데 (테두리/프레임형 포인터용)</summary>
    Center,
}

/// <summary>
/// 메뉴 한 벌을 키보드로 훑는 컴포넌트. 타이틀 버튼 3개와 옵션 창이 같은 것을 하나씩 붙여 쓴다.
///
/// 입력은 <see cref="Keyboard"/>를 직접 읽는다 - 이 프로젝트는 Input Action 에셋/바인딩을
/// 의도적으로 우회하고 있고(InputManager 참조), 확인 키로 쓰려는 Space/Z와 닫기의 X는 어차피
/// 기본 액션 에셋에 없어서 그쪽을 고쳐야 하기 때문이다. 그 에셋은 패키지 기본값이라 영향 범위가
/// 불투명하다.
///
/// ⚠️ 그래서 <b>EventSystem의 "Send Navigation Events"를 꺼야 한다.</b> 켜둔 채로 두면 내장
/// 모듈도 같은 방향키·Enter를 처리해 포커스가 두 칸씩 뛰거나 버튼이 두 번 눌린다. 마우스 클릭은
/// 그 체크박스와 무관하게 그대로 동작한다.
///
/// 포커스 표시는 <see cref="EventSystem.SetSelectedGameObject(GameObject)"/>에 맡긴다 - 어떻게
/// 보일지는 각 Selectable의 Transition(색/스프라이트/애니메이션)이 정하므로 코드가 관여하지 않는다.
/// </summary>
public class MenuKeyboardNavigator : MonoBehaviour
{
    [Header("항목")]
    [Tooltip("훑을 항목들. 화면에 보이는 위->아래 순서로 넣을 것 - 배치를 추측하지 않고 이 순서 " +
             "그대로 움직인다. Button은 확인 키로 눌리고, Slider는 좌우 키로 값이 바뀐다.")]
    [SerializeField] private Selectable[] items = new Selectable[0];

    [Tooltip("끝에서 한 번 더 누르면 반대편 끝으로 넘어간다.")]
    [SerializeField] private bool wrapAround = true;

    [Tooltip("이 오브젝트가 켜질 때 첫 항목에 포커스를 준다. 옵션 창처럼 열릴 때마다 " +
             "맨 위부터 시작해야 하는 메뉴에서 켠다.")]
    [SerializeField] private bool selectFirstOnEnable = true;

    [Header("연결")]
    [Tooltip("닫기 키(ESC/X)를 누르면 이 버튼을 누른 것으로 친다. 옵션 창이면 닫기 버튼을 넣는다 - " +
             "그래야 키와 버튼이 완전히 같은 경로를 탄다. 비워두면 닫기 키는 아무 일도 하지 않는다.")]
    [SerializeField] private Selectable cancelTarget;

    [Tooltip("이 오브젝트가 켜져 있는 동안에는 입력을 받지 않는다. 타이틀 쪽에는 옵션 창을 넣어 " +
             "창이 떠 있는 동안 뒤쪽 버튼이 움직이지 않게 한다.")]
    [SerializeField] private GameObject blockedWhileActive;

    [Header("포인터 (선택)")]
    [Tooltip("지금 포커스된 항목을 가리키며 따라다닐 표식. 비워두면 포인터를 쓰지 않는다. " +
             "항목과 다른 부모(다른 캔버스라도) 밑에 있어도 된다 - 월드 좌표로 맞춘다. " +
             "Image의 Raycast Target은 꺼두는 게 좋다(마우스 클릭을 가로막지 않게).")]
    [SerializeField] private RectTransform pointer;

    [Tooltip("항목의 어느 쪽에 붙일지.")]
    [SerializeField] private PointerSide pointerSide = PointerSide.Left;

    [Tooltip("그 자리에서 더 밀어낼 거리(px, 기준 해상도 1920x1080). 왼쪽에 붙였다면 " +
             "x에 음수를 줘서 항목에서 조금 떨어뜨린다.")]
    [SerializeField] private Vector2 pointerOffset = new Vector2(-20f, 0f);

    [Tooltip("항목 사이를 옮겨갈 때 미끄러지는 시정수(초). 0이면 즉시 튄다.")]
    [SerializeField] private float pointerSmoothTime = 0.08f;

    [Header("슬라이더")]
    [Tooltip("좌우 키 한 번에 움직일 값(슬라이더 범위 0~1 기준이라 0.05면 5%).")]
    [SerializeField] private float sliderStep = 0.05f;

    [Header("키")]
    [SerializeField] private Key[] upKeys = { Key.UpArrow, Key.W };
    [SerializeField] private Key[] downKeys = { Key.DownArrow, Key.S };
    [SerializeField] private Key[] leftKeys = { Key.LeftArrow, Key.A };
    [SerializeField] private Key[] rightKeys = { Key.RightArrow, Key.D };

    [Tooltip("항목을 실행하는 키.")]
    [SerializeField] private Key[] confirmKeys = { Key.Space, Key.Enter, Key.NumpadEnter, Key.Z };

    [Tooltip("창을 닫는 키. cancelTarget이 연결되어 있어야 의미가 있다.")]
    [SerializeField] private Key[] cancelKeys = { Key.Escape, Key.X };

    [Header("키 반복")]
    [Tooltip("키를 누르고 있을 때 반복이 시작되기까지의 시간(초). InputManager의 백스페이스 반복과 같은 방식.")]
    [SerializeField] private float repeatDelay = 0.4f;

    [Tooltip("반복이 시작된 뒤의 간격(초).")]
    [SerializeField] private float repeatInterval = 0.08f;

    private int _index;
    private float _repeatTimer;

    // 직전 프레임에 눌려 있던 방향. 방향이 바뀌면 반복 타이머를 처음부터 다시 잰다.
    private int _heldVertical;
    private int _heldHorizontal;

    // 켜진 그 프레임에는 키를 읽지 않는다. 옵션 창을 여는 Space가 같은 프레임에 옵션 쪽
    // 내비게이터에까지 닿아 첫 항목을 곧바로 눌러버리는 것을 막는다(닫을 때도 같다).
    private int _enabledFrame = -1;

    // 포인터가 아직 한 번도 놓인 적이 없으면 미끄러지지 않고 그 자리에 바로 찍는다 -
    // 메뉴가 열릴 때마다 포인터가 화면 밖에서 날아오면 어색하다.
    private bool _pointerPlaced;

    // GetWorldCorners가 채워줄 버퍼. 매 프레임 부르므로 배열을 새로 만들지 않는다.
    private readonly Vector3[] _corners = new Vector3[4];

    // 포인터 오프셋을 px로 다루기 위해 캔버스의 scaleFactor가 필요하다(해상도가 바뀌어도
    // 항목과 같은 비율로 떨어져 있게). 매니저 의존이 아니라 자기 계층 조회라 캐시해 둔다.
    private Canvas _canvas;

    private void OnEnable()
    {
        _enabledFrame = Time.frameCount;
        _repeatTimer = 0f;
        _heldVertical = 0;
        _heldHorizontal = 0;

        // 다시 열릴 때는 첫 항목에 바로 찍히게 한다(창을 닫을 때 있던 자리에서 미끄러져 오지 않게).
        _pointerPlaced = false;

        if (pointer != null && _canvas == null)
            _canvas = pointer.GetComponentInParent<Canvas>();

        if (items == null || items.Length == 0)
        {
            Debug.LogWarning("MenuKeyboardNavigator: items가 비어 있어 키보드로 움직일 항목이 없습니다.", this);
            return;
        }

        if (selectFirstOnEnable)
            _index = 0;

        Focus(_index);
    }

    private void Update()
    {
        // 막혀 있는 동안에는 포인터도 건드리지 않는다 - 지금 떠 있는 메뉴(옵션 창)가 같은
        // 포인터를 쓰고 있을 수 있어서, 여기서 같이 옮기면 서로 잡아당긴다.
        if (IsBlocked())
            return;

        // 마우스로 다른 항목을 만졌다면 그 자리를 이어받고, 선택이 사라졌다면(옵션 창이 닫혀
        // 선택 오브젝트가 비활성이 된 경우) 우리가 기억하는 자리로 되돌린다.
        SyncWithEventSystem();

        // 키는 켜진 첫 프레임만 건너뛰고, 포인터는 그 프레임에도 놓는다 - 한 프레임이라도
        // 엉뚱한 자리에 떠 있으면 눈에 띈다.
        if (Time.frameCount != _enabledFrame)
            HandleKeys();

        UpdatePointer();
    }

    private void HandleKeys()
    {
        if (WasPressedThisFrame(cancelKeys))
        {
            Cancel();
            return;
        }

        if (WasPressedThisFrame(confirmKeys))
        {
            Confirm();
            return;
        }

        HandleMove();
    }

    private bool IsBlocked()
    {
        if (Keyboard.current == null || items == null || items.Length == 0)
            return true;

        return blockedWhileActive != null && blockedWhileActive.activeInHierarchy;
    }

    // 방향키는 누른 순간 한 번 움직이고, 계속 누르고 있으면 repeatDelay 뒤부터 repeatInterval마다
    // 반복한다. 슬라이더를 좌우로 굴릴 때 한 칸씩 눌러대지 않아도 되게 하는 것이 주 목적이다.
    private void HandleMove()
    {
        var vertical = ReadAxis(downKeys, upKeys);
        var horizontal = ReadAxis(leftKeys, rightKeys);

        // 세로가 우선이다. 대각선으로 눌려도 한 축만 반응해야 예측 가능하다.
        var direction = vertical != 0 ? vertical : horizontal;
        var isVertical = vertical != 0;

        if (direction == 0)
        {
            _heldVertical = 0;
            _heldHorizontal = 0;
            return;
        }

        var changed = isVertical
            ? _heldVertical != direction
            : _heldHorizontal != direction;

        _heldVertical = isVertical ? direction : 0;
        _heldHorizontal = isVertical ? 0 : direction;

        if (changed)
        {
            _repeatTimer = repeatDelay;
            Move(direction, isVertical);
            return;
        }

        _repeatTimer -= Time.unscaledDeltaTime;
        if (_repeatTimer > 0f)
            return;

        _repeatTimer = repeatInterval;
        Move(direction, isVertical);
    }

    private void Move(int direction, bool isVertical)
    {
        if (isVertical)
        {
            MoveFocus(direction);
            return;
        }

        AdjustSlider(direction);
    }

    private void MoveFocus(int direction)
    {
        // direction은 아래가 +1이고 배열도 위->아래 순서라 그대로 더하면 된다.
        var next = _index + direction;

        if (next < 0 || next >= items.Length)
        {
            if (!wrapAround)
                return;

            next = next < 0 ? items.Length - 1 : 0;
        }

        Focus(next);
    }

    private void AdjustSlider(int direction)
    {
        if (Current() is Slider slider)
        {
            // ⚠️ SetValueWithoutNotify가 아니라 value로 넣는다. onValueChanged가 돌아야
            // OptionsPanel이 볼륨을 적용하고 % 라벨을 갱신한다.
            slider.value += sliderStep * direction;
        }
    }

    private void Confirm()
    {
        // 슬라이더는 확인 키에 반응하지 않는다 - 값은 좌우 키로 바꾼다.
        if (Current() is Button button)
            Press(button);
    }

    private void Cancel()
    {
        if (cancelTarget is Button button)
            Press(button);
    }

    private static void Press(Button button)
    {
        if (button != null && button.IsInteractable())
            button.onClick.Invoke();
    }

    private Selectable Current()
    {
        if (_index < 0 || _index >= items.Length)
            return null;

        return items[_index];
    }

    private void Focus(int index)
    {
        _index = Mathf.Clamp(index, 0, items.Length - 1);

        var target = Current();
        if (target == null)
        {
            Debug.LogWarning($"MenuKeyboardNavigator: items[{_index}]가 비어 있어 포커스를 줄 수 없습니다.", this);
            return;
        }

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogWarning("MenuKeyboardNavigator: 씬에 EventSystem이 없어 선택 표시를 할 수 없습니다.", this);
            return;
        }

        eventSystem.SetSelectedGameObject(target.gameObject);
    }

    // 포인터를 지금 포커스된 항목 옆으로 옮긴다. 항목과 부모가 달라도 되도록 월드 좌표로 맞춘다 -
    // 옵션 창의 항목은 Option Panel 밑에 있는데 포인터는 Canvas 밑에 둘 수 있기 때문이다.
    private void UpdatePointer()
    {
        if (pointer == null)
            return;

        var rect = Current() != null ? Current().transform as RectTransform : null;
        if (rect == null)
            return;

        // Overlay 캔버스에서는 UI의 월드 좌표가 곧 화면 픽셀이라, 오프셋에 scaleFactor만
        // 곱하면 해상도가 달라져도 항목과 같은 비율로 떨어져 있다(SpeechBubbleManager와 같은 방식).
        var scale = _canvas != null ? _canvas.scaleFactor : 1f;
        var goal = AnchorWorldPoint(rect) + (Vector3)(pointerOffset * scale);

        if (!_pointerPlaced || pointerSmoothTime <= 0f)
        {
            pointer.position = goal;
            _pointerPlaced = true;
            return;
        }

        // unscaledDeltaTime을 쓴다 - 이 컴포넌트는 일시정지 메뉴(timeScale 0)에도 그대로 붙일 수
        // 있어야 한다. 타이틀은 timeScale이 1이라 값이 같다.
        var t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(pointerSmoothTime, 0.0001f));
        pointer.position = Vector3.Lerp(pointer.position, goal, t);
    }

    private Vector3 AnchorWorldPoint(RectTransform rect)
    {
        // GetWorldCorners: 0 = 좌하, 1 = 좌상, 2 = 우상, 3 = 우하
        rect.GetWorldCorners(_corners);

        switch (pointerSide)
        {
            case PointerSide.Left:
                return (_corners[0] + _corners[1]) * 0.5f;

            case PointerSide.Right:
                return (_corners[2] + _corners[3]) * 0.5f;

            default:
                return (_corners[0] + _corners[2]) * 0.5f;
        }
    }

    // 마우스와 키보드가 같은 선택 상태를 공유하게 한다.
    private void SyncWithEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        var selected = eventSystem.currentSelectedGameObject;

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].gameObject == selected)
            {
                _index = i;
                return;
            }
        }

        // 우리 항목이 아니거나(빈 곳 클릭) 선택이 사라졌다(옵션 창이 닫혔다).
        // 기억하고 있던 자리로 되돌려야 방향키를 다시 눌렀을 때 그 자리에서 이어진다.
        Focus(_index);
    }

    private int ReadAxis(Key[] negative, Key[] positive)
    {
        var value = 0;

        if (IsPressed(negative))
            value -= 1;

        if (IsPressed(positive))
            value += 1;

        return value;
    }

    private static bool IsPressed(Key[] keys)
    {
        if (keys == null)
            return false;

        var keyboard = Keyboard.current;
        for (var i = 0; i < keys.Length; i++)
        {
            if (keys[i] != Key.None && keyboard[keys[i]].isPressed)
                return true;
        }

        return false;
    }

    private static bool WasPressedThisFrame(Key[] keys)
    {
        if (keys == null)
            return false;

        var keyboard = Keyboard.current;
        for (var i = 0; i < keys.Length; i++)
        {
            if (keys[i] != Key.None && keyboard[keys[i]].wasPressedThisFrame)
                return true;
        }

        return false;
    }
}
