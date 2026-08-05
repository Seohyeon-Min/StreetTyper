using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마더 드래곤 스테이지 보상에서 "지우기"를 골랐을 때 열리는 창. 지금 사전에 든 카드를 격자로
/// 펼치고, 플레이어가 이름을 타이핑한 한 장을 <see cref="WordDictionary"/>에서 지운다.
///
/// 취소는 없다 - 지우기를 고른 순간 반드시 한 장을 지운다. 그래서 명령 단어가 하나도 없고
/// 타이핑 대상은 보유 카드 이름뿐이다.
///
/// 카드 한 장을 그리는 규칙은 손패·보상·보유 목록과 똑같이 <see cref="CardView.SetCard"/> 하나가
/// 갖는다. 여기서 이름을 따로 그리지 않는 이유이기도 하다 - 네 화면에서 카드가 다르게 보이면 안 된다.
///
/// ⚠️ <b>항상 켜져 있는 오브젝트에 붙일 것.</b> 목록 패널 루트에 붙이면 그게 평소 비활성이라
/// 수신자 등록·해제가 같이 흔들린다. 켜고 끄는 건 이 컴포넌트가 아니라 인스펙터로 받은
/// <see cref="panel"/>이다(<see cref="CardCollectionPanel"/>과 같은 이유).
/// </summary>
public class CardDeletePanel : CommandWordReceiver
{
    [Header("UI")]
    [Tooltip("카드 목록 창 루트. 평소엔 비활성이어야 한다. 보상 카드 줄 위에 겹쳐 떠야 하므로 " +
             "보상 목록보다 뒤 형제(= 위에 그려지는 쪽)에 두고, 뒤가 비치지 않도록 화면을 덮는 " +
             "배경 이미지를 함께 둘 것.")]
    [SerializeField] private GameObject panel;

    [Tooltip("카드가 생성될 부모. 앵커를 화면 중앙에 둬야 격자 가운데 정렬이 맞는다.")]
    [SerializeField] private RectTransform container;

    [Tooltip("카드 한 장을 표시할 프리팹. 손패와 같은 Card.prefab을 그대로 쓴다.")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("제목을 그릴 라벨. 비워두면 제목 표시를 건너뛴다.")]
    [SerializeField] private TMP_Text titleLabel;

    [Tooltip("제목 옆/뒤에 띄울 이미지. 스프라이트를 안 넣었으면 오브젝트째 꺼진다.")]
    [SerializeField] private Image titleImage;

    [Header("표시")]
    [Tooltip("창 제목(한/영)과 이미지. 결과·보상 화면과 같은 구조를 쓴다.")]
    [SerializeField]
    private ScreenPresentation presentation = new ScreenPresentation("지울 카드를 입력하세요", "TYPE A CARD TO ERASE");

    [Header("데이터")]
    [Tooltip("지울 대상이 되는 사전. 여기 든 카드만 목록에 오른다.")]
    [SerializeField] private WordDictionary wordDictionary;

    [Header("배치")]
    [Tooltip("한 줄에 놓을 카드 수. 마지막 줄이 덜 차면 그 줄만 따로 가운데 정렬된다.")]
    [SerializeField] private int columns = 7;

    [Tooltip("카드 중심 사이의 거리(px). 카드 크기(cardScale 적용 후)보다 커야 서로 겹치지 않는다.")]
    [SerializeField] private Vector2 cellSize = new Vector2(150f, 220f);

    [Tooltip("카드 축소 배율. Card.prefab의 프레임이 200x300이라 원본 크기로는 여러 장이 화면에 " +
             "다 들어가지 않는다. 해금 단어가 늘면 이 값과 cellSize를 같이 줄일 것.")]
    [SerializeField] private float cardScale = 0.6f;

    [Header("열려 있는 동안 비켜날 UI")]
    [Tooltip("창이 열리면 여기 넣은 UI들이 offset만큼 밀려났다가 닫으면 제자리로 돌아온다. " +
             "보통 보상 카드 줄과 입력창(InputFieldDisplay)을 넣는다 - 이 창은 보상 줄 위에 " +
             "겹쳐 뜨므로 그대로 두면 뒤에 비친다. CardCollectionPanel과 같은 구조다.")]
    [SerializeField] private DisplacedUI[] displacedUI = new DisplacedUI[0];

