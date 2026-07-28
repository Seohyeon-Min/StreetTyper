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

    public ActionKind ActionKind => actionKind;
    public int StrengthBonus => strengthBonus;
    public int TimerDelta => timerDelta;
    public bool IgnoresDefense => ignoresDefense;
    public bool BreaksEnemyDefense => breaksEnemyDefense;
    public bool IsComboAttack => isComboAttack;
    public float ComboChancePercent => comboChancePercent;
    public int ComboMinHits => comboMinHits;
    public int ComboMaxHits => comboMaxHits;

    public override CardCategory Category => CardCategory.Action;
}
