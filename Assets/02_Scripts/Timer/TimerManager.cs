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

    /// <summary>AddTime/ReduceTime으로 시간이 실제로 증감했을 때만 발생한다(델타 전달).
    /// Update()의 매 프레임 자연 감소나 StartTimer/RestartTurn의 리셋에서는 발생하지 않는다 -
    /// UI가 "효과로 시간이 변했다"는 순간만 골라 반응(색 반짝임 등)할 수 있게 하기 위함이다.</summary>
    public event Action<float> OnTimeAdjusted;

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
        OnTimeAdjusted?.Invoke(amount);

        // 훅/어퍼컷처럼 시간을 깎는 단어가 남은 시간을 0으로 만들 수 있다. 여기서 만료를
        // 확인하지 않으면 타이머가 0에 멈춘 채 턴이 끝나지 않는다(입력도 계속 열려 있게 된다).
        // GDD가 말하는 "타이머가 깎여 턴이 더 빨리 끝나는 리스크"가 바로 이 경로다.
        CheckExpired();
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
        if (!_running)
            return;

        RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
        OnTimeChanged?.Invoke(RemainingTime);

        CheckExpired();
    }

    // 자연 감소(Update)와 단어 효과(AddTime) 양쪽에서 부르는 단일 만료 판정.
    // 한쪽에만 두면 다른 경로로 0이 됐을 때 턴이 끝나지 않는다.
    private void CheckExpired()
    {
        if (_expiredFired || RemainingTime > 0f)
            return;

        _expiredFired = true;
        _running = false;
        OnTimeExpired?.Invoke();
    }
}
