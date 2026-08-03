using UnityEngine;

// 슈퍼/울트라/하이퍼/메가톤 -> StatBonus, 파워 -> AmplifyNextStatBonus,
// 어썸 -> ScalingStatBonus, 퀵 -> TimerBonus, 럭키 -> LootBonusOnKill,
// 퍼펙트 -> TurnScalingStatBonus
//
// ⚠️ 순서를 바꾸거나 중간에 끼워 넣지 말 것 - 직렬화되는 건 인덱스라서 기존 카드 에셋이
// 조용히 다른 효과로 바뀐다(AttributeEffectType과 같은 이유). 새 값은 반드시 맨 뒤에 붙인다.
public enum ModifierEffectType
{
    StatBonus,
    AmplifyNextStatBonus,
    ScalingStatBonus,
    TimerBonus,
    LootBonusOnKill,

    /// <summary>이번 턴 진행 상황에 따라 값이 변하는 수치 상승(퍼펙트).
    /// 무엇을 셀지는 <see cref="ModifierCardData.ScalingSource"/>, 1단위당 값은 value가 정한다.</summary>
    TurnScalingStatBonus
}

[CreateAssetMenu(fileName = "New Modifier Card", menuName = "Deck Manager/Cards/Modifier Card")]
public class ModifierCardData : CardBase
{
    [SerializeField] private ModifierEffectType effectType;
    [SerializeField] private float value;

    [Tooltip("effectType이 Turn Scaling Stat Bonus일 때만 씁니다(퍼펙트). 이번 턴의 무엇을 셀지 " +
             "고르고, 1단위당 더할 값은 위 value입니다.")]
    [SerializeField] private TurnScalingSource scalingSource;

    [Header("럭키 - 보상이 쌓였을 때")]
    [Tooltip("effectType이 Loot Bonus On Kill일 때만 씁니다(럭키). 럭키로 얻은 보상 라운드가 아직 " +
             "남아 있는 동안 수치 칸에 statsLabel 대신 띄울 문구입니다. 비워두면 statsLabel 그대로 " +
             "나옵니다. 칸이 좁으니(42pt) 짧게 적을 것.")]
    [SerializeField] private string lootPendingStatsLabel;

    [Tooltip("위 문구의 영어판. 비워두면 한국어로 대체되고 경고가 남습니다.")]
    [SerializeField] private string lootPendingStatsLabelEn;

    public ModifierEffectType EffectType => effectType;
    public float Value => value;
    public TurnScalingSource ScalingSource => scalingSource;

    /// <summary>
    /// <see cref="ModifierEffectType.TurnScalingStatBonus"/> 카드가 <b>지금</b> 더해주는 값(퍼펙트).
    ///
    /// 파워(<see cref="ModifierEffectType.AmplifyNextStatBonus"/>) 보정은 여기 들어 있지 않다 -
    /// 그건 같은 조합에 파워가 몇 장 들어갔느냐에 달려서 카드 혼자서는 알 수 없고,
    /// <see cref="SkillResolver.Resolve"/>가 더한다. 슈퍼가 카드에 "+1"로 뜨지만 파워를 같이 넣으면
    /// 실제로 +2가 되는 것과 같은 규칙이다.
    /// </summary>
    public int CurrentTurnBonus => SkillResolver.ScalingBonus(scalingSource, Mathf.RoundToInt(value));

    /// <summary>
    /// 값이 런 도중 변하는 카드(어썸·퍼펙트)는 지금 수치를 카드에 그대로 보여준다 - 누적형이라
    /// "누적" 같은 고정 문구로는 지금 얼마인지 알 수가 없다. 인스펙터의 statsLabel을 포맷
    /// 문자열로 쓰고 {0} 자리에 런타임 값을 끼워 넣는다.
    ///
    /// 값이 바뀌는 순간 이미 그려둔 카드도 다시 써야 해서 <see cref="SkillResolver.OnCardValuesChanged"/>가
    /// 있고, <see cref="CardView"/>가 그걸 구독한다. 액션 카드 쪽의 같은 처리는
    /// <see cref="ActionCardData.StatsLabel"/>에 있다.
    ///
    /// {0}이 없으면 적힌 그대로 돌려준다 - 포맷을 안 넣은 카드에서 string.Format이 예외를 내거나
    /// 글자가 통째로 사라지는 일이 없게 하는 폴백이다.
    /// </summary>
    public override string StatsLabel
    {
        get
        {
            var label = base.StatsLabel;

            // ⚠️ 럭키는 아래 "{0} 가드"보다 먼저 처리해야 한다. 포맷을 쓰지 않고 문구 자체를
            // 갈아끼우는 유일한 케이스라, 가드에 걸리면(statsLabel에 {0}이 없다) 여기까지 오지 못한다.
            //
            // 보상이 쌓였다/아니다는 이분법이라 포맷 대신 완성된 문구를 따로 둔다 - "{0}"에 넣을
            // "됨"/"ED" 같은 조각을 코드에 박으면 인스펙터에서 문구를 못 바꾸게 된다.
            if (effectType == ModifierEffectType.LootBonusOnKill
                && SkillResolver.LootBonusRounds > 0
                && !string.IsNullOrEmpty(lootPendingStatsLabel))
            {
                return LanguageSettings.Pick(
                    lootPendingStatsLabel, lootPendingStatsLabelEn, this, nameof(lootPendingStatsLabelEn));
            }

            if (string.IsNullOrEmpty(label) || !label.Contains("{0}"))
                return label;

            switch (effectType)
            {
                case ModifierEffectType.ScalingStatBonus:
                    // 어썸은 0에서 시작해 오르기만 하므로 부호를 따로 붙일 일이 없다("+{0}" 포맷).
                    return string.Format(label, SkillResolver.AwesomeBonus);

                case ModifierEffectType.TurnScalingStatBonus:
                    // 퍼펙트는 시간을 늘리면 음수까지 내려간다. 부호를 붙여 넣으므로
                    // 포맷에 +를 적지 말 것 - "+-4"가 된다.
                    return string.Format(label, CurrentTurnBonus.ToString("+0;-0;0"));

                default:
                    return label;
            }
        }
    }

    public override CardCategory Category => CardCategory.Modifier;
}
