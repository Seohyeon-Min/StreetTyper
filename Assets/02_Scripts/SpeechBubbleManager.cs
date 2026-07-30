using UnityEngine;
using System.Collections;

public class SpeechBubbleManager : MonoBehaviour
{
    public static SpeechBubbleManager Instance;

    public GameObject speechBubblePrefab;
    public Transform canvasTransform;

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

    public void ShowBubble(string message, Vector3 worldPosition, bool isRightSide, float duration = 1.0f)
    {
        StartCoroutine(ShowBubbleRoutine(message, worldPosition, isRightSide, duration));
    }

    private IEnumerator ShowBubbleRoutine(string message, Vector3 worldPosition, bool isRightSide, float duration)
    {
        if (speechBubblePrefab == null || canvasTransform == null) yield break;

        GameObject bubbleObj = Instantiate(speechBubblePrefab, canvasTransform);

        SpeechBubble bubbleScript = bubbleObj.GetComponent<SpeechBubble>();
        if (bubbleScript != null)
        {
            bubbleScript.Setup(message, isRightSide);
        }

        Vector3 offset = isRightSide ? new Vector3(2.0f, 2.5f, 0) : new Vector3(-2.0f, 2.5f, 0);
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition + offset);

        RectTransform rectTransform = bubbleObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.position = screenPos;
        }

        bubbleObj.SetActive(true);

        yield return new WaitForSeconds(duration);
        Destroy(bubbleObj);
    }
}