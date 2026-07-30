using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class InputManager : MonoBehaviour
{
    public string CurrentInput { get; private set; } = string.Empty;
    public string Composition { get; private set; } = string.Empty;

    public event Action<char> OnCharacterEntered;
    public event Action OnSubmit;
    public event Action OnBackspace;
    public event Action<string> OnCompositionChanged;
    public event Action OnInputCleared;

    [SerializeField] private float backspaceRepeatDelay = 0.4f;
    [SerializeField] private float backspaceRepeatInterval = 0.05f;

    [Tooltip("한글 모드 강제를 다시 걸기까지의 최소 간격(초). 영문이 연타로 들어와도 IMM32 호출이 폭주하지 않게 한다.")]
    [SerializeField] private float imeForceCooldown = 0.2f;

    private bool _inputEnabled;
    private float _backspaceRepeatTimer;
    private float _lastImeForceTime = float.NegativeInfinity;

    private void Start()
    {
        EnableInput();
    }

    private void OnDestroy()
    {
        DisableInput();
    }

    // 창을 다시 활성화하면 IME 상태가 그동안 다른 앱에서 영문으로 바뀌어 있을 수 있다.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !_inputEnabled)
            return;

        Input.imeCompositionMode = IMECompositionMode.On;
        ForceHangulMode();
    }

    public void EnableInput()
    {
        if (_inputEnabled) return;
        _inputEnabled = true;

        if (Keyboard.current != null)
        {
            Keyboard.current.onTextInput += HandleTextInput;
            Keyboard.current.onIMECompositionChange += HandleCompositionChange;
            Keyboard.current.SetIMEEnabled(true);
        }

        // 조합을 상시 켜둔다. 기본값 Auto는 "텍스트 필드가 선택된 동안"에만 IME를 켜므로,
        // 이 줄이 없으면 플레이어가 입력창을 클릭해 TMP가 대신 On으로 바꿔주기 전까지 한글이
        // 조합되지 않는다. 턴 전환마다 EnableInput이 불리므로 여기가 자동 복구 지점이 된다.
        Input.imeCompositionMode = IMECompositionMode.On;

        // 방금 켠 IME는 이 프레임엔 아직 창에 붙지 않아 ImmGetContext가 빈 컨텍스트를 준다.
        // 한 프레임 뒤에 강제한다.
        if (isActiveAndEnabled)
            StartCoroutine(ForceHangulModeNextFrame());
        else
            ForceHangulMode();
    }

    public void DisableInput()
    {
        if (!_inputEnabled) return;
        _inputEnabled = false;

        if (Keyboard.current != null)
        {
            Keyboard.current.onTextInput -= HandleTextInput;
            Keyboard.current.onIMECompositionChange -= HandleCompositionChange;
            Keyboard.current.SetIMEEnabled(false);
        }

        Composition = string.Empty;
    }

    public void ClearInput()
    {
        CurrentInput = string.Empty;

        // 조합 중인 글자로 단어가 완성된 경우(퀵/잽/훅 등 한 음절 단어), Composition까지
        // 비워주지 않으면 이미 소비된 글자가 화면에 계속 남는다. OS IME 내부 상태는
        // 건드리지 않으므로 뒤늦은 커밋은 CardInputHandler의 에코 방어가 처리한다.
        Composition = string.Empty;

        OnInputCleared?.Invoke();
        OnCompositionChanged?.Invoke(Composition);
    }

    private void Update()
    {
        if (!_inputEnabled || Keyboard.current == null)
            return;

        // While the IME is composing a character, backspace edits the composition
        // itself (reported via onIMECompositionChange) - deleting from CurrentInput
        // here too would double-delete already committed characters.
        var isComposing = !string.IsNullOrEmpty(Composition);

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            if (!isComposing)
                HandleBackspace();
            _backspaceRepeatTimer = backspaceRepeatDelay;
        }
        else if (Keyboard.current.backspaceKey.isPressed)
        {
            _backspaceRepeatTimer -= Time.deltaTime;
            if (_backspaceRepeatTimer <= 0f)
            {
                if (!isComposing)
                    HandleBackspace();
                _backspaceRepeatTimer = backspaceRepeatInterval;
            }
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            OnSubmit?.Invoke();
        }
    }

    private void HandleTextInput(char character)
    {
        if (!IsHangul(character))
        {
            // 라틴 글자가 들어왔다는 건 IME가 영문 모드로 빠졌다는 뜻이다(플레이어가 한/영을
            // 눌렀거나 다른 앱에서 그 상태로 돌아왔거나). 이 글자는 버리고 곧바로 한글 모드를
            // 되돌려, 한 글자만 잃고 계속 타이핑할 수 있게 한다. 한/영 키 자체는 Windows IME가
            // 앱보다 먼저 처리하므로 막을 수 없고, 이 자가 복구가 그 대체책이다.
            // 숫자/공백까지 여기서 IME를 건드리면 조합 중인 글자가 끊길 수 있어 라틴 글자만 본다.
            if (IsLatinLetter(character))
                ForceHangulMode();

            return;
        }

        CurrentInput += character;
        OnCharacterEntered?.Invoke(character);
    }

    private IEnumerator ForceHangulModeNextFrame()
    {
        yield return null;
        ForceHangulMode();
    }

    private void ForceHangulMode()
    {
        // 조합 중에 변환 상태를 다시 쓰면 진행 중인 글자가 끊길 수 있으니 쿨다운으로 묶는다.
        if (Time.unscaledTime - _lastImeForceTime < imeForceCooldown)
            return;

        _lastImeForceTime = Time.unscaledTime;
        HangulImeMode.Force();
    }

    private void HandleCompositionChange(IMECompositionString composition)
    {
        Composition = composition.ToString();
        OnCompositionChanged?.Invoke(Composition);
    }

    private void HandleBackspace()
    {
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
