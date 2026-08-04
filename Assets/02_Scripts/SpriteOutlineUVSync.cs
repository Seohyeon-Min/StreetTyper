using UnityEngine;

// SpriteOutline 셰이더용 보조 컴포넌트. 애니메이션 프레임(Punch1~4 등)처럼 한 텍스처에 여러
// 스프라이트가 패딩 없이 붙어 있으면, 윤곽선의 팽창(dilation) 샘플링이 옆 프레임까지 읽어
// 이전/다음 프레임의 윤곽선이 같이 그려지는 문제가 있었다. 이 스프라이트 자신의 UV 사각형을
// 매 프레임 계산해 SpriteOutline.shader의 _SpriteUVRect로 넘기면, 셰이더가 그 범위 밖은
// 아예 샘플링하지 않아 옆 프레임을 침범하지 않는다.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineUVSync : MonoBehaviour
{
    private static readonly int SpriteUVRectID = Shader.PropertyToID("_SpriteUVRect");

    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private MaterialPropertyBlock block;
    private Sprite lastSprite;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        block = new MaterialPropertyBlock();
    }

    // 애니메이션이 매 프레임 다른 sprite로 갈아끼우므로 매 프레임 확인해야 한다.
    // Animator의 스프라이트 교체는 LateUpdate보다 먼저 끝나 있어 이 시점에 최신 값을 읽을 수 있다.
    void LateUpdate()
    {
        if (spriteRenderer == null) return;

        var sprite = spriteRenderer.sprite;
        if (sprite == lastSprite) return;
        lastSprite = sprite;

        if (sprite == null || sprite.texture == null) return;

        // textureRect는 런타임에 실제로 바인딩되는 텍스처(아틀라스로 패킹된 경우 포함) 기준
        // 픽셀 사각형이라, 소스 에셋 기준인 sprite.rect보다 이 용도에 맞는다.
        var rect = sprite.textureRect;
        var texWidth = sprite.texture.width;
        var texHeight = sprite.texture.height;
        if (texWidth <= 0 || texHeight <= 0) return;

        var uvRect = new Vector4(
            rect.xMin / texWidth,
            rect.yMin / texHeight,
            rect.xMax / texWidth,
            rect.yMax / texHeight
        );

        spriteRenderer.GetPropertyBlock(block);
        block.SetVector(SpriteUVRectID, uvRect);
        spriteRenderer.SetPropertyBlock(block);
    }
}
