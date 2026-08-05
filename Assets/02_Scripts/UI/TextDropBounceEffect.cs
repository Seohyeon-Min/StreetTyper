using System.Collections;
using TMPro;
using UnityEngine;

// TMP 텍스트를 글자 단위로 위에서 떨어뜨렸다가 바운스하며 제자리에 정렬시키는 연출.
// OnEnable마다 자동 재생된다(FadeInBackground/StageStartEffect와 같은 패턴 - 켜는 쪽 코드를
// 안 건드리고 이 컴포넌트만 붙이면 된다).
//
// 텍스트 오브젝트 자체가 아니라 "글자 하나하나"가 따로 떨어져야 하므로 일반적인 Transform/
// CanvasGroup 애니메이션으로는 안 되고, TMP_TextInfo.meshInfo의 정점을 직접 옮기는 방식이다
// (TMP 공식 예제 VertexShake류와 같은 기법).
[RequireComponent(typeof(TMP_Text))]
public class TextDropBounceEffect : MonoBehaviour
{
    [Tooltip("글자가 시작할 때 원래 위치보다 얼마나 위에서 시작할지(px).")]
    [SerializeField] private float dropHeight = 60f;

    [Tooltip("글자 한 개가 떨어져 자리잡는 데 걸리는 시간(초).")]
    [SerializeField] private float duration = 0.45f;

    [Tooltip("글자마다 이만큼 시차를 두고 떨어뜨린다(초/글자) - 한꺼번에 안 떨어지고 순서대로 " +
             "후두둑 떨어지는 느낌을 낸다(RewardCardView.dropStagger와 같은 결).")]
    [SerializeField] private float stagger = 0.03f;

    [Tooltip("떨어지는 진행 곡선. 값이 1을 넘으면 그 순간만큼 착지 지점을 지나쳐(바운스) 아래로 " +
             "내려갔다가 다시 1로 돌아온다. 기본값은 70% 지점에서 살짝 지나쳤다가 정착하는 1회 바운스.")]
    [SerializeField] private AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.72f, 1.12f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Tooltip("떨어지는 동안 같이 페이드인할지.")]
    [SerializeField] private bool fadeIn = true;

    private TMP_Text _text;
    private Coroutine _routine;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    // SetActive(true)될 때마다(다른 반짝임/등장 연출들과 같은 규칙) 자동으로 다시 재생된다.
    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    /// <summary>처음부터 다시 재생한다. OnEnable에서 자동으로도 불리지만, 텍스트 내용이
    /// 바뀐 뒤(예: 다른 문구로 교체) 다시 떨어뜨리고 싶을 때 직접 불러도 된다.</summary>
    public void Play()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        _text.ForceMeshUpdate();
        var textInfo = _text.textInfo;
        var charCount = textInfo.characterCount;

        if (charCount == 0)
        {
            _routine = null;
            yield break;
        }

        // 정착 위치(원래 정점)를 여기서 한 번만 캐싱한다 - 매 프레임 이 값 + 오프셋으로 다시
        // 계산해야지, 이전 프레임에 옮겨둔 값 위에 또 더하면 계속 누적되어 튕겨 날아간다.
        var originalPositions = new Vector3[charCount][];
        for (var i = 0; i < charCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            var vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
            var vi = charInfo.vertexIndex;
            originalPositions[i] = new[] { vertices[vi], vertices[vi + 1], vertices[vi + 2], vertices[vi + 3] };
        }

        var maxTime = (charCount - 1) * stagger + duration;

        // unscaledDeltaTime을 쓴다 - StageStartEffect/TextGateRevealAnimation과 같은 이유로,
        // 이 연출이 나중에 결과/일시정지 화면처럼 timeScale이 0이 될 수 있는 곳에도 그대로
        // 붙을 수 있어야 한다.
        var elapsed = 0f;
        while (elapsed < maxTime)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyFrame(textInfo, originalPositions, elapsed);
            yield return null;
        }

        ApplyFrame(textInfo, originalPositions, maxTime);
        _routine = null;
    }

    private void ApplyFrame(TMP_TextInfo textInfo, Vector3[][] originalPositions, float elapsed)
    {
        var charCount = textInfo.characterCount;

        for (var i = 0; i < charCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible || originalPositions[i] == null)
                continue;

            var delay = i * stagger;
            var started = elapsed >= delay;
            var t = duration > 0f ? Mathf.Clamp01((elapsed - delay) / duration) : 1f;
            var progress = started ? curve.Evaluate(t) : 0f;

            // ⚠️ LerpUnclamped를 써야 한다 - Mathf.Lerp는 t를 0~1로 잘라버려서 curve가 1을
            // 넘는 순간(바운스)을 표현해도 여기서 다시 뭉개진다(RewardCardView.dropCurve와 같은 함정).
            var offsetY = Mathf.LerpUnclamped(dropHeight, 0f, progress);
            var alpha = fadeIn ? (byte)Mathf.RoundToInt(Mathf.Clamp01(progress) * 255f) : (byte)255;

            var meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];
            var vertices = meshInfo.vertices;
            var colors = meshInfo.colors32;
            var vi = charInfo.vertexIndex;
            var original = originalPositions[i];

            for (var v = 0; v < 4; v++)
            {
                var pos = original[v];
                pos.y += offsetY;
                vertices[vi + v] = pos;

                if (fadeIn)
                {
                    var c = colors[vi + v];
                    c.a = alpha;
                    colors[vi + v] = c;
                }
            }
        }

        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }
}
