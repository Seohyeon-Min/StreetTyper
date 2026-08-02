using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타이핑 입력을 가져갈 수 있는 대상의 우선순위. 값이 클수록 먼저 가져간다.
///
/// 인스펙터에 노출하지 않는 코드 레벨 불변식이다 - 일시정지 메뉴가 전투 손패에 밀리는 일은
/// 있어서는 안 되고, 그런 건 디자이너가 조정할 값이 아니다.
/// </summary>
public enum TypingPriority
{
    /// <summary>손패 매칭. 항상 자기 차례라고 선언하는 최하위 폴백이다.</summary>
    Battle = 0,

    /// <summary>결과 화면 명령 단어. 전투가 끝나면 손패보다 먼저 가져간다.</summary>
    Result = 10,

    /// <summary>클리어 보상 카드 선택. 결과 화면과 같은 구간에 뜨지만 <b>먼저</b> 가져간다 -
    /// 보상을 정하기 전에는 "다음"으로 넘어가지 못하게 하는 게이트가 이 순서 하나로 성립한다.</summary>
    Reward = 15,

    /// <summary>일시정지 명령 단어. 무엇보다 우선한다.</summary>
    Pause = 20,

    /// <summary>일시정지 안에서 열리는 보유 카드 목록. 일시정지 메뉴 <b>위에</b> 겹쳐 뜨므로
    /// 그보다 먼저 가져간다 - 목록이 열려 있는 동안 "계속"/"타이틀"이 먹으면 안 된다.</summary>
    CardCollection = 30,
}

