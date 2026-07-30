using UnityEngine;

// 월드 오브젝트(캐릭터)를 따라다니는 Screen Space Overlay UI.
// HP/방어도 바와 적 의도 말풍선처럼 "항상 특정 캐릭터의 정보를 보여주는" UI가 공유한다.
//
// Overlay 캔버스에서는 RectTransform.position이 스크린 픽셀 좌표와 같으므로
// WorldToScreenPoint 결과를 그대로 대입하면 된다.
//
// 숨길 때 SetActive(false)를 쓰지 않고 CanvasGroup.alpha를 쓰는 게 중요하다 - 오브젝트를
// 끄면 LateUpdate도 같이 멈춰서, 대상이 다시 나타나도 스스로 되살아나지 못한다.
[RequireComponent(typeof(CanvasGroup))]
public class WorldAnchoredUI : MonoBehaviour
{
    [Tooltip("따라갈 대상. 런타임에 정해지는 경우(스폰되는 적) 비워두고 Bind로 넘긴다.")]
    [SerializeField] private Transform target;

    [Tooltip("스크린으로 변환한 뒤 밀어낼 픽셀 양(참조 해상도 1920x1080 기준). " +
             "UI 크기와 같은 단위라 위치 조정은 여기서 하는 게 직관적이다. y를 키우면 머리 위로 올라간다.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 80f);

    [Tooltip("대상의 어느 지점을 기준으로 삼을지(월드 단위). 보통 0으로 두고 screenOffset으로 맞춘다. " +
             "카메라가 orthographic size 5라 화면 세로가 10 월드 단위 = 1080p에서 1 월드가 약 108픽셀이다 - " +
             "여기에 1을 넣으면 화면이 훌쩍 움직인다.")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Tooltip("대상이 없거나 비활성이면 이 UI도 같이 숨긴다.")]
    [SerializeField] private bool hideWhenTargetMissing = true;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;
    private Camera _camera;

    public Transform Target => target;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvas = GetComponentInParent<Canvas>();

        if (_rectTransform == null)
            Debug.LogWarning("WorldAnchoredUI: RectTransform이 없습니다. UI 오브젝트에만 붙일 수 있습니다.", this);

        if (_canvas == null)
            Debug.LogWarning("WorldAnchoredUI: 부모에 Canvas가 없습니다. Canvas 아래에 두어야 합니다.", this);
    }

    /// <summary>런타임에 대상을 지정한다. 적처럼 스테이지마다 새로 스폰되는 대상은
    /// StageManager가 스폰 직후 이걸로 넘겨준다(뷰가 스스로 찾아다니지 않는다).</summary>
    public void Bind(Transform newTarget)
    {
        target = newTarget;
    }

    // 캐릭터가 움직이거나 애니메이션이 적용된 뒤에 위치를 잡아야 한 프레임 밀리지 않는다.
    private void LateUpdate()
    {
        if (_rectTransform == null)
            return;

        // 적은 Destroy(CharacterStats.Die)와 SetActive(false)(BattleManager.CheckGameState)
        // 두 경로로 사라진다 - 둘 다 걸러야 죽은 적 자리에 UI가 남지 않는다.
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (hideWhenTargetMissing)
                SetVisible(false);
            return;
        }

        // Camera.main은 태그 검색이라 매 프레임 부르면 비싸다. 씬이 바뀌면 무효화되므로
        // null일 때만 다시 찾는다.
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return;

        if (hideWhenTargetMissing)
            SetVisible(true);

        var screenPoint = _camera.WorldToScreenPoint(target.position + worldOffset);

        // screenOffset은 참조 해상도 기준 값이므로 실제 픽셀로 바꿀 때 scaleFactor를 곱한다.
        // 이걸 빼면 해상도에 따라 띄운 거리가 UI 크기와 어긋난다.
        var scale = _canvas != null ? _canvas.scaleFactor : 1f;
        screenPoint.x += screenOffset.x * scale;
        screenPoint.y += screenOffset.y * scale;

        // WorldToScreenPoint의 z에는 카메라와의 거리가 담겨 온다. Overlay 캔버스에서는
        // 평면이 z = 0이어야 하므로 그대로 대입하지 않는다.
        screenPoint.z = 0f;

        _rectTransform.position = screenPoint;
    }

    // alpha만 건드린다 - 오브젝트를 끄면 LateUpdate가 멈춰 스스로 되살아나지 못한다.
    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = visible ? 1f : 0f;
    }
}
