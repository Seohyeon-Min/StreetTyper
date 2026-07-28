using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardSlotView : MonoBehaviour
{
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private int slotIndex;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;

    private bool _subscribed;

    public int SlotIndex => slotIndex;

    /// <summary>
    /// 런타임에 생성된 카드를 슬롯에 연결합니다. HandFanLayout이 프리팹을 찍어낸 직후 호출합니다.
    /// 인스펙터에서 미리 연결해 둔 카드는 이 메서드를 거치지 않습니다.
    /// </summary>
    public void Bind(CardSlotManager manager, int index)
    {
        Unsubscribe();
        cardSlotManager = manager;
        slotIndex = index;
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        Refresh();
    }

    // Instantiate 직후의 OnEnable은 Bind보다 먼저 실행되므로 이 시점엔 매니저가 없을 수 있습니다.
    // 그 경우 구독은 Bind가 대신 처리합니다.
    private void Subscribe()
    {
        if (_subscribed || cardSlotManager == null || !isActiveAndEnabled)
            return;

        cardSlotManager.OnSlotChanged += HandleSlotChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || cardSlotManager == null)
            return;

        cardSlotManager.OnSlotChanged -= HandleSlotChanged;
        _subscribed = false;
    }

    private void HandleSlotChanged(int index, CardBase card)
    {
        if (index == slotIndex)
            SetCard(card);
    }

    private void Refresh()
    {
        if (cardSlotManager == null)
            return;

        var cards = cardSlotManager.CurrentCards;
        if (cards == null || slotIndex < 0 || slotIndex >= cards.Count)
            return;

        SetCard(cards[slotIndex]);
    }

    private void SetCard(CardBase card)
    {
        nameText.text = card != null ? card.CardName : string.Empty;
        iconImage.sprite = card != null ? card.Icon : null;
        iconImage.enabled = iconImage.sprite != null;
    }
}
