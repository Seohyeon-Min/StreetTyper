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

    /// <summary>이번 턴 카운트다운의 기준값. 슬라이더 max 등 UI가 비율을 계산할 때 쓴다.
    /// 마비 같은 효과로 늘어난 턴에는 BaseDuration보다 커진다.</summary>
    public float Duration { get; private set; }

    /// <summary>보너스가 붙지 않은 기본 제한 시간. UI가 "이번 턴이 평소보다 긴가"를
    /// 판단해 바 길이를 늘릴 때 기준으로 쓴다.</summary>
    public float BaseDuration => baseDuration;

    /// <summary>카운트다운이 실제로 도는 중인가. 턴 사이 대기·결과 화면·보상 화면에서는 false다.
    /// "이번 턴에 몇 초가 흘렀는가"를 읽는 쪽(SkillResolver)이 턴 밖의 값을 읽지 않으려면
    /// 이 값을 같이 봐야 한다 - RemainingTime은 턴이 끝난 뒤에도 그 자리에 남아 있다.</summary>
    public bool IsRunning => _running;

    public event Action<float> OnTimeChanged;
    public event Action OnTimeExpired;

    /// <summary>AddTime/ReduceTime으로 시간이 실제로 증감했을 때만 발생한다(델타 전달).
    /// Update()의 매 프레임 자연 감소나 StartTimer/RestartTurn의 리셋에서는 발생하지 않는다 -
    /// UI가 "효과로 시간이 변했다"는 순간만 골라 반응(색 반짝임 등)할 수 있게 하기 위함이다.</summary>
    public event Action<float> OnTimeAdjusted;

    private void Start()
    {
        // 여기서 카운트다운을 시작하지는 않는다 - 게이지만 가득 채워두고, 실제 시작은
        // 스테이지 시작 대기가 끝날 때 StageManager.BeginStageAfterDelay가 연다.
        // RestartTurn()을 부르면 StageManager.Start()와 실행 순서가 정해져 있지 않아
        // (프로젝트에 스크립트 실행 순서 설정이 없다) 이쪽이 나중에 돌 경우
        // 첫 스테이지의 대기 시간 동안 타이머가 줄어버린다.
        ResetToFull();
    }

    public void StartTimer(float duration)
    {
        Duration = duration;
        RemainingTime = duration;
        _running = true;
        _expiredFired = false;
        OnTimeChanged?.Invoke(RemainingTime);
    }

    /// <summary>이번 턴을 다시 시작한다. bonusSeconds는 상태이상(적 마비)처럼 제한 시간을
    /// 늘려주는 효과가 넘긴다 - Duration이 늘어난 값으로 잡히므로 슬라이더 최대치도 함께 커진다.</summary>
    public void RestartTurn(float bonusSeconds = 0f)
    {
        StartTimer(baseDuration + Mathf.Max(0f, bonusSeconds));
    }

    /// <summary>게이지를 최대치로 되돌리되 카운트다운은 시작하지 않는다. 턴 전환·스테이지 시작
    /// 대기 동안 타이머가 0에 붙어 있는 대신 가득 찬 채로 멈춰 있게 하기 위한 것으로,
    /// 실제 카운트다운 시작은 대기가 끝난 뒤 RestartTurn()이 맡는다.
    /// OnTimeAdjusted는 일부러 쏘지 않는다 - 그건 "단어 효과로 시간이 변했다"는 신호 전용이라
    /// 여기서 쏘면 대기에 들어갈 때마다 TimerView가 초록색으로 반짝인다.</summary>
    public void ResetToFull()
    {
        Duration = baseDuration;
        RemainingTime = baseDuration;
        _running = false;
        _expiredFired = false;
        OnTimeChanged?.Invoke(RemainingTime);
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
