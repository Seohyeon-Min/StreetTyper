using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Displays the game logo at startup, then hands off to the title scene.
/// The entire sequence uses unscaled time so it is not affected by a stale time scale.
/// </summary>
public sealed class LogoSplashController : MonoBehaviour
{
    [SerializeField] private Sprite logo;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;
    [SerializeField, Min(0f)] private float holdDuration = 1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float logoScale = 1f;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color logoColor = Color.white;

    private CanvasGroup _logoGroup;
    private bool _isLeaving;

    private void Awake()
    {
        Time.timeScale = 1f;
        BuildView();
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
        if (_isLeaving)
            return;

        var keyboard = Keyboard.current;
        bool keyboardSkip = keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.zKey.wasPressedThisFrame ||
             keyboard.escapeKey.wasPressedThisFrame);

        bool mouseSkip = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (keyboardSkip || mouseSkip)
            LoadTitle();
    }

    private void BuildView()
    {
        var canvasObject = new GameObject("Splash Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        StretchToParent(backgroundObject.GetComponent<RectTransform>());
        backgroundObject.GetComponent<Image>().color = backgroundColor;

        var logoObject = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        logoObject.transform.SetParent(canvasObject.transform, false);

        var logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = Vector2.zero;

        var logoImage = logoObject.GetComponent<Image>();
        logoImage.sprite = logo;
        logoImage.color = logoColor;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoImage.SetNativeSize();
        logoRect.localScale = Vector3.one * logoScale;

        _logoGroup = logoObject.GetComponent<CanvasGroup>();
        _logoGroup.alpha = 0f;
        _logoGroup.interactable = false;
        _logoGroup.blocksRaycasts = false;
    }

    private IEnumerator PlaySequence()
    {
        yield return Fade(0f, 1f, fadeInDuration);

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        yield return Fade(1f, 0f, fadeOutDuration);
        LoadTitle();
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            _logoGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _logoGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        _logoGroup.alpha = to;
    }

    private void LoadTitle()
    {
        if (_isLeaving)
            return;

        _isLeaving = true;
        StopAllCoroutines();
        SceneManager.LoadScene(GameScenes.Title);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
