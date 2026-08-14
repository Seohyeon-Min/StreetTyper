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

    [Tooltip("영문 모드 강제를 다시 걸기까지의 최소 간격(초). 한글이 연타로 들어와도 IMM32 호출이 폭주하지 않게 한다.")]
    [SerializeField] private float imeForceCooldown = 0.2f;

    [Tooltip("한국어 모드에서 물리 키를 받은 뒤 이 시간(초) 안에 들어온 한글 문자 입력은 " +
             "방금 그 키를 IME가 뒤늦게 조합해 보낸 메아리로 보고 버린다. 영문 강제가 통하지 않는 " +
             "환경(브라우저 IME)에서만 의미가 있는 값이라 넉넉하게 잡아도 된다.")]
    [SerializeField] private float syntheticKeyEchoWindow = 0.5f;

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
    private readonly DubeolsikHangulComposer _hangulComposer = new DubeolsikHangulComposer();

    // 마지막으로 물리 키를 조합기에 넣은 시각. 그 직후에 들어오는 한글 문자 입력은 같은
    // 키의 메아리라 버려야 한다(HandleKoreanTextInput 참조).
    private float _lastSyntheticKeyTime = float.NegativeInfinity;

    // 합성 조합이 시작될 때 이미 커밋되어 있던 글자. 조합기는 자기가 만든 글자만 알기 때문에,
    // 앞에 붙어 있던 것(OS IME 폴백으로 들어온 글자 등)을 여기 따로 들고 있어야 한다.
    // 조합기가 비어 있는 상태에서 첫 자모가 들어올 때마다 다시 잡는다.
    private string _syntheticBase = string.Empty;

    /// <summary>지금 타이핑을 받고 있는지. 일시정지가 멈추기 전 상태를 기억했다가 재개할 때
    /// 그대로 되돌리기 위해 필요하다 - 턴 전환 대기처럼 원래 잠겨 있던 중에 멈췄다면
    /// 재개하면서 켜면 안 된다.</summary>
    public bool IsInputEnabled => _inputEnabled;

    /// <summary>
    /// 지금 한글을 <b>우리가 직접</b> 조합하고 있는가(<see cref="DubeolsikHangulComposer"/>).
    ///
    /// 한국어 모드면 플랫폼과 무관하게 언제나 그렇다. 예전에는 WebGL에서만 그랬고 데스크톱은
    /// OS IME(IMM32로 한글 모드 강제)에 맡겼는데, <b>두 방식이 요구하는 한/영 상태가 정반대라</b>
    /// (합성 조합은 영문, OS IME는 한글) 어느 쪽이든 어긋나면 입력이 통째로 죽었다. 웹은 브라우저
    /// IME를 강제할 수단이 없어 더 심했다. 지금은 한 경로로 통일하고 IME는 어느 언어에서도 영문으로
    /// 고정하므로(<see cref="ApplyImeMode"/>) 한/영이 어느 상태든 결과가 같다.
    ///
    /// ⚠️ 플랫폼 분기(<c>#if UNITY_WEBGL</c>)로 되돌리지 말 것. 그러면 에디터 Play에서 실제
    /// 플레이 경로를 한 줄도 밟지 않게 되어 웹에서만 나는 버그가 생긴다.
    /// </summary>
    public bool UsesSyntheticHangul => LanguageSettings.IsKorean;

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
        _hangulComposer.Clear();
        _syntheticBase = string.Empty;
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

        // OS IME가 조합 중이라면 백스페이스를 IME가 먼저 가져가 자모를 지우고, 그 결과가
        // onIMECompositionChange로 들어온다. 여기서 CurrentInput까지 지우면 한 번에 두 글자가 날아간다.
        //
        // ⚠️ 합성 조합(UsesSyntheticHangul)에서는 반드시 꺼야 한다. 그쪽 Composition은 IME가 아니라
        // 우리 조합기가 만든 마지막 음절이라 <b>글자가 있는 동안 항상 채워져 있고</b>, 이 가드가 켜지면
        // IsCompositionStale()이 첫 번째 누름을 IME에게 양보해버려 <b>백스페이스를 두 번 눌러야 한 글자가
        // 지워진다</b>(실제로 웹에서 났던 버그다).
        var isComposing = !UsesSyntheticHangul && !string.IsNullOrEmpty(Composition);

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

        // ⚠️ 한국어의 글자 입력은 문자 이벤트가 아니라 여기서 물리 키로 읽는다.
        if (UsesSyntheticHangul)
            HandleHangulKeyPresses();

        HandleBurnTimeKey();
    }

    /// <summary>
    /// 한국어 모드의 글자 입력. <b>문자 이벤트(onTextInput)가 아니라 물리 키를 직접 읽는다.</b>
    ///
    /// ⭐ OS IME가 한글 모드면 라틴 문자가 아예 오지 않는다 - IME가 키를 가로채 자기 조합에
    /// 써버리기 때문이다. 그래서 예전에는 <b>플레이어가 한/영으로 키보드를 영문에 맞춰두어야만
    /// 입력이 됐다</b>(영문 강제가 실패하거나 쿨다운에 걸린 동안에는 친 글자가 통째로 사라졌다).
    /// 키는 IME보다 아래(Raw Input)에서 읽히므로 한/영이 어느 쪽이든 결과가 같다 -
    /// ESC·백스페이스·Ctrl을 읽는 방식과 같고, 이 프로젝트는 이미 그 경로에 의존하고 있다.
    ///
    /// 두벌식은 애초에 <b>자판 위치</b>로 정의된 배열이라 물리 키로 읽는 쪽이 오히려 정확하다
    /// (라틴 모드는 반대다 - 그쪽은 AZERTY 같은 배열에서 글자가 어긋나므로 문자 이벤트를 쓴다).
    ///
    /// ⚠️ IME 영문 강제(<see cref="ApplyImeMode"/>)를 같이 없애지 말 것. 강제를 그만두면 OS IME가
    /// 자기 조합 오버레이를 화면에 겹쳐 그려 유령 글자가 보인다 - 입력은 여기서 받고, 강제는
    /// 화면을 깨끗하게 유지하는 몫이다.
    /// </summary>
    private void HandleHangulKeyPresses()
    {
        var keyboard = Keyboard.current;

        // Ctrl은 시간 태우기(HandleBurnTimeKey), Alt는 OS 단축키다. 문자 이벤트는 이런 조합키를
        // 알아서 걸러 주지만 물리 키에는 그런 필터가 없으니 여기서 막는다.
        if (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed)
            return;

        var shiftHeld = keyboard.shiftKey.isPressed;

        for (var key = Key.A; key <= Key.Z; key++)
        {
            var control = keyboard[key];
            if (control == null || !control.wasPressedThisFrame)
                continue;

            // 조합기는 대문자를 쌍자음·이중모음으로 읽는다('R'→ㄲ). Shift를 실제로 눌렀는지
            // 직접 보므로 CapsLock에 속지 않는다 - 예전 NormalizeCapsLock이 하던 보정이
            // 물리 키를 읽는 것만으로 필요 없어졌다.
            var character = (char)((shiftHeld ? 'A' : 'a') + (key - Key.A));

            _lastSyntheticKeyTime = Time.unscaledTime;
            AppendHangulKey(character);
        }
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
        if (UsesSyntheticHangul)
        {
            HandleKoreanTextInput(character);
            return;
        }

        // 여기부터는 한국어가 아닌 언어(영어·프랑스어·스페인어·일본어)뿐이다 - 한국어는 위에서
        // 합성 조합 경로로 빠졌다. 넷 다 라틴 알파벳으로 친다.
        if (!IsLatinLetter(character))
        {
            // 한글이 들어왔다 = IME가 한글 모드로 빠졌다는 신호다. 영문으로 되돌린다
            // (숫자·기호는 한글 모드에서도 그대로 들어오므로 신호로 삼지 않는다).
            if (IsHangul(character))
                ApplyImeMode();

            return;
        }

        // 대문자로 정규화한다. 카드 영문 이름도 LanguageSettings.PickCardText에서 대문자로
        // 나오므로, CapsLock/Shift와 무관하게 매칭되고 비교하는 쪽은 Ordinal 그대로 둘 수 있다.
        // ⚠️ 이 둘은 반드시 같이 움직여야 한다 - 한쪽만 바꾸면 매칭이 통째로 깨진다.
        character = char.ToUpperInvariant(character);

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

    /// <summary>
    /// 한국어 모드에 <b>문자 이벤트</b>로 들어온 글자. 글자 입력의 본류는 물리 키
    /// (<see cref="HandleHangulKeyPresses"/>)이고 여기는 <b>영문 강제가 통하지 않는 환경을 위한
    /// 폴백</b>이다 - 브라우저 IME처럼 우리가 모드를 바꿀 수 없는 곳에서 이미 조합된 한글이
    /// 커밋되어 들어오는 경로.
    /// </summary>
    private void HandleKoreanTextInput(char character)
    {
        // 라틴 문자는 방금 물리 키로 이미 받은 그 키의 문자 이벤트다. 여기서 또 넣으면 한 번
        // 누른 키가 두 번 들어간다(문자 이벤트가 같은 프레임의 Update보다 먼저 도착하므로
        // 시간 창으로는 이걸 거를 수 없다 - 아예 받지 않는 게 맞다).
        if (!IsHangul(character))
            return;

        // 한글이 들어왔다 = IME가 한글 모드로 빠졌다는 신호다. 되돌려 둔다.
        ApplyImeMode();

        // 물리 키를 방금 받았다면 이건 그 키를 IME가 뒤늦게 조합해 보낸 메아리다. 우리 조합기가
        // 이미 같은 글자를 만들어 뒀으므로 버린다(강제가 통하는 환경에서는 여기까지 오지도 않는다).
        if (Time.unscaledTime - _lastSyntheticKeyTime < syntheticKeyEchoWindow)
            return;

        // 여기까지 왔으면 물리 키를 못 받고 있다는 뜻이라, 이 글자라도 받아 넣는 게 낫다.
        FlushSynthetic();

        if (CurrentInput.Length >= Mathf.Max(1, maxInputLength))
            return;

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.AddTypedCharacter();

        CurrentInput += character;
        OnCharacterEntered?.Invoke(character);
        OnCompositionChanged?.Invoke(Composition);
        DispatchToReceiver(CurrentInput, Composition);
    }

    /// <summary>
    /// 두벌식 키 하나를 조합기에 넣고 입력창을 갱신한다.
    ///
    /// 조합기가 만든 문자열의 <b>마지막 한 글자를 Composition, 앞부분을 CurrentInput</b>으로 나눠
    /// 싣는다. 그래야 매칭(TypingReceiver)·손패 들림(CardSlotView)이 OS IME 시절과 똑같은 모양의
    /// 입력을 보게 되어, 그쪽 코드를 하나도 고치지 않아도 된다.
    /// </summary>
    private void AppendHangulKey(char character)
    {
        // 조합기가 비어 있다면 지금 화면에 있는 글자가 이번 조합의 접두사다. OS IME가 만든 조합
        // 글자가 남아 있으면(위 폴백 경로) 그것까지 확정해 접두사로 삼는다.
        if (_hangulComposer.KeyCount == 0)
            _syntheticBase = CurrentInput + Composition;

        // 숫자·기호는 두벌식 표에 없어 여기서 버려진다 - 띄어쓰기 없이 잇는 게 규칙이라
        // 버퍼에 들어가면 매칭이 어긋난다(비한국어 경로가 라틴만 받는 것과 같은 이유).
        if (!_hangulComposer.TryAppend(character))
            return;

        var composed = _hangulComposer.Text;
        if (_syntheticBase.Length + composed.Length > Mathf.Max(1, maxInputLength))
        {
            _hangulComposer.Backspace();
            return;
        }

        // 실제로 버퍼에 들어간 글자만 센다. ⚠️ 예전 WebGL 경로는 이 호출이 빠져 있어
        // 한국어 플레이의 CPM이 통째로 0이었다.
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.AddTypedCharacter();

        // ⭐ 완성된 앞쪽 음절은 조합기에서 떼어 커밋된 글자로 옮긴다. 조합기에 마지막 한 음절만
        // 남아야 백스페이스가 "조합 중인 음절은 자모 하나씩, 그 앞은 음절 통째로" 동작한다
        // (DubeolsikHangulComposer.TakeCompletedSyllables 참조). 화면에 보이는 문자열
        // (_syntheticBase + 조합기 결과)은 떼어내기 전과 완전히 같다.
        _syntheticBase += _hangulComposer.TakeCompletedSyllables();

        ApplySyntheticText(_hangulComposer.Text);
        OnCharacterEntered?.Invoke(character);
        OnCompositionChanged?.Invoke(Composition);
        DispatchToReceiver(CurrentInput, Composition);
    }

    // 조합기가 만든 문자열을 입력창 두 칸(커밋 + 조합 중 한 글자)으로 나눠 싣는다.
    private void ApplySyntheticText(string composed)
    {
        if (composed.Length == 0)
        {
            CurrentInput = _syntheticBase;
            Composition = string.Empty;
            return;
        }

        CurrentInput = _syntheticBase + composed.Substring(0, composed.Length - 1);
        Composition = composed.Substring(composed.Length - 1);
    }

    // 조합기가 들고 있던 글자를 커밋된 글자로 확정하고 조합기를 비운다. 합성 조합과 OS IME가
    // 한 버퍼에 섞이는 지점(위 폴백 경로)에서 앞의 것을 먼저 못 박아두는 용도다.
    private void FlushSynthetic()
    {
        if (_hangulComposer.KeyCount > 0)
            CurrentInput = _syntheticBase + _hangulComposer.Text;

        Composition = string.Empty;
        _hangulComposer.Clear();
        _syntheticBase = string.Empty;
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

    /// <summary>
    /// IME 변환 모드를 <b>언제나 영문</b>으로 맞춘다. 한국어를 포함해 어느 언어에서도 OS IME로
    /// 한글을 조합하지 않기 때문이다 - 한글은 우리가 만든다(<see cref="UsesSyntheticHangul"/>).
    ///
    /// ⚠️ 예전처럼 한국어에서 한글 모드를 강제하는 코드로 되돌리지 말 것. 그러면 합성 조합기가
    /// 받아야 할 라틴 키가 IME에게 먼저 잡혀 한글 입력이 통째로 죽는다.
    /// </summary>
    private void ApplyImeMode()
    {
        // 합성 조합 중이라면 Composition은 IME가 아니라 우리 조합기가 소유한 글자다.
        // 여기서 지우면 조합기에는 그대로 남은 채 화면만 어긋난다.
        if (_hangulComposer.KeyCount == 0)
            ClearComposition();

        if (Time.unscaledTime - _lastImeForceTime < imeForceCooldown)
            return;

        _lastImeForceTime = Time.unscaledTime;

        HangulImeMode.SetAlphanumeric();
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
    ///
    /// ⚠️ <b>지금 구조에서는 도달하지 않는다.</b> OS 조합을 어느 언어에서도 받지 않게 되면서
    /// (<see cref="HandleCompositionChange"/>) <see cref="Composition"/>은 합성 조합기 전용 칸이
    /// 됐고, 그래서 Update의 isComposing 가드가 항상 false다. 강제가 통하지 않는 환경을 위한
    /// 안전망으로만 남겨둔 것이니 <b>살아 있는 경로로 읽지 말 것.</b>
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

    /// <summary>
    /// OS IME가 조합을 시작했다 = 변환 모드가 어긋났다는 신호다. <b>어느 언어에서도 받지 않고</b>
    /// 영문으로 되돌린다 - 한국어는 우리 조합기가, 나머지는 라틴 입력이 담당하므로 OS 조합이
    /// 끼어들 자리가 없다. <see cref="Composition"/>은 이제 합성 조합기 전용 칸이다.
    ///
    /// 조합이 시작되는 <b>그 순간</b> 되돌리는 게 중요하다. 커밋 시점까지 기다리면 한글은 다음
    /// 글자를 칠 때까지 커밋되지 않아 그동안 조합 문자열이 남고, 그러면 지울 수도 칠 수도 없는
    /// 상태가 된다(실제로 났던 버그다).
    /// </summary>
    private void HandleCompositionChange(IMECompositionString composition)
    {
        if (composition.Count == 0)
            return;

        ApplyImeMode();
    }

    private void HandleBackspace()
    {
        // ⭐ 한글 지우기는 두 단계다 - <b>조합 중인 마지막 음절은 자모 하나씩</b>, 그게 다 지워지고
        // 나면 <b>그 앞은 음절 통째로</b>. OS IME와 같은 동작이며 다음과 같이 지워진다:
        //
        //     엉엉엉 -> 엉엉어 -> 엉엉ㅇ -> 엉엉 -> 엉 -> (빈 입력)
        //
        // 이게 성립하는 건 조합기가 마지막 한 음절만 들고 있기 때문이다(AppendHangulKey가 앞쪽
        // 완성 음절을 그때그때 커밋된 글자로 떼어낸다). 전부 조합기에 쌓아두면 세 음절이 모두
        // 자모 단위로 분해되어 아홉 번을 눌러야 한다. 아래 커밋된 글자 경로는 그대로 두고
        // 떼어내는 쪽만 바꾸면 되는 게 이 구조의 요점이다.
        //
        // ⚠️ 여기서 DispatchToReceiver를 부르지 않는 것은 아래 경로와 같은 이유다 - 지우는 도중에
        // 평가하면 "펀치가"에서 한 글자를 지운 순간 "펀치"가 매칭되어 카드가 소비된다.
        // (예전 WebGL 경로는 이걸 부르고 있었다.) 화면은 CardSlotView가 매 프레임 폴링하므로
        // 디스패치 없이도 손패 들림은 그대로 따라온다.
        if (UsesSyntheticHangul && _hangulComposer.KeyCount > 0)
        {
            _hangulComposer.Backspace();
            ApplySyntheticText(_hangulComposer.Text);

            OnBackspace?.Invoke();
            OnCompositionChanged?.Invoke(Composition);
            return;
        }

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
