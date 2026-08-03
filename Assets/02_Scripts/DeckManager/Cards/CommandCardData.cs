using UnityEngine;

/// <summary>
/// 조합에 들어가지 않고 <b>화면에서 명령으로만 쓰이는</b> 카드(넘기기/지우기/계속/카드/타이틀).
///
/// 명령 단어를 카드로 보여주려고 만든 분류다. 예전에는 <c>CardView.SetText(문자열)</c>로 이름만
/// 그렸는데, 그러면 카드 분류 체계 밖에 있어서 겉모습이 모디파이어 카드와 구분되지 않았고
/// 일시정지와 보상 화면이 서로 다르게 그려질 참이었다. 진짜 <see cref="CardBase"/> 에셋으로 만들면
/// 손패·보상·일시정지가 전부 <see cref="CardView.SetCard"/> 하나로 그려진다.
///
/// 필드를 따로 두지 않는다 - 이름·설명·수치 한/영 여섯 칸이면 충분하고, 안내 문구는 카드 자체가
/// 단어를 보여주므로 쓰지 않는다.
///
/// ⚠️ 이 카드를 <see cref="WordUnlockManager"/>의 목록이나 <see cref="WordDictionary"/>에 넣지 말 것.
/// 넣으면 손패에 떠서 타이핑으로 소비되거나 보상 후보로 나온다. 세 곳(사전·해금·조합)이 각각
/// <see cref="CardCategory.Command"/>를 보고 막지만, 애초에 넣지 않는 게 맞다.
/// </summary>
[CreateAssetMenu(fileName = "New Command Card", menuName = "Deck Manager/Cards/Command Card")]
public class CommandCardData : CardBase
{
    public override CardCategory Category => CardCategory.Command;
}
