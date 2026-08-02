using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스테이지 클리어 보상 후보를 화면 가운데에 늘어놓는 순수 뷰.
// 카드 개수를 가운데 기준 좌우 대칭으로 배치한다 - 3장이면 두 번째 카드가 정확히 중앙에 온다.
//
// 게임 판단은 하지 않는다. "지금 어느 카드를 치고 있는가"는 RewardInputHandler가 정해서
// SetTypingCandidate로 알려주고, 여기는 그걸 화면에 비추기만 한다.
public class RewardCardView : MonoBehaviour
{
    [Tooltip("카드 한 장을 표시할 프리팹. 손패의 Card.prefab을 그대로 써도 된다.")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("생성된 카드가 들어갈 부모. 앵커를 화면 중앙으로 둬야 가운데 정렬이 맞는다.")]
    [SerializeField] private RectTransform container;

    [Tooltip("평소엔 꺼두고 보상을 보여줄 때만 켤 패널(배경/타이틀 등). 비워두면 이 부분은 건너뛴다 - 패널을 따로 안 쓰는 구성도 지원하기 위해서다.")]
    [SerializeField] private GameObject panel;

    [Tooltip("카드와 카드 사이의 빈 공간(px). 카드 폭은 프리팹에서 읽어 자동으로 더하므로, " +
             "여기 100을 넣으면 카드 사이가 실제로 100만큼 벌어진다.")]
    [SerializeField] private float spacing = 100f;

    [Tooltip("카드가 놓이는 높이. 0이면 container 중앙에 일렬로 놓인다.")]
    [SerializeField] private float verticalOffset;

    [Header("보상 화면 표시")]
    [Tooltip("보상 패널 제목(한/영)과 이미지. 결과 화면과 같은 구조를 쓴다.")]
    [SerializeField] private ScreenPresentation presentation = new ScreenPresentation("카드 획득", "CHOOSE A CARD");

    [Tooltip("제목을 그릴 라벨. 비워두면 제목 표시를 건너뛴다.")]
    [SerializeField] private TMP_Text titleLabel;

    [Tooltip("보상 화면에 띄울 이미지. 스프라이트를 안 넣었으면 오브젝트째 꺼진다.")]
    [SerializeField] private Image titleImage;

    [Header("타이핑 피드백")]
    [Tooltip("타이핑 중인 카드가 떠오르는 높이(px). 손패(CardSlotView)와 같은 값을 기본으로 둔다.")]
    [SerializeField] private float typingLiftHeight = 40f;

    [SerializeField] private float liftSmoothTime = 0.08f;

    [Tooltip("후보가 아닌 카드의 투명도. 아무 후보에도 안 맞는 글자를 치면 전부 이 값이 된다.")]
    [SerializeField] private float dimAlpha = 0.4f;

    [SerializeField] private bool logDebugEvents;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // 매 프레임 GetComponent를 부르지 않도록 스폰할 때 같이 모아둔다. _spawned와 인덱스가 같다.
    private readonly List<RectTransform> _rects = new List<RectTransform>();
    private readonly List<CardView> _views = new List<CardView>();
    private readonly List<float> _lifts = new List<float>();

    // 지금 타이핑 중인 카드. -1이면 후보 없음.
    private int _highlight = -1;

    // 입력창에 글자가 있는지. 후보가 없을 때 "아직 안 쳤다"와 "쳤는데 안 맞는다"를 갈라야 한다.
    private bool _hasInput;

    // 프리팹의 가로 크기. 배치 간격은 이 값 + spacing이라, 프리팹 크기를 바꿔도
    // 카드 사이 여백은 인스펙터에 적은 그대로 유지된다.
    private float _cardWidth;

    private void Awake()
    {
        CacheCardWidth();

        // 시작할 때는 아무것도 보이지 않아야 한다.
        Clear();
    }

    private void CacheCardWidth()
    {
        if (cardPrefab == null)
            return;

        var rect = cardPrefab.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogWarning("RewardCardView: cardPrefab에 RectTransform이 없어 카드 폭을 알 수 없습니다. " +
                             "간격이 spacing 값만으로 계산됩니다.", this);
            return;
        }

