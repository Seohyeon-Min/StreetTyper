using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;

    [Tooltip("아이콘을 보여줄 Image. SetupIcon/SetupIntent에서 쓴다.")]
    public Image iconImage;

    private Color _defaultTextColor;
    private bool _defaultTextColorCached;

    private void CacheDefaultTextColor()
    {
        if (_defaultTextColorCached || bubbleText == null)
            return;

        _defaultTextColor = bubbleText.color;
        _defaultTextColorCached = true;
    }

    // 꼬리는 프리팹에 배치해 둔 그대로 쓴다 - 스프라이트도 위치도 코드가 건드리지 않는다.
    public void Setup(string message)
    {
        CacheDefaultTextColor();

        if (bubbleText != null)
        {
            bubbleText.gameObject.SetActive(true);
            bubbleText.text = message;
            bubbleText.color = _defaultTextColor;
        }

        if (iconImage != null)
            iconImage.gameObject.SetActive(false);
    }

    // 텍스트 없이 스프라이트 아이콘만 보여준다.
    public void SetupIcon(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = icon;
        }

        if (bubbleText != null)
            bubbleText.gameObject.SetActive(false);
    }

    // 아이콘 + 텍스트를 같이 보여준다(적 인텐트: 아이콘이 먼저, 그 옆에 ActionType별 색이 입혀진
    // 텍스트). 가로 배치(아이콘→텍스트 순서)는 프리팹의 Horizontal Layout Group + 자식 순서가
    // 담당하고, 여기서는 내용과 색만 채운다.
    public void SetupIntent(Sprite icon, string message, Color textColor)
    {
        CacheDefaultTextColor();

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(icon != null);
            iconImage.sprite = icon;
        }

        if (bubbleText != null)
        {
            bubbleText.gameObject.SetActive(true);
            bubbleText.text = message;
            bubbleText.color = textColor;
        }
    }
}
