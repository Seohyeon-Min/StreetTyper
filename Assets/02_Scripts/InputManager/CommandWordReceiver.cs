using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 명령 단어(일시정지의 "계속"/"타이틀", 결과 화면의 "다음"/"다시")를 받는 수신자의 공통 뼈대.
/// 손패(<see cref="CardInputHandler"/>)와 달리 <b>단어가 인스펙터에 고정</b>되어 있고
/// 화면에 안내 문구를 띄운다는 점이 다르다.
///
/// 손패에는 안내 문구가 없으므로 이 계층을 <see cref="TypingReceiver"/>에서 한 단계 내렸다 -
/// 여기 있는 필드가 위에 있으면 카드 핸들러 인스펙터에 쓰지도 않는 안내 칸이 붙는다.
/// </summary>
public abstract class CommandWordReceiver : TypingReceiver
{
    [Header("안내 문구")]
    [Tooltip("여러 명령의 안내를 이어붙일 때 사이에 넣을 문자열")]
    [SerializeField] private string hintSeparator = "\n";

    [Tooltip("안내 맨 뒤에 공통으로 붙일 말. 일시정지의 '을 입력해주세요!' 같은 것.")]
    [SerializeField, TextArea] private string hintSuffix;

    [Tooltip("영어 안내의 꼬리말. 비워두면 아무것도 붙지 않는다(영어는 조사가 없어 보통 불필요하다).")]
    [SerializeField, TextArea] private string hintSuffixEn;

    [Tooltip("연결하면 안내 문구를 여기에 써 넣는다. 비워두면 BuildHint()를 부르는 쪽이 " +
             "문자열로 직접 가져간다(결과 화면이 그렇게 쓴다).")]
    [SerializeField] private TMP_Text hintLabel;

    /// <summary>지금 상황에 맞는 안내 문구를 조립한다. 보통 <see cref="JoinHints"/>로 만든다.</summary>
    public abstract string BuildHint();

    protected override void OnEnable()
    {
        base.OnEnable();

        // 언어가 바뀌면 명령 단어도 안내도 통째로 달라진다.
        LanguageSettings.OnChanged += RefreshHint;
        RefreshHint();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        LanguageSettings.OnChanged -= RefreshHint;
    }

    /// <summary>안내 라벨이 연결되어 있으면 지금 언어·단어에 맞춰 다시 써 넣는다.
    /// 씬에 글자를 박아두면 단어나 언어를 바꿔도 안내만 옛 상태로 남는다.</summary>
    protected void RefreshHint()
    {
        if (hintLabel != null)
            hintLabel.text = BuildHint();
    }

    /// <summary>안내 문구 여러 개를 구분자로 잇고 꼬리말을 붙인다. 빈 문구는 건너뛴다.
    /// 상태가 바뀔 때만 불리는 경로라 문자열 할당을 신경 쓰지 않아도 된다.</summary>
    protected string JoinHints(params string[] hints)
    {
        if (hints == null || hints.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();

        for (var i = 0; i < hints.Length; i++)
        {
            if (string.IsNullOrEmpty(hints[i]))
                continue;

            if (builder.Length > 0 && !string.IsNullOrEmpty(hintSeparator))
                builder.Append(hintSeparator);

            builder.Append(hints[i]);
        }

        if (builder.Length == 0)
            return string.Empty;

        // ⚠️ 여기는 LanguageSettings.Pick을 쓰지 않는다. 꼬리말("을 입력해주세요!")은 조사 때문에
        // 있는 것이라 한국어가 아닌 언어에서는 비워두는 게 정상 설정인데, Pick은 그걸 번역
        // 누락으로 보고 한국어를 되돌리면서 경고까지 낸다. 여기서는 빈 값이 곧 "꼬리말 없음"이다.
        //
        // ⚠️ IsEnglish가 아니라 IsKorean으로 가른다. 언어가 둘이던 시절에는 결과가 같았지만,
        // 다섯이 된 지금 IsEnglish로 가르면 불어·스페인어·일본어에 한국어 조사가 따라붙는다.
        var suffix = LanguageSettings.IsKorean ? hintSuffix : hintSuffixEn;
        if (!string.IsNullOrEmpty(suffix))
            builder.Append(suffix);

        return builder.ToString();
    }
}
