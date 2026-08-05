using UnityEngine;

// 회복 시 스프라이트 범위 내 랜덤한 위치에 이펙트를 하나 띄운다. HitEffectManager와 완전히
// 같은 이유로 싱글턴이다 - CharacterStats는 player뿐 아니라 적 프리팹마다 하나씩 붙어 있어서,
// [SerializeField] 손 연결 방식으로는 프리팹 개수만큼 같은 참조를 반복해서 심어줘야 한다.
public class HealEffectManager : MonoBehaviour
{
    public static HealEffectManager Instance;

    [Tooltip("회복 시 생성할 이펙트 프리팹. 재생 시간과 자동 파괴는 프리팹에 붙은 EffectBase가 스스로 책임진다.")]
    [SerializeField] private GameObject healEffectPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void PlayHealEffect(SpriteRenderer target)
    {
        if (healEffectPrefab == null)
        {
            Debug.LogWarning($"{nameof(HealEffectManager)}: healEffectPrefab이 비어 있습니다.", this);
            return;
        }

        if (target == null)
            return;

        var bounds = target.bounds;
        var spawnPos = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            target.transform.position.z
        );

        Instantiate(healEffectPrefab, spawnPos, Quaternion.identity);
    }
}
