using UnityEngine;

// 피격 시 스프라이트 범위 내 랜덤한 위치에 이펙트를 하나 띄운다.
//
// CameraShake/FloatingDamageManager와 같은 이유로 싱글턴이다: 이 매니저를 부르는 쪽이
// CharacterStats.TakeDamage인데, CharacterStats는 player 뿐 아니라 적 프리팹마다
// (enemy/strongEnemy/MotherDragon...) 하나씩 붙어 있어서, 일반적인 [SerializeField] 손 연결
// 방식으로는 프리팹 개수만큼 매번 같은 참조를 반복해서 심어줘야 한다. 싱글턴로 두면
// 씬에 하나만 있으면 되고 CharacterStats 쪽은 아무 것도 연결하지 않아도 된다.
public class HitEffectManager : MonoBehaviour
{
    public static HitEffectManager Instance;

    [Tooltip("피격 시 생성할 이펙트 프리팹. 재생 시간과 자동 파괴는 프리팹에 붙은 EffectBase가 스스로 책임진다.")]
    [SerializeField] private GameObject hitEffectPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void PlayHitEffect(SpriteRenderer target)
    {
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning($"{nameof(HitEffectManager)}: hitEffectPrefab이 비어 있습니다.", this);
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

        Instantiate(hitEffectPrefab, spawnPos, Quaternion.identity);
    }
}
