using UnityEngine;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;
    public RectTransform tailTransform;

    public Vector2 rightTailPos = new Vector2(50f, -10f);
    public Vector2 leftTailPos = new Vector2(-50f, -10f);

    public void Setup(string message, bool isRightSide)
    {
        if (bubbleText != null)
        {
            bubbleText.text = message;
        }

        if (tailTransform != null)
        {
            if (isRightSide)
            {
                tailTransform.anchoredPosition = rightTailPos;
                tailTransform.localScale = new Vector3(1f, 1f, 1f);
            }
            else
            {
                tailTransform.anchoredPosition = leftTailPos;
                tailTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }
}