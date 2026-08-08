/// <summary>
/// 조합에 들어가지 않고 <b>화면에서 명령으로만 쓰이는</b> 카드(넘기기/지우기/계속/카드/타이틀/다시하기).
///
/// 명령 단어를 카드로 보여주려고 만든 분류다. 예전에는 <c>CardView.SetText(문자열)</c>로 이름만
/// 그렸는데, 그러면 카드 분류 체계 밖에 있어서 겉모습이 모디파이어 카드와 구분되지 않았고
/// 일시정지와 보상 화면이 서로 다르게 그려질 참이었다. 진짜 <see cref="CardBase"/>로 만들면
/// 손패·보상·일시정지가 전부 <see cref="CardView.SetCard"/> 하나로 그려진다.
///
/// 자기 필드가 없다 - 이름·설명·수치 칸이면 충분하고 그건 전부 JSON에 있다. 안내 문구도
/// 카드 자체가 단어를 보여주므로 쓰지 않는다.
///
/// ⚠️ JSON 행에 <c>grantedAtStart</c>를 켜지 말 것. 켜면 사전에 들어가 손패에 뜨고 타이핑으로
/// 소비된다. <see cref="WordUnlockManager"/>가 해금 후보에서 <see cref="CardCategory.Command"/>를
/// 걸러내지만, 애초에 켜지 않는 게 맞다.
/// </summary>
public class CommandCardData : CardBase
{
    public CommandCardData(CardDefinition definition) : base(definition) { }

    public override CardCategory Category => CardCategory.Command;
}
