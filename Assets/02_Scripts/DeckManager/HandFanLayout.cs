using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬롯 개수만큼 카드 프리팹을 자식으로 만들고, 자식 RectTransform들을 부채꼴로 배치합니다.
/// 자식이 추가/제거되면 남은 것들이 알아서 새 위치로 미끄러집니다.
/// 카드에 무엇이 그려질지는 각 카드의 CardSlotView가 알아서 합니다 - 여기서는 생성과 배치만 합니다.
/// </summary>
[ExecuteAlways]
public class HandFanLayout : MonoBehaviour
{
    [Header("카드 생성")]
    [Tooltip("손패로 찍어낼 카드 프리팹(Card.prefab). 비워두면 이미 있는 자식만 배치합니다.")]
    [SerializeField] private CardSlotView cardPrefab;

    [Tooltip("카드를 연결할 슬롯 매니저. 이 매니저의 슬롯 개수(기본 5)만큼 카드를 만듭니다.")]
    [SerializeField] private CardSlotManager cardSlotManager;

    [Header("배치")]
    [Tooltip("카드 간 가로 간격. 카드 폭보다 작으면 겹칩니다.")]
    [SerializeField] private float spacing = 180f;

    [Tooltip("가운데 카드가 양끝보다 위로 솟는 높이")]
    [SerializeField] private float arcHeight = 30f;

    [Tooltip("양끝 카드의 기울기(도)")]
    [SerializeField] private float maxAngle = 8f;

    [Header("이동")]
    [Tooltip("목표 위치까지 도달하는 시정수. 작을수록 빠릅니다. 0이면 즉시 이동.")]
    [SerializeField] private float smoothTime = 0.08f;

    [Tooltip("가운데 카드가 위로 오도록 그리기 순서를 조정합니다.")]
    [SerializeField] private bool centerOnTop = false;

    private readonly List<RectTransform> _children = new List<RectTransform>();
    private readonly List<CardSlotView> _cards = new List<CardSlotView>();

    /// <summary>생성된 카드들. 슬롯 인덱스 순서입니다.</summary>
    public IReadOnlyList<CardSlotView> Cards => _cards;

    private void Start()
    {
        SpawnCards();
    }

    private void SpawnCards()
    {
        // [ExecuteAlways]라 편집 모드에서도 Start가 불립니다. 씬에 카드를 남기지 않도록 막습니다.
        if (!Application.isPlaying)
            return;

        if (cardPrefab == null)
        {
            Debug.LogWarning("HandFanLayout: Card Prefab이 비어 있어 카드를 만들지 않습니다. " +
                             "인스펙터에 Assets/03_Prefabs/Card.prefab을 연결하세요.", this);
            return;
        }

        if (cardSlotManager == null)
        {
            Debug.LogWarning("HandFanLayout: Card Slot Manager가 비어 있어 카드를 만들지 않습니다. " +
                             "인스펙터에 Deck Manager 오브젝트를 연결하세요.", this);
            return;
        }

        var count = cardSlotManager.SlotCount;
        for (var i = 0; i < count; i++)
        {
            // worldPositionStays: false - UI 프리팹은 로컬 좌표/스케일을 그대로 가져와야 합니다.
            var card = Instantiate(cardPrefab, transform, false);
            card.name = $"Card {i}";

            // Instantiate 직후엔 프리팹의 빈 참조를 그대로 들고 있으므로 여기서 슬롯을 물려줍니다.
            card.Bind(cardSlotManager, i);

            _cards.Add(card);
        }
    }

    private void LateUpdate()
    {
        CollectChildren();

        var count = _children.Count;
        if (count == 0)
            return;

        // 편집 모드에서는 Time.deltaTime이 신뢰할 수 없으므로 즉시 스냅합니다.
        var immediate = !Application.isPlaying || smoothTime <= 0f;
        var t = immediate ? 1f : 1f - Mathf.Exp(-Time.deltaTime / smoothTime);

        for (var i = 0; i < count; i++)
        {
            GetTarget(i, count, out var targetPos, out var targetAngle);

            var rect = _children[i];
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPos, t);

            // localEulerAngles.z는 0~360으로 감기므로 LerpAngle로 최단 경로를 택합니다.
            var angle = Mathf.LerpAngle(rect.localEulerAngles.z, targetAngle, t);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (centerOnTop)
            ApplyDrawOrder(count);
    }

    private void GetTarget(int index, int count, out Vector2 position, out float angle)
    {
        if (count == 1)
        {
            position = new Vector2(0f, arcHeight);
            angle = 0f;
            return;
        }

        var center = (count - 1) * 0.5f;
        var offset = index - center;        // -c .. +c
        var normalized = offset / center;   // -1 .. +1

        position = new Vector2(
            offset * spacing,
            arcHeight * (1f - normalized * normalized));

        angle = -maxAngle * normalized;
    }

    // UGUI는 형제 순서가 뒤일수록 위에 그려집니다.
    // 가운데에서 멀수록 앞쪽 순서로 보내면 가운데가 맨 위로 옵니다.
    private void ApplyDrawOrder(int count)
    {
        var center = (count - 1) * 0.5f;
        var sorted = new List<RectTransform>(_children);
        sorted.Sort((a, b) =>
            Mathf.Abs(_children.IndexOf(b) - center)
                .CompareTo(Mathf.Abs(_children.IndexOf(a) - center)));

        for (var i = 0; i < sorted.Count; i++)
            sorted[i].SetSiblingIndex(i);
    }

    private void CollectChildren()
    {
        _children.Clear();

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;

            if (child is RectTransform rect)
                _children.Add(rect);
        }
    }
}
