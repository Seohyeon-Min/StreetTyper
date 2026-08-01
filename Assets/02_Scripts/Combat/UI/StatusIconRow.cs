using TMPro;
using UnityEngine;

// 방어/화상/마비/냉동/데빌 상태를 아이콘 + 숫자로 보여준다. 방어는 방어도 "값", 나머지 넷은
// 남은 "턴 수"를 표시한다. 숫자 텍스트는 인스펙터에서 따로 연결하지 않는다 - 각 아이콘 밑에
// TMP_Text 자식을 미리 만들어두면(색은 아이콘마다 다르게 원하는 대로) Awake에서
// GetComponentInChildren으로 알아서 찾는다. 아이콘을 SetActive로 켜고 끄면 자식인 텍스트도
// 자동으로 같이 켜지고 꺼진다.
//
// HP 바 프리팹 안, WorldAnchoredUI가 붙은 오브젝트에 같이 두는 것을 전제로 한다. 기존 텍스트
// 한 줄짜리 StatusEffectView를 완전히 대체한다.
//
// 배치(왼쪽부터 순서대로, 방어가 항상 맨 앞)는 여기서 좌표 계산을 하지 않는다 - 부모 오브젝트에
// Horizontal Layout Group을 붙이고, 아래 아이콘들을 반드시 "방어 → 화상 → 마비 → 냉동 → 데빌"
// 순서로 자식에 배치해두면, 꺼진 아이콘은 레이아웃에서 자동으로 빠지고 켜진 것들만 그 순서
// 그대로 왼쪽부터 붙는다.
//
// 화상/마비/냉동은 StatusEffectManager 설계상 적한테만, 데빌은 플레이어한테만 걸리므로 이 바가
// 지금 전투 중인 적을 따라다니는지(IsCurrentEnemy)로 어느 쪽을 켤지 가른다 - 한 바에 둘 다
// 동시에 켜질 일은 없다.
[RequireComponent(typeof(WorldAnchoredUI))]
public class StatusIconRow : MonoBehaviour
{
    [SerializeField] private StatusEffectManager statusEffectManager;

    [Header("아이콘 (반드시 이 순서: 방어 → 화상 → 마비 → 냉동 → 데빌). 각 아이콘 밑에 숫자를 보여줄 TMP_Text 자식을 미리 만들어둘 것 - 자동으로 찾아서 쓴다.")]
    [Tooltip("방어 - CharacterStats.defense, 상태이상이 아니라 매 프레임 확인. 숫자는 방어도 값.")]
    [SerializeField] private GameObject defenseIcon;

    [Tooltip("화상 - 적 전용. 숫자는 남은 턴.")]
    [SerializeField] private GameObject burnIcon;

    [Tooltip("마비 - 적 전용. 숫자는 남은 턴.")]
    [SerializeField] private GameObject paralysisIcon;

    [Tooltip("냉동 - 적 전용. 숫자는 남은 턴.")]
    [SerializeField] private GameObject freezeIcon;

    [Tooltip("데빌 - 플레이어 전용. 숫자는 남은 턴.")]
    [SerializeField] private GameObject devilIcon;

    private WorldAnchoredUI _anchor;
    private CharacterStats _target;

    private TMP_Text _defenseText;
    private TMP_Text _burnText;
    private TMP_Text _paralysisText;
    private TMP_Text _freezeText;
    private TMP_Text _devilText;

    private void Awake()
    {
        _anchor = GetComponent<WorldAnchoredUI>();

        _defenseText = FindCountText(defenseIcon);
        _burnText = FindCountText(burnIcon);
        _paralysisText = FindCountText(paralysisIcon);
        _freezeText = FindCountText(freezeIcon);
        _devilText = FindCountText(devilIcon);
    }

    // 아이콘 밑에 미리 만들어둔 TMP_Text 자식을 찾는다. true를 넘겨야 아이콘이 꺼져 있는
    // 시작 상태에서도(비활성 오브젝트 자식) 찾을 수 있다.
    private static TMP_Text FindCountText(GameObject icon)
    {
        return icon != null ? icon.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void OnEnable()
    {
        if (statusEffectManager != null)
            statusEffectManager.OnEffectsChanged += RefreshStatusEffects;

        RefreshStatusEffects();
    }

    private void OnDisable()
    {
        if (statusEffectManager != null)
            statusEffectManager.OnEffectsChanged -= RefreshStatusEffects;
    }

    private void Update()
    {
        RefreshDefense();
    }

    private void RefreshDefense()
    {
        if (defenseIcon == null)
            return;

        if (_target == null && _anchor != null && _anchor.Target != null)
            _target = _anchor.Target.GetComponent<CharacterStats>();

        var hasDefense = _target != null && _target.defense > 0;
        defenseIcon.SetActive(hasDefense);

        if (_defenseText != null)
            _defenseText.text = hasDefense ? _target.defense.ToString() : string.Empty;
    }

    private void RefreshStatusEffects()
    {
        if (statusEffectManager == null || _anchor == null)
            return;

        var isEnemyBar = statusEffectManager.IsCurrentEnemy(_anchor.Target);
        var effects = statusEffectManager.EnemyEffects;

        int burnTurns = 0;
        var hasBurn = isEnemyBar && effects.TryGetValue(StatusEffectType.Burn, out burnTurns);
        SetSlot(burnIcon, _burnText, hasBurn, burnTurns);

        int paralysisTurns = 0;
        var hasParalysis = isEnemyBar && effects.TryGetValue(StatusEffectType.Paralysis, out paralysisTurns);
        SetSlot(paralysisIcon, _paralysisText, hasParalysis, paralysisTurns);

        int freezeTurns = 0;
        var hasFreeze = isEnemyBar && effects.TryGetValue(StatusEffectType.Freeze, out freezeTurns);
        SetSlot(freezeIcon, _freezeText, hasFreeze, freezeTurns);

        // 데빌은 상태이상 딕셔너리가 아니라 StatusEffectManager.DevilTurnsLeft 별도 필드다.
        var devilTurns = statusEffectManager.DevilTurnsLeft;
        SetSlot(devilIcon, _devilText, !isEnemyBar && devilTurns > 0, devilTurns);
    }

    private static void SetSlot(GameObject icon, TMP_Text text, bool active, int turns)
    {
        if (icon != null)
            icon.SetActive(active);

        if (text != null)
            text.text = active ? turns.ToString() : string.Empty;
    }
}
