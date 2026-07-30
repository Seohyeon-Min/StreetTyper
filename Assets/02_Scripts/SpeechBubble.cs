using UnityEngine;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;
    public RectTransform tailTransform;

    public Vector2 playerTailPos = new Vector2(20f, -5f);
    public Vector2 enemyTailPos = new Vector2(-20f, -5f);

    public void Setup(string message, bool isPlayer)
    {
        if (bubbleText != null)
        {
            bubbleText.text = message;
        }

        RectTransform bubbleRect = GetComponent<RectTransform>();

        if (tailTransform != null)
        {
            if (isPlayer)
            {
                if (bubbleRect != null) bubbleRect.pivot = new Vector2(0f, 0f);

                tailTransform.anchorMin = new Vector2(0f, 0f);
                tailTransform.anchorMax = new Vector2(0f, 0f);

                tailTransform.anchoredPosition = playerTailPos;
                tailTransform.localScale = new Vector3(1f, 1f, 1f);
            }
            else
            {
                if (bubbleRect != null) bubbleRect.pivot = new Vector2(1f, 0f);

                tailTransform.anchorMin = new Vector2(1f, 0f);
                tailTransform.anchorMax = new Vector2(1f, 0f);

                tailTransform.anchoredPosition = enemyTailPos;
                tailTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }
}