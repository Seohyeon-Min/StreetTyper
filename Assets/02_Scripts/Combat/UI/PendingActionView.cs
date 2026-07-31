using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // 추가: Image 컴포넌트 제어용

// 이번 턴에 쌓인 공격 문장을 플레이어 옆에 세로로 보여주는 순수 뷰. 게임 판단은 하지 않는다 -
// 무엇이 쌓였고 언제 빠지는지는 전부 PendingActionManager의 이벤트를 그대로 따라간다.
public class PendingActionView : MonoBehaviour
{
    [SerializeField] private PendingActionManager pendingActionManager;

    // ★ 추가: 타이머 종료 시 UI를 통째로 숨기기 위해 타이머 매니저 참조
    [Tooltip("타이머 종료 이벤트를 받기 위해 연결합니다.")]
    [SerializeField] private TimerManager timerManager;

    [Tooltip("문장 하나를 표시할 프리팹. 자기 자신이나 자식에 TMP_Text가 있어야 한다.")]
    [SerializeField] private GameObject entryPrefab;

    [Tooltip("생성된 항목이 들어갈 부모. 보통 Vertical Layout Group이 붙은 오브젝트.")]
    [SerializeField] private Transform container;

    // 화면에 떠 있는 항목들. PendingActionManager의 목록과 같은 순서를 유지한다.
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // ★ 추가: 말풍선 배경 이미지와 전체 투명도를 조절할 캔버스 그룹
    private Image _backgroundImage;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _backgroundImage = GetComponent<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();

        // CanvasGroup이 없다면 코드로 자동 추가해 줍니다.
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

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