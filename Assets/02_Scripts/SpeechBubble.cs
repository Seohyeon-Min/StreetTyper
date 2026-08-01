using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;
    public RectTransform tailTransform;

    [Header("Tail Sprites")]
    public Sprite normalTailSprite;

    // 우측 고정 - 우측 가장자리를 그 자리에 두고 텍스트 길이에 따라 왼쪽으로만 늘어난다.
    // 꼬리 위치(anchoredPosition)는 프리팹에 미리 잡아둔 값을 그대로 쓴다.
    public void Setup(string message)
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
                tailImage.sprite = normalTailSprite;
            }

            tailTransform.anchorMin = new Vector2(1f, 0f);
            tailTransform.anchorMax = new Vector2(1f, 0f);
        }

        RectTransform bubbleRect = GetComponent<RectTransform>();
        if (bubbleRect != null) bubbleRect.pivot = new Vector2(1f, 0f);
    }
}