        // 프리팹에 스케일이 걸려 있으면 화면에서 보이는 폭도 그만큼 달라진다.
        _cardWidth = rect.sizeDelta.x * Mathf.Abs(rect.localScale.x);
    }

    /// <summary>후보 카드를 화면에 펼친다. 목록이 비어 있으면 아무것도 표시하지 않는다.</summary>
    public void Show(IReadOnlyList<CardBase> cards)
    {
        Clear();

        if (cards == null || cards.Count == 0)
            return;

        if (cardPrefab == null || container == null)
        {
            Debug.LogWarning("RewardCardView: cardPrefab 또는 container가 연결되지 않아 보상 카드를 표시할 수 없습니다.", this);
            return;
        }

        if (panel != null)
            panel.SetActive(true);

        ApplyPresentation();

        for (var i = 0; i < cards.Count; i++)
            Spawn(cards[i], i, cards.Count);

        if (logDebugEvents)
            Debug.Log($"RewardCardView: 보상 후보 {cards.Count}장 표시", this);
    }

    /// <summary>표시를 지운다. 선택이 끝났을 때와 다음 스테이지가 시작될 때 호출한다.</summary>
    public void Clear()
    {
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }

        _spawned.Clear();
        _rects.Clear();
        _views.Clear();
        _lifts.Clear();

        _highlight = -1;
        _hasInput = false;

        if (titleImage != null)
            titleImage.gameObject.SetActive(false);

        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>지금 타이핑 중인 카드를 알려준다. index가 -1이면 어느 후보도 아니고,
    /// hasInput은 입력창에 글자가 들어 있는지다 - 둘을 갈라야 "아직 안 쳤다"와
    /// "쳤는데 아무것도 안 맞는다"를 다르게 보여줄 수 있다.</summary>
    public void SetTypingCandidate(int index, bool hasInput)
    {
        _highlight = index;
        _hasInput = hasInput;
    }

    // 제목과 이미지는 인스펙터 값에서 나온다. 화면에 나가는 글자를 코드에 박지 않는 게 기본 사양이다.
    private void ApplyPresentation()
    {
        if (titleLabel != null)
        {
            titleLabel.text = presentation.Title(this, nameof(presentation));
            titleLabel.color = presentation.TitleColor;
        }

        if (titleImage == null)
            return;

        // ⚠️ 스프라이트가 없을 때 sprite = null로 두면 사라지는 게 아니라 흰 사각형이 그려진다.
        // 오브젝트째 꺼야 한다(CardView가 배지를 다루는 방식과 같다).
        if (presentation.Image == null)
        {
            titleImage.gameObject.SetActive(false);
            return;
        }

        titleImage.sprite = presentation.Image;
        titleImage.gameObject.SetActive(true);
    }

    // 들림과 흐리게를 매 프레임 목표값으로 부드럽게 따라가게 한다.
    // ⚠️ Time.deltaTime을 쓴다 - 일시정지가 timeScale = 0 하나로 성립하는 전제를 지키기 위해서다.
    private void Update()
    {
        if (_spawned.Count == 0)
            return;

        var t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(liftSmoothTime, 0.0001f));

        for (var i = 0; i < _spawned.Count; i++)
        {
            var isCandidate = i == _highlight;

            var targetLift = isCandidate ? typingLiftHeight : 0f;
            _lifts[i] = Mathf.Lerp(_lifts[i], targetLift, t);

            if (_rects[i] != null)
            {
                var pos = _rects[i].anchoredPosition;
                _rects[i].anchoredPosition = new Vector2(pos.x, verticalOffset + _lifts[i]);
            }

            if (_views[i] == null)
                continue;

            // 아무것도 안 쳤으면 전부 선명하게, 치고 있으면 후보만 선명하게.
            // 글자는 있는데 후보가 하나도 없으면(오타) 전부 흐려져 "이걸로는 아무것도 못 얻는다"를 알린다.
            var opaque = !_hasInput || isCandidate;
            _views[i].SetAlpha(opaque ? 1f : dimAlpha);
        }
    }

    private void Spawn(CardBase card, int index, int total)
    {
        var instance = Instantiate(cardPrefab, container);

        var rect = instance.GetComponent<RectTransform>();
        if (rect != null)
        {
            // 카드 중심 사이의 거리 = 카드 폭 + 여백. 폭을 더하지 않으면 spacing이
            // 카드 폭보다 작을 때 서로 겹친다(Card.prefab이 100이라 spacing 100이면 딱 붙는다).
            var step = _cardWidth + spacing;

            // 가운데를 0으로 두고 좌우로 벌린다. (total - 1) * 0.5가 중심 인덱스이므로
            // 홀수 장이면 가운데 카드가 정확히 x = 0에 놓인다.
            var x = (index - (total - 1) * 0.5f) * step;
            rect.anchoredPosition = new Vector2(x, verticalOffset);

            // 손패에서는 HandFanLayout이 부채꼴로 카드를 기울인다. 여기는 그 레이아웃 밖이라
            // 기울기 없이 반듯하게 세운다. Card.prefab 루트 자체의 회전은 지금 항등이라
            // 이 줄은 사실상 방어용이다(기울기는 NameText 자식에 7도가 따로 박혀 있고, 그건 아트 의도다).
            rect.localRotation = Quaternion.identity;
        }

        // 손패용 Card.prefab을 재사용하는 경우 슬롯 로직이 같이 딸려온다.
        // 보상 표시는 슬롯과 무관하므로 꺼서 손패 이벤트에 반응하지 않게 한다.
        // (그리기는 CardView가 따로 하므로 꺼도 카드 내용은 정상적으로 나온다.)
        var slotView = instance.GetComponent<CardSlotView>();
        if (slotView != null)
            slotView.enabled = false;

        // 손패와 똑같은 뷰를 그대로 쓴다 - 이름·설명·프레임·배지 규칙이 한 곳에만 있어야
        // 보상 카드와 손패가 서로 다르게 보이는 일이 없다.
        var cardView = instance.GetComponent<CardView>();
        if (cardView != null)
            cardView.SetCard(card);
        else
            Debug.LogWarning("RewardCardView: cardPrefab에 CardView가 없어 보상 카드를 그릴 수 없습니다.", this);

        _spawned.Add(instance);
        _rects.Add(rect);
        _views.Add(cardView);
        _lifts.Add(0f);
    }
}
