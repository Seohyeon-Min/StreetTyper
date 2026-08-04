using System.Collections;
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

    [Tooltip("보상 화면이 뜰 때 같이 재생할 TextGateRevealAnimation들(제목 텍스트, 배경 윈도우 등 - " +
             "마스크마다 컴포넌트가 하나씩 따로 필요하다). BattleManager.resultReveals/PauseManager.titleReveal과 " +
             "같은 컴포넌트지만 여긴 아무도 Play()를 부르지 않으면 마스크가 계속 닫힌 채(폭 0)로 남는다 - " +
             "Show()가 패널을 켤 때 여기 담긴 것 전부를 같이 재생한다.")]
    [SerializeField] private TextGateRevealAnimation[] reveals;

    [Header("타이핑 피드백")]
    [Tooltip("타이핑 중인 카드가 떠오르는 높이(px). 손패(CardSlotView)와 같은 값을 기본으로 둔다.")]
    [SerializeField] private float typingLiftHeight = 40f;

    [SerializeField] private float liftSmoothTime = 0.08f;

    [Tooltip("후보가 아닌 카드의 투명도. 아무 후보에도 안 맞는 글자를 치면 전부 이 값이 된다.")]
    [SerializeField] private float dimAlpha = 0.4f;

    [Header("등장 연출 - 위에서 후두둑 떨어지기")]
    [Tooltip("카드가 시작할 때 최종 위치보다 얼마나 위에서 시작할지(px).")]
    [SerializeField] private float dropHeight = 600f;

    [Tooltip("카드 한 장이 떨어지는 데 걸리는 시간(초).")]
    [SerializeField] private float dropDuration = 0.35f;

    [Tooltip("카드마다 이만큼 시차를 두고 떨어뜨린다(초/장) - 한꺼번에 안 떨어지고 순서대로 " +
             "후두둑 떨어지는 느낌을 낸다. 패배 시 손패가 시차를 두고 무너지는 것(CardSlotView.PlayCollapse)과 같은 결.")]
    [SerializeField] private float dropStagger = 0.06f;

    [Tooltip("떨어지는 동안의 흔들림 회전 폭(도). 카드마다 이 범위 안에서 무작위로 시작해 0(반듯한 자세)으로 정착한다.")]
    [SerializeField] private float dropMaxRotation = 12f;

    [Tooltip("떨어지는 속도 곡선. 값이 1을 넘으면 그 순간만큼 착지 지점을 지나쳐(반동) 아래로 " +
             "내려갔다가 다시 1로 돌아온다 - entranceY = Lerp(dropHeight, 0, 곡선값)이라, 곡선값이 " +
             "1보다 크면 entranceY가 0보다 작아져(제자리보다 아래) 튕기는 것처럼 보인다. 기본값은 " +
             "70% 지점에서 살짝(12%) 지나쳤다가 100% 지점에서 반듯하게 멈추는 1회 바운스.")]
    [SerializeField] private AnimationCurve dropCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.72f, 1.12f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("퇴장 연출 - 고른 카드만 남기고 나머지는 떨어뜨리기")]
    [Tooltip("고른 카드를 뺀 나머지가 무너지듯 떨어지는 거리(px). 패배 시 손패가 무너지는 것 " +
             "(CardSlotView.PlayCollapse)과 같은 값을 기본으로 둔다.")]
    [SerializeField] private float exitFallDistance = 400f;

    [Tooltip("떨어지며 도는 최대 회전각(도). 카드마다 이 범위 안에서 좌우 무작위로 정해진다.")]
    [SerializeField] private float exitFallMaxRotation = 50f;

    [Tooltip("카드 한 장이 떨어져 사라지는 데 걸리는 시간(초).")]
    [SerializeField] private float exitFallDuration = 0.5f;

    [Tooltip("카드마다 이만큼 시차를 두고 떨어뜨린다(초/장) - 한꺼번에 안 떨어지고 후두둑 떨어지는 느낌.")]
    [SerializeField] private float exitFallStagger = 0.06f;

    [Tooltip("떨어지며 가속하는 곡선. 기본은 EaseIn(초반 느리게, 후반 빠르게) - CardSlotView의 무너짐과 같다.")]
    [SerializeField] private AnimationCurve exitFallCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));

    [Tooltip("나머지가 다 떨어진 뒤, 고른 카드 혼자 남아 보이는 시간(초). 이 시간이 지나야 패널을 닫는다.")]
    [SerializeField] private float exitKeepLinger = 0.5f;

    [SerializeField] private bool logDebugEvents;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // 매 프레임 GetComponent를 부르지 않도록 스폰할 때 같이 모아둔다. _spawned와 인덱스가 같다.
    private readonly List<RectTransform> _rects = new List<RectTransform>();
    private readonly List<CardView> _views = new List<CardView>();
    private readonly List<float> _lifts = new List<float>();

    // 등장 연출(위에서 떨어지기) 진행 상태. _spawned와 인덱스가 같다.
    private readonly List<float> _dropElapsed = new List<float>();
    private readonly List<float> _dropDelay = new List<float>();
    private readonly List<float> _dropRotationStart = new List<float>();

    // 퇴장 연출이 진행 중인 동안은 Update()의 평소 들림/등장 로직이 위치를 같이 건드리면
    // 안 되므로 여기서 막는다. ExitRoutine이 남은 카드의 위치를 전담한다.
    private bool _exiting;
    private Coroutine _exitRoutine;

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

        if (reveals != null)
        {
            foreach (var reveal in reveals)
            {
                if (reveal != null)
                    reveal.Play();
            }
        }

        for (var i = 0; i < cards.Count; i++)
            Spawn(cards[i], i, cards.Count);

        if (logDebugEvents)
            Debug.Log($"RewardCardView: 보상 후보 {cards.Count}장 표시", this);
    }

    /// <summary>표시를 즉시 지운다(연출 없음). 다음 스테이지가 시작될 때나, 퇴장 연출 도중에
    /// 플레이어가 "다음"을 먼저 쳐서 앞질러 넘어갈 때(StageManager.LoadStage) 쓴다.</summary>
    public void Clear()
    {
        // 진행 중이던 퇴장 연출과, 그 안에서 개별로 시작한 낙하 코루틴들을 전부 멈춘다 -
        // 이 컴포넌트가 도는 코루틴은 지금 전부 퇴장 연출뿐이라 StopAllCoroutines로 충분하다.
        // 이게 없으면 앞질러 넘어간 뒤에도 낙하 코루틴이 살아남아 이미 Destroy된 카드를
        // 건드리거나, 다음 스테이지로 넘어간 뒤에 onComplete(다음 스테이지 진행)이 한 번 더
        // 불려 스테이지를 건너뛴다.
        StopAllCoroutines();
        _exitRoutine = null;
        _exiting = false;

        ClearVisuals();
    }

    /// <summary>선택이 끝났을 때의 퇴장 연출. keepIndex 카드(고른 카드)만 그 자리에 잠시
    /// 머물고, 나머지는 패배 시 손패가 무너지는 것과 같은 방식으로 시차를 두고 떨어지며
    /// 사라진다. reveals(제목/배경 게이트)는 열릴 때와 반대로 닫힌다. 다 끝나면 패널을 끄고
    /// onComplete를 부른다. keepIndex가 -1이면(넘기기·지우기처럼 남길 카드가 없는 경우) 전부
    /// 떨어뜨린다. 표시된 카드가 없으면(후보가 애초에 없던 라운드) 연출 없이 곧바로 끝낸다.</summary>
    public void PlayExit(int keepIndex, System.Action onComplete)
    {
        if (_spawned.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        StopAllCoroutines();
        _exitRoutine = StartCoroutine(ExitRoutine(keepIndex, onComplete));
    }

    private IEnumerator ExitRoutine(int keepIndex, System.Action onComplete)
    {
        _exiting = true;

        if (reveals != null)
        {
            foreach (var reveal in reveals)
            {
                if (reveal != null)
                    reveal.PlayReverse();
            }
        }

        // keepIndex를 뺀 나머지에만 낙하 코루틴을 건다 - 시차는 "떨어지는 카드끼리의 순서"
        // 기준이라, 가운데 카드를 남겨도 나머지끼리는 듬성듬성해 보이지 않는다.
        var fallerCount = 0;
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (i == keepIndex)
                continue;

            var rotationTarget = Random.Range(-exitFallMaxRotation, exitFallMaxRotation);
            StartCoroutine(FallAwayRoutine(i, fallerCount * exitFallStagger, rotationTarget));
            fallerCount++;
        }

        var fallTime = fallerCount > 0 ? (fallerCount - 1) * exitFallStagger + exitFallDuration : 0f;

        // 나머지가 다 떨어진 뒤, 고른 카드가 혼자 남아 보이는 여유를 준 다음에야 패널을 닫는다.
        yield return new WaitForSeconds(fallTime + exitKeepLinger);

        _exiting = false;
        _exitRoutine = null;
        ClearVisuals();
        onComplete?.Invoke();
    }

    private IEnumerator FallAwayRoutine(int index, float delay, float rotationTarget)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var rect = _rects[index];
        var view = _views[index];
        if (rect == null)
            yield break;

        var startPos = rect.anchoredPosition;

        var elapsed = 0f;
        while (elapsed < exitFallDuration)
        {
            elapsed += Time.deltaTime;
            var t = exitFallCurve.Evaluate(Mathf.Clamp01(elapsed / exitFallDuration));
            rect.anchoredPosition = new Vector2(startPos.x, startPos.y - exitFallDistance * t);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, rotationTarget, t));

            if (view != null)
                view.SetAlpha(1f - t);

            yield return null;
        }
    }

    // Clear()/ExitRoutine이 공유하는 실제 정리 로직. 코루틴 상태(_exiting/_exitRoutine)는
    // 부르는 쪽이 각자의 사정에 맞게 처리하므로 여기서는 건드리지 않는다.
    private void ClearVisuals()
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
        _dropElapsed.Clear();
        _dropDelay.Clear();
        _dropRotationStart.Clear();

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
        // 퇴장 연출 중에는 ExitRoutine/FallAwayRoutine이 위치·회전·알파를 전담한다 - 여기서
        // 같이 건드리면 매 프레임 서로 덮어써 카드가 떨리거나 제자리로 되돌아간다.
        if (_exiting || _spawned.Count == 0)
            return;

        var t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(liftSmoothTime, 0.0001f));

        for (var i = 0; i < _spawned.Count; i++)
        {
            // 등장 연출 - 카드마다 dropDelay만큼 늦게 시작해 한꺼번에 안 떨어지고
            // 순서대로 후두둑 떨어지는 느낌을 낸다. delay가 지나기 전엔 진행도 0(위쪽 시작 위치).
            _dropElapsed[i] += Time.deltaTime;
            var dropStarted = _dropElapsed[i] >= _dropDelay[i];
            var dropT = dropDuration > 0f
                ? Mathf.Clamp01((_dropElapsed[i] - _dropDelay[i]) / dropDuration)
                : 1f;
            var dropProgress = dropStarted ? dropCurve.Evaluate(dropT) : 0f;

            var isCandidate = i == _highlight;

            var targetLift = isCandidate ? typingLiftHeight : 0f;
            _lifts[i] = Mathf.Lerp(_lifts[i], targetLift, t);

            if (_rects[i] != null)
            {
                // ⚠️ LerpUnclamped를 써야 한다 - Mathf.Lerp는 t를 0~1로 잘라버려서
                // dropCurve가 1을 넘는 순간(반동)을 표현해도 여기서 다시 뭉개진다.
                var entranceY = Mathf.LerpUnclamped(dropHeight, 0f, dropProgress);
                var pos = _rects[i].anchoredPosition;
                _rects[i].anchoredPosition = new Vector2(pos.x, verticalOffset + _lifts[i] + entranceY);

                var rotation = Mathf.LerpUnclamped(_dropRotationStart[i], 0f, dropProgress);
                _rects[i].localRotation = Quaternion.Euler(0f, 0f, rotation);
            }

            if (_views[i] == null)
                continue;

            // 아무것도 안 쳤으면 전부 선명하게, 치고 있으면 후보만 선명하게.
            // 글자는 있는데 후보가 하나도 없으면(오타) 전부 흐려져 "이걸로는 아무것도 못 얻는다"를 알린다.
            // 등장 중(dropProgress < 1)에는 여기에 곱해 페이드인시킨다.
            var opaque = !_hasInput || isCandidate;
            _views[i].SetAlpha((opaque ? 1f : dimAlpha) * dropProgress);
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

            // 최종 위치가 아니라 등장 연출의 시작 위치(위쪽, dropHeight만큼 띄운 자리)에서
            // 스폰한다 - 다음 Update()가 첫 프레임부터 낙하를 그려야 하므로, 여기서 최종
            // 위치를 넣으면 한 프레임 동안 잘못된 자리에 있다가 순간이동하는 것처럼 보인다.
            rect.anchoredPosition = new Vector2(x, verticalOffset + dropHeight);
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
        {
            cardView.SetCard(card);
            // 등장 연출이 첫 프레임부터 페이드인을 그리므로 스폰 시점엔 투명하게 시작한다.
            cardView.SetAlpha(0f);
        }
        else
        {
            Debug.LogWarning("RewardCardView: cardPrefab에 CardView가 없어 보상 카드를 그릴 수 없습니다.", this);
        }

        _spawned.Add(instance);
        _rects.Add(rect);
        _views.Add(cardView);
        _lifts.Add(0f);

        // 카드마다 dropStagger만큼 시차를 두고, 좌우 무작위 회전에서 시작해 반듯한 자세로 정착한다.
        _dropElapsed.Add(0f);
        _dropDelay.Add(index * dropStagger);
        _dropRotationStart.Add(Random.Range(-dropMaxRotation, dropMaxRotation));
    }
}
