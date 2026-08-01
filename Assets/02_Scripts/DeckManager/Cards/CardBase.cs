using UnityEngine;

public enum CardCategory
{
    Modifier,
    Time,
    Type,
    Action
}

// 표시 문자열은 한국어/영어 한 쌍씩 들고 있고 프로퍼티에서 고른다. CardName 소비처가
// 열다섯 곳인데 전부 프로퍼티를 거치므로, 여기서 고르면 매칭·표시·체인·로그가 한 번에 따라온다.
public abstract class CardBase : ScriptableObject
{
    [Header("한국어")]
    [SerializeField] private string cardName;

    [SerializeField, TextArea] private string description;

    [Tooltip("카드 위쪽에 크게 뜨는 한 줄 요약(위력·상태이상·배수 등). " +
             "표시 칸이 좁고 글자가 크므로 4자 이내로 적을 것 - 넘으면 칸 밖으로 삐져나옵니다.")]
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
    public string StatsLabel => LanguageSettings.Pick(statsLabel, statsLabelEn, this, "statsLabelEn");

    public Sprite Icon => icon;

    public abstract CardCategory Category { get; }
}
