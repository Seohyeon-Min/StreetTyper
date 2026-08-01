using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // 추가: Image 컴포넌트 제어용

// 이번 턴에 쌓인 공격 문장을 플레이어 옆에 세로로 보여주는 순수 뷰. 게임 판단은 하지 않는다 -
// 무엇이 쌓였고 언제 빠지는지는 전부 PendingActionManager의 이벤트를 그대로 따라간다.
//
// 위치를 WorldAnchoredUI로 재사용하지 않고 여기서 직접 잡는 이유:
// WorldAnchoredUI는 CanvasGroup.alpha를 자기가 소유해 LateUpdate마다 1로 되돌린다.
// 이 뷰는 목록이 비었을 때와 턴이 끝났을 때(HandleTimeExpired) alpha로 스스로를 숨기므로,
// 둘을 같은 오브젝트에 붙이면 그 숨김이 매 프레임 덮어써진다. 게다가 같은 GameObject에
// 붙은 두 컴포넌트의 LateUpdate 순서는 보장되지 않아 증상이 들쭉날쭉해진다.
// 위치와 가시성을 한 컴포넌트가 함께 소유하는 쪽이 안전하다.
public class PendingActionView : MonoBehaviour
{
    // 피벗을 여기에 두면 목록이 이 한 점에서 아래로만 자란다.
    private static readonly Vector2 TopMiddlePivot = new Vector2(0.5f, 1f);

    [SerializeField] private PendingActionManager pendingActionManager;

    // ★ 추가: 타이머 종료 시 UI를 통째로 숨기기 위해 타이머 매니저 참조
    [Tooltip("타이머 종료 이벤트를 받기 위해 연결합니다.")]
    [SerializeField] private TimerManager timerManager;

    [Tooltip("문장 하나를 표시할 프리팹. 자기 자신이나 자식에 TMP_Text가 있어야 한다.")]
    [SerializeField] private GameObject entryPrefab;

    [Tooltip("생성된 항목이 들어갈 부모. 보통 Vertical Layout Group이 붙은 오브젝트.")]
    [SerializeField] private Transform container;

    [Header("위치")]
    [Tooltip("따라갈 대상. 03_WORLD의 player를 넣으면 캔버스가 아니라 플레이어를 기준으로 붙는다.\n" +
             "비워두면 따라가지 않고 지금처럼 캔버스 앵커 위치에 그대로 머문다(예전 동작).")]
    [SerializeField] private Transform followTarget;

