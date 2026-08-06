using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Drops TMP characters one by one and settles them on their original baseline
/// with a short bounce. Attach this directly to a TMP text object.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class TMPCharacterDropBounce : MonoBehaviour
{
    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float characterDuration = 0.55f;

    [Min(0f)]
    [SerializeField] private float characterStagger = 0.045f;

    [SerializeField] private bool useUnscaledTime = true;

    [Header("Motion")]
    [Tooltip("Distance above the final baseline where each character starts.")]
    [SerializeField] private float dropHeight = 180f;

    [Header("Exit")]
    [Min(0.01f)]
    [SerializeField] private float exitDuration = 0.35f;
    [SerializeField] private float exitStagger = 0.025f;
    [SerializeField] private float exitDropDistance = 160f;

    [Header("Appearance")]
    [SerializeField] private bool fadeIn = true;

    private TMP_Text _text;
    private TMP_MeshInfo[] _originalMeshInfo;
    private Coroutine _routine;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;
        RestoreOriginalMesh();
    }

    public void Play()
    {
        if (!isActiveAndEnabled)
            return;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(PlayRoutine());
    }

    /// <summary>Falls downward and fades out, then invokes the callback.</summary>
    public void PlayExit(Action onComplete = null)
    {
        if (!isActiveAndEnabled)
        {
            onComplete?.Invoke();
            return;
        }

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(ExitRoutine(onComplete));
    }

    private IEnumerator PlayRoutine()
    {
        // Let localization/layout changes made by another OnEnable finish first.
        yield return null;

        _text.ForceMeshUpdate();
        _originalMeshInfo = _text.textInfo.CopyMeshInfoVertexData();

        var visibleIndex = 0;
        var visibleCount = 0;
        for (var i = 0; i < _text.textInfo.characterCount; i++)
            if (_text.textInfo.characterInfo[i].isVisible)
                visibleCount++;

        var effectiveDuration = Mathf.Max(0.01f, characterDuration * 0.75f);
        var effectiveStagger = characterStagger * 0.8f;
        var totalDuration = effectiveDuration + Mathf.Max(0, visibleCount - 1) * effectiveStagger;
        var elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            visibleIndex = 0;

            for (var i = 0; i < _text.textInfo.characterCount; i++)
            {
                var character = _text.textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                var progress = Mathf.Clamp01((elapsed - visibleIndex * effectiveStagger) / effectiveDuration);
                ApplyCharacter(character, progress);
                visibleIndex++;
            }

            UploadMesh();
            yield return null;
        }

        RestoreOriginalMesh();
        _routine = null;
    }

    private IEnumerator ExitRoutine(Action onComplete)
    {
        RestoreOriginalMesh();
        _text.ForceMeshUpdate();
        _originalMeshInfo = _text.textInfo.CopyMeshInfoVertexData();

        var visibleCount = 0;
        for (var i = 0; i < _text.textInfo.characterCount; i++)
            if (_text.textInfo.characterInfo[i].isVisible)
                visibleCount++;

        var totalDuration = exitDuration + Mathf.Max(0, visibleCount - 1) * exitStagger;
        var elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var visibleIndex = 0;
            for (var i = 0; i < _text.textInfo.characterCount; i++)
            {
                var character = _text.textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                var progress = Mathf.Clamp01((elapsed - visibleIndex * exitStagger) /
                                             Mathf.Max(0.01f, exitDuration));
                ApplyExitCharacter(character, progress);
                visibleIndex++;
            }

            UploadMesh();
            yield return null;
        }

        for (var i = 0; i < _text.textInfo.characterCount; i++)
        {
            var character = _text.textInfo.characterInfo[i];
            if (character.isVisible)
                ApplyExitCharacter(character, 1f);
        }
        UploadMesh();

        _routine = null;
        onComplete?.Invoke();
    }

    private void ApplyCharacter(TMP_CharacterInfo character, float progress)
    {
        var materialIndex = character.materialReferenceIndex;
        var vertexIndex = character.vertexIndex;
        if (_originalMeshInfo == null || materialIndex < 0 || materialIndex >= _originalMeshInfo.Length)
            return;

        var sourceVertices = _originalMeshInfo[materialIndex].vertices;
        var targetVertices = _text.textInfo.meshInfo[materialIndex].vertices;
        var sourceColors = _originalMeshInfo[materialIndex].colors32;
        var targetColors = _text.textInfo.meshInfo[materialIndex].colors32;

        var curveValue = EvaluateDoubleBounce(progress);
        var offset = Vector3.up * (dropHeight * curveValue);
        var alphaMultiplier = fadeIn ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2.5f)) : 1f;

        for (var corner = 0; corner < 4; corner++)
        {
            var index = vertexIndex + corner;
            targetVertices[index] = sourceVertices[index] + offset;

            var color = sourceColors[index];
            color.a = (byte)Mathf.RoundToInt(color.a * alphaMultiplier);
            targetColors[index] = color;
        }
    }

    private static float EvaluateDoubleBounce(float progress)
    {
        // Fall past the baseline, rebound, hit it a second time with less height,
        // then settle. This is procedural so old serialized curves cannot override it.
        if (progress < 0.48f)
            return Mathf.Lerp(1f, -0.13f, Smooth01(progress / 0.48f));
        if (progress < 0.64f)
            return Mathf.Lerp(-0.13f, 0.07f, Smooth01((progress - 0.48f) / 0.16f));
        if (progress < 0.82f)
            return Mathf.Lerp(0.07f, -0.035f, Smooth01((progress - 0.64f) / 0.18f));
        return Mathf.Lerp(-0.035f, 0f, Smooth01((progress - 0.82f) / 0.18f));
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void ApplyExitCharacter(TMP_CharacterInfo character, float progress)
    {
        var materialIndex = character.materialReferenceIndex;
        var vertexIndex = character.vertexIndex;
        if (_originalMeshInfo == null || materialIndex < 0 || materialIndex >= _originalMeshInfo.Length)
            return;

        var sourceVertices = _originalMeshInfo[materialIndex].vertices;
        var targetVertices = _text.textInfo.meshInfo[materialIndex].vertices;
        var sourceColors = _originalMeshInfo[materialIndex].colors32;
        var targetColors = _text.textInfo.meshInfo[materialIndex].colors32;
        var eased = progress * progress;
        var offset = Vector3.down * (exitDropDistance * eased);

        for (var corner = 0; corner < 4; corner++)
        {
            var index = vertexIndex + corner;
            targetVertices[index] = sourceVertices[index] + offset;
            var color = sourceColors[index];
            color.a = (byte)Mathf.RoundToInt(color.a * (1f - progress));
            targetColors[index] = color;
        }
    }

    private void UploadMesh()
    {
        for (var i = 0; i < _text.textInfo.meshInfo.Length; i++)
        {
            var meshInfo = _text.textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            meshInfo.mesh.colors32 = meshInfo.colors32;
            _text.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    private void RestoreOriginalMesh()
    {
        if (_text == null || _originalMeshInfo == null)
            return;

        for (var i = 0; i < _originalMeshInfo.Length && i < _text.textInfo.meshInfo.Length; i++)
        {
            var source = _originalMeshInfo[i];
            var target = _text.textInfo.meshInfo[i];
            source.vertices.CopyTo(target.vertices, 0);
            source.colors32.CopyTo(target.colors32, 0);
            target.mesh.vertices = target.vertices;
            target.mesh.colors32 = target.colors32;
            _text.UpdateGeometry(target.mesh, i);
        }
    }
}
