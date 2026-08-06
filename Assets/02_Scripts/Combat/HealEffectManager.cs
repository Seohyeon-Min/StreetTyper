using System.Collections;
using UnityEngine;

/// <summary>플레이어의 가드/회복 시 몸 주변에서 위로 떠오르는 공용 스프라이트 파티클.</summary>
public class HealEffectManager : MonoBehaviour
{
    public static HealEffectManager Instance;

    [Header("파티클 스프라이트")]
    [Tooltip("가드 파티클 이미지. 비우면 기본 사각형을 사용한다.")]
    [SerializeField] private Sprite guardParticleSprite;

    [Tooltip("힐 파티클 이미지. 비우면 기본 사각형을 사용한다.")]
    [SerializeField] private Sprite healParticleSprite;

    [Tooltip("플레이어 가드와 힐 파티클 위치. 씬의 Pos Transform을 연결한다.")]
    [SerializeField] private Transform particleSpawnPoint;

    [Tooltip("적 가드 파티클 위치. 비우면 해당 적의 Transform 위치를 사용한다.")]
    [SerializeField] private Transform enemyParticleSpawnPoint;

    [Header("색상")]
    [SerializeField] private Color guardParticleColor = new Color(0.25f, 0.75f, 1f, 0.9f);
    [SerializeField] private Color healParticleColor = new Color(0.35f, 1f, 0.45f, 0.9f);

    [Header("가드 설정")]
    [Min(1)] [SerializeField] private int guardParticleCount = 10;
    [Tooltip("가드 파티클 배속.")]
    [Min(0.01f)] [SerializeField] private float guardParticleSpeed = 1f;
    [Tooltip("가드 파티클의 월드 크기.")]
    [Min(0.001f)] [SerializeField] private float guardParticleSize = 0.14f;
    [Tooltip("가드 파티클 지속시간(초).")]
    [Min(0.01f)] [SerializeField] private float guardParticleLifetime = 0.75f;
    [Tooltip("가드 파티클 상승 거리.")]
    [Min(0f)] [SerializeField] private float guardRiseDistance = 1.1f;
    [Tooltip("가드 파티클 시작점이 퍼질 수평 엣지 길이. Spawn Point를 중심으로 좌우 절반씩 펼쳐진다.")]
    [Min(0f)] [SerializeField] private float guardSpawnEdgeLength = 1f;

    [Header("힐 설정")]
    [Min(1)] [SerializeField] private int healParticleCount = 10;
    [Tooltip("힐 파티클 배속.")]
    [Min(0.01f)] [SerializeField] private float healParticleSpeed = 1f;
    [Tooltip("힐 파티클의 월드 크기.")]
    [Min(0.001f)] [SerializeField] private float healParticleSize = 0.14f;
    [Tooltip("힐 파티클 지속시간(초).")]
    [Min(0.01f)] [SerializeField] private float healParticleLifetime = 0.75f;
    [Tooltip("힐 파티클 상승 거리.")]
    [Min(0f)] [SerializeField] private float healRiseDistance = 1.1f;
    [Tooltip("힐 파티클 시작점이 퍼질 수평 엣지 길이. Spawn Point를 중심으로 좌우 절반씩 펼쳐진다.")]
    [Min(0f)] [SerializeField] private float healSpawnEdgeLength = 1f;

    [Header("공용 움직임")]
    [Min(0f)] [SerializeField] private float horizontalSpread = 0.45f;
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private static Sprite defaultSquareSprite;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);
    }

    public void PlayGuardEffect(SpriteRenderer target)
    {
        PlayRisingEffect(target, guardParticleSprite, guardParticleColor,
            guardParticleCount, guardParticleSpeed, guardParticleSize,
            guardParticleLifetime, guardRiseDistance, guardSpawnEdgeLength);
    }

    public void PlayHealEffect(SpriteRenderer target)
    {
        PlayRisingEffect(target, healParticleSprite, healParticleColor,
            healParticleCount, healParticleSpeed, healParticleSize,
            healParticleLifetime, healRiseDistance, healSpawnEdgeLength);
    }

    private void PlayRisingEffect(SpriteRenderer target, Sprite sprite, Color color,
                                  int count, float speed, float size, float lifetime,
                                  float distance, float spawnEdgeLength)
    {
        if (target == null)
            return;

        if (sprite == null)
            sprite = GetDefaultSquareSprite();

        var isEnemy = target.GetComponent<EnemyBase>() != null;
        var configuredSpawnPoint = isEnemy ? enemyParticleSpawnPoint : particleSpawnPoint;
        var origin = configuredSpawnPoint != null ? configuredSpawnPoint.position : target.transform.position;

        for (var i = 0; i < count; i++)
        {
            var halfEdge = spawnEdgeLength * 0.5f;
            var start = new Vector3(
                origin.x + Random.Range(-halfEdge, halfEdge),
                origin.y,
                target.transform.position.z);

            StartCoroutine(RiseParticle(
                sprite,
                color,
                start,
                target.sortingLayerID,
                target.sortingOrder + 2,
                Random.Range(-horizontalSpread, horizontalSpread),
                Random.Range(0.8f, 1.2f),
                speed,
                size,
                lifetime,
                distance));
        }
    }

    private IEnumerator RiseParticle(Sprite sprite, Color color, Vector3 start, int sortingLayerId,
                                     int sortingOrder, float horizontalDrift, float heightMultiplier,
                                     float speed, float size, float lifetime, float distance)
    {
        var particle = new GameObject("BuffRiseParticle", typeof(SpriteRenderer));
        particle.transform.SetParent(transform, true);
        particle.transform.position = start;
        particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));

        var renderer = particle.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;

        var spriteExtent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        var scale = spriteExtent > 0f ? size / spriteExtent : size;
        particle.transform.localScale = Vector3.one * scale * Random.Range(0.75f, 1.25f);

        var elapsed = 0f;
        var spin = Random.Range(-80f, 80f);
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime * speed;
            var progress = Mathf.Clamp01(elapsed / lifetime);
            var rise = riseCurve != null ? riseCurve.Evaluate(progress) : progress;
            var alpha = fadeCurve != null ? fadeCurve.Evaluate(progress) : 1f - progress;

            particle.transform.position = start + new Vector3(
                horizontalDrift * rise,
                distance * heightMultiplier * rise,
                0f);
            particle.transform.Rotate(0f, 0f, spin * Time.deltaTime * speed);

            var currentColor = color;
            currentColor.a *= Mathf.Clamp01(alpha);
            renderer.color = currentColor;
            yield return null;
        }

        Destroy(particle);
    }

    private static Sprite GetDefaultSquareSprite()
    {
        if (defaultSquareSprite == null)
        {
            defaultSquareSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            defaultSquareSprite.name = "Runtime Default Square Particle";
        }

        return defaultSquareSprite;
    }
}
