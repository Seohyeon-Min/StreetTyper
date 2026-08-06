using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

[System.Serializable]
public class StorySentence
{
    [TextArea(2, 4)] public string text;
    [Min(0f)]
    [Tooltip("이 문장이 전부 출력된 뒤 다음 문장으로 자동 진행하기까지의 시간")]
    public float autoPlayTime = 2f;
}

[System.Serializable]
public class StorySlide
{
    public Sprite image;
    [Tooltip("이 화면에서 순서대로 출력할 문장들")]
    public StorySentence[] sentences;
    [HideInInspector, TextArea(3, 5)] public string text;
    [Tooltip("슬라이드의 모든 텍스트가 출력된 뒤 다음 화면으로 넘어가기 전까지 대기하는 시간")]
    public float duration = 3f;
}

public class IntroManager : MonoBehaviour
{
    [Header("UI 연결")]
    public Image storyImage;
    public TextMeshProUGUI storyText;
    public Image skipGaugeFill;

    [Header("스토리 설정")]
    public StorySlide[] slides;
    [Tooltip("글자가 하나씩 나오는 속도 (낮을수록 빠름)")]
    public float typingSpeed = 0.05f;

    [Header("스킵 설정")]
    public float skipHoldTime = 3f;

    [Header("씬 전환")]
    [Min(0.01f)]
    [Tooltip("다음 씬으로 넘어가기 전 화면이 흰색으로 차오르는 시간")]
    public float whiteFadeDuration = 0.45f;

    private float _currentHoldTime = 0f;
    private bool _isSkipping = false;
    private bool _isTransitioning = false;
    private bool _isTyping = false;
    private bool _forceShowLineText = false;

    void Start()
    {
        if (skipGaugeFill != null) skipGaugeFill.fillAmount = 0f;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayIntroBGM();
        }

        StartCoroutine(PlayStoryRoutine());
    }

    void Update()
    {
        if (_isSkipping || Keyboard.current == null) return;

        // 1. ESC를 꾹 눌러서 인트로 전체 스킵
        if (Keyboard.current.escapeKey.isPressed)
        {
            _currentHoldTime += Time.deltaTime;

            if (skipGaugeFill != null)
                skipGaugeFill.fillAmount = _currentHoldTime / skipHoldTime;

            if (_currentHoldTime >= skipHoldTime)
            {
                _isSkipping = true;
                GoToBattleScene();
            }
        }
        else
        {
            if (_currentHoldTime > 0f)
            {
                _currentHoldTime = 0f;
                if (skipGaugeFill != null) skipGaugeFill.fillAmount = 0f;
            }
        }

        // 2. 빨리보기 기능 (타이핑 중일 때 스페이스바, 엔터, 좌클릭)
        if (_isTyping && IsNextInputPressed())
        {
            _forceShowLineText = true;
        }
    }

    private IEnumerator PlayStoryRoutine()
    {
        for (int i = 0; i < slides.Length; i++)
        {
            if (_isSkipping) yield break;

            storyImage.sprite = slides[i].image;
            storyText.text = "";

            StorySentence[] sentences = GetSentences(slides[i]);

            for (int sentenceIndex = 0; sentenceIndex < sentences.Length; sentenceIndex++)
            {
                if (_isSkipping) yield break;

                _isTyping = true;
                _forceShowLineText = false;

                string currentLine = sentences[sentenceIndex].text ?? string.Empty;

                for (int j = 0; j < currentLine.Length; j++)
                {
                    if (_forceShowLineText)
                    {
                        storyText.text += currentLine.Substring(j);
                        break;
                    }

                    storyText.text += currentLine[j];
                    yield return new WaitForSeconds(typingSpeed);
                }

                _isTyping = false;
                yield return null;

                if (sentenceIndex < sentences.Length - 1)
                {
                    storyText.text += "\n\n";
                }

                float sentenceTimer = 0f;
                float autoPlayTime = Mathf.Max(0f, sentences[sentenceIndex].autoPlayTime);
                while (sentenceTimer < autoPlayTime)
                {
                    if (_isSkipping) yield break;
                    if (IsNextInputPressed()) break;

                    sentenceTimer += Time.deltaTime;
                    yield return null;
                }
            }

            // 슬라이드의 모든 줄이 출력된 후 대기
            float timer = 0f;
            while (timer < slides[i].duration)
            {
                if (_isSkipping) yield break;

                if (IsNextInputPressed())
                {
                    break;
                }

                timer += Time.deltaTime;
                yield return null;
            }
        }

        if (!_isSkipping)
        {
            GoToBattleScene();
        }
    }

    private static StorySentence[] GetSentences(StorySlide slide)
    {
        if (slide.sentences != null && slide.sentences.Length > 0)
        {
            return slide.sentences;
        }

        string[] legacyLines = string.IsNullOrEmpty(slide.text)
            ? new[] { string.Empty }
            : slide.text.Split('\n');
        StorySentence[] converted = new StorySentence[legacyLines.Length];

        for (int i = 0; i < legacyLines.Length; i++)
        {
            converted[i] = new StorySentence
            {
                text = legacyLines[i],
                autoPlayTime = 0.1f
            };
        }

        return converted;
    }

    private bool IsNextInputPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    private void GoToBattleScene()
    {
        if (_isTransitioning) return;

        _isTransitioning = true;
        SceneWhiteFadeIn.Play(GameScenes.Battle, whiteFadeDuration);
    }
}
