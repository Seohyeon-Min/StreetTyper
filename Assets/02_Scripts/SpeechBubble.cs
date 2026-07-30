using UnityEngine;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;
    public RectTransform tailTransform;

    public Vector2 playerTailPos = new Vector2(-15f, -15f);
    public Vector2 enemyTailPos = new Vector2(15f, -15f);

    public void Setup(string message, bool isPlayer)
    {
        if (bubbleText != null)
        {
            bubbleText.text = message;
        }

        if (tailTransform != null)
        {
            if (isPlayer)
            {
                tailTransform.anchoredPosition = playerTailPos;
                tailTransform.localScale = new Vector3(1f, 1f, 1f);
            }
            else
            {
                tailTransform.anchoredPosition = enemyTailPos;
                tailTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }
}