using System;
using System.Collections.Generic;
using UnityEngine;

// 체인에 담긴 단어 값 + 시전자의 힘만으로 ResolvedAction을 계산한다.
// 대상의 방어도나 상태이상은 여기서 모른다 - 그 상호작용은 CombatManager가 담당한다.
public class SkillResolver : MonoBehaviour
{
    [Header("디버그")]
    [Tooltip("켜면 상태이상 확률 판정을 건너뛰고 항상 부여한다. 파이어/일렉트릭/아이스가 " +
             "20%라 동작 확인이 어려울 때만 켜고, 밸런스 확인 전에 반드시 끌 것.")]
    [SerializeField] private bool alwaysApplyStatusEffect;

    /// <summary>
    /// 어썸(<see cref="ModifierEffectType.ScalingStatBonus"/>)이 지금 더해주는 값 =
    /// "이번 런에서 어썸을 성공한 횟수". 런 단위 상태라 <see cref="ResetRun"/>이 0으로 되돌린다.
    ///
    /// ⚠️ <b>static인 데는 이유가 있다.</b> 어썸 카드가 자기 <see cref="CardBase.StatsLabel"/>에
    /// 지금 누적치를 띄워야 하는데, `ScriptableObject`인 카드 에셋은 씬에 있는 이 컴포넌트를
    /// 참조할 수 없다. <see cref="CardBase.CardName"/>이 static <see cref="LanguageSettings"/>를
    /// 읽어 언어를 고르는 것과 똑같은 구조이고, 같은 이유로(카드마다 인스펙터 배선을 늘리지
    /// 않으려고) 이렇게 두었다. `SkillResolver`는 씬에 하나뿐이라 인스턴스 상태와 갈릴 일이 없다.
    /// </summary>
    public static int AwesomeBonus { get; private set; }

    // ── 이번 턴 누적 ────────────────────────────────────────────────────────
    // 퍼펙트/니킥/춉/박치기(TurnScalingSource)가 읽는 값이다. 위 AwesomeBonus와 같은 이유로
    // static이다 - 카드 에셋이 자기 StatsLabel에 지금 값을 띄워야 하기 때문이다.
    // 턴이 끝날 때 DeckManager가, 스테이지가 바뀔 때 StageManager가 ResetTurn()으로 비운다.

    /// <summary>이번 턴에 완성한 조합(액션)의 수.</summary>
    public static int ActionsThisTurn { get; private set; }

    /// <summary>이번 턴에 쓴 수식어(<see cref="CardCategory.Modifier"/>) 카드의 수.</summary>
    public static int ModifiersThisTurn { get; private set; }

    // 효과로 변한 초의 합(양수 = 늘어남). 자연 감소는 여기 들어오지 않는다.
    // 카드가 읽는 건 부호를 뒤집은 SecondsSpentThisTurn 쪽이다.
    private static float _timerChangeThisTurn;

    /// <summary>이번 턴에 단어 효과로 <b>줄어든</b> 초. 늘렸으면 음수다.</summary>
    public static int SecondsSpentThisTurn => Mathf.RoundToInt(-_timerChangeThisTurn);

    /// <summary>
    /// 카드에 띄우는 수치(<see cref="AwesomeBonus"/>·이번 턴 누적)가 바뀌었다.
    ///
    /// 이미 그려둔 카드를 다시 쓰게 하는 게 목적이다 - 손패에 어썸이 두 장 떠 있거나
    /// (슬롯 간 중복은 의도된 동작이다) 춉·박치기가 손패에 남아 있는 채로 다른 조합을 완성하면,
    /// 쓰지 않은 그 카드들의 수치도 그 자리에서 같이 변해야 한다. <see cref="CardView"/>가 구독한다.
    /// </summary>
    public static event Action OnCardValuesChanged;

    // static 필드는 Play를 멈춰도 남을 수 있다. LanguageSettings와 같은 이유로 초기화 지점을
    // 명시해 둔다 - 이전 판의 누적치나 죽은 구독자가 다음 Play로 넘어가지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticState()
    {
        AwesomeBonus = 0;
        ActionsThisTurn = 0;
        ModifiersThisTurn = 0;
        _timerChangeThisTurn = 0f;
        OnCardValuesChanged = null;
    }

    /// <summary>런(게임 한 판)이 새로 시작될 때 호출한다. 어썸 누적과 이번 턴 누적을 모두 되돌린다.</summary>
    public void ResetRun()
    {
        AwesomeBonus = 0;
        ResetTurn();
    }

