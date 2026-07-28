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
        OnInputCleared?.Invoke();
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
            if(Input.imeCompositionMode == IMECompositionMode.Auto){
                Input.imeCompositionMode = IMECompositionMode.On;
            } else
            {
                Input.imeCompositionMode = IMECompositionMode.Auto;
            }
        }
    }

    private static bool IsHangul(char character)
    {
        // Hangul Syllables (가-힣) + Hangul Compatibility Jamo (ㄱ-ㅣ)
        return (character >= '가' && character <= '힣') ||
               (character >= 'ㄱ' && character <= 'ㅣ');
    }
}
