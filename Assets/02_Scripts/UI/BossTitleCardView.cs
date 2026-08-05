using UnityEngine;

/// <summary>
/// 보스 인트로 타이틀 카드의 겉모습만 담당하는 순수 뷰(RewardCardView.ApplyPresentation과
/// 같은 패턴). 제목·부제·이미지·글자색을 전부 인스펙터에서 바꿀 수 있고, StageManager는
/// 이 오브젝트를 켜고 끄기만 할 뿐 문구를 알지 못한다 - 화면에 나가는 글자와 이미지는 예외
/// 없이 인스펙터에서 바꿀 수 있어야 한다는 이 프로젝트의 기본 사양이다.
///
/// 카드가 켜지는 순간(OnEnable) 스스로 문구를 채우므로 <b>바깥에서 배선할 참조가 없다.</b>
/// </summary>
public class BossTitleCardView : MonoBehaviour
{
    [Tooltip("제목 + 이미지 + 글자색 한 덩어리. 인스펙터에서 문구·이미지·색을 전부 바꿀 수 있다.")]
    [SerializeField] private ScreenPresentation presentation = new ScreenPresentation("마그나 마테르", "Magna Mater");

    [Tooltip("부제 문구(한국어).")]
    [SerializeField, TextArea] private string subtitleKorean = "~너가 할 수 있는 모든걸 보여줘!~";

    [Tooltip("부제 문구(영어). 비워두면 한국어로 대체되고 경고가 남는다.")]
    [SerializeField, TextArea] private string subtitleEnglish = "~Show me everything you've got!~";

    [Header("References")]
    [SerializeField] private TMPro.TextMeshProUGUI titleLabel;
    [SerializeField] private TMPro.TextMeshProUGUI subtitleLabel;

    [Tooltip("presentation에 이미지를 넣었을 때 그 스프라이트를 갈아끼울 대상. presentation.image가 " +
             "비어 있으면 이 이미지는 건드리지 않는다 - 인스펙터에서 직접 넣어둔 카드 배경 아트를 " +
             "그대로 쓰는 게 기본이다.")]
    [SerializeField] private UnityEngine.UI.Image titleImage;

    // 카드가 켜질 때마다 스스로 문구를 채운다 - 같은 오브젝트의 StageStartEffect가 OnEnable에서
    // 등장 연출을 알아서 재생하는 것과 같은 방식이다. 부르는 쪽(StageManager)이 참조를 들고
    // ApplyPresentation을 불러줘야 하는 구조였을 때, 그 배선 한 칸이 비면 씬에 구워진 한국어
    // 텍스트가 영어 모드에서도 그대로 나왔다 - 실제로 겪은 버그다. 배선을 늘리지 않는 쪽이 낫다.
    private void OnEnable()
    {
        ApplyPresentation();
    }

    /// <summary>지금 언어에 맞는 제목/부제/이미지를 라벨에 채워 넣는다. OnEnable이 자동으로
    /// 부르므로 밖에서 따로 부를 필요는 없다(카드를 켜지 않은 채 미리 갱신하고 싶을 때만 쓴다).</summary>
    public void ApplyPresentation()
    {
        if (titleLabel != null)
        {
            titleLabel.text = presentation.Title(this, nameof(presentation));
            titleLabel.color = presentation.TitleColor;
        }
        else
        {
            Debug.LogWarning($"{nameof(BossTitleCardView)}: {nameof(titleLabel)}이 연결되지 않아 제목이 " +
                             "언어에 따라 바뀌지 않습니다(씬에 저장된 문구가 그대로 나옵니다).", this);
        }

        // ⚠️ presentation.Image가 비어 있으면 이 이미지를 <b>건드리지 않는다.</b> 여기 연결되는 건
        // 보상 화면처럼 "제목 옆 장식 이미지"가 아니라 카드 배경 아트라, 예전처럼 오브젝트를
        // 꺼버리면 인스펙터에서 직접 넣어둔 배경이 통째로 사라져 카드가 안 뜬 것처럼 보인다
        // (실제로 겪은 버그다 - 글자색이 어두우면 더더욱 아무것도 안 보인다).
        // sprite = null로 지우지도 않는다(스프라이트 없는 Image는 흰 사각형이 된다).
        if (titleImage != null && presentation.Image != null)
        {
            titleImage.sprite = presentation.Image;
            titleImage.gameObject.SetActive(true);
        }

        if (subtitleLabel != null)
            subtitleLabel.text = LanguageSettings.Pick(subtitleKorean, subtitleEnglish, this, nameof(subtitleKorean));
        else
            Debug.LogWarning($"{nameof(BossTitleCardView)}: {nameof(subtitleLabel)}이 연결되지 않아 부제가 " +
                             "언어에 따라 바뀌지 않습니다(씬에 저장된 문구가 그대로 나옵니다).", this);
    }
}
