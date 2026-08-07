using UnityEngine;

// 슈퍼/울트라/하이퍼/메가톤 -> StatBonus, 파워 -> AmplifyNextStatBonus,
// 어썸 -> ScalingStatBonus, 퀵 -> TimerBonus, 럭키 -> LootBonusOnKill,
// 퍼펙트 -> TurnScalingStatBonus
//
// JSON에 이름으로 적히므로 순서를 바꿔도 안전하다(AttributeEffectType과 같다).
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

/// <summary>수치를 올리거나 시간을 바꾸는 수식어 단어. 값은 <c>CardLocalization.json</c>의 한 행에서 온다.</summary>
public class ModifierCardData : CardBase
{
    private readonly ModifierEffectType effectType;
    private readonly float value;

    // effectType이 TurnScalingStatBonus일 때만 쓴다(퍼펙트). 이번 턴의 무엇을 셀지 고르고,
    // 1단위당 더할 값은 위 value다.
    private readonly TurnScalingSource scalingSource;

    // 아래 셋은 effectType이 LootBonusOnKill일 때만 쓴다(럭키) - 이번 스테이지에서 처음 쓸 때의
    // 확률(%), 한 번 쓸 때마다 오르는 폭(%p), 아무리 써도 넘지 않는 상한(%).
    private readonly float chancePercent;
    private readonly float chanceGainPerUse;
    private readonly float maxChancePercent;

    public ModifierCardData(CardDefinition definition) : base(definition)
    {
        effectType = CardDatabase.ParseEnum<ModifierEffectType>(definition.effectType, definition.id, nameof(definition.effectType));
        value = definition.value;
        scalingSource = CardDatabase.ParseEnum<TurnScalingSource>(definition.scalingSource, definition.id, nameof(definition.scalingSource));
        chancePercent = definition.chancePercent;
        chanceGainPerUse = definition.chanceGainPerUse;
        maxChancePercent = definition.maxChancePercent;
    }

    /// <summary>럭키로 얻은 보상 라운드가 아직 남아 있는 동안 수치 칸에 <see cref="StatsLabel"/>
    /// 대신 띄울 문구(보상 -> 보상됨). 다른 카드 텍스트와 같이 CardLocalization.json에서 오며,
    /// 그 카드에 <c>koLabelPending</c> 계열 칸이 없으면 비어 돌아온다 - 지금 이 칸을 가진 건
    /// 럭키 하나뿐이다.
    ///
    /// ⚠️ 예전에는 이 두 문구만 에셋에 <c>lootPendingStatsLabel</c>/<c>…En</c>으로 직렬화되어
    /// <b>한/영 두 벌뿐이었다.</b> 언어가 다섯으로 늘면서 프랑스어·스페인어·일본어 모드에서
    /// 럭키만 영어 "LOOTED"로 튀었다 - 카드 라벨 중 유일하게 JSON을 안 타던 예외였다.</summary>
    private string PendingStatsLabel => LanguageSettings.PickCardText(CardId, "labelPending");

    public ModifierEffectType EffectType => effectType;
    public float Value => value;
    public TurnScalingSource ScalingSource => scalingSource;

    /// <summary>
    /// 수치를 설명에 그대로 띄운다 - {0}에 value, {1}에 지금 턴 보너스(퍼펙트용)가 들어간다.
    ///
    /// ⚠️ <b>예전엔 이 재정의가 아예 없어서</b> ModifierCardData 계열(슈퍼/울트라/하이퍼/메가톤/
    /// 파워/퀵/럭키/어썸/퍼펙트)만 설명에 숫자를 넣을 수 없었다. AttributeCardData와
    /// ActionCardData는 진작 Fill을 거치는데 여기만 빠져 있어서, 설명에 "{0}"을 적으면
    /// 값이 채워지는 게 아니라 <b>중괄호가 그대로 화면에 나왔다.</b> 그래서 이 계열 카드는
    /// 수치를 글자로 박아둘 수밖에 없었고, 인스펙터에서 value를 바꿔도 설명은 옛 숫자로 남았다.
    ///
    /// 포맷 자리가 없으면 적힌 그대로 나오므로, 지금까지 쓰던 고정 문구는 그대로 동작한다.
    /// </summary>
    public override string Description
    {
        get
        {
            // 효과마다 {0}에 오는 값이 다르다 - 그 카드에서 <b>가장 중요한 숫자</b>가 언제나
            // 첫 자리에 오게 한다. StatsLabel이 effectType으로 갈리는 것과 같은 방식이다.
            switch (effectType)
            {
                // 럭키: {0}=지금 확률(%), {1}=한 번 쓸 때 오르는 폭(%p), {2}=상한(%)
                case ModifierEffectType.LootBonusOnKill:
                    return Fill(RawDescription, Mathf.RoundToInt(CurrentChancePercent),
                        Mathf.RoundToInt(chanceGainPerUse), Mathf.RoundToInt(maxChancePercent));

                // 퍼펙트: {0}=1단위당 값, {1}=지금 턴 보너스
                case ModifierEffectType.TurnScalingStatBonus:
                    return Fill(RawDescription, value, CurrentTurnBonus);

                default:
                    return Fill(RawDescription, value);
            }
        }
    }

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
    /// 럭키가 <b>지금</b> 성공할 확률(%). 이번 런에서 쓴 횟수만큼 올라가고 상한에서 멈춘다.
    ///
    /// ⚠️ 계산과 표시가 <see cref="SkillResolver.LuckyChance"/> 하나를 같이 거친다 - 카드에 뜬
    /// 숫자와 실제로 굴리는 확률이 어긋나면 그게 곧 버그로 보인다(ScalingBonus와 같은 규칙).
    /// </summary>
    public float CurrentChancePercent =>
        SkillResolver.LuckyChance(chancePercent, chanceGainPerUse, maxChancePercent);

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
            // "됨"/"ED" 같은 조각을 코드에 박으면 JSON에서 문구를 못 바꾸게 된다.
            if (effectType == ModifierEffectType.LootBonusOnKill
                && SkillResolver.LootBonusRounds > 0)
            {
                var pending = PendingStatsLabel;

                // JSON에 이 칸이 없는 카드는 평소 라벨 그대로 둔다 - 비어 있는 수치 칸이
                // 뜨는 것보다 낫고, 럭키 말고는 애초에 이 칸을 쓰지 않는다.
                if (!string.IsNullOrEmpty(pending))
                    return pending;
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
