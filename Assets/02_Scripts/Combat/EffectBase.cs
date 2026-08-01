using System.Collections.Generic;
using UnityEngine;

// 이펙트 프리팹 공통 베이스. 이펙트가 스프라이트 하나짜리든, 셰이더+파티클이 여러 겹 합쳐진 것이든
// 상관없이 lifetime 하나로 전체 오브젝트 수명을 관리하고 다 되면 스스로 파괴한다.
// 자식 중 _Progress(0~1) 프로퍼티를 쓰는 렌더러가 있으면, 그 머티리얼의 _Duration(초) 값을
// 읽어서 레이어마다 서로 다른 속도로 재생해준다(_Duration이 없으면 lifetime을 그대로 씀).
// _Seed 프로퍼티를 쓰는 렌더러가 있으면 스폰될 때마다 랜덤값을 한 번 넣어줘서 매번 다른 모양으로 보이게 한다.
// 파티클 시스템은 각자 알아서 재생되므로 따로 건드리지 않는다.
// (team17_gamejam의 같은 이름 스크립트를 그대로 가져왔다.)
public class EffectBase : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int DurationId = Shader.PropertyToID("_Duration");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");

    [Tooltip("_Seed 프로퍼티를 랜덤으로 채울 때 뽑는 범위. 셰이더들의 _Seed Range(0,100)에 맞춰져 있다.")]
    [SerializeField] private Vector2 seedRange = new Vector2(0f, 100f);

    [Tooltip("이펙트 오브젝트 전체가 살아있는 시간(초). 레이어별 _Duration이 더 길어도 이 값이 지나면 통째로 파괴된다. -1이면 시간으로 자동 파괴되지 않고, 외부에서 Destroy를 호출할 때까지 계속 재생된다.")]
    [SerializeField] private float lifetime = 1f;

    [Tooltip("선형으로 흐르는 진행도(0~1)를 이 커브로 리매핑해서 _Progress로 보낸다. 기본값(직선)이면 리매핑 없이 그대로 나감.")]
    [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("스폰된 위치 기준으로 이펙트를 이 만큼 어긋나게 그린다.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    private struct ProgressTarget
    {
        public Renderer Renderer;
        public float Duration;
    }

    private readonly List<ProgressTarget> progressTargets = new();
    private MaterialPropertyBlock propertyBlock;
    private float elapsed;

    protected virtual void Awake()
    {
        transform.position += positionOffset;
        propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
        {
            Material material = candidate.sharedMaterial;
            if (material == null)
                continue;

            if (material.HasProperty(SeedId))
                ApplyRandomSeed(candidate);

            if (!material.HasProperty(ProgressId))
                continue;

            float duration = material.HasProperty(DurationId)
                ? Mathf.Max(0.01f, material.GetFloat(DurationId))
                : (lifetime < 0f ? Mathf.Infinity : lifetime);

            progressTargets.Add(new ProgressTarget { Renderer = candidate, Duration = duration });
        }
    }

    // 스폰될 때 한 번만 랜덤 시드를 넣어준다(매 프레임 갱신할 필요 없음).
    private void ApplyRandomSeed(Renderer target)
    {
        float randomSeed = Random.Range(seedRange.x, seedRange.y);
        target.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(SeedId, randomSeed);
        target.SetPropertyBlock(propertyBlock);
    }

    protected virtual void Update()
    {
        elapsed += Time.deltaTime;

        foreach (ProgressTarget target in progressTargets)
        {
            float linearT = Mathf.Clamp01(elapsed / target.Duration);
            float progress = Mathf.Clamp01(progressCurve.Evaluate(linearT));
            target.Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ProgressId, progress);
            target.Renderer.SetPropertyBlock(propertyBlock);
        }

        if (lifetime >= 0f && elapsed >= lifetime)
            Destroy(gameObject);
    }

    protected virtual void OnValidate()
    {
        if (lifetime < 0f)
            lifetime = -1f; // -1은 "외부에서 파괴할 때까지 무한 재생"을 뜻하는 값으로 고정한다.
        else if (lifetime < 0.01f)
            lifetime = 0.01f;
    }
}
