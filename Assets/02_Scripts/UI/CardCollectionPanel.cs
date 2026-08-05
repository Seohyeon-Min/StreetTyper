using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일시정지 중 "카드"를 쳐서 여는 보유 카드 목록. 지금 사전(<see cref="WordDictionary"/>)에 들어
/// 있는 카드 - 즉 이번 런에서 해금해 실제로 손패에 뜰 수 있는 카드 - 를 전부 격자로 펼친다.
/// 닫을 때는 "닫기"를 친다.
///
/// 열고 닫는 것 말고는 게임 판단을 하지 않는다. 카드 한 장을 그리는 규칙은 손패·보상 화면과
/// 똑같이 <see cref="CardView.SetCard"/> 하나가 갖는다 - 세 화면에서 카드가 서로 다르게
/// 보이는 일이 없어야 한다.
///
/// ⚠️ <b>항상 켜져 있는 오브젝트에 붙일 것</b>(보통 PauseManager와 같은 Pause Canvas).
/// 목록 패널 루트에 붙이면 그게 평소 비활성이라 등록·해제가 같이 흔들린다. 여기서 켜고 끄는 건
/// 이 컴포넌트가 아니라 인스펙터로 받은 <see cref="panel"/>이다.
/// </summary>
public class CardCollectionPanel : CommandWordReceiver
{
    [Header("UI")]
    [Tooltip("카드 목록 창 루트. 평소엔 비활성이어야 한다. 일시정지 메뉴 위에 겹쳐 떠야 하므로 " +
             "Pause Panel보다 뒤 형제(= 위에 그려지는 쪽)에 두고, 뒤가 비치지 않도록 화면을 " +
             "덮는 배경 이미지를 함께 둘 것.")]
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
    [Tooltip("목록 창 제목(한/영)과 이미지. 결과·보상 화면과 같은 구조를 쓴다.")]
    [SerializeField] private ScreenPresentation presentation = new ScreenPresentation("보유 카드", "YOUR CARDS");

    [Header("데이터")]
    [Tooltip("지금 보유 중인 단어. 여기 들어 있는 것만 손패에 뜨므로 그대로 목록이 된다.")]
    [SerializeField] private WordDictionary wordDictionary;

    [Header("배치")]
    [Tooltip("한 줄에 놓을 카드 수. 마지막 줄이 덜 차면 그 줄만 따로 가운데 정렬된다.")]
    [SerializeField] private int columns = 7;

    [Tooltip("카드 중심 사이의 거리(px). 카드 크기(cardScale 적용 후)보다 커야 서로 겹치지 않는다.")]
    [SerializeField] private Vector2 cellSize = new Vector2(150f, 220f);

    [Tooltip("카드 축소 배율. Card.prefab의 프레임이 200x300이라 원본 크기로는 28장이 화면에 " +
             "다 들어가지 않는다. 해금 단어가 늘면 이 값과 cellSize를 같이 줄일 것.")]
    [SerializeField] private float cardScale = 0.6f;

    [Header("열려 있는 동안 비켜날 UI")]
    [Tooltip("목록이 열리면 여기 넣은 UI들이 offset만큼 밀려났다가 닫으면 제자리로 돌아온다. " +
             "보통 PauseHand(명령 카드 줄)와 InputFieldDisplay(입력창)를 넣는다.")]
    [SerializeField] private DisplacedUI[] displacedUI = new DisplacedUI[0];

    [Tooltip("비켜나고 돌아오는 데 걸리는 시간(초). 0이면 즉시 이동한다.")]
    [SerializeField] private float displaceDuration = 0.15f;

