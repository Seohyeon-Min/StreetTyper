using UnityEngine;

public enum CardCategory
{
    Modifier,
    Time,
    Type,
    Action
}

/// <summary>
/// 카드의 값이 "이번 턴에 무엇을 얼마나 했는가"에 따라 변할 때 무엇을 셀지 고른다.
/// <b>액션 카드(니킥/춉/박치기)와 수식어 카드(퍼펙트)가 같이 쓴다</b> - 그래서 특정 카드 타입이
/// 아니라 여기(CardBase.cs)에 둔다.
///
/// ⚠️ 세는 값은 전부 <b>지금 완성하는 이 조합 이전까지</b>의 누적이다 - 자기 자신은 세지 않는다.
/// 어썸(<see cref="ModifierEffectType.ScalingStatBonus"/>)이 "이전에 성공한 횟수"만 세는 것과 같은
/// 규칙이고, 그래서 이런 카드들은 턴 후반에 쓸수록(또는 니킥처럼 앞에 쓸수록) 값이 달라진다.
///
/// ⚠️ <see cref="AttributeEffectType"/>과 마찬가지로 <b>순서를 바꾸지 말 것</b> - 직렬화되는 건
/// 인덱스라서, 중간에 값을 끼워 넣으면 기존 에셋이 조용히 다른 항목을 가리키게 된다.
/// </summary>
public enum TurnScalingSource
{
    /// <summary>스케일링 없음. 기존 카드는 전부 여기다.</summary>
    None,

    /// <summary>이번 턴에 단어 효과로 <b>줄어든</b> 초(퍼펙트). 훅/어퍼컷처럼 시간을 깎았으면
    /// 양수가 되어 값이 오르고, 잽/퀵으로 늘렸으면 음수가 되어 깎인다.
    /// ⚠️ 세는 건 <b>효과로 변한 초</b>뿐이다 - 자연 감소는 포함하지 않는다(그것까지 세면
    /// 늘어나는 경우가 없어져 "늘어난 초마다 감소"라는 규칙 자체가 성립하지 않는다).</summary>
    SecondsSpentThisTurn,

    /// <summary>이번 턴에 완성한 조합(액션)의 수. 춉은 양수로 올리고 니킥은 음수로 깎는다.</summary>
    ActionsThisTurn,

    /// <summary>이번 턴에 쓴 수식어(<see cref="CardCategory.Modifier"/>) 카드의 수(박치기).</summary>
    ModifiersThisTurn,
}

// 표시 문자열은 한국어/영어 한 쌍씩 들고 있고 프로퍼티에서 고른다. CardName 소비처가
// 열다섯 곳인데 전부 프로퍼티를 거치므로, 여기서 고르면 매칭·표시·체인·로그가 한 번에 따라온다.
public abstract class CardBase : ScriptableObject
{
    [Header("한국어")]
    [SerializeField] private string cardName;

    [SerializeField, TextArea] private string description;

    [Tooltip("카드 위쪽에 크게 뜨는 한 줄 요약(위력·상태이상·배수 등). " +
             "표시 칸이 좁고 글자가 크므로 4자 이내로 적을 것 - 넘으면 칸 밖으로 삐져나옵니다. " +
             "어썸처럼 값이 런 도중 변하는 카드는 여기에 {0}을 넣어 포맷으로 씁니다(ModifierCardData 참조).")]
    [SerializeField] private string statsLabel;

    [Header("영어")]
    [Tooltip("영어 모드에서 타이핑할 단어. 소문자로 적을 것 - 입력이 소문자로 정규화되어 들어옵니다. " +
             "공백을 넣지 말 것(띄어쓰기 없이 이어 치는 게 규칙입니다). " +
             "다른 카드 이름의 접두사가 되면 그 카드는 영영 입력할 수 없습니다.")]
    [SerializeField] private string cardNameEn;

    [SerializeField, TextArea] private string descriptionEn;

    [SerializeField] private string statsLabelEn;

    [Header("공통")]
    [SerializeField] private Sprite icon;

    public string CardName => LanguageSettings.Pick(cardName, cardNameEn, this, "cardNameEn");
    public string Description => LanguageSettings.Pick(description, descriptionEn, this, "descriptionEn");
    /// <summary>카드 위쪽 큰 글씨 요약. 값이 런 도중 변하는 카드(어썸)는 하위 클래스가
    /// 재정의해 지금 수치를 끼워 넣는다 - <see cref="ModifierCardData.StatsLabel"/> 참조.</summary>
    public virtual string StatsLabel => LanguageSettings.Pick(statsLabel, statsLabelEn, this, "statsLabelEn");

    public Sprite Icon => icon;

    public abstract CardCategory Category { get; }
}
