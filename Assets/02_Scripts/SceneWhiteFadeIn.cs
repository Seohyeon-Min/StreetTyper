using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneWhiteFadeIn : MonoBehaviour
{
    private Image _overlay;

    public static void Play(string sceneName, float duration)
    {
        GameObject fadeObject = new GameObject(
            "White Scene Transition",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(SceneWhiteFadeIn));

        Canvas canvas = fadeObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = fadeObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject(
            "White Overlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.SetParent(fadeObject.transform, false);
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        SceneWhiteFadeIn transition = fadeObject.GetComponent<SceneWhiteFadeIn>();
        transition._overlay = imageObject.GetComponent<Image>();
        transition._overlay.color = new Color(1f, 1f, 1f, 0f);
        transition._overlay.raycastTarget = true;

        DontDestroyOnLoad(fadeObject);
        transition.StartCoroutine(transition.TransitionRoutine(sceneName, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator TransitionRoutine(string sceneName, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            _overlay.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        _overlay.color = Color.white;
        SceneManager.LoadScene(sceneName);
        yield return null;

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            _overlay.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        Destroy(gameObject);
    }
}
