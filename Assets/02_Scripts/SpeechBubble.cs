using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI bubbleText;

    [Tooltip("아이콘을 보여줄 Image. SetupIcon/SetupIntent에서 쓴다.")]
    public Image iconImage;

    [Tooltip("좌우를 뒤집을 꼬리. 비워두면 SetMirrored가 아무것도 하지 않는다(꼬리가 프리팹에 " +
             "배치된 방향 그대로 남는다).")]
    public RectTransform tail;

    private Color _defaultTextColor;
    private bool _defaultTextColorCached;

    // 꼬리의 프리팹 원래 배치. Awake에서 한 번만 잡는다 - 뒤집힌 상태에서 다시 읽으면
    // 그 값이 "원래 자리"로 굳어 되돌릴 수 없다(DisplacedUI가 원점을 한 번만 읽는 것과 같은 이유).
    private Vector2 _tailAnchorMin, _tailAnchorMax, _tailPivot, _tailAnchoredPos;
    private Vector3 _tailScale;
    private bool _tailCaptured;
    private bool _mirrored;

    private void Awake()
    {
        CaptureTail();
    }

    private void CaptureTail()
    {
        if (_tailCaptured || tail == null)
            return;

        _tailAnchorMin = tail.anchorMin;
        _tailAnchorMax = tail.anchorMax;
        _tailPivot = tail.pivot;
        _tailAnchoredPos = tail.anchoredPosition;
        _tailScale = tail.localScale;
        _tailCaptured = true;
    }

    /// <summary>말풍선을 세로축 기준으로 좌우 대칭시킨다(꼬리가 반대쪽을 향한다).
    /// 플레이어와 적이 <b>같은 말풍선 오브젝트를 번갈아 쓰는</b> 경우에 필요하다 - EventManager의
    /// 대사 말풍선이 그렇다(하나를 만들어 두고 화자만 바꿔 가며 쓴다).
    ///
    /// ⚠️ 몸통과 글자는 뒤집지 않는다. 루트 스케일을 -1로 만들면 글자까지 거울처럼 뒤집힌다.
    /// 뒤집는 건 꼬리 하나뿐이고, 앵커·피벗·x오프셋·x스케일을 프리팹 값 기준으로 반전시킨다.</summary>
    public void SetMirrored(bool mirrored)
    {
        CaptureTail();

        if (tail == null || _mirrored == mirrored)
            return;

        _mirrored = mirrored;

        if (!mirrored)
        {
            tail.anchorMin = _tailAnchorMin;
            tail.anchorMax = _tailAnchorMax;
            tail.pivot = _tailPivot;
            tail.anchoredPosition = _tailAnchoredPos;
            tail.localScale = _tailScale;
            return;
        }

        tail.anchorMin = new Vector2(1f - _tailAnchorMin.x, _tailAnchorMin.y);
        tail.anchorMax = new Vector2(1f - _tailAnchorMax.x, _tailAnchorMax.y);
        tail.pivot = new Vector2(1f - _tailPivot.x, _tailPivot.y);
        tail.anchoredPosition = new Vector2(-_tailAnchoredPos.x, _tailAnchoredPos.y);
        tail.localScale = new Vector3(-_tailScale.x, _tailScale.y, _tailScale.z);
    }

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
