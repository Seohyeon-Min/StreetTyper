using System;
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

    private bool _inputEnabled;
    private float _backspaceRepeatTimer;

    private void Start()
    {
        EnableInput();
    }

    private void OnDestroy()
    {
        DisableInput();
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

        ChangeHangul();

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
            return;

        CurrentInput += character;
        OnCharacterEntered?.Invoke(character);
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

    private static void ChangeHangul()
    {
        if (Keyboard.current.rightAltKey.wasPressedThisFrame){
                Input.imeCompositionMode = IMECompositionMode.On;
        }
    }

    private static bool IsHangul(char character)
    {
        // Hangul Syllables (가-힣) + Hangul Compatibility Jamo (ㄱ-ㅣ)
        return (character >= '가' && character <= '힣') ||
               (character >= 'ㄱ' && character <= 'ㅣ');
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