    /// <summary>플레이어 턴이 끝났을 때 호출한다. "이번 턴에 몇 번 했는가" 계열 누적만 비우고
    /// 어썸(런 단위)은 건드리지 않는다.</summary>
    public void ResetTurn()
    {
        ActionsThisTurn = 0;
        ModifiersThisTurn = 0;
        _timerChangeThisTurn = 0f;

        // 손패에 남아 있는 춉/박치기 같은 카드가 새 턴의 0으로 되돌아가 보여야 한다.
        OnCardValuesChanged?.Invoke();
    }

    /// <summary>
    /// <paramref name="source"/>를 <paramref name="perUnit"/>배 한 위력 보너스.
    /// <see cref="Resolve"/>의 계산과 <see cref="ActionCardData.StatsLabel"/>의 표시가
    /// <b>같은 함수</b>를 쓰게 하려고 공개해 둔다 - 카드에 뜬 숫자와 실제로 들어가는 피해가
    /// 어긋나면 그게 곧 버그로 보인다.
    /// </summary>
    public static int ScalingBonus(TurnScalingSource source, int perUnit)
    {
        switch (source)
        {
            case TurnScalingSource.SecondsSpentThisTurn:
                return SecondsSpentThisTurn * perUnit;

            case TurnScalingSource.ActionsThisTurn:
                return ActionsThisTurn * perUnit;

            case TurnScalingSource.ModifiersThisTurn:
                return ModifiersThisTurn * perUnit;

            default:
                return 0;
        }
    }

