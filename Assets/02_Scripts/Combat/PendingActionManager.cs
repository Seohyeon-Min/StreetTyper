using System;
using System.Collections.Generic;
using UnityEngine;

// 한 턴 동안 완성된 체인의 계산 결과를 모아뒀다가, 턴이 끝날 때 순서대로 꺼내 쓴다.
// 먼저 완성한 조합이 먼저 나가는 FIFO다 - 화면엔 "쌓이는" 것처럼 보이지만 재생 순서는 쌓인 순서 그대로다.
//
// 여기서는 적용도 재생도 하지 않는다. 꺼내서 CombatManager로 넘기고 사이사이 간격을 두는 것은
// DeckManager(PlayPendingActions)가 맡는다 - 전투 배선의 중심을 한 곳에 유지하기 위한 분리다.
public class PendingActionManager : MonoBehaviour
{
    // 표시용 문장과 확정된 수치를 한 쌍으로 들고 있는다. SkillResolver가 치명타 확률과
    // 콤보 타격 횟수까지 난수로 굴려 정수로 접어둔 결과이므로, 재생 시점에 다시 계산하지 않는다.
    public class Entry
    {
        public string SkillName;
        public ResolvedAction Action;
    }

    [SerializeField] private bool logDebugEvents;

    private readonly List<Entry> _entries = new List<Entry>();

    /// <summary>쌓인 순서 그대로의 목록. 뷰가 순회해서 화면에 쌓아 보여준다.</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    public int Count => _entries.Count;

    public event Action<Entry> OnActionQueued;
    public event Action<Entry> OnActionDequeued;
    public event Action OnCleared;

    public void Enqueue(string skillName, ResolvedAction action)
    {
        if (action == null)
        {
            Debug.LogWarning("PendingActionManager: action이 null이라 쌓지 않았습니다.", this);
            return;
        }

        var entry = new Entry { SkillName = skillName, Action = action };
        _entries.Add(entry);

        if (logDebugEvents)
            Debug.Log($"Pending += [{skillName}] (쌓인 개수: {_entries.Count})", this);

        OnActionQueued?.Invoke(entry);
    }

    /// <summary>가장 먼저 쌓인 것부터 꺼낸다. 비어 있으면 false를 돌려준다.</summary>
    public bool TryDequeue(out Entry entry)
    {
        if (_entries.Count == 0)
        {
            entry = null;
            return false;
        }

        entry = _entries[0];
        _entries.RemoveAt(0);

        if (logDebugEvents)
            Debug.Log($"Pending -> [{entry.SkillName}] (남은 개수: {_entries.Count})", this);

        OnActionDequeued?.Invoke(entry);
        return true;
    }

    /// <summary>남은 것을 전부 버린다. 적을 처치해 재생이 중단됐을 때, 그리고
    /// 스테이지가 바뀌거나 승패가 갈렸을 때 호출된다.</summary>
    public void Clear()
    {
        if (_entries.Count == 0)
            return;

        _entries.Clear();

        if (logDebugEvents)
            Debug.Log("Pending cleared", this);

        OnCleared?.Invoke();
    }
}
