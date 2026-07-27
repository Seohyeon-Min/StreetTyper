using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public string CurrentInput { get; private set; } = string.Empty;

    public event Action<char> OnCharacterEntered;
    public event Action OnSubmit;
    public event Action OnBackspace;

    private bool _inputEnabled;

    private void OnDestroy()
    {
        DisableInput();
    }

    public void EnableInput()
    {
        if (_inputEnabled) return;
        _inputEnabled = true;

        if (Keyboard.current != null)
            Keyboard.current.onTextInput += HandleTextInput;
    }

    public void DisableInput()
    {
        if (!_inputEnabled) return;
        _inputEnabled = false;

        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= HandleTextInput;
    }

    public void ClearInput()
    {
        CurrentInput = string.Empty;
    }

    private void Update()
    {
        if (!_inputEnabled || Keyboard.current == null)
            return;

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            HandleBackspace();

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            OnSubmit?.Invoke();
        }
    }

    private void HandleTextInput(char character)
    {
        if (!IsAlphabet(character))
            return;

        CurrentInput += character;
        OnCharacterEntered?.Invoke(character);
    }

    private void HandleBackspace()
    {
        if (CurrentInput.Length == 0)
            return;

        CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
        OnBackspace?.Invoke();
    }

    private static bool IsAlphabet(char character)
    {
        return (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z');
    }
}
