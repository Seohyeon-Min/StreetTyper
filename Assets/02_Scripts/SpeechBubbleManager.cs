using UnityEngine;
using System.Collections;

public class SpeechBubbleManager : MonoBehaviour
{
    public static SpeechBubbleManager Instance;

    public GameObject speechBubblePrefab;
    public Transform canvasTransform;

    //[Header("Screen Ratio Settings (0.0 ~ 1.0)")]
    //[Tooltip("0.5가 화면 정중앙입니다. 0.25는 왼쪽 25%, 0.75는 오른쪽 25%를 의미합니다.")]
    public Vector2 playerScreenRatio = new Vector2(0.25f, 0.65f);
    public Vector2 enemyScreenRatio = new Vector2(0.75f, 0.65f);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 다른 스크립트에서 에러가 나지 않도록 worldPosition 매개변수는 남겨두지만, 계산에는 사용하지 않습니다.
    public Vector3 GetBubbleScreenPosition(Vector3 worldPosition, bool isPlayer)
    {
        // 캐릭터의 위치를 무시하고, 인스펙터에서 설정한 화면 비율(%)을 사용합니다.
        Vector2 ratio = isPlayer ? playerScreenRatio : enemyScreenRatio;

        // 화면의 실제 픽셀 가로/세로 길이에 비율을 곱해 완벽한 스크린 좌표를 계산합니다.
        return new Vector3(Screen.width * ratio.x, Screen.height * ratio.y, 0);
    }

    public void ShowBubble(string message, Vector3 worldPosition, bool isPlayer, float duration = 1.0f)
    {
        StartCoroutine(ShowBubbleRoutine(message, isPlayer, duration));
    }

    private IEnumerator ShowBubbleRoutine(string message, bool isPlayer, float duration)
    {
        if (speechBubblePrefab == null || canvasTransform == null) yield break;

        GameObject bubbleObj = Instantiate(speechBubblePrefab, canvasTransform);

        SpeechBubble bubbleScript = bubbleObj.GetComponent<SpeechBubble>();
        if (bubbleScript != null)
        {
            bubbleScript.Setup(message, isPlayer);
        }

        RectTransform rectTransform = bubbleObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // 화면 비율 기반으로 위치 지정
            rectTransform.position = GetBubbleScreenPosition(Vector3.zero, isPlayer);
        }

        bubbleObj.SetActive(true);

        yield return new WaitForSeconds(duration);
        Destroy(bubbleObj);
    }
}