/// <summary>
/// 타이핑으로 단어를 맞히는 대상의 공통 뼈대. 세 곳(손패 / 일시정지 / 결과 화면)이 같은
/// 파이프라인을 복붙하고 있던 것을 여기 하나로 모았다.
///
/// ⚠️ <b>스스로 InputManager 이벤트를 구독하지 않는다.</b> InputManager가 등록된 수신자 중
/// 우선순위가 가장 높으면서 <see cref="WantsInput"/>이 true인 <b>딱 하나</b>에게만 넘겨준다.
/// 예전에는 셋이 전부 이벤트를 받아놓고 각자 timeScale·IsGameOver를 보며 스스로 비켜섰는데,
/// 그러면 네 번째가 붙을 때마다 기존 셋의 가드를 전부 손봐야 했다. 이제 "내가 언제 활성인가"만
/// 각자 알면 되고, 중복 수신은 구조적으로 불가능하다.
/// </summary>
public abstract class TypingReceiver : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("타이핑 입력을 넘겨줄 매니저. 여기에 자기 자신을 등록한다.")]
    [SerializeField] protected InputManager inputManager;

    /// <summary>이 수신자가 입력을 가져가는 순서. 클수록 먼저다.</summary>
    public abstract TypingPriority Priority { get; }

    /// <summary>지금 이 수신자가 입력을 가져가야 하는가. 활성 조건을 아는 곳에 판단을 남긴다.</summary>
    public abstract bool WantsInput();

    /// <summary>지금 노려야 할 단어들. 인덱스가 <see cref="OnCommandMatched"/>로 그대로 넘어간다.
    /// 글자마다 불리므로 <b>매번 새 목록을 만들지 말고 캐시한 배열을 채워서 돌려줄 것.</b>
    /// 빈 항목은 매칭과 진행 판정 양쪽에서 건너뛰므로 null 대신 빈 문자열을 넣으면 안전하다.</summary>
    protected abstract IReadOnlyList<string> Targets { get; }

    /// <summary>단어가 정확히 맞았을 때. 입력창은 이미 비워진 뒤에 불린다.</summary>
    protected abstract void OnCommandMatched(int index, bool wasComposing);

    /// <summary>
    /// 어느 단어로도 이어지지 않게 된 순간 <b>한 번만</b> 불린다.
    ///
    /// ⚠️ 기본 정책은 <b>입력창을 비우지 않는 것</b>이다. 잘못 친 글자를 곧바로 지워버리면
    /// 플레이어가 자기가 무엇을 틀렸는지 볼 수가 없다. 화면에 남겨두고 백스페이스로 직접 지우게
    /// 한다(길이 상한은 InputManager.maxInputLength가 맡는다).
    ///
    /// 그래서 오타 뒤에는 이어서 쳐도 매칭되지 않는다 - 매칭은 버퍼 전체와의 정확 일치라
    /// 앞의 잘못된 글자가 남아 있는 한 어떤 단어도 완성되지 않는다. 지우는 건 플레이어 몫이다.
    ///
    /// 하위 클래스는 여기에 소리·연출 같은 피드백만 얹으면 된다.
    /// </summary>
    protected virtual void HandleTypo()
    {
    }

    // 조합 중 매칭으로 단어를 소비하면 OS IME는 그 글자를 여전히 조합 중이라고 알고 있어서,
    // 다음 글자를 이어 치는 순간 뒤늦게 커밋되어 되돌아온다. 그 메아리를 한 번만 걸러낸다.
    private string _pendingEcho;

    // 지금 입력이 어떤 단어로도 이어지지 않는 상태인지. 입력을 자동으로 지우지 않는 수신자가
    // 있어서 그 상태가 여러 글자에 걸쳐 이어지는데, HandleTypo는 들어선 순간에만 한 번 부른다.
    private bool _notProgressing;

    protected virtual void OnEnable()
    {
        if (inputManager == null)
        {
            Debug.LogWarning($"{GetType().Name}: inputManager가 연결되지 않아 타이핑을 받을 수 없습니다.", this);
            return;
        }

        inputManager.RegisterReceiver(this);
    }

    protected virtual void OnDisable()
    {
        if (inputManager != null)
            inputManager.UnregisterReceiver(this);
    }

    /// <summary>
    /// InputManager가 "지금은 네 차례다"라고 판단해 넘겨준 입력을 평가한다.
    /// committed는 커밋된 글자들, composing은 IME가 아직 조합 중인 글자(없으면 빈 문자열)다.
    ///
    /// 커밋 경로에서 composing을 비워 넘기는 규칙은 InputManager가 지킨다 - 커밋되는 순간
    /// Composition은 아직 방금 커밋된 옛 값을 들고 있어서, 그대로 이어붙이면 "펀펀"처럼 중복된다.
    /// </summary>
    internal void Dispatch(string committed, string composing)
    {
        var typed = committed + composing;

        // ClearInput()이 비었음을 알려 오면 오타 상태를 되돌린다.
        // 이 조기 리턴이 매칭 경로의 재진입(ClearInput -> Dispatch)도 함께 끊는다.
        if (typed.Length == 0)
        {
            _notProgressing = false;
            return;
        }

        // 메아리는 커밋되어 committed에 들어왔을 때만 판정한다. 조합 단계에서 표시를 써버리면
        // 정작 커밋된 메아리를 걸러내지 못한다.
        if (_pendingEcho != null && committed.Length > 0)
        {
            var echo = _pendingEcho;
            _pendingEcho = null;

            if (committed == echo)
            {
                inputManager.ClearInput();
                return;
            }
        }

        var targets = Targets;
        if (targets == null)
            return;

        for (var i = 0; i < targets.Count; i++)
        {
            if (string.IsNullOrEmpty(targets[i]) || targets[i] != typed)
                continue;

            var wasComposing = composing.Length > 0;
            _notProgressing = false;

            inputManager.ClearInput();

            // 조합 중이던 글자로 맞혔다면 OS IME는 아직 그 글자를 붙잡고 있다.
            // ⚠️ 표시는 반드시 ClearInput 뒤에 남겨야 한다 - ClearInput이 이 메서드를
            // 재진입시키므로 앞에 두면 그 자리에서 지워진다.
            // ⚠️ 돌아오는 건 단어 전체가 아니라 조합 중이던 그 글자다("펀치"를 조합 중에
            // 맞히면 커밋된 건 "펀", 메아리로 오는 건 "치").
            if (wasComposing)
                _pendingEcho = composing;

            OnCommandMatched(i, wasComposing);
            return;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            if (string.IsNullOrEmpty(targets[i]))
                continue;

            if (InputManager.IsValidProgress(committed, composing, targets[i]))
            {
                _notProgressing = false;
                return;
            }
        }

        // 어긋나기 시작한 순간에만 한 번 알린다. 매 글자마다 부르면 로그와 연출이 폭주한다.
        if (_notProgressing)
            return;

        _notProgressing = true;
        HandleTypo();
    }
}