    public ResolvedAction Resolve(IReadOnlyList<WordInstance> chain, int casterPower)
    {
        // WordChainManager가 마지막 단어는 항상 Action임을 보장한다.
        var actionCard = (ActionCardData)chain[chain.Count - 1].Card;

        // 파워는 타이핑 순서와 무관하게 체인 안의 모든 수치상승 단어에 똑같이 적용되어야 하므로
        // (Super Power Punch == Power Super Punch) 다른 값을 계산하기 전에 개수부터 센다.
        var powerCount = 0;

        // 박치기(ModifiersThisTurn)가 읽을 값. 이번 조합이 끝난 뒤 누적에 더한다.
        var modifierCount = 0;

        foreach (var word in chain)
        {
            if (word.Card is ModifierCardData m && m.EffectType == ModifierEffectType.AmplifyNextStatBonus)
                powerCount++;

            if (word.Card != null && word.Card.Category == CardCategory.Modifier)
                modifierCount++;
        }

        // 힘을 먼저 깔고 시작해야 GDD의 "펀치 = 힘+3"이 성립하고,
        // 뒤에서 곱하는 타격 횟수(트리플 등)도 (힘+보너스) 전체에 걸린다.
        // 턴 스케일링(퍼펙트/니킥/춉/박치기)도 액션 카드 자신의 값이라 같은 자리에 더한다.
        // ⚠️ 지금 완성하는 이 조합은 아직 누적에 들어가지 않았다 - 누적은 아래 맨 끝에서 한다.
        var totalValue = casterPower + actionCard.CurrentStrengthBonus;
        var timerChange = (float)actionCard.TimerDelta;
        var hitMultiplier = 1;
        var lifeStealRate = 0f;
        var damageReduction = 0f;
        var criticalChancePercent = 0f;
        var criticalMultiplier = 1f;
        var lootBonusOnKill = false;
        var statusEffect = StatusEffectType.None;
        var usedAwesome = false;

        foreach (var word in chain)
        {
            if (word.Card is ModifierCardData modifier)
            {
                switch (modifier.EffectType)
                {
                    case ModifierEffectType.StatBonus:
                        totalValue += Mathf.RoundToInt(modifier.Value) + powerCount;
                        break;
                    case ModifierEffectType.ScalingStatBonus:
                        // 이번 조합은 아직 세지 않은 값이라 첫 어썸은 보너스가 0이고, 쓸수록 커진다.
                        // powerCount를 더하는 건 다른 수치 상승 단어와 같은 규칙(파워가 효과를 +1)이다.
                        totalValue += AwesomeBonus + powerCount;
                        usedAwesome = true;
                        break;
                    case ModifierEffectType.TurnScalingStatBonus:
                        // 퍼펙트 - 이번 턴에 줄어든 초만큼 위력이 오른다(늘렸으면 깎인다).
                        // 어썸과 마찬가지로 지금 조합은 아직 누적에 들어가 있지 않고,
                        // 수치 상승 단어이므로 파워 보정도 똑같이 받는다.
                        totalValue += modifier.CurrentTurnBonus + powerCount;
                        break;
                    case ModifierEffectType.TimerBonus:
                        timerChange += modifier.Value;
                        break;
                    case ModifierEffectType.LootBonusOnKill:
                        lootBonusOnKill = true;
                        break;
                    // AmplifyNextStatBonus(파워)는 위에서 powerCount로 이미 반영했다.
                }
            }
            else if (word.Card is AttributeCardData attribute)
            {
                switch (attribute.EffectType)
                {
                    case AttributeEffectType.LifeDrain:
                        lifeStealRate = attribute.Value;
                        break;
                    case AttributeEffectType.DamageReduction:
                        damageReduction = attribute.Value;
                        break;
                    case AttributeEffectType.CritMultiplier:
                        criticalChancePercent = attribute.ChancePercent;
                        criticalMultiplier = attribute.Value;
                        break;
                    case AttributeEffectType.RepeatAction:
                        hitMultiplier = Mathf.RoundToInt(attribute.Value);
                        break;
                    case AttributeEffectType.StatusChanceSingle:
                        if (RollStatusChance(attribute.ChancePercent))
                            statusEffect = attribute.StatusEffect;
                        break;
                    case AttributeEffectType.StatusChanceAll:
                        // StatusEffectType이 값 하나뿐이라 셋을 동시에 못 담는다 - GDD의
                        // "화상 > 마비 > 얼음" 우선순위를 "하나만 나타난다면 화상"으로 단순화했다.
                        if (RollStatusChance(attribute.ChancePercent))
                            statusEffect = StatusEffectType.Burn;
                        break;
                }
            }
        }

        // 니킥처럼 깎는 스케일링이 붙은 카드는 여기까지 오면서 음수가 될 수 있다.
        // 음수인 채로 타격 횟수를 곱하면 더 깊이 내려가고, 그대로 나가면 피해가 회복으로
        // 둔갑한다(CharacterStats.TakeDamage는 음수를 걸러내지 않는다). 곱하기 전에 한 번 막는다.
        totalValue = Mathf.Max(0, totalValue);

        var hitCount = hitMultiplier * RollComboHits(actionCard);
        totalValue *= hitCount;

        // ⚠️ UnityEngine.Random으로 명시한다 - 이 파일은 using System을 쓰므로 그냥 Random이라고
        // 적으면 System.Random과 모호해져 컴파일이 깨진다(WordUnlockManager도 같은 이유로 명시한다).
        if (UnityEngine.Random.Range(0f, 100f) < criticalChancePercent)
            totalValue = Mathf.RoundToInt(totalValue * criticalMultiplier);

        var result = new ResolvedAction
        {
            IgnoresDefense = actionCard.IgnoresDefense,
            BreaksEnemyDefense = actionCard.BreaksEnemyDefense,
            StatusEffect = statusEffect,
            DamageReduction = damageReduction,
            TimerChange = timerChange,
            LootBonusOnKill = lootBonusOnKill
        };

        if (actionCard.ActionKind == ActionKind.Attack)
            result.Damage = totalValue;
        else
            result.Defense = totalValue;

        var baseForHeal = actionCard.ActionKind == ActionKind.Attack ? result.Damage : result.Defense;
        result.Heal = Mathf.RoundToInt(lifeStealRate / 100f * baseForHeal);

        // 계산이 다 끝난 뒤에 센다 - 이번 조합의 보너스에는 반영되지 않아야
        // "이전에 성공한 횟수 / 이번 턴에 이미 한 만큼"이라는 규칙이 성립한다.
        // 턴 누적도 같은 이유로 여기서 더한다.
        if (usedAwesome)
            AwesomeBonus++;

        ActionsThisTurn++;
        ModifiersThisTurn += modifierCount;
        _timerChangeThisTurn += timerChange;

        // 손패에 남아 있는 어썸·춉·박치기·퍼펙트의 수치 칸을 방금 바뀐 값으로 다시 쓰게 한다.
        OnCardValuesChanged?.Invoke();

        return result;
    }

    // 파이어/일렉트릭/아이스가 20%라 동작 확인이 어렵다. 디버그 토글이 켜져 있으면
    // 판정을 건너뛰고 항상 성공시킨다 - 밸런스를 볼 때는 반드시 꺼야 한다.
    private bool RollStatusChance(float chancePercent)
    {
        if (alwaysApplyStatusEffect)
            return true;

        return UnityEngine.Random.Range(0f, 100f) < chancePercent;
    }

    // 뎀프시롤처럼 콤보 확률형 액션의 타격 횟수를 굴린다. 콤보가 아니면 1회.
    private static int RollComboHits(ActionCardData actionCard)
    {
        if (!actionCard.IsComboAttack)
            return 1;

        var hits = actionCard.ComboMinHits;
        while (hits < actionCard.ComboMaxHits && UnityEngine.Random.Range(0f, 100f) < actionCard.ComboChancePercent)
            hits++;

        return hits;
    }
}
