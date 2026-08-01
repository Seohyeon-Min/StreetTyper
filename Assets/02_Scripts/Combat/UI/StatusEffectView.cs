using System.Text;
using TMPro;
using UnityEngine;

// 지금 걸려 있는 상태이상과 남은 턴을 한 줄로 보여주는 순수 뷰.
// HP 바 프리팹 안에 넣어 캐릭터 위에 뜨게 하는 것을 전제로 한다 - 확률로 걸리는 효과
// (파이어 20% 등)가 실제로 성공했는지 화면에서 확인할 방법이 이것뿐이다.
//
// 플레이어/적이 같은 HPBar 프리팹을 공유하므로, 어느 쪽 효과를 보여줄지는 인스턴스마다 고른다.
public class StatusEffectView : MonoBehaviour
{
    [SerializeField] private StatusEffectManager statusEffectManager;

    [Tooltip("상태이상을 표시할 라벨. 한글이 들어가므로 폰트는 Paperlogy여야 한다.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("아무것도 걸려 있지 않을 때 라벨을 끌지 여부.")]
    [SerializeField] private bool hideWhenEmpty = true;

    [Tooltip("무엇을 그렸는지 로그로 남긴다. 어느 HP 바의 라벨이 표시 중인지 찾을 때 켠다.")]
    [SerializeField] private bool logDebugEvents;

    // 같은 HP 바가 누구를 따라다니는지로 "적 쪽인지 플레이어 쪽인지"를 판별한다.
    // 예전엔 showPlayerEffects 토글을 인스턴스마다 켜뒀는데, 그 오버라이드가 한 번
    // Revert되자 플레이어 바가 적 상태이상을 그대로 그리는 버그가 났다.
    private WorldAnchoredUI _anchor;

    private readonly StringBuilder _builder = new StringBuilder();

    private void Awake()
    {
        _anchor = GetComponent<WorldAnchoredUI>();

        if (_anchor == null)
            Debug.LogWarning("StatusEffectView: 같은 오브젝트에 WorldAnchoredUI가 없어 적/플레이어를 " +
                             "구분할 수 없습니다. HP 바 루트에 함께 붙여 주세요.", this);
    }

    private void OnEnable()
    {
        if (statusEffectManager == null)
        {
            Debug.LogWarning("StatusEffectView: statusEffectManager가 연결되지 않아 상태이상을 표시할 수 없습니다.", this);
            return;
        }

        statusEffectManager.OnEffectsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (statusEffectManager != null)
            statusEffectManager.OnEffectsChanged -= Refresh;
    }

    private void Refresh()
    {
        if (label == null)
            return;

        _builder.Clear();

        // 이 바가 지금 전투 중인 적을 따라다니고 있으면 적 상태이상을, 아니면 플레이어 효과를 그린다.
        var isEnemyBar = _anchor != null && statusEffectManager.IsCurrentEnemy(_anchor.Target);

        if (isEnemyBar)
            BuildEnemyEffects();
        else
            BuildPlayerEffects();

        label.text = _builder.ToString();

        if (hideWhenEmpty)
            label.gameObject.SetActive(_builder.Length > 0);

        // 플레이어/적 라벨이 헷갈릴 때 어느 쪽이 무엇을 그렸는지 바로 알 수 있게 한다.
        if (logDebugEvents && _builder.Length > 0)
        {
            var mode = isEnemyBar ? "적(화상/마비/얼음)" : "플레이어(데빌)";
            Debug.Log($"[상태이상 표시] {name} / 모드={mode} / 라벨={label.name} → \"{_builder}\"", this);
        }
    }

    private void BuildEnemyEffects()
    {
        foreach (var pair in statusEffectManager.EnemyEffects)
        {
            if (_builder.Length > 0)
                _builder.Append("  ");

            _builder.Append(StatusEffectManager.GetDisplayName(pair.Key)).Append(' ').Append(pair.Value);
        }
    }

    private void BuildPlayerEffects()
    {
        var turns = statusEffectManager.DevilTurnsLeft;
        if (turns > 0)
            _builder.Append(StatusEffectManager.DevilDisplayName).Append(' ').Append(turns);
    }
}
