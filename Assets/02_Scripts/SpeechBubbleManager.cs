using UnityEngine;
using System.Collections;

public class SpeechBubbleManager : MonoBehaviour
{
    public static SpeechBubbleManager Instance;

    public GameObject speechBubblePrefab;
    public Transform canvasTransform;

    public Vector3 playerOffset = new Vector3(2.0f, 2.5f, 0);
    public Vector3 enemyOffset = new Vector3(-2.0f, 2.5f, 0);

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

    public Vector3 GetBubbleScreenPosition(Vector3 worldPosition, bool isPlayer)
    {
        Vector3 offset = isPlayer ? playerOffset : enemyOffset;
        return Camera.main.WorldToScreenPoint(worldPosition + offset);
    }

    public void ShowBubble(string message, Vector3 worldPosition, bool isPlayer, float duration = 1.0f)
    {
        StartCoroutine(ShowBubbleRoutine(message, worldPosition, isPlayer, duration));
    }

    private IEnumerator ShowBubbleRoutine(string message, Vector3 worldPosition, bool isPlayer, float duration)
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
            rectTransform.position = GetBubbleScreenPosition(worldPosition, isPlayer);
        }

        bubbleObj.SetActive(true);

        yield return new WaitForSeconds(duration);
        Destroy(bubbleObj);
    }
}