    [Tooltip("진행도(0~1)에 따른 밀림 비율. 기본은 EaseInOut. 닫힐 때는 같은 곡선을 거꾸로 " +
             "훑으므로 따로 반대 곡선을 만들 필요가 없다.")]
    [SerializeField] private AnimationCurve displaceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("명령 단어")]
    [SerializeField]
    private TypedCommand closeCommand = new TypedCommand(
        "닫기", "close",
        "\"{0}\"를 입력하면 목록이 닫힙니다.",
        "Type \"{0}\" to close the list.");

    [SerializeField] private bool logDebugEvents;

    private bool _isOpen;

    // 명령 단어를 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private readonly string[] _targets = new string[1];

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // 비켜나기 진행 상태 + 로직. CardDeletePanel과 같은 것을 쓴다(UIDisplacement 참조).
    private readonly UIDisplacement _displacement = new UIDisplacement();

    /// <summary>목록이 지금 떠 있는가. PauseManager가 ESC를 "목록만 닫기"로 돌리는 데 쓴다.</summary>
    public bool IsOpen => _isOpen;

    /// 일시정지 메뉴 위에 겹쳐 뜨므로 그보다 먼저 가져간다 - 목록이 열려 있는 동안 "계속"/"타이틀"이
    /// 먹으면 안 되고, 이 순서 하나로 별도 상태 플래그 없이 성립한다.
    public override TypingPriority Priority => TypingPriority.CardCollection;

    public override bool WantsInput() => _isOpen;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            _targets[0] = closeCommand.Word(this, nameof(closeCommand));
            return _targets;
        }
    }

    public override string BuildHint()
    {
        return JoinHints(closeCommand.Hint(this, nameof(closeCommand)));
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        Close();
    }

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        else
            Debug.LogWarning("CardCollectionPanel: panel이 연결되지 않아 카드 목록 창을 여닫을 수 없습니다.", this);

        _displacement.CaptureOrigins(displacedUI, this, nameof(displacedUI));
    }

    // ⚠️ unscaledDeltaTime을 쓴다(useUnscaledTime: true) - 목록은 Time.timeScale == 0인 일시정지
    // 위에서 열릴 수 있으므로 보통의 deltaTime이면 아예 움직이지 않는다(CardSlotView·HandFanLayout과
    // 같은 이유). 결과 화면에서 열 때는 timeScale이 1이라 두 값이 같으므로 그대로 동작한다.
    // 반대로 CardDeletePanel은 멈춘 화면 위에서 열리지 않아 false를 넘긴다.
    private void LateUpdate()
    {
        _displacement.Tick(displacedUI, _isOpen, displaceDuration, useUnscaledTime: true, displaceCurve);
    }

    private void OnValidate()
    {
        _displacement.MarkDirty();
    }

    /// <summary>보유 카드를 펼친다. PauseManager가 "카드"를 받았을 때 부른다.</summary>
    public void Open()
    {
        if (_isOpen)
            return;

        _isOpen = true;

        // 가리는 UI를 비켜나게 한다(실제 이동은 LateUpdate가 이어서 한다).
        _displacement.MarkDirty();

        if (panel != null)
            panel.SetActive(true);

        ApplyPresentation();

        // 안내 라벨이 패널 안에 있으면 지금까지 꺼져 있었을 수 있다. 켜는 김에 다시 써 넣는다.
        RefreshHint();

        Rebuild();
    }

    /// <summary>목록을 닫고 카드를 치운다. "닫기"를 맞혔을 때와, 일시정지가 풀리거나
    /// 타이틀로 나갈 때 PauseManager가 부른다.</summary>
    public void Close()
    {
        if (!_isOpen)
            return;

        // ⚠️ ClearInput보다 먼저 내려야 한다. ClearInput은 "비었다"를 수신자에게도 디스패치하는데,
        // 그때 아직 열린 상태면 이쪽이 그 신호를 가로채 일시정지 메뉴의 오타 상태가 안 풀린다.
        _isOpen = false;

        // 비켜났던 UI를 제자리로 돌린다. 일시정지가 그대로 풀려 이 창이 꺼져도 이 컴포넌트는
        // Pause Canvas에 붙어 계속 살아 있으므로 복귀는 끝까지 재생된다.
        _displacement.MarkDirty();

        ClearCards();

        if (panel != null)
            panel.SetActive(false);

        // 치다 만 "닫기"가 남아 일시정지 명령 단어에 섞이지 않게 비운다.
        if (inputManager != null)
            inputManager.ClearInput();
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

    // 열 때마다 새로 그린다. 보상으로 카드가 늘어나므로 한 번 만들어두고 재사용하지 않는다.
    private void Rebuild()
    {
        ClearCards();

        if (wordDictionary == null)
        {
            Debug.LogWarning("CardCollectionPanel: wordDictionary가 연결되지 않아 보유 카드를 알 수 없습니다.", this);
            return;
        }

        if (cardPrefab == null || container == null)
        {
            Debug.LogWarning("CardCollectionPanel: cardPrefab 또는 container가 연결되지 않아 카드를 표시할 수 없습니다.", this);
            return;
        }

        var cards = wordDictionary.Words;
        for (var i = 0; i < cards.Count; i++)
            Spawn(cards[i], i, cards.Count);

        if (logDebugEvents)
            Debug.Log($"CardCollectionPanel: 보유 카드 {cards.Count}장 표시", this);
    }

    private void Spawn(CardBase card, int index, int total)
    {
        var instance = Instantiate(cardPrefab, container);

        var rect = instance.GetComponent<RectTransform>();
        if (rect != null)
        {
            var cols = Mathf.Max(1, columns);
            var rows = Mathf.CeilToInt(total / (float)cols);
            var row = index / cols;
            var col = index % cols;

            // 마지막 줄은 덜 찰 수 있다. 그 줄에 실제로 몇 장이 있는지로 중심을 잡아야
            // 한 장뿐인 줄이 왼쪽 끝에 혼자 붙지 않는다.
            var inRow = Mathf.Min(cols, total - row * cols);

            // 가운데를 0으로 두고 좌우/상하로 벌린다. (n - 1) * 0.5가 중심 인덱스다.
            var x = (col - (inRow - 1) * 0.5f) * cellSize.x;
            var y = -(row - (rows - 1) * 0.5f) * cellSize.y;
            rect.anchoredPosition = new Vector2(x, y);

            // 손패에서는 HandFanLayout이 카드를 부채꼴로 기울인다. 여기는 그 레이아웃 밖이라
            // 반듯하게 세운다(기울기는 NameText 자식에 7도가 따로 박혀 있고, 그건 아트 의도다).
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * cardScale;
        }

        // 손패용 Card.prefab을 재사용하는 경우 슬롯 로직이 같이 딸려온다. 목록은 슬롯과도
        // 타이핑과도 무관하므로 꺼둔다(그리기는 CardView가 따로 하므로 내용은 정상적으로 나온다).
        var slotView = instance.GetComponent<CardSlotView>();
        if (slotView != null)
            slotView.enabled = false;

        var cardView = instance.GetComponent<CardView>();
        if (cardView != null)
            cardView.SetCard(card);
        else
            Debug.LogWarning("CardCollectionPanel: cardPrefab에 CardView가 없어 카드를 그릴 수 없습니다.", this);

        _spawned.Add(instance);
    }

    private void ClearCards()
    {
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }

        _spawned.Clear();
    }
}
