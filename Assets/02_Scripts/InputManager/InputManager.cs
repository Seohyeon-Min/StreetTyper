using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class InputManager : MonoBehaviour
{
    public string CurrentInput { get; private set; } = string.Empty;
    public string Composition { get; private set; } = string.Empty;

    // 아래 넷은 "지금 입력창이 어떤 상태인가"를 그대로 비추는 뷰(InputFieldDisplay)용이다.
    // 게임 판단을 하는 쪽은 이 이벤트가 아니라 TypingReceiver로 등록해서 배타적으로 받는다.
    public event Action<char> OnCharacterEntered;
    public event Action OnBackspace;
    public event Action<string> OnCompositionChanged;
    public event Action OnInputCleared;

    /// <summary>ESC. 일시정지 토글에 쓴다.</summary>
    public event Action OnCancel;

    /// <summary>스페이스. 이벤트 대사를 넘기는 데 쓴다.</summary>
    public event Action OnAdvance;

    /// <summary>Ctrl을 누르고 있는 동안 반복해서 발생한다. 남은 시간을 일부러 깎는 데 쓴다
    /// (얼마나 깎을지는 받는 쪽인 DeckManager가 정한다 - 여기는 입력만 본다).</summary>
    public event Action OnBurnTime;

    [SerializeField] private float backspaceRepeatDelay = 0.4f;
    [SerializeField] private float backspaceRepeatInterval = 0.05f;

    [Tooltip("Ctrl을 누른 뒤 시간이 깎이기 시작할 때까지의 대기(초). 이 시간이 지나기 전에 떼면 " +
             "아무 일도 일어나지 않는다 - 잘못 눌렀을 때를 위한 여유이자, 연타로 공짜 차감을 " +
             "얻지 못하게 하는 장치다.")]
    [SerializeField] private float ctrlRepeatDelay = 0.4f;

    [Tooltip("Ctrl을 계속 누르고 있을 때 시간이 깎이는 간격(초).")]
    [SerializeField] private float ctrlRepeatInterval = 0.1f;

    [Tooltip("한글 모드 강제를 다시 걸기까지의 최소 간격(초). 영문이 연타로 들어와도 IMM32 호출이 폭주하지 않게 한다.")]
    [SerializeField] private float imeForceCooldown = 0.2f;

    [Tooltip("입력창에 쌓아둘 수 있는 최대 글자 수. 오타가 나도 입력을 지우지 않고 플레이어가 " +
             "직접 지우는 방식이라, 무한정 길어지지 않게 상한을 둔다. 가장 긴 단어(uppercut, 8자)보다 " +
             "넉넉해야 한다.")]
    [SerializeField] private int maxInputLength = 12;

    private bool _inputEnabled;
    private float _backspaceRepeatTimer;
    private float _ctrlRepeatTimer;
    private float _lastImeForceTime = float.NegativeInfinity;

    // 직전 백스페이스를 눌렀을 때의 조합 문자열. 눌러도 값이 그대로면 IME가 받지 않은 것이라
    // 미러가 낡았다고 판단한다(IsCompositionStale 참조).
    private string _lastBackspaceComposition;
    private readonly DubeolsikHangulComposer _webHangul = new DubeolsikHangulComposer();

    /// <summary>지금 타이핑을 받고 있는지. 일시정지가 멈추기 전 상태를 기억했다가 재개할 때
    /// 그대로 되돌리기 위해 필요하다 - 턴 전환 대기처럼 원래 잠겨 있던 중에 멈췄다면
    /// 재개하면서 켜면 안 된다.</summary>
    public bool IsInputEnabled => _inputEnabled;

    public bool UsesSyntheticHangul
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LanguageSettings.Current == GameLanguage.Korean;
#else
            return false;
#endif
        }
    }

    // 타이핑을 가져갈 수 있는 대상들. 우선순위 내림차순으로 꽂아 두므로 Dispatch는 앞에서부터
    // 훑기만 하면 된다. 각 수신자가 OnEnable에서 스스로 등록한다 - 인스펙터로 주입받은
    // 참조를 통해 등록하는 것이라 서비스 로케이터가 아니라 CardSlotView.Bind와 같은 명시적 주입이다.
    private readonly List<TypingReceiver> _receivers = new List<TypingReceiver>();

    /// <summary>타이핑 수신자를 등록한다. 우선순위가 높은 쪽이 앞에 오도록 정렬해 넣는다.</summary>
    public void RegisterReceiver(TypingReceiver receiver)
    {
        if (receiver == null || _receivers.Contains(receiver))
            return;

        var index = 0;
        while (index < _receivers.Count && _receivers[index].Priority >= receiver.Priority)
            index++;

        _receivers.Insert(index, receiver);
    }

    public void UnregisterReceiver(TypingReceiver receiver)
    {
        if (receiver != null)
            _receivers.Remove(receiver);
    }

    /// <summary>
    /// 지금 입력을 가져가는 수신자. 없으면 null.
    ///
    /// 우선순위가 높은 쪽부터 훑다가 처음으로 자기 차례라고 답한 곳에서 멈춘다 - 실제 디스패치와
    /// <b>똑같은 판정</b>이라, "누가 입력을 받는가"를 화면 연출 쪽에서 다시 추측할 필요가 없다.
    ///
    /// ⚠️ 캐시하지 않고 부를 때마다 훑는다. 이 프로젝트엔 스크립트 실행 순서 설정이 없어서
    /// 프레임 앞머리에 캐시해두면 "그 프레임에 일시정지가 걸렸는가"를 읽는 쪽마다 다르게 볼 수
    /// 있다. 수신자는 여섯을 넘지 않고 WantsInput()은 전부 필드 검사 수준이라 값이 싸다.
    /// </summary>
    public TypingReceiver ActiveReceiver
    {
        get
        {
            for (var i = 0; i < _receivers.Count; i++)
            {
                var receiver = _receivers[i];
                if (receiver == null || !receiver.isActiveAndEnabled || !receiver.WantsInput())
                    continue;

                return receiver;
            }

            return null;
        }
    }

    /// <summary>이 수신자가 지금 입력을 가져가는 쪽인가. 수신자 자신이 연출을 멈출지 판단할 때 쓴다
    /// (<see cref="TypingReceiver.HasTypingFocus"/>).</summary>
    public bool HasTypingFocus(TypingReceiver receiver) => receiver != null && ActiveReceiver == receiver;

    /// <summary>
    /// 이 단어가 <b>지금 입력을 받는 화면</b>이 노리는 대상인가.
    ///
    /// 카드 한 장(CardSlotView)은 자기가 어느 수신자에 속하는지 모른다 - 손패로도, 일시정지
    /// 명령 카드로도, 결과 화면 명령 카드로도 쓰이기 때문이다. 그래서 "누구의 카드인가"를
    /// 배선으로 들고 다니는 대신 "내 단어가 지금 대상 목록에 있는가"를 묻는다. 일시정지가
    /// 입력을 가져가면 손패 단어는 대상 목록에서 빠지므로, 카드가 알아서 반응을 멈춘다.
    /// </summary>
    public bool IsTypingTarget(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        var receiver = ActiveReceiver;
        if (receiver == null)
            return false;

        var targets = receiver.ActiveTargets;
        if (targets == null)
            return false;

        for (var i = 0; i < targets.Count; i++)
        {
            if (!string.IsNullOrEmpty(targets[i]) && string.Equals(targets[i], word, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 지금 입력을 가져갈 수신자 <b>하나</b>에게만 넘긴다.
    ///
    /// 예전에는 세 핸들러가 전부 이벤트를 받아놓고 각자 timeScale·IsGameOver를 보며 스스로
    /// 비켜섰다. 그 구조에서는 일시정지 중에 명령 단어의 첫 글자가 손패 쪽에서 오타로 처리되어
    /// ClearInput이 불리는 바람에 명령 단어를 끝까지 칠 수 없었다. 여기서 하나만 고르면
    /// 그런 종류의 사고가 구조적으로 일어나지 않는다.
    /// </summary>
    private void DispatchToReceiver(string committed, string composing)
    {
        ActiveReceiver?.Dispatch(committed, composing);
    }

    private void Start()
    {
        EnableInput();
    }

    private void OnDestroy()
    {
        // ⚠️ DisableInput()을 부르지 않는다 - 그 안의 ClearInput이 OnInputCleared 같은 이벤트를
        // 쏘는데, 파괴 시점에는 구독자(InputFieldDisplay 등)가 이미 파괴돼 있을 수 있어
        // MissingReference가 난다. 여기서 필요한 건 키보드 구독을 끊는 것뿐이다.
        _inputEnabled = false;
        UnsubscribeKeyboard();
    }

    // 창을 다시 활성화하면 IME 상태가 그동안 다른 앱에서 바뀌어 있을 수 있다.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !_inputEnabled)
            return;

        Input.imeCompositionMode = IMECompositionMode.On;
        ApplyImeMode();
    }

    // 타이틀에서 언어를 바꾸고 돌아오는 경로 말고, 입력이 이미 열린 상태에서 언어가 바뀌는
    // 경우에도 IME가 따라오게 한다. EnableInput은 이미 켜져 있으면 곧바로 리턴하므로
    // 그것만으로는 반영되지 않는다.
    private void OnEnable()
    {
        LanguageSettings.OnChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        LanguageSettings.OnChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        // 언어가 바뀌면 이전 언어로 치던 글자는 의미가 없다.
        ClearInput();

        if (!_inputEnabled)
            return;

        // 쿨다운을 무시하고 즉시 반영한다 - 사용자가 방금 버튼을 누른 결과라 기다릴 이유가 없다.
        _lastImeForceTime = float.NegativeInfinity;
        ApplyImeMode();
    }

    public void EnableInput()
    {
        if (_inputEnabled) return;
        _inputEnabled = true;

        if (Keyboard.current != null)
        {
            Keyboard.current.onTextInput += HandleTextInput;
            Keyboard.current.onIMECompositionChange += HandleCompositionChange;

            // 영어에서도 IME를 켜둔다. 꺼버리면 창에 IME 컨텍스트가 붙지 않아
            // ImmGetContext가 0을 주고, 영문 모드로 되돌리는 IMM32 호출이 아예 먹지 않는다.
            // 영문 변환 모드에서는 IME가 ASCII를 그대로 통과시키므로 조합도 끼어들지 않는다.
            Keyboard.current.SetIMEEnabled(true);
        }

        // 조합을 상시 켜둔다. 기본값 Auto는 "텍스트 필드가 선택된 동안"에만 IME를 켜므로,
        // 이 줄이 없으면 플레이어가 입력창을 클릭해 TMP가 대신 On으로 바꿔주기 전까지 한글이
        // 조합되지 않는다. 턴 전환마다 EnableInput이 불리므로 여기가 자동 복구 지점이 된다.
        Input.imeCompositionMode = IMECompositionMode.On;

        // 입력이 잠긴 동안 플레이어가 계속 쳤을 수 있다. 우리는 구독을 끊어 못 받았지만
        // OS IME는 그동안에도 조합을 쌓아둘 수 있어서, 그냥 열면 그 글자가 새 턴의 첫 글자에
        // 섞여 들어온다("딜레이 중 타이핑이 막히는 건 보이기에만 그렇다"는 증상이 이것이다).
        // 우리 버퍼를 먼저 비우고, IME 쪽 조합은 컨텍스트가 붙는 다음 프레임에 버리게 한다.
        ClearInput();

        // ⚠️ 키 반복 타이머도 같이 되돌린다. 플레이어가 키를 누른 채로 턴 전환을 지나면
        // wasPressedThisFrame은 이미 지난 턴에서 소비됐고 타이머는 0 이하로 남아 있어,
        // 입력이 열리는 첫 프레임에 대기 없이 곧바로 반복이 터진다.
        _backspaceRepeatTimer = backspaceRepeatDelay;
        _ctrlRepeatTimer = ctrlRepeatDelay;

        // 방금 켠 IME는 이 프레임엔 아직 창에 붙지 않아 ImmGetContext가 빈 컨텍스트를 준다.
        // 한 프레임 뒤에 맞춘다.
        if (isActiveAndEnabled)
            StartCoroutine(BeginInputNextFrame());
        else
            ApplyImeMode();
    }

    public void DisableInput()
    {
        if (!_inputEnabled) return;
        _inputEnabled = false;

        // 입력창에 남아 있던 글자를 여기서 비운다. 부르는 쪽이 ClearInput을 따로 붙이는 걸
        // 잊어도 잠긴 화면에 옛 글자가 남지 않는다(턴 전환 대기 내내 보인다).
        //
        // ⚠️ IME를 끄기 <b>전에</b> 불러야 한다 - 안에서 도는 조합 취소는 IME가 아직
        // 살아 있을 때만 먹는다. 남겨두면 다음에 입력이 열릴 때 그 글자가 뒤늦게 커밋되어
        // 새 턴의 첫 글자에 섞인다.
        ClearInput();

        UnsubscribeKeyboard();
    }

    private void UnsubscribeKeyboard()
    {
        if (Keyboard.current == null)
            return;

        Keyboard.current.onTextInput -= HandleTextInput;
        Keyboard.current.onIMECompositionChange -= HandleCompositionChange;
        Keyboard.current.SetIMEEnabled(false);
    }

    public void ClearInput()
    {
        _webHangul.Clear();
        CurrentInput = string.Empty;

        // 조합 중인 글자로 단어가 완성된 경우(퀵/잽/훅 등 한 음절 단어, 또는 "펀치"의 마지막 "치")
        // 우리 버퍼를 비우는 것만으로는 부족하다. **OS IME는 그 글자를 여전히 붙잡고 있어서**
        // 다음 입력 때 뒤늦게 커밋되어 돌아오고, 입력창에 이전 단어의 마지막 글자가 남는다.
        // IME에게도 조합을 버리라고 알려야 근본적으로 끊긴다.
        if (Composition.Length > 0)
            HangulImeMode.CancelComposition();

        Composition = string.Empty;
        _lastBackspaceComposition = null;

        OnInputCleared?.Invoke();
        OnCompositionChanged?.Invoke(Composition);

        // 수신자에게도 "비었다"를 알려 오타 상태(TypingReceiver의 _notProgressing)를 되돌린다.
        // 이게 없으면 오타 한 번 뒤에는 두 번째 오타부터 아무 반응이 없다.
        //
        // ⚠️ 백스페이스(HandleBackspace)는 일부러 디스패치하지 않는다. 지우는 도중에 평가하면
        // "펀치가"에서 한 글자를 지운 순간 "펀치"가 매칭되어 의도치 않게 카드가 소비된다.
        DispatchToReceiver(CurrentInput, Composition);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // ⚠️ ESC와 스페이스는 _inputEnabled 가드보다 위에 있어야 한다.
        // 턴 전환 대기처럼 타이핑이 잠긴 구간에서도 일시정지는 걸려야 하기 때문이다.
        // 스페이스는 HandleTextInput이 어차피 버리는 문자라 타이핑과 충돌하지 않는다.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            OnCancel?.Invoke();

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            OnAdvance?.Invoke();

        if (!_inputEnabled)
            return;

        // 한글 조합 중에는 백스페이스를 IME가 먼저 가져가 자모를 지우고, 그 결과가
        // onIMECompositionChange로 들어온다. 여기서 CurrentInput까지 지우면 한 번에 두 글자가 날아간다.
        //
        // 영어 모드에서는 이 가드를 걸지 않는다. 조합 단계가 없어서 걸 이유가 없고, IME가 한글로
        // 빠져 조합 문자열이 남아 있을 때 백스페이스까지 막아버리면 플레이어가 입력을 지울
        // 방법이 사라진다(위 HandleCompositionChange 참조 - 그쪽이 근본 원인을 막고 여기는 이중 방어다).
        var isComposing = !LanguageSettings.IsEnglish && !string.IsNullOrEmpty(Composition);

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            if (!isComposing || IsCompositionStale())
                HandleBackspace();
            _backspaceRepeatTimer = backspaceRepeatDelay;
        }
        else if (Keyboard.current.backspaceKey.isPressed)
        {
            _backspaceRepeatTimer -= Time.deltaTime;
            if (_backspaceRepeatTimer <= 0f)
            {
                if (!isComposing || IsCompositionStale())
                    HandleBackspace();
                _backspaceRepeatTimer = backspaceRepeatInterval;
            }
        }

        HandleBurnTimeKey();
    }

    /// <summary>Ctrl을 누르고 있는 동안 <see cref="OnBurnTime"/>을 반복해서 쏜다.
    ///
    /// ⚠️ <b>누른 첫 프레임에는 쏘지 않는다.</b> 백스페이스처럼 wasPressedThisFrame에서 곧바로
    /// 발동하면 탭 한 번당 공짜로 한 틱을 얻어, 연타가 홀드보다 이득이 된다(시간을 태워 퍼펙트를
    /// 키우는 게 목적이라 그건 비용 없는 이득이 되어버린다). 첫 프레임에는 대기만 걸고,
    /// 실제 차감은 반복 분기에서만 한다.</summary>
    private void HandleBurnTimeKey()
    {
        var ctrlHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;

        if (!ctrlHeld)
        {
            _ctrlRepeatTimer = ctrlRepeatDelay;
            return;
        }

        _ctrlRepeatTimer -= Time.deltaTime;
        if (_ctrlRepeatTimer > 0f)
            return;

        _ctrlRepeatTimer = ctrlRepeatInterval;
        OnBurnTime?.Invoke();

        // ⚠️ 이 호출 안에서 턴이 끝날 수 있다. 남은 시간이 0이 되면 TimerManager.AddTime이
        // 그 자리에서 CheckExpired -> OnTimeExpired -> DeckManager의 턴 전환을 동기적으로 돌리고,
        // 그 첫 구간이 DisableInput()을 부른다. 그 뒤로는 이 Update를 계속 진행하면 안 된다.
        if (!_inputEnabled)
            _ctrlRepeatTimer = ctrlRepeatDelay;
    }

    private void HandleTextInput(char character)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (LanguageSettings.Current == GameLanguage.Korean)
        {
            if (!_webHangul.TryAppend(character))
                return;

            var composed = _webHangul.Text;
            if (composed.Length > Mathf.Max(1, maxInputLength))
            {
                _webHangul.Backspace();
                return;
            }

            CurrentInput = composed.Length > 0 ? composed.Substring(0, composed.Length - 1) : string.Empty;
            Composition = composed.Length > 0 ? composed.Substring(composed.Length - 1) : string.Empty;
            OnCharacterEntered?.Invoke(character);
            OnCompositionChanged?.Invoke(Composition);
            DispatchToReceiver(CurrentInput, Composition);
            return;
        }
#endif

        // [수정] 한국어가 아닌 모든 언어(영어, 프랑스어, 스페인어)는 알파벳 입력을 받습니다.
        if (LanguageSettings.Current != GameLanguage.Korean)
        {
            if (!IsLatinLetter(character))
            {
                if (IsHangul(character))
                    ApplyImeMode();

                return;
            }

            // 대문자로 정규화한다. 카드 영문 이름도 LanguageSettings.PickCardText에서 대문자로
            // 나오므로, CapsLock/Shift와 무관하게 매칭되고 비교하는 쪽은 Ordinal 그대로 둘 수 있다.
            // ⚠️ 이 둘은 반드시 같이 움직여야 한다 - 한쪽만 바꾸면 매칭이 통째로 깨진다.
            character = char.ToUpperInvariant(character);
        }
        else if (!IsHangul(character))
        {
            if (IsLatinLetter(character))
                ApplyImeMode();

            return;
        }

        if (CurrentInput.Length >= Mathf.Max(1, maxInputLength))
            return;

        // 실제로 버퍼에 들어간 글자만 센다(길이 상한에 걸려 버려진 글자는 제외).
        // 오타·나중에 지운 글자도 그대로 센다 - 결과 화면의 "1분당 평균 글자 수"는 누적 타이핑 량이다.
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.AddTypedCharacter();

        CurrentInput += character;
        OnCharacterEntered?.Invoke(character);

        DispatchToReceiver(CurrentInput, string.Empty);
    }

    // IME 컨텍스트가 창에 붙은 뒤에 조합을 버리고 변환 모드를 맞춘다. 이 두 가지 모두
    // ImmGetContext가 유효해야 하므로 한 프레임 뒤여야 한다.
    private IEnumerator BeginInputNextFrame()
    {
        yield return null;

        // 잠긴 동안 IME가 쌓아둔 조합을 버린다.
        HangulImeMode.CancelComposition();

        // ⚠️ 조합만이 아니라 <b>입력창 전체</b>를 비운다.
        //
        // EnableInput은 구독을 먼저 걸고(onTextInput) 그 다음에 ClearInput을 부른다. 그런데
        // 입력이 잠긴 동안 플레이어가 계속 쳤다면 OS IME가 그 글자를 붙잡고 있다가 구독이
        // 걸리는 순간 커밋해서 보내는데, 그게 ClearInput <b>뒤에</b> 도착하면 그대로 남는다.
        // 예전에는 여기서 ClearComposition만 해서 조합만 지우고 이미 커밋된 글자는 못 지웠다 -
        // "턴이 넘어갈 때 막 치고 있으면 그때 친 게 새 턴 입력창에 남는" 증상이 이것이다.
        //
        // 새 턴 첫 프레임에 플레이어가 의도적으로 친 글자까지 같이 날아가지만, 그건 16ms짜리
        // 창이라 실제로 칠 수 없는 시간이다.
        ClearInput();

        ApplyImeMode();
    }

    // IME 변환 모드를 지금 언어에 맞춘다. 한국어면 한글, 영어면 영문이다.
    // 한쪽만 강제하면 반대 언어로 바꿨을 때 IME가 이전 상태로 남아 입력이 통째로 사라진다.
    private void ApplyImeMode()
    {
        ClearComposition();

        if (Time.unscaledTime - _lastImeForceTime < imeForceCooldown)
            return;

        _lastImeForceTime = Time.unscaledTime;

        // [수정] 현재 언어가 한국어일 때만 한글 IME를 강제하고, 나머지는 영문 모드로 강제합니다.
        HangulImeMode.SetHangul(LanguageSettings.Current == GameLanguage.Korean);
    }

    // 조합 미러만 비운다. CurrentInput(커밋된 글자)은 건드리지 않는다 - 플레이어가 지금까지
    // 친 것은 그대로 남아 있어야 하고, 지우는 건 백스페이스의 몫이다.
    private void ClearComposition()
    {
        if (Composition.Length == 0)
            return;

        Composition = string.Empty;
        _lastBackspaceComposition = null;
        OnCompositionChanged?.Invoke(Composition);
    }

    /// <summary>
    /// 조합이 살아 있다면 백스페이스는 IME가 받아 조합 글자를 바꾼다. 눌렀는데도 조합 문자열이
    /// 직전과 똑같다면 아무도 받지 않았다는 뜻 - 미러가 낡은 것이다.
    ///
    /// 조합 도중에 한/영을 누르고 곧바로 백스페이스를 치는 경우가 여기 걸린다. 그 경로에는
    /// 모드를 되돌릴 입력이 없어서 ApplyImeMode가 불리지 않고, 그대로 두면 그 턴 내내
    /// 지울 수도 칠 수도 없다.
    /// </summary>
    private bool IsCompositionStale()
    {
        if (_lastBackspaceComposition != Composition)
        {
            // 첫 백스페이스는 IME에게 양보한다. 조합이 살아 있다면 이 입력으로 바뀔 것이다.
            _lastBackspaceComposition = Composition;
            return false;
        }

        ClearComposition();
        return true;
    }

    private void HandleCompositionChange(IMECompositionString composition)
    {
        var text = composition.ToString();

        // [수정] 한국어가 아닐 때 한글 조합이 들어오면 무시하고 영문 모드로 돌립니다.
        if (LanguageSettings.Current != GameLanguage.Korean && !string.IsNullOrEmpty(text))
        {
            ApplyImeMode();
            return;
        }

        Composition = text;
        _lastBackspaceComposition = null;
        OnCompositionChanged?.Invoke(Composition);

        DispatchToReceiver(CurrentInput, Composition);
    }

    private void HandleBackspace()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (LanguageSettings.Current == GameLanguage.Korean)
        {
            if (!_webHangul.Backspace())
                return;

            var composed = _webHangul.Text;
            CurrentInput = composed.Length > 0 ? composed.Substring(0, composed.Length - 1) : string.Empty;
            Composition = composed.Length > 0 ? composed.Substring(composed.Length - 1) : string.Empty;
            OnBackspace?.Invoke();
            OnCompositionChanged?.Invoke(Composition);
            DispatchToReceiver(CurrentInput, Composition);
            return;
        }
#endif

        if (CurrentInput.Length == 0)
            return;

        CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
        OnBackspace?.Invoke();
    }

    private static bool IsHangul(char character)
    {
        // Hangul Syllables (가-힣) + Hangul Compatibility Jamo (ㄱ-ㅣ)
        return (character >= '가' && character <= '힣') ||
               (character >= 'ㄱ' && character <= 'ㅣ');
    }

    // IME가 영문 모드로 빠졌다는 신호. 숫자/기호는 한글 모드에서도 그대로 들어오므로 제외한다.
    private static bool IsLatinLetter(char character)
    {
        return (character >= 'a' && character <= 'z') ||
               (character >= 'A' && character <= 'Z');
    }

    // 표준 유니코드 한글 음절 분해 공식의 초성 테이블. 순서가 정해져 있어 임의로 바꾸면 안 된다.
    private static readonly char[] LeadConsonants =
    {
        'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ',
        'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'
    };

    /// <summary>
    /// 완성형 음절(가-힣)이면 그 음절의 초성을, 아직 모음이 안 붙은 낱자음(예: 조합 중인 "ㅋ")이면
    /// 그 자체를 반환한다. 모음이거나 한글이 아니면 null - "쿠"/"퀴"/"퀵"처럼 조합이 진행돼도
    /// 초성은 그대로라, 조합 중인 글자가 어떤 완성 음절을 향하고 있는지 초성 단위로 비교할 수 있다.
    /// </summary>
    public static char? GetLeadConsonant(char character)
    {
        if (character >= '가' && character <= '힣')
            return LeadConsonants[(character - 0xAC00) / (21 * 28)];

        if (Array.IndexOf(LeadConsonants, character) >= 0)
            return character;

        return null;
    }

    /// <summary>
    /// committed(커밋된 글자들) 뒤에 composing(조합 중인 한 글자, 없으면 빈 문자열)이 이어져도
    /// target 단어를 향해 여전히 유효하게 진행 중인지 판단한다. committed는 정확한 접두사여야
    /// 하고, composing이 있다면 그 초성이 target의 다음 글자 초성과 같아야 한다 - 모음이 아직
    /// 안 붙었거나(예: "ㅋ") 잘못된 모음을 짚었어도 초성만 맞으면 유효한 것으로 본다. 매칭
    /// 로직(CardInputHandler)과 손패 들림 애니메이션(CardSlotView)이 같은 판정을 공유한다.
    /// </summary>
    public static bool IsValidProgress(string committed, string composing, string target)
    {
        if (!target.StartsWith(committed, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrEmpty(composing))
            return true;

        if (committed.Length >= target.Length)
            return false;

        var targetLead = GetLeadConsonant(target[committed.Length]);
        var composingLead = GetLeadConsonant(composing[0]);

        return targetLead.HasValue && composingLead.HasValue && targetLead == composingLead;
    }
}
