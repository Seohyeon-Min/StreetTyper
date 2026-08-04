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

    [Header("전체 클리어 연출 - 가라앉기")]
    [Tooltip("전체 클리어 시 손패가 아래로 내려가는 거리(px). 무너짐과 달리 회전도 시차도 없다.")]
    [SerializeField] private float sinkDistance = 300f;

    [Tooltip("가라앉는 데 걸리는 시간(초). 무너짐보다 느긋하게 두면 \"끝났다\"는 느낌이 난다.")]
    [SerializeField] private float sinkDuration = 0.6f;

    [Tooltip("가라앉는 이징. 기본은 EaseOut(처음 빠르고 끝에서 부드럽게 멎는다) - 무너짐의 " +
             "EaseIn(가속해서 떨어진다)과 반대라 두 연출이 확실히 다르게 보인다.")]
    [SerializeField] private AnimationCurve sinkCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    private bool _subscribed;
    private CardBase _currentCard;

    // 타이핑 들림 판정에 실제로 비교하는 목표 단어. 보통은 _currentCard.CardName과 같지만,
    // BindStatic으로 전환됐을 때는 카드 데이터 없이 이 값만 따로 갖는다(예: 일시정지 명령 단어).
    private string _liftTargetWord;

    private float _liftAmount;
    private float _swapOffset;
    private float _collapseRotation;
    private bool _isSwapping;
    private bool _isLeaving;
    private Coroutine _swapCoroutine;

    public int SlotIndex => slotIndex;

    /// <summary>이 카드가 자리에서 <b>물러나는 중</b>인가 - 패배 무너짐(<see cref="PlayCollapse"/>,
    /// 자기 차례를 기다리는 stagger 구간 포함)과 가라앉기(<see cref="PlayExit"/>) 둘 다 해당한다.
    /// 손패가 다 치워진 뒤에 무언가를 이어 붙이려는 쪽이 본다 - 결과 화면 명령 카드가 손패가
    /// 전부 사라진 다음에 떠오르는 게 그것이다(<see cref="HandFanLayout.IsLeaving"/>).
    ///
    /// 두 연출을 한 값으로 묶은 이유: 패배는 무너뜨리고 전체 클리어는 가라앉히는데, 기다리는
    /// 쪽(ResultInputHandler)은 <b>어느 쪽인지 알 필요가 없다</b> - "치워졌는가"만 알면 된다.
    ///
    /// ⚠️ <see cref="_isSwapping"/>과 다르다. 그쪽은 무너짐이 끝나도 <b>일부러 켜둔 채</b> 남고
    /// (Update의 들림 로직이 이미 떨어진 카드를 다시 들어올리지 않게) 평범한 슬롯 교체에도 켜진다.
    /// 그 값으로는 "물러나는 중인가"도 "끝났는가"도 알 수 없어서 플래그를 따로 둔다.</summary>
    public bool IsLeaving => _isLeaving;

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

    /// <summary>CardSlotManager 슬롯 없이, 명령 카드 하나를 보여준다(예: 일시정지 중
    /// "계속"/"타이틀"). 기존 슬롯 구독은 끊어서 CurrentCards가 바뀌어도 이 카드는 영향받지
    /// 않는다 - 애초에 슬롯을 대표하는 게 아니라 빌려 쓰는 것뿐이다. slotIndex는 건드리지 않고
    /// 그대로 남겨둔다 - 나중에 원래 슬롯으로 복원할 때 필요하다.</summary>
    public void BindStatic(CardBase card, InputManager input)
    {
        Unsubscribe();
        inputManager = input;

        // ⚠️ _currentCard는 비워 둔다. 이건 "이 슬롯이 대표하는 손패 카드"라서, 빌려 쓰는 명령
        // 카드를 넣으면 슬롯 로직이 진짜 손패 카드로 오인한다. 타이핑 들림 판정이 실제로 비교하는
        // 건 아래 _liftTargetWord 쪽이라 이렇게 갈라둬도 동작에는 문제가 없다.
        _currentCard = null;
        _liftTargetWord = card != null ? card.CardName : null;

        if (cardView != null)
            cardView.SetCard(card);
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

        // ⚠️ 입력을 다른 화면이 가져갔으면 이 카드는 반응하지 않는다. 일시정지·결과 화면이
        // 열리면 손패 단어는 대상 목록에서 빠지므로 여기가 false가 되고, 반대로 그 화면의
        // 명령 카드는 자기 단어가 목록에 들어와 평소처럼 떠오른다 - 카드가 자기 주인을 알
        // 필요 없이 "지금 내 단어가 노려지고 있는가"만 물으면 된다.
        //
        // 이게 없으면 일시정지 중 "계속"의 "ㄱ"에 손패의 "가드"가 같이 떠오르고, 보상 화면에서
        // 후보 카드 이름을 칠 때 뒤에 남아 있는 손패까지 덩달아 움직인다.
        var isMine = inputManager.IsTypingTarget(_liftTargetWord);

        // CardInputHandler의 매칭 판정과 같은 기준(InputManager.IsValidProgress)을 써야
        // 화면 연출과 실제 매칭이 서로 다른 카드를 가리키는 일이 없다.
        var isCandidate = isMine && hasInput &&
                          InputManager.IsValidProgress(committed, composing, _liftTargetWord);

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
    /// Bind 등 무엇이든). 꺼져 있던 오브젝트라도 자동으로 켠다. duration을 비워두면 인스펙터의
    /// enterDuration을 쓴다 - 어떤 화면은 더 천천히 떠오르게 하고 싶을 때만 넘긴다(예: 결과
    /// 화면의 "다시하기"/"타이틀로").</summary>
    public void PlayEnter(float fromOffset, System.Action setContent = null, float? duration = null)
    {
        gameObject.SetActive(true);
        setContent?.Invoke();
        RestartSwapCoroutine(EnterRoutine(fromOffset, duration ?? enterDuration));
    }

    /// <summary>패배 시 카드가 무너지듯 회전하며 떨어져 사라진다. delay를 인덱스에 비례해 다르게
    /// 주면(호출부 책임) 카드마다 시차가 생겨 한꺼번에 안 무너지고 와르르 무너지는 느낌이 난다.
    /// PlayExit/PlayEnter와 달리 끝나도 onComplete가 없다 - 이 카드는 다음 RefillAll이 새 내용을
    /// 채우며 PlaySwap으로 원래 자리로 되돌려 놓을 때까지 그냥 떨어진 채로 남아 있으면 된다.</summary>
    public void PlayCollapse(float delay)
    {
        RestartSwapCoroutine(CollapseRoutine(delay));
    }

    /// <summary>전체 클리어 시 손패를 아래로 가라앉힌다. 무너짐(<see cref="PlayCollapse"/>)과 달리
    /// 회전도 시차도 없이 그냥 내려간다 - "졌다"가 아니라 "다 끝냈다"라 요란할 이유가 없다.
    ///
    /// ⚠️ <see cref="PlayExit"/>가 아니라 <see cref="PlayCollapse"/> 쪽 방식이다. PlayExit은 끝나면
    /// <c>_isSwapping</c>을 내려서 카드가 원래 부채꼴 자리로 <b>도로 튀어오르고</b>, 그래서 부르는
    /// 쪽이 onComplete에서 오브젝트를 꺼야 한다(일시정지가 그렇게 쓴다). 그런데 여기서 끄면
    /// OnSlotChanged 구독이 끊겨 <b>재시작 후 RefillAll이 이 카드를 되살리지 못한다</b> - 결과
    /// 화면에서 "다시하기"로 돌아온 판에 손패가 영영 안 보이게 된다. 내려간 자리에 그대로 두면
    /// 다음 PlaySwap(RefillAll)이 알아서 정상 상태로 되돌린다.</summary>
    public void PlaySink()
    {
        RestartSwapCoroutine(SinkRoutine());
    }

    private IEnumerator SinkRoutine()
    {
        _isSwapping = true;
        _isLeaving = true;

        var elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = sinkCurve.Evaluate(Mathf.Clamp01(elapsed / sinkDuration));
            _swapOffset = Mathf.Lerp(0f, -sinkDistance, t);
            SetAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        _swapOffset = -sinkDistance;
        SetAlpha(0f);
        _swapCoroutine = null;
        _isLeaving = false;

        // _isSwapping은 켜 둔 채로 남긴다 - 끄면 VerticalOffset이 _liftAmount(0)로 돌아가
        // 이미 내려가 안 보이는 카드가 원래 자리로 튀어오른다(PlayCollapse와 같은 이유).
    }

    private IEnumerator CollapseRoutine(float delay)
    {
        _isSwapping = true;

        // stagger로 자기 차례를 기다리는 동안도 "무너지는 중"이다 - 여기서 켜지 않으면
        // 마지막 카드가 아직 시작도 안 했는데 IsLeaving이 false로 보인다.
        _isLeaving = true;

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
        _isLeaving = false;
        // _isSwapping은 켜 둔 채로 남긴다 - 꺼버리면 Update()의 타이핑 들림 로직이 다시 돌아
        // 이미 떨어져 안 보이는 카드를 다시 위로 들어올리려 든다. 다음 PlaySwap(RefillAll)이
        // _isSwapping을 다시 관리하며 정상 상태로 되돌린다.
    }

    private void RestartSwapCoroutine(IEnumerator routine)
    {
        if (_swapCoroutine != null)
            StopCoroutine(_swapCoroutine);

        // 무너지던 중에 다른 연출이 끼어들면(재시작 후 RefillAll의 PlaySwap 등) 그 코루틴은
        // 여기서 끊긴다 - 끝을 못 봤으니 플래그를 여기서 내려야 영영 true로 남지 않는다.
        // CollapseRoutine이 곧바로 다시 켜므로(StartCoroutine은 첫 구간을 그 자리에서 실행한다)
        // 무너짐을 새로 거는 경우에는 값이 그대로 유지된다.
        _isLeaving = false;

        _swapCoroutine = StartCoroutine(routine);
    }

    private IEnumerator ExitRoutine(float offset, System.Action onComplete)
    {
        _isSwapping = true;
        _isLeaving = true;
        yield return AnimateSwap(0f, offset, 1f, 0f, exitDuration);
        onComplete?.Invoke();
        _isSwapping = false;
        _isLeaving = false;
        _swapCoroutine = null;
    }

    private IEnumerator EnterRoutine(float fromOffset, float duration)
    {
        _isSwapping = true;
        yield return AnimateSwap(fromOffset, 0f, 0f, 1f, duration);
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
        // ⚠️ 슬롯에 묶이지 않은 카드는 손대지 않는다. 일시정지 명령 카드가 그렇다 -
        // BindStatic이 이미 내용을 채워놨는데 여기서 비우면 그걸 지워버린다.
        // (Start()가 Refresh를 부르는데, 명령 카드는 Instantiate -> BindStatic -> Start 순서라
        //  Start가 항상 나중이다. 실제로 카드가 통째로 비어 보이는 회귀가 났던 자리다.)
        if (cardSlotManager == null)
            return;

        // 여기부터는 슬롯 카드다. 슬롯을 읽을 수 없으면 그냥 리턴하지 말고 빈 카드로 둔다 -
        // 두고 나오면 Card.prefab에 저장돼 있는 예시 문구("파워" / "테스트테스트…" / "+1")가
        // 그대로 화면에 보인다. 이 프로젝트엔 스크립트 실행 순서 설정이 없어서 HandFanLayout이
        // 카드를 스폰하고 Bind할 때 CardSlotManager.Awake가 아직 안 돌았을 수 있고, 그러면
        // _currentCards가 null이라 여기로 온다.
        var cards = cardSlotManager.CurrentCards;
        if (cards == null || slotIndex < 0 || slotIndex >= cards.Count)
        {
            SetCard(null);
            return;
        }

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
