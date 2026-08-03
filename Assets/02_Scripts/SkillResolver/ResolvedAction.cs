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
    public bool LootBonusOnKill;
}