    [Tooltip("대상 위치에서 밀어낼 픽셀 양(참조 해상도 1920x1080 기준). UI 크기와 같은 단위라 " +
             "위치 조정은 여기서 하는 게 직관적이다. x는 오른쪽, y는 위쪽이 +다.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(-120f, 360f);

    [Tooltip("대상의 어느 지점을 기준으로 삼을지(월드 단위). 보통 0으로 두고 screenOffset으로 맞춘다.\n" +
             "카메라가 orthographic size 5라 1 월드가 1080p에서 약 108픽셀이다 - 1만 넣어도 화면이 훌쩍 움직인다.")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Tooltip("피벗을 TopMiddle(0.5, 1)로 고정해 목록이 위아래로 벌어지지 않고 아래로만 쌓이게 한다.\n" +
             "끄면 인스펙터에서 피벗을 직접 만질 수 있다.")]
    [SerializeField] private bool pinToTop = true;

    // 화면에 떠 있는 항목들. PendingActionManager의 목록과 같은 순서를 유지한다.
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // ★ 추가: 말풍선 배경 이미지와 전체 투명도를 조절할 캔버스 그룹
    private Image _backgroundImage;
    private CanvasGroup _canvasGroup;

    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Camera _camera;

    private void Awake()
    {
        _backgroundImage = GetComponent<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        // CanvasGroup이 없다면 코드로 자동 추가해 줍니다.
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ApplyTopPivot();
        UpdateVisibility();
    }

    private void OnEnable()
    {
        if (pendingActionManager == null)
        {
            Debug.LogWarning("PendingActionView: pendingActionManager가 연결되지 않아 쌓인 공격을 표시할 수 없습니다.", this);
            return;
        }

        pendingActionManager.OnActionQueued += HandleQueued;
        pendingActionManager.OnActionDequeued += HandleDequeued;
        pendingActionManager.OnCleared += HandleCleared;

        // ★ 추가: 타이머 이벤트 구독
        if (timerManager != null)
            timerManager.OnTimeExpired += HandleTimeExpired;

        // 이 뷰가 뒤늦게 켜졌다면 이미 쌓여 있는 것들을 따라잡는다.
        Rebuild();
    }

    private void OnDisable()
    {
        if (pendingActionManager == null)
            return;

        pendingActionManager.OnActionQueued -= HandleQueued;
        pendingActionManager.OnActionDequeued -= HandleDequeued;
        pendingActionManager.OnCleared -= HandleCleared;

        // ★ 추가: 타이머 이벤트 구독 해제
        if (timerManager != null)
            timerManager.OnTimeExpired -= HandleTimeExpired;
    }

    private void HandleQueued(PendingActionManager.Entry entry)
    {
        Spawn(entry);
        UpdateVisibility(); // ★ 텍스트가 추가되었으니 말풍선을 보이게 업데이트
    }

    // 꺼내는 건 항상 가장 먼저 쌓인 것(FIFO)이므로 화면에서도 맨 앞을 지운다.
    // 어느 항목이 나갔는지 매칭하지 않아도 순서만 맞으면 정확하다.
    private void HandleDequeued(PendingActionManager.Entry entry)
    {
        if (_spawned.Count == 0)
            return;

        var oldest = _spawned[0];
        _spawned.RemoveAt(0);

        if (oldest != null)
            Destroy(oldest);

        UpdateVisibility(); // ★ 텍스트가 다 빠졌는지 확인하고 가시성 업데이트
    }

    private void HandleCleared()
    {
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }

        _spawned.Clear();
        UpdateVisibility(); // ★ 버퍼가 비었으니 말풍선 숨김 처리
    }

    private void Rebuild()
    {
        HandleCleared();

        var entries = pendingActionManager.Entries;
        for (var i = 0; i < entries.Count; i++)
            Spawn(entries[i]);

        UpdateVisibility(); // ★ 초기 상태 가시성 업데이트
    }

    // ★ 추가: 버퍼에 아이템이 있는지 확인하여 배경과 투명도를 조절하는 함수
    private void UpdateVisibility()
    {
        bool hasActions = _spawned.Count > 0;

        if (_backgroundImage != null)
            _backgroundImage.enabled = hasActions;

        if (_canvasGroup != null)
            _canvasGroup.alpha = hasActions ? 1f : 0f;
    }

    // ★ 추가: 타이머가 0초가 되면(플레이어 턴 종료) 호출되어 UI를 즉시 투명하게 만듦
    private void HandleTimeExpired()
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    // ── 위치 ────────────────────────────────────────────────────────────────

    // 목록이 아래로만 자라게 하는 건 레이아웃 정렬이 아니라 피벗이다. 피벗이 가운데(0.5)면
    // ContentSizeFitter가 높이를 늘릴 때 위아래로 똑같이 벌어진다. 위(1)로 올리면
    // 그 한 점이 제자리에 고정되고 늘어난 만큼은 전부 아래로 간다.
    private void ApplyTopPivot()
    {
        if (!pinToTop || _rectTransform == null)
            return;

        // 같은 값이면 건드리지 않는다 - 에디터에서 매번 씬이 Dirty로 잡히는 걸 막는다.
        if (_rectTransform.pivot == TopMiddlePivot)
            return;

        _rectTransform.pivot = TopMiddlePivot;
    }

    // 캐릭터가 움직인 뒤에 위치를 잡아야 한 프레임 밀리지 않는다(돌진 연출 중에도 따라붙는다).
    private void LateUpdate()
    {
        // 비워두면 따라가지 않는다. 인스펙터 참조 누락이 아니라 "캔버스 고정"이라는
        // 정상 모드라 경고를 남기지 않는다.
        if (followTarget == null || _rectTransform == null)
            return;

        // Camera.main은 태그 검색이라 매 프레임 부르면 비싸다. 씬이 바뀌면 무효화되므로
        // null일 때만 다시 찾는다.
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return;

        var screenPoint = _camera.WorldToScreenPoint(followTarget.position + worldOffset);

        // screenOffset은 참조 해상도 기준 값이라 실제 픽셀로 바꿀 때 scaleFactor를 곱한다.
        // 이걸 빼면 해상도에 따라 띄운 거리가 UI 크기와 어긋난다.
        var scale = _canvas != null ? _canvas.scaleFactor : 1f;
        screenPoint.x += screenOffset.x * scale;
        screenPoint.y += screenOffset.y * scale;

        // WorldToScreenPoint의 z에는 카메라와의 거리가 담겨 온다. Overlay 캔버스에서는
        // 평면이 z = 0이어야 하므로 그대로 대입하지 않는다.
        screenPoint.z = 0f;

        // Overlay 캔버스에서는 RectTransform.position이 곧 스크린 픽셀 좌표다.
        // 피벗이 TopMiddle이므로 이 점이 목록의 "위쪽 가운데"가 된다.
        _rectTransform.position = screenPoint;
    }

#if UNITY_EDITOR
    // 인스펙터에서 값을 바꾸는 즉시 씬 뷰에 반영되게 한다(Play를 눌러야 확인되는 걸 막는다).
    private void OnValidate()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        ApplyTopPivot();
    }
#endif

    private void Spawn(PendingActionManager.Entry entry)
    {
        if (entryPrefab == null || container == null)
        {
            Debug.LogWarning("PendingActionView: entryPrefab 또는 container가 연결되지 않았습니다.", this);
            return;
        }

        var instance = Instantiate(entryPrefab, container);

        var label = instance.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = entry.SkillName;
        else
            Debug.LogWarning("PendingActionView: entryPrefab에 TMP_Text가 없어 문장을 표시할 수 없습니다.", this);

        _spawned.Add(instance);
    }
}