using System.Collections.Generic;

public class ResolvedAction
{
    public int Damage;
    public int Defense;
    public int Heal;

    public bool IgnoresDefense;
    public bool BreaksEnemyDefense;

    /// <summary>이번 공격이 거는 상태이상. 컬러풀이 화상·마비·얼음을 <b>각각</b> 굴리므로
    /// 여러 개가 한꺼번에 들어올 수 있고, 하나도 안 걸리면 비어 있다.
    ///
    /// ⚠️ 여기 담기는 건 <see cref="SkillResolver.Resolve"/>가 <b>그 시점에 굴린 결과</b>다.
    /// 이 객체는 PendingActionManager에 쌓였다가 턴 끝에 재생되므로, 적용할 때 다시 굴리면
    /// 조합을 완성한 순간과 다른 결과가 나온다.</summary>
    public readonly List<StatusEffectType> StatusEffects = new List<StatusEffectType>();

    public float DamageReduction;
    public float TimerChange;

    /// <summary>럭키 확률 판정에 성공했는가. true면 이 조합이 적용될 때 보상 라운드가 하나 쌓이고,
    /// 그 스테이지를 클리어할 때 보상 창이 한 번 더 열린다.
    ///
    /// ⚠️ 옛 이름은 <c>LootBonusOnKill</c>이었는데 <b>이름이 규칙을 잘못 말하고 있었다</b> -
    /// "이 공격이 처치했을 때만"이 아니라 "판정에 성공했으면 이후 클리어할 때"다. 실제로 그
    /// 이름대로 처치 여부를 같이 보는 코드가 있어서, 판정에 성공해도 그 콤보가 마지막 일격이
    /// 아니면 아무 일도 일어나지 않았다.</summary>
    public bool GrantsLootBonus;

    public int HitCount = 1;
}
