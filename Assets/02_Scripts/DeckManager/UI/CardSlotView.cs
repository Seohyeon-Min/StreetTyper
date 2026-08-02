using System.Collections;
using UnityEngine;

// 한 슬롯의 동작(슬롯 구독 + 타이핑 들림/교체 애니메이션)만 담당한다.
// 카드를 실제로 그리는 일은 같은 오브젝트의 CardView가 맡는다 - 그쪽은 매니저를 모르므로
// 보상 화면처럼 슬롯이 없는 곳에서도 그대로 쓸 수 있다.
public class CardSlotView : MonoBehaviour
{
    [SerializeField] private CardSlotManager cardSlotManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private int slotIndex;

    [Tooltip("이 카드의 겉모습을 그리는 뷰. 보통 같은 오브젝트에 붙어 있습니다.")]
    [SerializeField] private CardView cardView;

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

    [Header("패배 연출 - 무너짐")]
    [Tooltip("패배 시 카드가 무너지듯 떨어지는 거리(px).")]
    [SerializeField] private float collapseFallDistance = 400f;

    [Tooltip("떨어지며 도는 최대 회전각(도). 카드마다 이 범위 안에서 좌우 무작위로 정해진다.")]
    [SerializeField] private float collapseMaxRotation = 50f;

    [Tooltip("무너져 사라지는 데 걸리는 시간(초).")]
    [SerializeField] private float collapseDuration = 0.5f;

