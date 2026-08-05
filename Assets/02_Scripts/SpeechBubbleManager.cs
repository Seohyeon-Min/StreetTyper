using UnityEngine;
using System.Collections;

public class SpeechBubbleManager : MonoBehaviour
{
    public static SpeechBubbleManager Instance;

    public GameObject speechBubblePrefab;
    [Tooltip("마더 드래곤 본인의 대사와 인텐트에 사용하는 전용 프리팹.")]
    public GameObject motherDragonSpeechBubblePrefab;
    [Tooltip("마더 드래곤 구간에서 플레이어가 말할 때 사용할 전용 프리팹. 비워두면 일반 말풍선을 쓴다.")]
    public GameObject motherDragonPlayerSpeechBubblePrefab;
    public Transform canvasTransform;

    [Header("말풍선 위치 (참조 해상도 1920x1080 기준 픽셀)")]
    [Tooltip("플레이어 말풍선을 캐릭터에서 얼마나 밀어낼지. SpeechBubble.Setup이 pivot을 좌하단으로 " +
             "잡아 오른쪽 위로 펼쳐지므로, x를 양수로 두면 캐릭터 오른쪽에 붙는다.")]
    public Vector2 playerScreenOffset = new Vector2(60f, 120f);

    [Tooltip("적 말풍선 오프셋. 적은 pivot이 우하단이라 왼쪽 위로 펼쳐진다 - x를 음수로 두어 " +
             "플레이어와 좌우 대칭이 되게 한다.")]
    public Vector2 enemyScreenOffset = new Vector2(-60f, 120f);

    [Tooltip("캐릭터의 어느 지점을 기준으로 삼을지(월드 단위). 보통 0으로 두고 위 픽셀 값으로 맞춘다. " +
             "카메라가 orthographic size 5라 화면 세로가 10 월드 단위 = 1080p에서 1 월드가 약 108픽셀이다.")]
    public Vector3 bubbleWorldOffset = Vector3.zero;

    [Header("폴백 위치 (0.0 ~ 1.0)")]
    [Tooltip("카메라를 못 찾는 등 캐릭터 좌표를 쓸 수 없을 때만 사용하는 화면 비율. " +
             "0.5가 화면 정중앙, 0.25는 왼쪽 25%입니다. 평소에는 쓰이지 않는다.")]
    public Vector2 playerScreenRatio = new Vector2(0.25f, 0.65f);
    public Vector2 enemyScreenRatio = new Vector2(0.75f, 0.65f);

    private Camera _camera;
    private Canvas _canvas;

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

    // 말풍선은 항상 특정 캐릭터가 하는 말이므로 그 캐릭터 위에 떠야 한다.
    // Overlay 캔버스에서는 RectTransform.position이 스크린 픽셀 좌표와 같아서
    // WorldToScreenPoint 결과를 그대로 쓸 수 있다.
    public Vector3 GetBubbleScreenPosition(Vector3 worldPosition, bool isPlayer)
    {
        // Camera.main은 태그 검색이라 비싸다. 씬이 바뀌면 무효화되므로 null일 때만 다시 찾는다.
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
        {
            // 캐릭터 좌표를 화면 좌표로 옮길 수 없다 - 화면 비율로 폴백한다.
            Vector2 ratio = isPlayer ? playerScreenRatio : enemyScreenRatio;
            return new Vector3(Screen.width * ratio.x, Screen.height * ratio.y, 0f);
        }

        Vector3 screenPoint = _camera.WorldToScreenPoint(worldPosition + bubbleWorldOffset);

        // 플레이어와 적이 좌우 대칭으로 밀려나도록 오프셋을 따로 쓴다.
        // 참조 해상도 기준 값이므로 실제 픽셀로 바꿀 때 캔버스 scaleFactor를 곱한다.
        Vector2 offset = isPlayer ? playerScreenOffset : enemyScreenOffset;
        float scale = canvasTransform != null ? GetCanvasScaleFactor() : 1f;

        screenPoint.x += offset.x * scale;
        screenPoint.y += offset.y * scale;

        // WorldToScreenPoint의 z에는 카메라와의 거리가 담겨 온다.
        // Overlay 캔버스는 평면이 z = 0이어야 하므로 버린다.
        screenPoint.z = 0f;

        return screenPoint;
    }

    private float GetCanvasScaleFactor()
    {
        if (_canvas == null)
            _canvas = canvasTransform.GetComponentInParent<Canvas>();

        return _canvas != null ? _canvas.scaleFactor : 1f;
    }

    public void ShowBubble(string message, Vector3 worldPosition, bool isPlayer, float duration = 1.0f)
    {
        StartCoroutine(ShowBubbleRoutine(speechBubblePrefab, message, worldPosition, isPlayer, duration));
    }

    public void ShowMotherDragonBubble(string message, Vector3 worldPosition, bool isPlayer, float duration = 1.0f)
    {
        var prefab = isPlayer
            ? (motherDragonPlayerSpeechBubblePrefab != null ? motherDragonPlayerSpeechBubblePrefab : speechBubblePrefab)
            : (motherDragonSpeechBubblePrefab != null ? motherDragonSpeechBubblePrefab : speechBubblePrefab);
        StartCoroutine(ShowBubbleRoutine(prefab, message, worldPosition, isPlayer, duration));
    }

    private IEnumerator ShowBubbleRoutine(GameObject prefab, string message, Vector3 worldPosition,
                                          bool isPlayer, float duration)
    {
        if (prefab == null || canvasTransform == null) yield break;

        GameObject bubbleObj = Instantiate(prefab, canvasTransform);

        SpeechBubble bubbleScript = bubbleObj.GetComponent<SpeechBubble>();
        if (bubbleScript != null)
        {
            // 프리팹의 꼬리는 적 기준(오른쪽)으로 배치돼 있다 - 플레이어가 말할 때는 좌우로
            // 뒤집어야 꼬리가 화자를 향한다. 위치는 아래 GetBubbleScreenPosition이 이미
            // 좌우 대칭 오프셋으로 잡는다.
            bubbleScript.SetMirrored(isPlayer);
            bubbleScript.Setup(message);
        }

        RectTransform rectTransform = bubbleObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // 말한 캐릭터의 월드 위치를 기준으로 배치한다. 일회성 말풍선이고 캐릭터가
            // 떠 있는 동안 움직이지 않으므로 생성 시점에 한 번 잡으면 충분하다.
            rectTransform.position = GetBubbleScreenPosition(worldPosition, isPlayer);
        }

        bubbleObj.SetActive(true);

        yield return new WaitForSeconds(duration);
        Destroy(bubbleObj);
    }
}
