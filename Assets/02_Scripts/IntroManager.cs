using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

// 인스펙터에서 이미지와 텍스트를 세트로 관리하기 위한 클래스입니다.
[System.Serializable]
public class StorySlide
{
    public Sprite image;
    [TextArea(3, 5)] public string text;
    public float duration = 3f; // 이 슬라이드를 몇 초 동안 보여줄지
}

public class IntroManager : MonoBehaviour
{
    [Header("UI 연결")]
    public Image storyImage;
    public TextMeshProUGUI storyText;
    public Image skipGaugeFill; // 스킵 게이지 (Fill Amount를 조절)

    [Header("스토리 설정")]
    public StorySlide[] slides; // 인스펙터에서 여러 장을 추가할 수 있습니다.

    [Header("스킵 설정")]
    public float skipHoldTime = 3f; // 스킵에 필요한 시간(초)

    private float _currentHoldTime = 0f;
    private bool _isSkipping = false;

    void Start()
    {
        if (skipGaugeFill != null) skipGaugeFill.fillAmount = 0f;
        StartCoroutine(PlayStoryRoutine());
    }

    void Update()
    {
        if (_isSkipping || Keyboard.current == null) return;

        // 스페이스바를 꾹 누르고 있는지 체크
        if (Keyboard.current.anyKey.isPressed)
        {
            _currentHoldTime += Time.deltaTime;

            // 게이지 UI 업데이트
            if (skipGaugeFill != null)
                skipGaugeFill.fillAmount = _currentHoldTime / skipHoldTime;

            // 3초 이상 꾹 눌렀다면 스킵 발동!
            if (_currentHoldTime >= skipHoldTime)
            {
                _isSkipping = true;
                GoToBattleScene();
            }
        }
        else
        {
            // 손을 떼면 게이지 초기화
            if (_currentHoldTime > 0f)
            {
                _currentHoldTime = 0f;
                if (skipGaugeFill != null) skipGaugeFill.fillAmount = 0f;
            }
        }
    }

    private IEnumerator PlayStoryRoutine()
    {
        // slides 배열에 등록된 이야기들을 순서대로 보여줍니다.
        for (int i = 0; i < slides.Length; i++)
        {
            if (_isSkipping) yield break; // 스킵 중이면 코루틴 즉시 종료

            storyImage.sprite = slides[i].image;
            storyText.text = slides[i].text;

            // 설정한 시간만큼 대기
            yield return new WaitForSeconds(slides[i].duration);
        }

        // 모든 이야기가 끝나면 자연스럽게 타이틀로 넘어갑니다.
        if (!_isSkipping)
        {
            GoToBattleScene();
        }
    }

    private void GoToBattleScene()
    {
        SceneManager.LoadScene(GameScenes.Battle);
    }
}