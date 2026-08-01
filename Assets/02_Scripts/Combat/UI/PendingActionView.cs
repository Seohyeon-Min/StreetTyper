using System.Collections.Generic;
using UnityEngine;

// 이번 턴에 쌓인 공격 문장을 플레이어 옆에 세로로 보여주는 순수 뷰. 게임 판단은 하지 않는다 -
// 무엇이 쌓였고 언제 빠지는지는 전부 PendingActionManager의 이벤트를 그대로 따라간다.
//
// 아이콘 없는 SpeechBubble이다. 배경 이미지·꼬리·ContentSizeFitter까지 전부 SpeechBubble
// 프리팹(PendingActionBubble.prefab)이 이미 하고 있던 일이라, 여기서 다시 만들지 않고
// bubblePrefab을 Instantiate해서 그 컴포넌트에 위임한다(RewardCardView.cardPrefab,
// SpeechBubbleManager.speechBubblePrefab과 같은 패턴). 다른 점은 텍스트가 하나가 아니라
// 줄바꿈으로 이어붙인 여러 줄이라는 것과, 지속 시간이 아니라 목록이 비었는지로 스스로를
// 숨긴다는 것뿐이다.
//
// 이 오브젝트 자신은 위치만 잡는 빈 앵커다 - bubblePrefab을 자식으로 Instantiate해두면
// 그 안에서 정한 앵커/피벗/꼬리 배치가 그대로 유지된 채 이 오브젝트를 따라다닌다.
public class PendingActionView : MonoBehaviour
{
    [Tooltip("쌓인 문장을 보여줄 말풍선 프리팹(예: PendingActionBubble.prefab). Awake에서 자식으로 Instantiate한다.")]
    [SerializeField] private SpeechBubble bubblePrefab;

    [SerializeField] private PendingActionManager pendingActionManager;

    [Tooltip("타이머 종료 이벤트를 받기 위해 연결합니다.")]
    [SerializeField] private TimerManager timerManager;

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

    [Tooltip("쌓인 줄이 하나 늘어날 때마다 폰트 크기를 이만큼 줄인다. 0으로 두면 항상 프리팹 기본 크기.")]
    [SerializeField] private float fontSizeStepPerLine = 2f;

    [Tooltip("아무리 줄이 많이 쌓여도 이 크기 밑으로는 안 내려간다.")]
    [SerializeField] private float minFontSize = 12f;

    // 쌓인 문장들. PendingActionManager와 같은 순서(FIFO)를 유지한다.
    private readonly List<string> _lines = new List<string>();

    private SpeechBubble _speechBubble;
    private float _baseFontSize;
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Camera _camera;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        if (bubblePrefab != null)
        {
            _speechBubble = Instantiate(bubblePrefab, transform);

            // 프리팹에 지정된 폰트 크기를 기준값으로 기억해둔다 - 줄 수에 따라 여기서부터 줄여나간다.
            if (_speechBubble.bubbleText != null)
                _baseFontSize = _speechBubble.bubbleText.fontSize;
        }
        else
        {
            Debug.LogWarning("PendingActionView: bubblePrefab이 연결되지 않아 쌓인 공격을 표시할 수 없습니다.", this);
        }

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

        if (timerManager != null)
            timerManager.OnTimeExpired -= HandleTimeExpired;
    }

    private void HandleQueued(PendingActionManager.Entry entry)
    {
        _lines.Add(entry.SkillName);
        RefreshText();
        UpdateVisibility();
    }

    // 꺼내는 건 항상 가장 먼저 쌓인 것(FIFO)이므로 화면에서도 맨 앞을 지운다.
    private void HandleDequeued(PendingActionManager.Entry entry)
    {
        if (_lines.Count == 0)
            return;

        _lines.RemoveAt(0);
        RefreshText();
        UpdateVisibility();
    }

    private void HandleCleared()
    {
        _lines.Clear();
        RefreshText();
        UpdateVisibility();
    }

    private void Rebuild()
    {
        _lines.Clear();

        var entries = pendingActionManager.Entries;
        for (var i = 0; i < entries.Count; i++)
            _lines.Add(entries[i].SkillName);

        RefreshText();
        UpdateVisibility();
    }

    private void RefreshText()
    {
        if (_speechBubble == null)
            return;

        _speechBubble.Setup(string.Join("\n", _lines));

        // 줄이 늘어날수록 글자를 작게 - 안 그러면 말풍선이 한없이 커진다.
        if (_speechBubble.bubbleText != null)
        {
            var size = _baseFontSize - fontSizeStepPerLine * _lines.Count;
            _speechBubble.bubbleText.fontSize = Mathf.Max(minFontSize, size);
        }
    }

    // 말풍선 인스턴스 자체를 켜고 끈다. PendingActionView는 별도 오브젝트라 꺼도
    // OnEnable 구독에는 영향이 없다(WorldAnchoredUI가 CanvasGroup을 쓰는 이유였던
    // "같은 오브젝트의 LateUpdate가 멈춘다" 문제가 애초에 생기지 않는다).
    private void UpdateVisibility()
    {
        if (_speechBubble != null)
            _speechBubble.gameObject.SetActive(_lines.Count > 0);
    }

    // 타이머가 0초가 되면(플레이어 턴 종료) 호출되어 말풍선을 즉시 숨김
    private void HandleTimeExpired()
    {
        if (_speechBubble != null)
            _speechBubble.gameObject.SetActive(false);
    }

    // ── 위치 ────────────────────────────────────────────────────────────────

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
        // 피벗/앵커는 bubblePrefab 쪽에서 정한 그대로이므로, 여기서는 이 앵커 오브젝트의
        // 위치만 스크린 좌표로 옮긴다.
        _rectTransform.position = screenPoint;
    }
}
