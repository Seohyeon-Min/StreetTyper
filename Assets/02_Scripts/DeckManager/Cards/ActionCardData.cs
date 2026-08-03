using UnityEngine;

public enum ActionKind
{
    Attack,
    Defense
}

[CreateAssetMenu(fileName = "New Action Card", menuName = "Deck Manager/Cards/Action Card")]
public class ActionCardData : CardBase
{
    [SerializeField] private ActionKind actionKind;
    [SerializeField] private int strengthBonus;
    [SerializeField] private int timerDelta;
    [SerializeField] private bool ignoresDefense;      // 페인트: 적 방어도 무시
    [SerializeField] private bool breaksEnemyDefense;   // 어퍼컷: 적 방어도를 0으로
    [SerializeField] private bool isComboAttack;        // 뎀프시롤: 재사용 콤보
    [SerializeField] private float comboChancePercent;
    [SerializeField] private int comboMinHits;
    [SerializeField] private int comboMaxHits;

    [Header("이번 턴 스케일링")]
    [Tooltip("위력이 이번 턴 진행 상황에 따라 변하는 카드만 설정합니다(니킥/춉/박치기). " +
             "None이면 기존 카드와 똑같이 동작합니다.")]
    [SerializeField] private TurnScalingSource scalingSource;

    [Tooltip("위 항목 1단위당 더할 위력. 음수를 넣으면 깎입니다(니킥은 -1). " +
             "힘 + 위력 보너스에 더해지며, 파워(AmplifyNextStatBonus)의 영향은 받지 않습니다 - " +
             "수식어 효과가 아니라 액션 카드 자신의 값이라 펀치의 +3과 같은 취급입니다.")]
    [SerializeField] private int scalingPerUnit;

    public ActionKind ActionKind => actionKind;
    public int StrengthBonus => strengthBonus;
    public int TimerDelta => timerDelta;
    public bool IgnoresDefense => ignoresDefense;
    public bool BreaksEnemyDefense => breaksEnemyDefense;
    public bool IsComboAttack => isComboAttack;
    public float ComboChancePercent => comboChancePercent;
    public int ComboMinHits => comboMinHits;
    public int ComboMaxHits => comboMaxHits;
    public TurnScalingSource ScalingSource => scalingSource;
    public int ScalingPerUnit => scalingPerUnit;

    /// <summary>
    /// 이 카드가 <b>지금</b> 더해주는 위력 = 고정 보너스 + 이번 턴 스케일링.
    /// 스케일링이 없는 카드(펀치 등)는 <see cref="StrengthBonus"/>와 같다.
    ///
    /// <see cref="SkillResolver.Resolve"/>의 피해 계산과 아래 <see cref="StatsLabel"/>의 표시가
    /// 둘 다 이걸 쓴다 - 카드에 뜬 숫자와 실제로 들어가는 피해가 어긋나지 않게 하기 위해서다.
    /// </summary>
    public int CurrentStrengthBonus => strengthBonus + SkillResolver.ScalingBonus(scalingSource, scalingPerUnit);

    /// <summary>콤보 확률을 설명에 그대로 띄운다 - {0}=최소 타수, {1}=최대 타수, {2}=추가 타수 확률.
    ///
    /// 뎀프시롤이 이걸 쓴다. "2~5회"라고만 적으면 균등 분포처럼 읽히는데 실제 <see cref="SkillResolver"/>는
    /// <b>최소 타수에서 시작해 추가 타수마다 확률을 새로 굴린다</b>(2타 50%, 3타 25%, 4·5타 각 12.5%).
    /// 확률을 같이 보여줘야 실제 세기를 가늠할 수 있다. 포맷 자리가 없으면 적힌 그대로 나온다.</summary>
    public override string Description =>
        Fill(RawDescription, comboMinHits, comboMaxHits, comboChancePercent);

    /// <summary>
    /// 니킥/춉/박치기처럼 이번 턴 상황에 따라 위력이 변하는 카드는 지금 수치를 그대로
    /// 보여준다 - 어썸과 같은 이유다(고정 문구로는 지금 얼마인지 알 수가 없다).
    /// 인스펙터의 statsLabel을 포맷("{0}")으로 쓰고 {0} 자리에 현재 위력 보너스를 끼워 넣는다.
    ///
    /// 부호를 붙여 넣으므로 포맷에 +를 따로 적지 말 것 - 니킥은 액션을 많이 하면 음수까지 내려간다
    /// ("+-2"가 되지 않게 여기서 +2 / -2 / 0 세 가지로 나눠 쓴다).
    ///
    /// {0}이 없으면 적힌 그대로 돌려준다 - 포맷을 안 넣은 카드에서 string.Format이 예외를 내거나
    /// 글자가 통째로 사라지는 일이 없게 하는 폴백이다(ModifierCardData와 같은 처리).
    /// </summary>
    public override string StatsLabel
    {
        get
        {
            var label = base.StatsLabel;

            if (scalingSource == TurnScalingSource.None || string.IsNullOrEmpty(label) || !label.Contains("{0}"))
                return label;

            return string.Format(label, CurrentStrengthBonus.ToString("+0;-0;0"));
        }
    }

    public override CardCategory Category => CardCategory.Action;
}