    [Tooltip("점점 가속하며 떨어지는 느낌을 위한 이징. 기본은 EaseIn(초반 느리게, 후반 빠르게).")]
    [SerializeField] private AnimationCurve collapseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));

    private bool _subscribed;
    private CardBase _currentCard;

    // 타이핑 들림 판정에 실제로 비교하는 목표 단어. 보통은 _currentCard.CardName과 같지만,
    // BindStatic으로 전환됐을 때는 카드 데이터 없이 이 값만 따로 갖는다(예: 일시정지 명령 단어).
    private string _liftTargetWord;

    private float _liftAmount;
    private float _swapOffset;
    private float _collapseRotation;
    private bool _isSwapping;
    private Coroutine _swapCoroutine;

    public int SlotIndex => slotIndex;

    /// <summary>카드가 지금 이 프레임에 떠 있어야 할 높이. HandFanLayout이 부채꼴 목표 위치에 더해서 쓴다.</summary>
    public float VerticalOffset => _isSwapping ? _swapOffset : _liftAmount;

    /// <summary>무너지는 동안 부채꼴 각도에 더할 회전(도). 평소엔 0 - HandFanLayout이 계산하는
    /// 각도를 그대로 쓴다. VerticalOffset과 같은 자리(HandFanLayout.LateUpdate)에서 더해진다.</summary>
    public float RotationOffset => _collapseRotation;

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

    /// <summary>CardSlotManager 슬롯 없이, 고정된 단어 하나를 카드처럼 보여준다(예: 일시정지 중
    /// "계속"/"타이틀"). 기존 슬롯 구독은 끊어서 CurrentCards가 바뀌어도 이 카드는 영향받지
    /// 않는다 - 애초에 슬롯을 대표하는 게 아니라 빌려 쓰는 것뿐이다. slotIndex는 건드리지 않고
    /// 그대로 남겨둔다 - 나중에 원래 슬롯으로 복원할 때 필요하다.</summary>
    public void BindStatic(string label, InputManager input)
    {
        Unsubscribe();
        inputManager = input;
        _currentCard = null;
        _liftTargetWord = label;

        if (cardView != null)
            cardView.SetText(label);
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
        if (cardView == null)
            Debug.LogWarning("CardSlotView: cardView가 연결되지 않아 이 슬롯의 카드가 갱신되지 않습니다.", this);

        Refresh();
    }

    // 타이핑 중 들림은 매 프레임 부드럽게 접근해야 하므로 이벤트 구독이 아니라 폴링한다 -
    // 어차피 이 감쇠 자체가 여러 프레임에 걸쳐 진행되어야 해서 Update가 필요하고,
    // 그렇다면 CurrentInput을 그 안에서 바로 읽는 쪽이 별도 이벤트 배선보다 단순하다.
    private void Update()
    {
        if (inputManager == null || _isSwapping || string.IsNullOrEmpty(_liftTargetWord))
            return;

        var committed = inputManager.CurrentInput;
        var composing = inputManager.Composition;
        var hasInput = committed.Length > 0 || composing.Length > 0;

        // CardInputHandler의 매칭 판정과 같은 기준(InputManager.IsValidProgress)을 써야
        // 화면 연출과 실제 매칭이 서로 다른 카드를 가리키는 일이 없다.
        var isCandidate = hasInput && InputManager.IsValidProgress(committed, composing, _liftTargetWord);

        var target = isCandidate ? typingLiftHeight : 0f;

        // unscaledDeltaTime을 쓴다 - 이 카드는 일시정지 중 "계속"/"타이틀" 명령 단어로도
        // 재사용되는데(PauseCommandCardsView.BindStatic), 그때는 Time.timeScale == 0이라
        // 보통의 deltaTime을 쓰면 들림 애니메이션이 아예 멈춰서 안 움직인다. 평소(타임스케일
        // 1)에는 deltaTime과 값이 같으므로 손패 들림 동작은 그대로다.
        var t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(liftSmoothTime, 0.0001f));
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

    /// <summary>지금 내용을 밀어내며(offset 방향) 페이드아웃한 뒤 onComplete를 부른다. 슬롯 교체
    /// 이벤트와 무관하게 밖에서 직접 호출할 수 있다 - 일시정지 메뉴처럼 카드 자리를 통째로
    /// 다른 용도로 바꿀 때, 다른 메뉴에서도 같은 방식으로 재사용하기 위한 범용 API다.
    /// offset을 양수로 주면 위로 올라가며 사라지고, 음수를 주면 아래로 가라앉듯 사라진다.</summary>
    public void PlayExit(float offset, System.Action onComplete = null)
    {
        RestartSwapCoroutine(ExitRoutine(offset, onComplete));
    }

    /// <summary>offset 위치(투명)에서 시작해 제자리(0, 불투명)까지 움직이며 페이드인한다.
    /// setContent가 있으면 애니메이션을 시작하기 전에 먼저 불러서 내용을 채운다(BindStatic이나
    /// Bind 등 무엇이든). 꺼져 있던 오브젝트라도 자동으로 켠다.</summary>
    public void PlayEnter(float fromOffset, System.Action setContent = null)
    {
        gameObject.SetActive(true);
        setContent?.Invoke();
        RestartSwapCoroutine(EnterRoutine(fromOffset));
    }

    /// <summary>패배 시 카드가 무너지듯 회전하며 떨어져 사라진다. delay를 인덱스에 비례해 다르게
    /// 주면(호출부 책임) 카드마다 시차가 생겨 한꺼번에 안 무너지고 와르르 무너지는 느낌이 난다.
    /// PlayExit/PlayEnter와 달리 끝나도 onComplete가 없다 - 이 카드는 다음 RefillAll이 새 내용을
    /// 채우며 PlaySwap으로 원래 자리로 되돌려 놓을 때까지 그냥 떨어진 채로 남아 있으면 된다.</summary>
    public void PlayCollapse(float delay)
    {
        RestartSwapCoroutine(CollapseRoutine(delay));
    }

    private IEnumerator CollapseRoutine(float delay)
    {
        _isSwapping = true;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 카드마다 좌우 무작위 회전을 줘야 한 방향으로 가지런히 쓰러지지 않고 흩어지는 느낌이 난다.
        var rotationTarget = Random.Range(-collapseMaxRotation, collapseMaxRotation);

        var elapsed = 0f;
        while (elapsed < collapseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = collapseCurve.Evaluate(Mathf.Clamp01(elapsed / collapseDuration));
            _swapOffset = Mathf.Lerp(0f, -collapseFallDistance, t);
            _collapseRotation = Mathf.Lerp(0f, rotationTarget, t);
            SetAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        _swapOffset = -collapseFallDistance;
        _collapseRotation = rotationTarget;
        SetAlpha(0f);
        _swapCoroutine = null;
        // _isSwapping은 켜 둔 채로 남긴다 - 꺼버리면 Update()의 타이핑 들림 로직이 다시 돌아
        // 이미 떨어져 안 보이는 카드를 다시 위로 들어올리려 든다. 다음 PlaySwap(RefillAll)이
        // _isSwapping을 다시 관리하며 정상 상태로 되돌린다.
    }

    private void RestartSwapCoroutine(IEnumerator routine)
    {
        if (_swapCoroutine != null)
            StopCoroutine(_swapCoroutine);

        _swapCoroutine = StartCoroutine(routine);
    }

    private IEnumerator ExitRoutine(float offset, System.Action onComplete)
    {
        _isSwapping = true;
        yield return AnimateSwap(0f, offset, 1f, 0f, exitDuration);
        onComplete?.Invoke();
        _isSwapping = false;
        _swapCoroutine = null;
    }

    private IEnumerator EnterRoutine(float fromOffset)
    {
        _isSwapping = true;
        yield return AnimateSwap(fromOffset, 0f, 0f, 1f, enterDuration);
        _isSwapping = false;
        _swapCoroutine = null;
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
        // unscaledDeltaTime을 쓴다 - PlayExit/PlayEnter는 일시정지 전환(Time.timeScale이 1에서
        // 0으로 바뀌는 도중)에도 쓰이는데, 보통의 deltaTime을 쓰면 애니메이션이 재생되다가
        // timeScale이 0이 되는 순간 멈춰버린다. 평소(타임스케일 1)에는 값이 같으므로 실제
        // 게임플레이 카드 교체 애니메이션(PlaySwap)에는 영향이 없다.
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
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
        if (cardView != null)
            cardView.SetAlpha(alpha);
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

    // _currentCard/_liftTargetWord는 타이핑 들림 판정(Update)에 계속 필요하므로 여기서 들고 있는다.
    // 그리는 일 자체는 CardView가 한다.
    private void SetCard(CardBase card)
    {
        _currentCard = card;
        _liftTargetWord = card != null ? card.CardName : null;

        // 무너짐 연출이 남긴 회전을 되돌린다 - 안 그러면 재시작 후 새로 뽑힌 카드가
        // 기울어진 채로 시작한다(HandFanLayout이 매 프레임 RotationOffset을 더하기 때문).
        _collapseRotation = 0f;

        if (cardView != null)
            cardView.SetCard(card);
    }
}
