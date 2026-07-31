using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 상태 이상의 부여·지속·해제를 전부 소유한다. 대상은 플레이어와 적 둘뿐이고,
// 적은 스테이지마다 바뀌므로 스테이지 전환 시 ClearAll로 비운다.
//
// 지속 감소와 화상 피해는 DeckManager가 적 턴 직후에 OnEnemyTurnEnded를 불러 처리한다 -
// EnemyManager/BattleManager를 수정하지 않기 위한 배치다(둘 다 다른 작업자 영역).
public class StatusEffectManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("화상 피해를 준 뒤 HP 표시와 승패 판정을 갱신하는 데 쓴다.")]
    [SerializeField] private BattleManager battleManager;

    [SerializeField] private EnemyManager enemyManager;

    [Header("지속")]
    [Tooltip("화상/얼음/데빌이 유지되는 턴 수. 마비는 GDD상 '다음 턴' 1회성이라 여기 따르지 않는다.")]
    [SerializeField] private int defaultDuration = 3;

    [Header("수치")]
    [Tooltip("화상 1회 피해 = 적 최대 체력 * 이 비율. GDD는 1/8.")]
    [SerializeField] private float burnDamageRatio = 0.125f;

    [Tooltip("적이 마비면 플레이어의 다음 턴 제한 시간이 이만큼 늘어난다(초).")]
    [SerializeField] private float paralysisTimerBonus = 5f;

    [Tooltip("얼음이 적의 힘을 이만큼 깎는다. 힘은 0 아래로 내려가지 않는다.")]
    [SerializeField] private int freezePowerPenalty = 3;

    [Tooltip("데빌이 걸린 동안 플레이어가 받는 피해 배율. 25% 감소면 0.75.")]
    [SerializeField] private float devilDamageMultiplier = 0.75f;

    [Header("연출")]
    [Tooltip("화상 피해를 넣기 전 잠깐 두는 시간(초). 적 공격과 같은 순간에 HP가 두 번 줄면 " +
             "어느 쪽이 화상인지 알 수 없어서, 사이를 벌려 따로 보이게 한다.")]
    [SerializeField] private float burnDamageDelay = 0.4f;

    [Tooltip("화상 피해가 들어갈 때 적 위에 말풍선으로 수치를 띄운다.")]
    [SerializeField] private bool showBurnBubble = true;

    [SerializeField] private float burnBubbleDuration = 0.8f;

    [SerializeField] private bool logDebugEvents;

    // 적에게 걸린 상태이상별 남은 턴. 재부여는 값 덮어쓰기라 "턴수 갱신"이 자연스럽게 성립한다.
    private readonly Dictionary<StatusEffectType, int> _enemyEffects = new Dictionary<StatusEffectType, int>();

    // 얼음은 최소 0 제한 때문에 실제로 깎인 양이 freezePowerPenalty와 다를 수 있다.
    // 복원할 때 어긋나지 않도록 깎은 양을 그대로 기억해 둔다.
    private int _appliedFreezePenalty;

    private CharacterStats _devilTarget;
    private int _devilTurnsLeft;

    /// <summary>상태이상이 걸리거나 풀리거나 턴이 줄어들 때마다 발생한다. 표시용 뷰가 구독한다.</summary>
    public event Action OnEffectsChanged;

    /// <summary>적에게 걸린 상태이상과 남은 턴. 뷰가 화면에 나열할 때 쓴다.</summary>
    public IReadOnlyDictionary<StatusEffectType, int> EnemyEffects => _enemyEffects;

    /// <summary>데빌의 남은 턴. 0이면 걸려 있지 않다.</summary>
    public int DevilTurnsLeft => _devilTurnsLeft;

    /// <summary>이 Transform이 지금 전투 중인 적인지. 표시용 뷰가 "내가 적 쪽인가 플레이어 쪽인가"를
    /// 스스로 판별하는 데 쓴다 - 인스펙터 토글에 기대면 오버라이드가 날아갔을 때 조용히 틀린다.</summary>
    public bool IsCurrentEnemy(Transform target)
    {
        if (target == null || enemyManager == null || enemyManager.currentEnemy == null)
            return false;

        return target == enemyManager.currentEnemy.transform;
    }

    /// <summary>화상/마비/얼음을 적에게 건다. 이미 걸려 있으면 남은 턴만 갱신된다.</summary>
    public void ApplyToEnemy(StatusEffectType effect, CharacterStats enemy)
    {
        if (effect == StatusEffectType.None || enemy == null)
            return;

        var alreadyApplied = _enemyEffects.ContainsKey(effect);
        _enemyEffects[effect] = defaultDuration;

        // 얼음은 스탯을 직접 건드리므로 처음 걸릴 때만 깎는다(재부여는 턴수만 갱신).
        if (effect == StatusEffectType.Freeze && !alreadyApplied)
        {
            _appliedFreezePenalty = Mathf.Min(freezePowerPenalty, enemy.power);
            enemy.power -= _appliedFreezePenalty;

            if (logDebugEvents)
                Debug.Log($"[상태이상] 얼음: 적 힘 -{_appliedFreezePenalty} (현재 {enemy.power})", this);
        }

        if (logDebugEvents)
            Debug.Log($"[상태이상] {effect} {defaultDuration}턴 {(alreadyApplied ? "갱신" : "부여")}", this);

        OnEffectsChanged?.Invoke();
    }

    /// <summary>데빌 - 플레이어가 받는 피해를 줄인다.</summary>
    public void ApplyDevil(CharacterStats player)
    {
        if (player == null)
            return;

        _devilTarget = player;
        _devilTurnsLeft = defaultDuration;
        player.damageTakenMultiplier = devilDamageMultiplier;

        if (logDebugEvents)
            Debug.Log($"[상태이상] 데빌: 받는 피해 x{devilDamageMultiplier} ({defaultDuration}턴)", this);

        OnEffectsChanged?.Invoke();
    }

    /// <summary>적이 마비면 플레이어의 다음 턴에 더해줄 시간(초). 아니면 0.</summary>
    public float GetTimerBonus()
    {
        return _enemyEffects.ContainsKey(StatusEffectType.Paralysis) ? paralysisTimerBonus : 0f;
    }

    /// <summary>적 턴이 끝난 직후 호출한다. 화상 피해를 넣고 모든 지속을 1턴 줄인다.
    /// 코루틴인 이유는 화상 피해를 적 공격과 겹치지 않게 잠깐 띄워 보여주기 위해서다 -
    /// 호출부에서 `yield return`으로 기다린다.</summary>
    public IEnumerator OnEnemyTurnEnded()
    {
        var enemy = enemyManager != null ? enemyManager.currentEnemy : null;

        if (enemy != null && _enemyEffects.ContainsKey(StatusEffectType.Burn))
        {
            // 적 공격 직후 HP가 연달아 두 번 줄면 어느 쪽이 화상인지 분간이 안 된다.
            if (burnDamageDelay > 0f)
                yield return new WaitForSeconds(burnDamageDelay);

            ApplyBurnDamage(enemy);

            if (showBurnBubble && burnBubbleDuration > 0f)
                yield return new WaitForSeconds(burnBubbleDuration);
        }

        TickDownEnemyEffects(enemy);
        TickDownDevil();

        OnEffectsChanged?.Invoke();
    }

    // 화상은 지속 피해라 방어도를 무시한다.
    //
    // ⚠️ TakeDamage를 쓰지 않는 이유: CharacterStats.Die()가 Destroy(gameObject)를 부르는데,
    // BattleManager.CheckGameState의 승리 판정은 currentEnemy가 null이 아닐 것을 요구한다.
    // 화상으로 적을 파괴해 버리면 승리 처리가 통째로 건너뛰어진다. currentHP를 직접 깎고
    // UpdateUI를 부르면 CheckGameState가 정상적인 SetActive(false) 경로를 탄다.
    private void ApplyBurnDamage(CharacterStats enemy)
    {
        var damage = Mathf.Max(1, Mathf.RoundToInt(enemy.maxHP * burnDamageRatio));
        enemy.currentHP = Mathf.Max(0, enemy.currentHP - damage);

        if (logDebugEvents)
            Debug.Log($"[상태이상] 화상 피해 {damage} (적 HP {enemy.currentHP}/{enemy.maxHP})", this);

        // 수치를 적 위에 띄워 "지금 줄어든 건 화상 때문"이라는 걸 알 수 있게 한다.
        if (showBurnBubble && SpeechBubbleManager.Instance != null)
            SpeechBubbleManager.Instance.ShowBubble($"화상 {damage}", enemy.transform.position, false, burnBubbleDuration);

        if (battleManager != null)
            battleManager.UpdateUI();
        else
            Debug.LogWarning("StatusEffectManager: battleManager가 연결되지 않아 화상 피해가 UI와 승패 판정에 반영되지 않습니다.", this);
    }

    private void TickDownEnemyEffects(CharacterStats enemy)
    {
        if (_enemyEffects.Count == 0)
            return;

        // 순회 중 제거할 수 없으므로 만료된 것을 모아 뒤에서 지운다.
        var expired = new List<StatusEffectType>();

        var keys = new List<StatusEffectType>(_enemyEffects.Keys);
        foreach (var effect in keys)
        {
            var left = _enemyEffects[effect] - 1;

            if (left <= 0)
                expired.Add(effect);
            else
                _enemyEffects[effect] = left;
        }

        foreach (var effect in expired)
        {
            _enemyEffects.Remove(effect);
            OnEffectExpired(effect, enemy);
        }
    }

    private void OnEffectExpired(StatusEffectType effect, CharacterStats enemy)
    {
        // 얼음이 풀리면 깎았던 만큼만 되돌린다. 그 사이 적이 버프(IncreasePower)를 받았어도
        // 증감이 대칭이라 결과가 어긋나지 않는다.
        if (effect == StatusEffectType.Freeze && enemy != null && _appliedFreezePenalty > 0)
        {
            enemy.power += _appliedFreezePenalty;

            if (logDebugEvents)
                Debug.Log($"[상태이상] 얼음 해제: 적 힘 +{_appliedFreezePenalty} (현재 {enemy.power})", this);
        }

        if (effect == StatusEffectType.Freeze)
            _appliedFreezePenalty = 0;

        if (logDebugEvents)
            Debug.Log($"[상태이상] {effect} 만료", this);
    }

    private void TickDownDevil()
    {
        if (_devilTurnsLeft <= 0)
            return;

        _devilTurnsLeft--;

        if (_devilTurnsLeft > 0)
            return;

        if (_devilTarget != null)
            _devilTarget.damageTakenMultiplier = 1f;

        _devilTarget = null;

        if (logDebugEvents)
            Debug.Log("[상태이상] 데빌 만료", this);
    }

    /// <summary>전부 비운다. 스테이지가 바뀌거나 전투가 끝날 때 호출한다.
    /// 이전 적은 이미 파괴/비활성이므로 얼음 복원은 시도하지 않고 상태만 지운다.</summary>
    public void ClearAll()
    {
        _enemyEffects.Clear();
        _appliedFreezePenalty = 0;

        // 데빌은 플레이어에게 걸린 것이라 반드시 되돌려야 한다 - 안 그러면 다음 판까지 감소가 남는다.
        if (_devilTarget != null)
            _devilTarget.damageTakenMultiplier = 1f;

        _devilTarget = null;
        _devilTurnsLeft = 0;

        if (logDebugEvents)
            Debug.Log("[상태이상] 전부 해제", this);

        OnEffectsChanged?.Invoke();
    }
}
