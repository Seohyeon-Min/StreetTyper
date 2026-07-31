using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;
    public RectTransform tailTransform;

    [Header("Tail Sprites")]
    public Sprite normalTailSprite;
    public Sprite thinkTailSprite;  

    public Vector2 playerTailPos = new Vector2(20f, -5f);
    public Vector2 enemyTailPos = new Vector2(-20f, -5f);

    public void Setup(string message, bool isPlayer, bool isNormalTail = true)
    {
        if (bubbleText != null)
        {
            bubbleText.text = message;
        }

        if (tailTransform != null)
        {
            Image tailImage = tailTransform.GetComponent<Image>();
            if (tailImage != null)
            {
                tailImage.sprite = isNormalTail ? normalTailSprite : thinkTailSprite;
            }
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