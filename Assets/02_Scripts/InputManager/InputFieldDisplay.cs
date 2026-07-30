using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TMP_InputField))]
public class InputFieldDisplay : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;

    private TMP_InputField _inputField;

    private void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();
        _inputField.readOnly = true;
    }

    private void OnEnable()
    {
        inputManager.OnCharacterEntered += HandleCharacterEntered;
        inputManager.OnBackspace += Refresh;
        inputManager.OnCompositionChanged += HandleCompositionChanged;
        inputManager.OnInputCleared += Refresh;
    }

    private void OnDisable()
    {
        inputManager.OnCharacterEntered -= HandleCharacterEntered;
        inputManager.OnBackspace -= Refresh;
        inputManager.OnCompositionChanged -= HandleCompositionChanged;
        inputManager.OnInputCleared -= Refresh;
    }

    private void HandleCharacterEntered(char _)
    {
        Refresh();
    }

    private void HandleCompositionChanged(string _)
    {
        Refresh();
    }

    private void Refresh()
    {
        _inputField.SetTextWithoutNotify(inputManager.CurrentInput + inputManager.Composition);
        UpdateImeCursorPosition();
    }

    // Tells the OS where our text ends so the native IME composition overlay
    // (the underlined in-progress Hangul block) is drawn there instead of at
    // whatever position it last defaulted to.
    private void UpdateImeCursorPosition()
    {
        if (Keyboard.current == null)
            return;

        var textComponent = _inputField.textComponent;
        textComponent.ForceMeshUpdate();

        var rectTransform = textComponent.rectTransform;
        var textInfo = textComponent.textInfo;

        Vector3 worldPoint;
        if (textInfo.characterCount > 0)
        {
            var lastChar = textInfo.characterInfo[textInfo.characterCount - 1];
            worldPoint = rectTransform.TransformPoint(lastChar.topRight);
        }
        else
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            worldPoint = corners[1]; // top-left
        }

        var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
        // SetIMECursorPosition expects pixels from the upper-left, moving down and right,
        // while screen points from WorldToScreenPoint are bottom-left origin - flip Y.
        Keyboard.current.SetIMECursorPosition(new Vector2(screenPoint.x, Screen.height - screenPoint.y));
    }
}
