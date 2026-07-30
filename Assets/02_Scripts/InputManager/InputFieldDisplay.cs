using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 순수 뷰. InputManager의 커밋된 문자 + 조합 중인 문자를 TMP 라벨에 비춘다.
///
/// 일부러 TMP_InputField가 아니라 그냥 TextMeshProUGUI다. 입력 필드를 쓰면 (1) 선택될 때
/// imeCompositionMode를 On으로, 해제될 때 Auto로 되돌려 InputManager가 소유해야 할 IME 상태를
/// 뺏어가고, (2) 클릭 가능한 UI가 되어 플레이어가 "입력창을 눌러야 하나?" 하고 헷갈린다.
/// 타이핑은 씬 어디에도 포커스 없이 InputManager가 직접 받는다.
/// </summary>
public class InputFieldDisplay : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;

    [Tooltip("타이핑한 글자를 비출 TMP 라벨. 입력 필드가 아니라 그냥 텍스트여야 한다.")]
    [SerializeField] private TextMeshProUGUI text;

    private void OnEnable()
    {
        if (inputManager == null || text == null)
        {
            Debug.LogWarning($"{nameof(InputFieldDisplay)}: " +
                             $"{(inputManager == null ? nameof(inputManager) : nameof(text))}가 비어 있어 입력창이 갱신되지 않는다.", this);
            return;
        }

        inputManager.OnCharacterEntered += HandleCharacterEntered;
        inputManager.OnBackspace += Refresh;
        inputManager.OnCompositionChanged += HandleCompositionChanged;
        inputManager.OnInputCleared += Refresh;
    }

    private void OnDisable()
    {
        if (inputManager == null || text == null)
            return;

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
        text.text = inputManager.CurrentInput + inputManager.Composition;
        UpdateImeCursorPosition();
    }

    // Tells the OS where our text ends so the native IME composition overlay
    // (the underlined in-progress Hangul block) is drawn there instead of at
    // whatever position it last defaulted to.
    private void UpdateImeCursorPosition()
    {
        if (Keyboard.current == null)
            return;

        text.ForceMeshUpdate();

        var rectTransform = text.rectTransform;
        var textInfo = text.textInfo;

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
