using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardSlotView : MonoBehaviour
{
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private int slotIndex;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;

    [Header("타이핑 애니메이션")]
    [Tooltip("입력 중인 문자열이 이 카드 이름의 접두사일 때 떠오르는 높이")]
    [SerializeField] private float typingLiftHeight = 40f;

    [Tooltip("들림 목표치에 도달하는 시정수. HandFanLayout.smoothTime과 같은 방식.")]
    [SerializeField] private float liftSmoothTime = 0.08f;

    [Tooltip("카드가 소모되어 빠져나갈 때 도달하는 높이")]
    [SerializeField] private float exitLiftHeight = 80f;

    [Tooltip("빠져나가는(기존 카드 페이드아웃) 애니메이션 길이(초)")]
    [SerializeField] private float exitDuration = 0.12f;

    [Tooltip("새 카드가 내려와 자리잡는(페이드인) 애니메이션 길이(초)")]
    [SerializeField] private float enterDuration = 0.15f;

    private bool _subscribed;
    private CardBase _currentCard;
    private float _liftAmount;
    private float _swapOffset;
    private bool _isSwapping;
    private Coroutine _swapCoroutine;

    public int SlotIndex => slotIndex;

    /// <summary>카드가 지금 이 프레임에 떠 있어야 할 높이. HandFanLayout이 부채꼴 목표 위치에 더해서 쓴다.</summary>
    public float VerticalOffset => _isSwapping ? _swapOffset : _liftAmount;

    /// <summary>
    /// 런타임에 생성된 카드를 슬롯에 연결합니다. HandFanLayout이 프리팹을 찍어낸 직후 호출합니다.
    /// 인스펙터에서 미리 연결해 둔 카드는 이 메서드를 거치지 않습니다.
    /// </summary>
    public void Bind(CardSlotManager manager, int index, InputManager input)
    {
        Unsubscribe();
        cardSlotManager = manager;
        slotIndex = index;
        inputManager = input;
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

    // 타이핑 중 들림은 매 프레임 부드럽게 접근해야 하므로 이벤트 구독이 아니라 폴링한다 -
    // 어차피 이 감쇠 자체가 여러 프레임에 걸쳐 진행되어야 해서 Update가 필요하고,
    // 그렇다면 CurrentInput을 그 안에서 바로 읽는 쪽이 별도 이벤트 배선보다 단순하다.
    private void Update()
    {
        if (inputManager == null || _isSwapping || _currentCard == null)
            return;

        var committed = inputManager.CurrentInput;
        var composing = inputManager.Composition;
        var hasInput = committed.Length > 0 || composing.Length > 0;

        // CardInputHandler의 매칭 판정과 같은 기준(InputManager.IsValidProgress)을 써야
        // 화면 연출과 실제 매칭이 서로 다른 카드를 가리키는 일이 없다.
        var isCandidate = hasInput && InputManager.IsValidProgress(committed, composing, _currentCard.CardName);

        var target = isCandidate ? typingLiftHeight : 0f;
        var t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(liftSmoothTime, 0.0001f));
        _liftAmount = Mathf.Lerp(_liftAmount, target, t);
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
        if (index != slotIndex)
            return;

        if (_swapCoroutine != null)
            StopCoroutine(_swapCoroutine);

        _swapCoroutine = StartCoroutine(PlaySwap(card));
    }

    // 기존 카드를 밀어올리며 페이드아웃 -> (안 보이는 정점에서) 데이터 교체 -> 새 카드를 내리며 페이드인.
    private IEnumerator PlaySwap(CardBase newCard)
    {
        _isSwapping = true;

        yield return AnimateSwap(0f, exitLiftHeight, 1f, 0f, exitDuration);

        SetCard(newCard);

        yield return AnimateSwap(exitLiftHeight, 0f, 0f, 1f, enterDuration);

        _isSwapping = false;
        _swapCoroutine = null;
    }

    private IEnumerator AnimateSwap(float fromOffset, float toOffset, float fromAlpha, float toAlpha, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            _swapOffset = Mathf.Lerp(fromOffset, toOffset, t);
            SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        _swapOffset = toOffset;
        SetAlpha(toAlpha);
    }

    private void SetAlpha(float alpha)
    {
        nameText.alpha = alpha;

        var color = iconImage.color;
        color.a = alpha;
        iconImage.color = color;
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
        _currentCard = card;
        nameText.text = card != null ? card.CardName : string.Empty;

        // card.Icon이 없으면 프리팹에 미리 박아둔 카드 프레임 스프라이트를 그대로 둔다 -
        // null로 덮어쓰거나 꺼버리지 않는다. 카드 데이터에 아이콘이 아직 없는 게 정상 상태다.
        if (card != null && card.Icon != null)
        {
            iconImage.sprite = card.Icon;
            iconImage.enabled = true;
        }
    }
}