    [Tooltip("비켜나고 돌아오는 데 걸리는 시간(초). 0이면 즉시 이동한다.")]
    [SerializeField] private float displaceDuration = 0.15f;

    [Tooltip("진행도(0~1)에 따른 밀림 비율. 기본은 EaseInOut. 닫힐 때는 같은 곡선을 거꾸로 " +
             "훑으므로 따로 반대 곡선을 만들 필요가 없다. CardCollectionPanel과 같은 기본값이다.")]
    [SerializeField] private AnimationCurve displaceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("타이핑 피드백")]
    [Tooltip("타이핑 중인 카드가 떠오르는 높이(px). 손패·보상 화면과 같은 값을 기본으로 둔다.")]
    [SerializeField] private float typingLiftHeight = 40f;

    [SerializeField] private float liftSmoothTime = 0.08f;

    [Tooltip("지금 치는 글자로 갈 수 없는 카드의 투명도. 아무 카드에도 안 맞으면 전부 이 값이 된다.")]
    [SerializeField] private float dimAlpha = 0.4f;

    [SerializeField] private bool logDebugEvents;

    /// <summary>한 장을 지워 창이 닫혔다. RewardInputHandler가 받아 그 보상 라운드를 끝낸다.</summary>
    public event Action OnDeleteFinished;

    private bool _isOpen;

    /// <summary>목록이 지금 떠 있는가.</summary>
    public bool IsOpen => _isOpen;

    // 지금 목록에 오른 카드. Targets의 인덱스와 그대로 맞물린다.
    private readonly List<CardBase> _cards = new List<CardBase>();

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // 비켜나기 진행 상태 + 로직. CardCollectionPanel과 같은 것을 쓴다(UIDisplacement 참조).
    private readonly UIDisplacement _displacement = new UIDisplacement();

    // 매 프레임 GetComponent를 부르지 않도록 스폰할 때 같이 모아둔다. _spawned와 인덱스가 같다.
    private readonly List<RectTransform> _rects = new List<RectTransform>();
    private readonly List<CardView> _views = new List<CardView>();
    private readonly List<float> _lifts = new List<float>();

    // 카드 이름을 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private string[] _targets = Array.Empty<string>();

    // 지금 타이핑 중인 카드. -1이면 후보 없음.
    private int _highlight = -1;

    // 입력창에 글자가 있는지. "아직 안 쳤다"와 "쳤는데 아무것도 안 맞는다"를 갈라야 한다.
    private bool _hasInput;

    /// 보상 줄(15)보다 먼저 가져간다 - 목록이 떠 있는 동안 후보 카드나 "넘기기"가 먹으면 안 된다.
    public override TypingPriority Priority => TypingPriority.RewardDelete;

    public override bool WantsInput() => _isOpen;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            // CardName은 언어에 따라 갈리는 프로퍼티라 캐시하지 않고 매번 읽는다.
            for (var i = 0; i < _cards.Count; i++)
                _targets[i] = _cards[i].CardName;

            return _targets;
        }
    }

    // 지울 카드 이름이 곧 타이핑 대상이라 따로 안내할 명령 단어가 없다. 제목이 안내를 대신한다.
    public override string BuildHint() => string.Empty;

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        if (index < 0 || index >= _cards.Count)
            return;

        var card = _cards[index];

        if (wordDictionary != null && wordDictionary.RemoveWord(card))
        {
            if (logDebugEvents)
                Debug.Log($"CardDelete: '{card.CardName}' 삭제 (남은 {wordDictionary.Words.Count}장)", this);
        }
        else
        {
            Debug.LogWarning($"CardDelete: '{card.CardName}'을(를) 지우지 못했습니다. " +
                             "wordDictionary 연결을 확인하세요.", this);
        }

        Close();
    }

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        else
            Debug.LogWarning("CardDeletePanel: panel이 연결되지 않아 창을 여닫을 수 없습니다.", this);

        if (wordDictionary == null)
            Debug.LogWarning("CardDeletePanel: wordDictionary가 연결되지 않아 지울 카드를 알 수 없습니다.", this);

        _displacement.CaptureOrigins(displacedUI, this, nameof(displacedUI));
    }

    // ⚠️ useUnscaledTime: false - 이 창은 timeScale이 1인 보상 구간에서만 뜬다. 일시정지가
    // timeScale = 0 하나로 성립하는 프로젝트 전제를 따르는 쪽이 맞다(CardCollectionPanel이
    // true인 건 그쪽이 일시정지 "위에서" 열리기 때문이고, 여기는 다르다).
    //
    // 위치를 쓰는 건 LateUpdate다 - 레이아웃이 자식을 배치한 뒤에 줄 전체(부모)를 옮긴다.
    private void LateUpdate()
    {
        _displacement.Tick(displacedUI, _isOpen, displaceDuration, useUnscaledTime: false, displaceCurve);
    }

    // offset은 배치를 눈으로 보며 맞추는 값이라 보통 Play 중에 조정하게 된다. 도착해서 좌표
    // 쓰기를 멈춘 상태에서는 다음 여닫이까지 반영이 안 보이므로 여기서 다시 움직이게 한다.
    private void OnValidate()
    {
        _displacement.MarkDirty();
    }

    /// <summary>지울 카드를 고르는 창을 연다. RewardInputHandler가 "지우기"를 받았을 때 부른다.</summary>
    public void Open()
    {
        if (_isOpen)
            return;

        Rebuild();

        // 지울 게 하나도 없으면 여는 대신 곧바로 끝낸다 - 취소가 없는 창이라 열리면 갇힌다.
        if (_cards.Count == 0)
        {
            Debug.LogWarning("CardDeletePanel: 사전이 비어 있어 지울 카드가 없습니다. 창을 열지 않고 넘깁니다.", this);
            ClearCards();
            OnDeleteFinished?.Invoke();
            return;
        }

        _isOpen = true;
        _highlight = -1;
        _hasInput = false;

        // 가리는 UI를 비켜나게 한다(실제 이동은 LateUpdate가 이어서 한다).
        _displacement.MarkDirty();

        if (panel != null)
            panel.SetActive(true);

        ApplyPresentation();

        // 보상 줄에서 치던 "지우기"가 남아 카드 이름에 섞이지 않게 비운다.
        if (inputManager != null)
            inputManager.ClearInput();
    }

    private void Close()
    {
        if (!_isOpen)
            return;

        // ⚠️ ClearInput보다 먼저 내려야 한다. ClearInput은 "비었다"를 수신자에게도 디스패치하는데,
        // 그때 아직 열린 상태면 이쪽이 그 신호를 가로챈다.
        _isOpen = false;

        // 비켜났던 UI를 제자리로 돌린다. 이 컴포넌트는 창(panel)이 아니라 항상 켜져 있는
        // 오브젝트에 붙어 있으므로 복귀 이동은 끝까지 재생된다.
        _displacement.MarkDirty();

        ClearCards();

        if (panel != null)
            panel.SetActive(false);

        if (inputManager != null)
            inputManager.ClearInput();

        OnDeleteFinished?.Invoke();
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
        if (presentation.Image == null)
        {
            titleImage.gameObject.SetActive(false);
            return;
        }

        titleImage.sprite = presentation.Image;
        titleImage.gameObject.SetActive(true);
    }

    // 지금 치고 있는 글자가 어느 카드를 향하는지 비춘다. 판정은 손패(CardSlotView)·보상 화면과
    // 같은 InputManager.IsValidProgress를 쓰므로 네 화면의 들림 기준이 어긋나지 않는다.
    //
    // ⚠️ Time.deltaTime을 쓴다 - 이 창은 timeScale이 1인 보상 구간에서만 뜬다. 일시정지가
    // timeScale = 0 하나로 성립하는 프로젝트 전제를 따르는 쪽이 맞다(CardCollectionPanel이
    // unscaledDeltaTime인 건 그쪽이 일시정지 "위에서" 열리기 때문이고, 여기는 다르다).
    private void Update()
    {
        if (!_isOpen || _spawned.Count == 0)
            return;

        // ⚠️ 열려 있다고 해서 입력이 이쪽이라는 보장은 없다 - 일시정지(Pause = 20)가 이 창
        // (RewardDelete = 18)보다 먼저 가져간다. 그때는 읽기를 멈추고 평상 상태로 되돌려,
        // 플레이어가 치는 "계속"에 지울 카드가 덩달아 떠오르지 않게 한다.
        if (!HasTypingFocus)
        {
            _hasInput = false;
            _highlight = -1;
        }
        else if (inputManager != null)
        {
            var committed = inputManager.CurrentInput;
            var composing = inputManager.Composition;
            _hasInput = committed.Length > 0 || composing.Length > 0;

            _highlight = -1;
            if (_hasInput)
            {
                for (var i = 0; i < _cards.Count; i++)
                {
                    if (!InputManager.IsValidProgress(committed, composing, _cards[i].CardName))
                        continue;

                    _highlight = i;
                    break;
                }
            }
        }

        var t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(liftSmoothTime, 0.0001f));

        for (var i = 0; i < _spawned.Count; i++)
        {
            var isCandidate = i == _highlight;

            var targetLift = isCandidate ? typingLiftHeight : 0f;
            _lifts[i] = Mathf.Lerp(_lifts[i], targetLift, t);

            if (_rects[i] != null)
            {
                var pos = _rects[i].anchoredPosition;
                _rects[i].anchoredPosition = new Vector2(pos.x, GridY(i) + _lifts[i]);
            }

            if (_views[i] == null)
                continue;

            // 아무것도 안 쳤으면 전부 선명하게, 치고 있으면 목표만 선명하게.
            _views[i].SetAlpha(!_hasInput || isCandidate ? 1f : dimAlpha);
        }
    }

    // 열 때마다 새로 그린다. 보상으로 카드가 늘고 지우기로 줄어든다.
    private void Rebuild()
    {
        ClearCards();

        if (wordDictionary == null)
            return;

        if (cardPrefab == null || container == null)
        {
            Debug.LogWarning("CardDeletePanel: cardPrefab 또는 container가 연결되지 않아 카드를 표시할 수 없습니다.", this);
            return;
        }

        var words = wordDictionary.Words;
        for (var i = 0; i < words.Count; i++)
            _cards.Add(words[i]);

        if (_targets.Length != _cards.Count)
            _targets = new string[_cards.Count];

        for (var i = 0; i < _cards.Count; i++)
            Spawn(_cards[i], i, _cards.Count);
    }

    // 격자에서 이 카드가 놓일 세로 위치. 들림 애니메이션이 매 프레임 여기에 더해 쓴다.
    private float GridY(int index)
    {
        var cols = Mathf.Max(1, columns);
        var rows = Mathf.CeilToInt(_cards.Count / (float)cols);
        var row = index / cols;

        return -(row - (rows - 1) * 0.5f) * cellSize.y;
    }

    private void Spawn(CardBase card, int index, int total)
    {
        var instance = Instantiate(cardPrefab, container);

        var rect = instance.GetComponent<RectTransform>();
        if (rect != null)
        {
            var cols = Mathf.Max(1, columns);
            var row = index / cols;
            var col = index % cols;

            // 마지막 줄은 덜 찰 수 있다. 그 줄에 실제로 몇 장이 있는지로 중심을 잡아야
            // 한 장뿐인 줄이 왼쪽 끝에 혼자 붙지 않는다.
            var inRow = Mathf.Min(cols, total - row * cols);

            var x = (col - (inRow - 1) * 0.5f) * cellSize.x;
            rect.anchoredPosition = new Vector2(x, GridY(index));

            // 손패에서는 HandFanLayout이 카드를 부채꼴로 기울인다. 여기는 그 레이아웃 밖이라
            // 반듯하게 세운다(기울기는 NameText 자식에 7도가 따로 박혀 있고, 그건 아트 의도다).
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * cardScale;
        }

        // 손패용 Card.prefab을 재사용하는 경우 슬롯 로직이 같이 딸려온다. 여기는 슬롯과 무관하므로
        // 꺼둔다(그리기는 CardView가 따로 하므로 꺼도 카드 내용은 정상적으로 나온다).
        var slotView = instance.GetComponent<CardSlotView>();
        if (slotView != null)
            slotView.enabled = false;

        var cardView = instance.GetComponent<CardView>();
        if (cardView != null)
            cardView.SetCard(card);
        else
            Debug.LogWarning("CardDeletePanel: cardPrefab에 CardView가 없어 카드를 그릴 수 없습니다.", this);

        _spawned.Add(instance);
        _rects.Add(rect);
        _views.Add(cardView);
        _lifts.Add(0f);
    }

    private void ClearCards()
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
        _cards.Clear();

        _highlight = -1;
        _hasInput = false;
    }
}
