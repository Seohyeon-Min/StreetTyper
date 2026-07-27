using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardSlotView : MonoBehaviour
{
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private int slotIndex;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;

    private void OnEnable()
    {
        cardSlotManager.OnSlotChanged += HandleSlotChanged;
    }

    private void OnDisable()
    {
        cardSlotManager.OnSlotChanged -= HandleSlotChanged;
    }

    private void Start()
    {
        SetCard(cardSlotManager.CurrentCards[slotIndex]);
    }

    private void HandleSlotChanged(int index, CardBase card)
    {
        if (index == slotIndex)
            SetCard(card);
    }

    private void SetCard(CardBase card)
    {
        nameText.text = card.CardName;
        iconImage.sprite = card.Icon;
        iconImage.enabled = card.Icon != null;
    }
}
