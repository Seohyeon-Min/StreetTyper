using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIConfettiBurst : MonoBehaviour
{
    private static readonly Color[] DefaultColors =
    {
        new Color(1f, 0.18f, 0.48f),
        new Color(1f, 0.78f, 0.08f),
        new Color(0.15f, 0.82f, 1f),
        new Color(0.5f, 0.25f, 1f),
        new Color(0.22f, 1f, 0.5f),
        Color.white
    };

    private readonly List<GameObject> _particles = new List<GameObject>();
    private RectTransform _root;
    private Color[] _colors;
    private Sprite _sparkleSprite;

    public static void Play(RectTransform panel, Color[] colors, Sprite sparkleSprite)
    {
        if (panel == null) return;

        var parent = panel;
        var parentCanvas = panel.GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.transform is RectTransform canvasRect)
            parent = canvasRect;

        var oldEffect = parent.GetComponentInChildren<UIConfettiBurst>(true);
        if (oldEffect != null)
            Destroy(oldEffect.gameObject);

        var effectObject = new GameObject("Game Clear Confetti", typeof(RectTransform), typeof(Canvas), typeof(UIConfettiBurst));
        var effectRect = effectObject.GetComponent<RectTransform>();
        effectRect.SetParent(parent, false);
        effectRect.anchorMin = Vector2.zero;
        effectRect.anchorMax = Vector2.one;
        effectRect.offsetMin = Vector2.zero;
        effectRect.offsetMax = Vector2.zero;
        effectRect.SetAsLastSibling();

        var canvas = effectObject.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        var effect = effectObject.GetComponent<UIConfettiBurst>();
        effect._root = effectRect;
        effect._colors = colors != null && colors.Length > 0 ? colors : DefaultColors;
        effect._sparkleSprite = sparkleSprite;
        effect.StartCoroutine(effect.PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        yield return null;
        SpawnDiamondSparkles(18);
        SpawnSide(true, 34);
        SpawnSide(false, 34);

        yield return new WaitForSecondsRealtime(0.22f);
        SpawnSide(true, 24);
        SpawnSide(false, 24);
    }

    private void SpawnDiamondSparkles(int count)
    {
        var bounds = _root.rect;
        for (var i = 0; i < count; i++)
        {
            var sparkleObject = new GameObject("Diamond Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = sparkleObject.GetComponent<RectTransform>();
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = RandomEdgePosition(bounds);

            var size = Random.Range(10f, 27f);
            rect.sizeDelta = Vector2.one * size;
            rect.localRotation = Quaternion.Euler(0f, 0f, _sparkleSprite == null ? 45f : 0f);
            rect.localScale = Vector3.zero;

            var image = sparkleObject.GetComponent<Image>();
            image.sprite = _sparkleSprite;
            image.preserveAspect = _sparkleSprite != null;
            image.raycastTarget = false;
            var tint = Color.Lerp(Color.white, _colors[Random.Range(0, _colors.Length)], 0.18f);
            tint.a = 0f;
            image.color = tint;

            _particles.Add(sparkleObject);
            StartCoroutine(AnimateDiamondSparkle(
                sparkleObject,
                rect,
                image,
                Random.Range(0.15f, 1.8f),
                Random.Range(2.4f, 4.2f)));
        }
    }

    private static Vector2 RandomEdgePosition(Rect bounds)
    {
        float horizontalInset = bounds.width * 0.06f;
        float verticalInset = bounds.height * 0.06f;
        float sideBandWidth = bounds.width * 0.1f;
        float topBandHeight = bounds.height * 0.16f;
        int edge = Random.Range(0, 4);

        if (edge < 2)
        {
            return new Vector2(
                Random.Range(bounds.xMin + horizontalInset, bounds.xMax - horizontalInset),
                Random.Range(bounds.yMax - topBandHeight, bounds.yMax - verticalInset));
        }

        bool left = edge == 2;
        return new Vector2(
            left
                ? Random.Range(bounds.xMin + horizontalInset, bounds.xMin + horizontalInset + sideBandWidth)
                : Random.Range(bounds.xMax - horizontalInset - sideBandWidth, bounds.xMax - horizontalInset),
            Random.Range(bounds.yMin + bounds.height * 0.24f, bounds.yMax - verticalInset));
    }

    private IEnumerator AnimateDiamondSparkle(GameObject sparkleObject, RectTransform rect, Image image,
        float initialDelay, float cycleDuration)
    {
        yield return new WaitForSecondsRealtime(initialDelay);
        var baseColor = image.color;

        while (sparkleObject != null)
        {
            float elapsed = 0f;
            while (elapsed < cycleDuration && sparkleObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / cycleDuration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float softenedPulse = pulse * pulse;

                rect.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.15f, softenedPulse);
                var color = baseColor;
                color.a = softenedPulse * 0.9f;
                image.color = color;
                yield return null;
            }

            if (sparkleObject == null)
                yield break;

            rect.localScale = Vector3.zero;
            var hiddenColor = baseColor;
            hiddenColor.a = 0f;
            image.color = hiddenColor;
            yield return new WaitForSecondsRealtime(Random.Range(0.7f, 2.2f));
        }
    }

    private void SpawnSide(bool fromLeft, int count)
    {
        var bounds = _root.rect;
        var origin = new Vector2(
            fromLeft ? bounds.xMin + 12f : bounds.xMax - 12f,
            bounds.yMin + 28f);

        for (var i = 0; i < count; i++)
        {
            var particleObject = new GameObject("Confetti", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = particleObject.GetComponent<RectTransform>();
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = origin + Random.insideUnitCircle * 18f;

            var size = Random.Range(9f, 22f);
            rect.sizeDelta = Vector2.one * size;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var image = particleObject.GetComponent<Image>();
            image.sprite = null;
            image.raycastTarget = false;
            image.color = _colors[Random.Range(0, _colors.Length)];

            var horizontalSpeed = Random.Range(260f, 690f) * (fromLeft ? 1f : -1f);
            var velocity = new Vector2(horizontalSpeed, Random.Range(620f, 1050f));
            var spin = Random.Range(-650f, 650f);
            var duration = Random.Range(1.35f, 2.25f);

            _particles.Add(particleObject);
            StartCoroutine(AnimateParticle(particleObject, rect, image, velocity, spin, duration));
        }
    }

    private IEnumerator AnimateParticle(GameObject particleObject, RectTransform rect, Image image,
        Vector2 velocity, float spin, float duration)
    {
        var origin = rect.anchoredPosition;
        var startColor = image.color;
        var elapsed = 0f;

        while (elapsed < duration && particleObject != null)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = origin + velocity * elapsed + Vector2.down * (520f * elapsed * elapsed * 0.5f);
            rect.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);

            var color = startColor;
            color.a = t < 0.72f ? 1f : 1f - Mathf.InverseLerp(0.72f, 1f, t);
            image.color = color;
            yield return null;
        }

        _particles.Remove(particleObject);
        if (particleObject != null)
            Destroy(particleObject);
    }

    private void OnDestroy()
    {
        for (var i = 0; i < _particles.Count; i++)
        {
            if (_particles[i] != null)
                Destroy(_particles[i]);
        }
        _particles.Clear();
    }
}
