using TMPro;
using UnityEngine;

// 결과 통계 패널(패배/전체 클리어에서 뜨는 상세 통계)의 각 줄을 개별 TMP_Text로 그린다.
// 예전엔 제목+라벨+숫자가 한 덩어리 문자열(BattleManager.statsFormat)로 합쳐져 statsText
// 하나에 몰아넣었는데, 그러면 숫자 칸의 폰트·위치를 라벨과 따로 디자인할 수 없었다.
// 지금은 제목 1개 + 라벨 5개 + 값 5개를 각자의 TMP_Text로 나눠 받는다 - 라벨과 숫자를
// 자유롭게 배치/디자인해도 이 스크립트를 고칠 필요가 없다.
//
// 라벨 문구는 언어가 바뀔 때만(RefreshLabels), 값은 결과가 뜰 때마다(SetStats) 갱신한다.
public class ResultStatsView : MonoBehaviour
{
    [Header("제목")]
    [SerializeField] private TMP_Text titleText;

    [Header("라벨 (배치·디자인은 프리팹에서, 문구만 여기서 채운다)")]
    [SerializeField] private TMP_Text highestStageLabel;
    [SerializeField] private TMP_Text cpmLabel;
    [SerializeField] private TMP_Text wordsUsedLabel;
    [SerializeField] private TMP_Text damageDealtLabel;
    [SerializeField] private TMP_Text damageTakenLabel;

    [Header("값 (SetStats로 결과 화면이 뜰 때마다 갱신)")]
    [SerializeField] private TMP_Text highestStageValue;
    [SerializeField] private TMP_Text cpmValue;
    [SerializeField] private TMP_Text wordsUsedValue;
    [SerializeField] private TMP_Text damageDealtValue;
    [SerializeField] private TMP_Text damageTakenValue;

    [Header("라벨 문구 - 한국어/영어")]
    [SerializeField] private string highestStageLabelText = "최고 도달 스테이지";
    [SerializeField] private string highestStageLabelTextEn = "Highest stage";

    [SerializeField] private string cpmLabelText = "평균 타자 속도 (CPM)";
    [SerializeField] private string cpmLabelTextEn = "Average speed (CPM)";

    [SerializeField] private string wordsUsedLabelText = "사용한 단어 수";
    [SerializeField] private string wordsUsedLabelTextEn = "Words used";

    [SerializeField] private string damageDealtLabelText = "누적 가한 데미지";
    [SerializeField] private string damageDealtLabelTextEn = "Total damage dealt";

    [SerializeField] private string damageTakenLabelText = "누적 받은 데미지";
    [SerializeField] private string damageTakenLabelTextEn = "Total damage taken";

    private void OnEnable()
    {
        LanguageSettings.OnChanged += RefreshLabels;
        RefreshLabels();
    }

    private void OnDisable()
    {
        LanguageSettings.OnChanged -= RefreshLabels;
    }

    private void RefreshLabels()
    {
        SetLabel(highestStageLabel, highestStageLabelText, highestStageLabelTextEn, nameof(highestStageLabelTextEn));
        SetLabel(cpmLabel, cpmLabelText, cpmLabelTextEn, nameof(cpmLabelTextEn));
        SetLabel(wordsUsedLabel, wordsUsedLabelText, wordsUsedLabelTextEn, nameof(wordsUsedLabelTextEn));
        SetLabel(damageDealtLabel, damageDealtLabelText, damageDealtLabelTextEn, nameof(damageDealtLabelTextEn));
        SetLabel(damageTakenLabel, damageTakenLabelText, damageTakenLabelTextEn, nameof(damageTakenLabelTextEn));
    }

    private void SetLabel(TMP_Text label, string korean, string english, string fieldName)
    {
        if (label != null)
            label.text = LanguageSettings.Pick(korean, english, this, fieldName);
    }

    /// <summary>결과 화면이 뜰 때마다 제목과 수치를 채운다. 비어 있는 칸은 조용히 건너뛴다 -
    /// 디자인 단계에서 아직 안 만든 줄이 있어도 나머지는 정상 표시된다.</summary>
    public void SetStats(string title, Color titleColor, int highestStage, int totalStages,
        int cpm, int wordsUsed, int damageDealt, int damageTaken)
    {
        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = titleColor;
        }

        if (highestStageValue != null) highestStageValue.text = $"{highestStage} / {totalStages}";
        if (cpmValue != null) cpmValue.text = cpm.ToString();
        if (wordsUsedValue != null) wordsUsedValue.text = wordsUsed.ToString();
        if (damageDealtValue != null) damageDealtValue.text = damageDealt.ToString();
        if (damageTakenValue != null) damageTakenValue.text = damageTaken.ToString();
    }
}
