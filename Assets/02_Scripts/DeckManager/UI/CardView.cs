using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 카드 한 장의 "겉모습"만 담당하는 순수 뷰 - 이름·설명·카테고리 프레임·효과 배지.
// 매니저 참조가 하나도 없는 게 핵심이다. 손패(CardSlotView)와 보상 화면(RewardCardView)이
// 같은 Card.prefab을 쓰는데, 보상 카드는 슬롯도 타이핑도 없어서 예전엔 CardSlotView를
// 통째로 꺼두고 이름/아이콘을 따로 다시 그려야 했다. 그리기를 여기로 떼어내면 양쪽이
// SetCard 하나만 부르면 된다.
//
// 페이드는 요소별 알파가 아니라 CanvasGroup 하나로 처리한다. 예전엔 CardSlotView가
// 이름과 아이콘의 알파만 따로 만졌는데, 그래서 나중에 붙은 Description이 교체 애니메이션
// 중에 혼자 안 사라졌다. 표시 요소가 늘어도 코드를 고칠 필요가 없는 쪽을 택한다.
[RequireComponent(typeof(CanvasGroup))]
public class CardView : MonoBehaviour
{
    [Header("표시 요소")]
    [Tooltip("카드 배경 프레임. 카테고리에 따라 스프라이트가 바뀝니다.")]
    [SerializeField] private Image frameImage;

    [Tooltip("프레임 위에 겹쳐 뜨는 효과 배지.")]
    [SerializeField] private Image badgeImage;

    [SerializeField] private TMP_Text nameText;

    [Tooltip("CardBase.Description을 그대로 표시합니다. 한글이 들어가므로 폰트는 Paperlogy여야 합니다.")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("CardBase.StatsLabel을 표시하는 칸(위력 +3, 화상 같은 한 줄 요약). " +
             "글자가 커서 4자를 넘으면 칸 밖으로 나갑니다.")]
    [SerializeField] private TMP_Text statsText;

