using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 이번 턴에 쌓인 공격 문장을 플레이어 옆에 세로로 보여주는 순수 뷰. 게임 판단은 하지 않는다 -
// 무엇이 쌓였고 언제 빠지는지는 전부 PendingActionManager의 이벤트를 그대로 따라간다.
public class PendingActionView : MonoBehaviour
{
    [SerializeField] private PendingActionManager pendingActionManager;

    [Tooltip("문장 하나를 표시할 프리팹. 자기 자신이나 자식에 TMP_Text가 있어야 한다.")]
    [SerializeField] private GameObject entryPrefab;

    [Tooltip("생성된 항목이 들어갈 부모. 보통 Vertical Layout Group이 붙은 오브젝트.")]
    [SerializeField] private Transform container;

    // 화면에 떠 있는 항목들. PendingActionManager의 목록과 같은 순서를 유지한다.
    private readonly List<GameObject> _spawned = new List<GameObject>();

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
    }

    private void HandleQueued(PendingActionManager.Entry entry)
    {
        Spawn(entry);
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
    }

    private void HandleCleared()
    {
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }

        _spawned.Clear();
    }

    private void Rebuild()
    {
        HandleCleared();

        var entries = pendingActionManager.Entries;
        for (var i = 0; i < entries.Count; i++)
            Spawn(entries[i]);
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
