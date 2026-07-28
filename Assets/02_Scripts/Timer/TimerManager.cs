using System;
using UnityEngine;

// 플레이어 턴의 입력 제한 시간을 관리한다. 단어 효과(퀵/잽/훅/어퍼컷 등)에 의한 시간
// 증감은 AddTime으로 받기만 하고, 그 값을 누가 언제 넘기는지는 모른다 - DeckManager가
// ResolvedAction.TimerChange를 체인 완성 시점에 넘겨준다.
public class TimerManager : MonoBehaviour
{
    [Tooltip("GDD의 기본 타이머(예: 10초). RestartTurn()이 매번 이 값으로 되돌아간다.")]
    [SerializeField] private float baseDuration = 10f;

    private bool _running;
    private bool _expiredFired;

    public float RemainingTime { get; private set; }

    /// <summary>이번 턴 카운트다운의 기준값. 슬라이더 max 등 UI가 비율을 계산할 때 쓴다.</summary>
    public float Duration { get; private set; }

    public event Action<float> OnTimeChanged;
    public event Action OnTimeExpired;

    private void Start()
    {
        // 스탠드얼론으로도 굴러가게 자체 시작하지만, 실제 매 턴 재시작은 DeckManager가
        // RestartTurn()을 명시적으로 호출한다.
        RestartTurn();
    }

    public void StartTimer(float duration)
    {
        Duration = duration;
        RemainingTime = duration;
        _running = true;
        _expiredFired = false;
        OnTimeChanged?.Invoke(RemainingTime);
    }

    public void RestartTurn()
    {
        StartTimer(baseDuration);
    }

    public void AddTime(float amount)
    {
        if (!_running || Mathf.Approximately(amount, 0f))
            return;

        RemainingTime = Mathf.Max(0f, RemainingTime + amount);
        OnTimeChanged?.Invoke(RemainingTime);
    }

    public void ReduceTime(float amount)
    {
        AddTime(-amount);
    }

    public void StopTimer()
    {
        _running = false;
    }

    private void Update()
    {
        if (!_running || RemainingTime <= 0f)
            return;

        RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
        OnTimeChanged?.Invoke(RemainingTime);

        if (RemainingTime <= 0f && !_expiredFired)
        {
            _expiredFired = true;
            _running = false;
            OnTimeExpired?.Invoke();
        }
    }
}
