using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

[System.Serializable]
public class StorySlide
{
    public Sprite image;
    [TextArea(3, 5)] public string text;
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

    private float _currentHoldTime = 0f;
    private bool _isSkipping = false;
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

            string[] lines = slides[i].text.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (_isSkipping) yield break;

                _isTyping = true;
                _forceShowLineText = false;

                string currentLine = lines[lineIndex];

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

                // [수정] 다음 줄이 남아있다면 키 입력을 기다리지 않고 바로 넘어갑니다.
                if (lineIndex < lines.Length - 1)
                {
                    storyText.text += "\n";

                    // 줄이 바뀔 때 아주 잠깐(0.1초) 대기하여 자연스러운 리듬을 만듭니다.
                    yield return new WaitForSeconds(0.1f);
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
        SceneManager.LoadScene(GameScenes.Battle);
    }
}