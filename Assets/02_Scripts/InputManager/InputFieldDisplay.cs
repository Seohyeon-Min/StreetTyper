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
    [SerializeField] private StageManager stageManager;
    [SerializeField] private RewardInputHandler rewardInputHandler;

    [Tooltip("타이핑한 글자를 비출 TMP 라벨. 입력 필드가 아니라 그냥 텍스트여야 한다.")]
    [SerializeField] private TextMeshProUGUI text;

    [Header("Idle Hint")]
    [SerializeField, Min(0f)] private float idleHintDelay = 2f;
    [SerializeField, Min(0f)] private float idleHintFadeDuration = 0.35f;
    [SerializeField] private Color idleHintColor = new Color(0.6666667f, 0.6666667f, 0.6666667f, 0.7058824f);

    [Header("Idle Hint Localization")]
    [SerializeField] private string idleHintKorean = "타이핑을 시작하세요...";
    [SerializeField] private string idleHintEnglish = "Start typing...";
    [SerializeField] private string idleHintFrench = "Commencez à taper...";
    [SerializeField] private string idleHintSpanish = "Empieza a escribir...";
    [SerializeField] private string idleHintJapanese = "入力を始めよう…";

    [Header("First Reward Hint Localization")]
    [SerializeField] private string rewardHintKorean = "카드를 입력하세요...";
    [SerializeField] private string rewardHintEnglish = "Type a card...";
    [SerializeField] private string rewardHintFrench = "Tapez une carte...";
    [SerializeField] private string rewardHintSpanish = "Escribe una carta...";
    [SerializeField] private string rewardHintJapanese = "カードを入力してください…";

    [Header("Language Fonts")]
    [SerializeField] private TMP_FontAsset koreanFont;
    [SerializeField] private TMP_FontAsset englishFont;
    [SerializeField] private TMP_FontAsset frenchFont;
    [SerializeField] private TMP_FontAsset spanishFont;
    [SerializeField] private TMP_FontAsset japaneseFont;

    private Color _inputColor;
    private float _idleTime;
    private bool _showingIdleHint;
    private bool _hasInputColor;
    private TMP_FontAsset _defaultFont;
    private bool _wasRewardSelecting;
    private bool _firstRewardCompleted;

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
        LanguageSettings.OnChanged += HandleLanguageChanged;
        if (rewardInputHandler != null)
            rewardInputHandler.OnSelectionFinished += HandleFirstRewardCompleted;

        if (!_hasInputColor)
        {
            _inputColor = text.color;
            _defaultFont = text.font;
            _hasInputColor = true;
        }

        ApplyLanguageFont();
        ResetIdleHint();
    }

    private void OnDisable()
    {
        if (inputManager == null || text == null)
            return;

        inputManager.OnCharacterEntered -= HandleCharacterEntered;
        inputManager.OnBackspace -= Refresh;
        inputManager.OnCompositionChanged -= HandleCompositionChanged;
        inputManager.OnInputCleared -= Refresh;
        LanguageSettings.OnChanged -= HandleLanguageChanged;
        if (rewardInputHandler != null)
            rewardInputHandler.OnSelectionFinished -= HandleFirstRewardCompleted;
    }

    private void HandleCharacterEntered(char _)
    {
        ResetIdleHint();
        Refresh();
    }

    private void HandleCompositionChanged(string _)
    {
        ResetIdleHint();
        Refresh();
    }

    private void Refresh()
    {
        ResetIdleHint();
        text.text = inputManager.CurrentInput + inputManager.Composition;
        UpdateImeCursorPosition();
    }

    private void Update()
    {
        if (inputManager == null || text == null || stageManager == null ||
            !stageManager.IsFirstStage || _firstRewardCompleted || !inputManager.IsInputEnabled)
        {
            ResetIdleHint();
            return;
        }

        bool rewardSelecting = rewardInputHandler != null && rewardInputHandler.IsSelecting;
        if (rewardSelecting && !_wasRewardSelecting)
            ResetIdleHint();

        _wasRewardSelecting = rewardSelecting;

        if (!string.IsNullOrEmpty(inputManager.CurrentInput) ||
            !string.IsNullOrEmpty(inputManager.Composition))
        {
            ResetIdleHint();
            return;
        }

        if (rewardSelecting)
        {
            UpdateHint(GetLocalizedRewardHint(), idleHintDelay);
            return;
        }

        UpdateHint(GetLocalizedIdleHint(), idleHintDelay);
    }

    private void UpdateHint(string hint, float delay)
    {
        _idleTime += Time.unscaledDeltaTime;
        if (_idleTime < delay)
            return;

        if (!_showingIdleHint)
        {
            _showingIdleHint = true;
            text.text = hint;
        }

        float fade = idleHintFadeDuration <= 0f
            ? 1f
            : Mathf.Clamp01((_idleTime - delay) / idleHintFadeDuration);
        var transparentHintColor = idleHintColor;
        transparentHintColor.a = 0f;
        text.color = Color.Lerp(transparentHintColor, idleHintColor, fade);
    }

    private void ResetIdleHint()
    {
        _idleTime = 0f;

        if (!_hasInputColor || text == null)
            return;

        text.color = _inputColor;
        if (_showingIdleHint)
            text.text = string.Empty;

        _showingIdleHint = false;
    }

    private void HandleLanguageChanged()
    {
        ApplyLanguageFont();

        if (_showingIdleHint)
            text.text = _wasRewardSelecting ? GetLocalizedRewardHint() : GetLocalizedIdleHint();
    }

    private string GetLocalizedIdleHint()
    {
        switch (LanguageSettings.Current)
        {
            case GameLanguage.Korean: return idleHintKorean;
            case GameLanguage.French: return idleHintFrench;
            case GameLanguage.Spanish: return idleHintSpanish;
            case GameLanguage.Japanese: return idleHintJapanese;
            default: return idleHintEnglish;
        }
    }

    private string GetLocalizedRewardHint()
    {
        switch (LanguageSettings.Current)
        {
            case GameLanguage.Korean: return rewardHintKorean;
            case GameLanguage.French: return rewardHintFrench;
            case GameLanguage.Spanish: return rewardHintSpanish;
            case GameLanguage.Japanese: return rewardHintJapanese;
            default: return rewardHintEnglish;
        }
    }

    private void HandleFirstRewardCompleted()
    {
        if (stageManager == null || !stageManager.IsFirstStage)
            return;

        _firstRewardCompleted = true;
        ResetIdleHint();
    }

    private void ApplyLanguageFont()
    {
        if (text == null)
            return;

        TMP_FontAsset selectedFont;
        switch (LanguageSettings.Current)
        {
            case GameLanguage.Korean: selectedFont = koreanFont; break;
            case GameLanguage.French: selectedFont = frenchFont; break;
            case GameLanguage.Spanish: selectedFont = spanishFont; break;
            case GameLanguage.Japanese: selectedFont = japaneseFont; break;
            default: selectedFont = englishFont; break;
        }

        text.font = selectedFont != null ? selectedFont : _defaultFont;
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
