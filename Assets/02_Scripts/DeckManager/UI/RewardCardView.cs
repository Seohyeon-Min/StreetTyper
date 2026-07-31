using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스테이지 클리어로 얻은 단어 카드를 화면 가운데에 늘어놓는 순수 뷰.
// 카드 개수가 3장(럭키면 4장)으로 바뀌므로 가운데를 기준으로 좌우 대칭이 되게 배치한다 -
// 3장이면 두 번째 카드가, 4장이면 2~3번째 사이가 정확히 중앙에 온다.
public class RewardCardView : MonoBehaviour
{
    [Tooltip("카드 한 장을 표시할 프리팹. 손패의 Card.prefab을 그대로 써도 된다.")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("생성된 카드가 들어갈 부모. 앵커를 화면 중앙으로 둬야 가운데 정렬이 맞는다.")]
    [SerializeField] private RectTransform container;

    [Tooltip("카드와 카드 사이의 빈 공간(px). 카드 폭은 프리팹에서 읽어 자동으로 더하므로, " +
             "여기 100을 넣으면 카드 사이가 실제로 100만큼 벌어진다.")]
    [SerializeField] private float spacing = 100f;

    [Tooltip("카드가 놓이는 높이. 0이면 container 중앙에 일렬로 놓인다.")]
    [SerializeField] private float verticalOffset;

    [SerializeField] private bool logDebugEvents;

    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    /// <summary>얻은 카드를 화면에 펼친다. 목록이 비어 있으면 아무것도 표시하지 않는다.</summary>
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

        for (var i = 0; i < cards.Count; i++)
            Spawn(cards[i], i, cards.Count);

        if (logDebugEvents)
            Debug.Log($"RewardCardView: 보상 카드 {cards.Count}장 표시", this);
    }

    /// <summary>표시를 지운다. 다음 스테이지가 시작될 때 호출한다.</summary>
    public void Clear()
    {
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }

        _spawned.Clear();
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

            // Card.prefab에는 손패 부채꼴용 기울기(약 10도)가 박혀 있다. 손패에서는
            // HandFanLayout이 매 프레임 덮어쓰지만 여기는 그 레이아웃 밖이라 그대로 드러난다.
            rect.localRotation = Quaternion.identity;
        }

        // 손패용 Card.prefab을 재사용하는 경우 슬롯 로직이 같이 딸려온다.
        // 보상 표시는 슬롯과 무관하므로 꺼서 손패 이벤트에 반응하지 않게 한다.
        var slotView = instance.GetComponent<CardSlotView>();
        if (slotView != null)
            slotView.enabled = false;

        var label = instance.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = card != null ? card.CardName : string.Empty;
        else
            Debug.LogWarning("RewardCardView: cardPrefab에 TMP_Text가 없어 단어 이름을 표시할 수 없습니다.", this);

        // 카드 데이터에 아이콘이 있을 때만 덮어쓴다 - 없으면 프리팹의 기본 카드 프레임을 그대로 둔다.
        if (card != null && card.Icon != null)
        {
            var icon = instance.GetComponentInChildren<Image>();
            if (icon != null)
                icon.sprite = card.Icon;
        }

        _spawned.Add(instance);
    }
}