    [Tooltip("교체 애니메이션의 페이드를 이 하나로 처리합니다. 요소별 알파를 따로 만지지 않습니다. " +
             "RequireComponent로 자동 추가되므로 같은 오브젝트의 것을 넣으면 됩니다.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("카테고리별 프레임")]
    [Tooltip("액션 카드(조합을 완성하는 단어)용 프레임.")]
    [SerializeField] private Sprite actionFrame;

    [Tooltip("액션이 아닌 카드(모디파이어/타임/타입) 전부가 쓰는 프레임.")]
    [SerializeField] private Sprite defaultFrame;

    [Tooltip("명령 카드(넘기기/지우기/계속/카드/타이틀)용 프레임. 조합에 쓰는 카드가 아니라는 게 " +
             "한눈에 보이도록 다른 색을 씁니다. 비워두면 defaultFrame으로 대신합니다.")]
    [SerializeField] private Sprite commandFrame;

    [Header("효과 배지")]
    [Tooltip("액션 카드에 붙는 배지.")]
    [SerializeField] private Sprite actionBadge;

    [Tooltip("타격 횟수를 늘리는 카드(더블/트리플)에 붙는 배지.")]
    [SerializeField] private Sprite multiplyBadge;

    [Tooltip("위 둘에 해당하지 않는 나머지 카드 전부가 쓰는 배지.")]
    [SerializeField] private Sprite upBadge;

    // 지금 그리고 있는 카드. 어썸처럼 수치가 런 도중 변하는 카드를 다시 써야 해서 들고 있는다.
    // 빈 슬롯에서는 null이라, 수치 갱신(ApplyStatsLabel)이 그 칸을 건드리지 않는다.
    private CardBase _card;

    // Card.prefab에 박혀 있는 원래 프레임. 빈 슬롯이 되면 여기로 되돌린다 - 안 그러면
    // 직전 카드의 카테고리 프레임(분홍/회색)이 그대로 남아 "이전 카드가 아직 있는" 것처럼 보인다.
    // Awake에서 한 번만 읽는 게 중요하다. 나중에 읽으면 이미 갈아끼운 프레임이 "원래 것"으로 굳는다.
    private Sprite _prefabFrame;
    private bool _prefabFrameCaptured;

    private void Awake()
    {
        CapturePrefabFrame();
        WarnIfUnassigned();
    }

    private void CapturePrefabFrame()
    {
        if (_prefabFrameCaptured || frameImage == null)
            return;

        _prefabFrame = frameImage.sprite;
        _prefabFrameCaptured = true;
    }

    // 매니저 참조가 아니라 static 이벤트라 이 뷰의 "매니저를 모른다"는 성질은 그대로다
    // (LanguageSettings.OnChanged와 같은 결).
    private void OnEnable()
    {
        SkillResolver.OnCardValuesChanged += ApplyStatsLabel;
    }

    private void OnDisable()
    {
        SkillResolver.OnCardValuesChanged -= ApplyStatsLabel;
    }

    /// <summary>카드 데이터를 화면에 반영합니다. card가 null이면 <see cref="Clear"/>와 같습니다.</summary>
    public void SetCard(CardBase card)
    {
        if (card == null)
        {
            Clear();
            return;
        }

        _card = card;

        if (nameText != null)
            nameText.text = card.CardName;

        if (descriptionText != null)
            descriptionText.text = card.Description;

        ApplyStatsLabel();

        // 배지는 스프라이트를 null로 지우지 않고 Image를 끈다. 스프라이트가 없는 Image는
        // 사라지는 게 아니라 흰 사각형으로 그려지기 때문이다.
        var badge = BadgeFor(card);
        if (badgeImage != null)
        {
            badgeImage.enabled = badge != null;
            if (badge != null)
                badgeImage.sprite = badge;
        }

        // 프레임 스프라이트가 비어 있으면 프리팹에 박아둔 것을 그대로 둔다.
        // null로 덮어쓰면 Play 시작과 동시에 카드 프레임이 사라진다(예전에 실제로 났던 버그다).
        var frame = FrameFor(card);
        if (frameImage != null && frame != null)
            frameImage.sprite = frame;
    }

    /// <summary>빈 카드로 되돌린다 - <b>Card.prefab의 기본 카드 이미지만 남고 글자와 배지는 전부
    /// 사라진다.</b> 스테이지가 바뀌어 손패를 비울 때(<c>CardSlotManager.EmptyAllSlots</c>) 쓴다.
    ///
    /// ⚠️ <b>프레임을 프리팹 원본으로 되돌리는 게 핵심이다.</b> 예전에는 "프레임은 빈 슬롯에서도
    /// 남겨둔다"며 그냥 뒀는데, 그러면 직전 카드의 카테고리 프레임(분홍 액션/회색 명령)이 그대로
    /// 남아 <b>이전 카드가 아직 손에 있는 것처럼 보인다.</b>
    ///
    /// 프레임 자체를 끄지는 않는다 - 카드 자리가 통째로 사라지면 부채꼴 배치가 흔들린다.</summary>
    public void Clear()
    {
        _card = null;

        if (nameText != null)
            nameText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = string.Empty;

        if (statsText != null)
            statsText.text = string.Empty;

        if (badgeImage != null)
            badgeImage.enabled = false;

        // Awake보다 먼저 불릴 수 있다(런타임 생성 직후 Bind -> Refresh 경로). 그때도 원본을 잡는다.
        CapturePrefabFrame();

        if (frameImage != null && _prefabFrame != null)
            frameImage.sprite = _prefabFrame;
    }

    // 예전에는 SetText(문자열)로 명령 단어를 카드처럼 그렸다. 지금은 명령 단어도 CommandCardData
    // 에셋이라 SetCard 하나로 그린다 - 손패·보상·일시정지가 같은 규칙으로 그려지고, 프레임과
    // 문구를 인스펙터에서 바꿀 수 있다. 그 메서드로 되돌리지 말 것.

    // 수치 칸만 지금 값으로 다시 쓴다. 어썸처럼 런 도중 값이 변하는 카드 때문에 필요하다 -
    // 손패에 어썸이 두 장 떠 있으면(슬롯 간 중복은 의도된 동작) 쓰지 않은 쪽도 같이 갱신되어야 한다.
    private void ApplyStatsLabel()
    {
        if (statsText != null)
            statsText.text = _card != null ? _card.StatsLabel : string.Empty;
    }

    /// <summary>카드 전체의 투명도. CardSlotView의 교체 애니메이션이 부릅니다.</summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    private Sprite FrameFor(CardBase card)
    {
        switch (card.Category)
        {
            case CardCategory.Action:
                return actionFrame;

            // 명령 카드 프레임이 비어 있으면 기본 프레임으로 떨어진다 - null을 돌려주면
            // SetCard가 프레임을 그대로 두므로 직전 카드의 프레임이 남는다.
            case CardCategory.Command:
                return commandFrame != null ? commandFrame : defaultFrame;

            default:
                return defaultFrame;
        }
    }

    // 배지는 "무슨 효과인가"의 표현이라 조합 규칙상의 분류(Category)와 기준이 다르다.
    // 더블/트리플을 Category.Time으로 보지 않고 RepeatAction을 직접 보는 이유가 그것이다 -
    // 지금은 결과가 같지만, Time으로 분류되는 건 AttributeCardData.Category의 구현 세부사항이다.
    private Sprite BadgeFor(CardBase card)
    {
        // 명령 카드는 "무슨 효과인가"가 없으므로 배지를 달지 않는다. null을 돌려주면
        // SetCard가 Image를 꺼준다(스프라이트만 지우면 흰 사각형이 남는다).
        if (card.Category == CardCategory.Command)
            return null;

        if (card is AttributeCardData attribute && attribute.EffectType == AttributeEffectType.RepeatAction)
            return multiplyBadge;

        if (card.Category == CardCategory.Action)
            return actionBadge;

        return upBadge;
    }

    // 인스펙터 연결이 빠지면 조용히 아무 일도 안 일어나는 대신 어느 칸이 비었는지 알려준다.
    private void WarnIfUnassigned()
    {
        if (frameImage == null)
            Debug.LogWarning("CardView: frameImage가 연결되지 않아 카테고리별 프레임이 바뀌지 않습니다.", this);

        if (badgeImage == null)
            Debug.LogWarning("CardView: badgeImage가 연결되지 않아 효과 배지가 표시되지 않습니다.", this);

        if (nameText == null)
            Debug.LogWarning("CardView: nameText가 연결되지 않아 단어 이름이 표시되지 않습니다.", this);

        if (descriptionText == null)
            Debug.LogWarning("CardView: descriptionText가 연결되지 않아 카드 설명이 표시되지 않습니다.", this);

        if (statsText == null)
            Debug.LogWarning("CardView: statsText가 연결되지 않아 수치 요약이 표시되지 않습니다.", this);

        if (canvasGroup == null)
            Debug.LogWarning("CardView: canvasGroup이 연결되지 않아 카드 교체 페이드가 동작하지 않습니다.", this);

        if (actionFrame == null || defaultFrame == null)
            Debug.LogWarning("CardView: actionFrame/defaultFrame이 비어 있어 해당 카테고리의 프레임이 사라집니다.", this);

        if (actionBadge == null || multiplyBadge == null || upBadge == null)
            Debug.LogWarning("CardView: 효과 배지 스프라이트(actionBadge/multiplyBadge/upBadge) 중 비어 있는 칸이 있습니다.", this);
    }
}
